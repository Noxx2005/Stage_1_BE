using Microsoft.EntityFrameworkCore;

namespace HngStageOne.Api.Data;

public static class AuthSchemaInitializer
{
    public static async Task EnsureAuthTablesAsync(AppDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Users" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
                "GitHubId" INTEGER NOT NULL,
                "GitHubUsername" TEXT NOT NULL,
                "Email" TEXT NULL,
                "AvatarUrl" TEXT NULL,
                "Role" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "LastLoginAt" TEXT NULL
            );
            """);

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Users" ADD COLUMN "IsActive" INTEGER NOT NULL DEFAULT 1;
                """);
        }
        catch
        {
            // Existing SQLite databases may already have the column from migrations.
        }

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_GitHubId" ON "Users" ("GitHubId");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_Users_GitHubUsername" ON "Users" ("GitHubUsername");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RefreshTokens" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RefreshTokens" PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "TokenHash" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL,
                "RevokedAt" TEXT NULL,
                "ReplacedByTokenId" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "CreatedByIp" TEXT NULL,
                "UserAgent" TEXT NULL,
                CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "OAuthSessions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_OAuthSessions" PRIMARY KEY,
                "State" TEXT NOT NULL,
                "CodeVerifier" TEXT NOT NULL,
                "RedirectUri" TEXT NOT NULL,
                "ClientType" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "ConsumedAt" TEXT NULL,
                "TokenResultJson" TEXT NULL
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_OAuthSessions_State" ON "OAuthSessions" ("State");
            """);
    }
}
