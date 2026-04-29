using HngStageOne.Api.Clients.Implementations;
using HngStageOne.Api.Clients.Interfaces;
using HngStageOne.Api.Constants;
using HngStageOne.Api.Data;
using HngStageOne.Api.Middleware;
using HngStageOne.Api.Options;
using HngStageOne.Api.Repositories.Implementations;
using HngStageOne.Api.Repositories.Interfaces;
using HngStageOne.Api.Services;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.ValidateStageThreeConfiguration();

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=hng_stage_one.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<InsightaAuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));
builder.Services.Configure<ExternalApiOptions>(builder.Configuration.GetSection("ExternalApis"));

// Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HNG Stage 2 Queryable Intelligence API",
        Version = "v1",
        Description = "Queryable demographic intelligence API with advanced filters, pagination, sorting, and natural language search."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid access token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token)
                    && context.Request.Cookies.TryGetValue(AuthConstants.AccessTokenCookieName, out var cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.AdminOnlyPolicy, policy => policy.RequireRole(AuthConstants.AdminRole));
    options.AddPolicy(AuthConstants.AnalystOrAdminPolicy, policy => policy.RequireRole(AuthConstants.AdminRole, AuthConstants.AnalystRole));
});

var rateLimitOptions = builder.Configuration.GetSection("RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("fixed", context =>
    {
        var partitionKey = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.User.Identity.Name ?? "user"
            : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = context.Request.Path.StartsWithSegments("/api/v1/auth")
                || context.Request.Path.StartsWithSegments("/auth")
                ? 1_000_000
                : rateLimitOptions.ApiPermitLimit,
            Window = TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes),
            QueueLimit = 0
        });
    });
});

// Register Repositories
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

// Register Services
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IProfileQueryValidator, ProfileQueryValidator>();
builder.Services.AddScoped<INaturalLanguageProfileQueryParser, NaturalLanguageProfileQueryParser>();
builder.Services.AddScoped<IProfileSeedService, ProfileSeedService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Register HTTP Clients
builder.Services.AddHttpClient<IGenderizeClient, GenderizeClient>();
builder.Services.AddHttpClient<IAgifyClient, AgifyClient>();
builder.Services.AddHttpClient<INationalizeClient, NationalizeClient>();
builder.Services.AddHttpClient<IAuthService, AuthService>();

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredCors", policyBuilder =>
    {
        policyBuilder.SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Forward headers for reverse proxy (e.g., Nginx on EC2)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Apply Migrations Automatically on startup
// Safe for production as EF Core handles concurrency
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();
        await AuthSchemaInitializer.EnsureAuthTablesAsync(dbContext);

        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var seedService = scope.ServiceProvider.GetRequiredService<IProfileSeedService>();
        var seedPath = FindSeedFilePath(app.Environment.ContentRootPath);

        if (seedPath is not null)
        {
            var insertedCount = await seedService.SeedAsync(seedPath);
            seedLogger.LogInformation("Seeded {InsertedCount} profiles from {SeedPath}", insertedCount, seedPath);
        }
        else
        {
            seedLogger.LogWarning("Seed file not found from content root {ContentRootPath}", app.Environment.ContentRootPath);
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database initialization failed");
        if (app.Environment.IsProduction())
        {
            throw;
        }
    }
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HngStageOne API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseMiddleware<RequestLoggingMiddleware>();

// Use CORS
app.UseCors("ConfiguredCors");

app.UseMiddleware<AuthRateLimitMiddleware>();
app.UseRateLimiter();

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Skip HTTPS redirection - let reverse proxy handle it
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<ActiveUserMiddleware>();
app.UseMiddleware<ApiVersionHeaderMiddleware>();
app.UseMiddleware<CsrfProtectionMiddleware>();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("fixed");

// Health check endpoint
app.MapGet("/", () => Results.Ok(new
{
    status = "success",
    message = "API is running"
}));

await app.RunAsync();

static string? FindSeedFilePath(string contentRootPath)
{
    var directoriesToCheck = new[]
    {
        contentRootPath,
        Directory.GetParent(contentRootPath)?.FullName,
        Directory.GetParent(Directory.GetParent(contentRootPath ?? string.Empty)?.FullName ?? string.Empty)?.FullName
    }
    .Where(path => !string.IsNullOrWhiteSpace(path))
    .Distinct(StringComparer.OrdinalIgnoreCase);

    foreach (var directory in directoriesToCheck)
    {
        var candidatePath = Path.Combine(directory!, "seed_profiles.json");
        if (File.Exists(candidatePath))
        {
            return candidatePath;
        }
    }

    return null;
}
