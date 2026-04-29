# Insighta Labs+ Stage 3 Backend

ASP.NET Core backend for the Insighta Labs+ profile intelligence platform. Stage 2 profile filtering, sorting, pagination, and natural language search are preserved, with Stage 3 security and multi-interface access added on top.

## System Architecture

- Backend API: ASP.NET Core, EF Core, SQLite
- Auth: GitHub OAuth with PKCE, JWT access tokens, rotating refresh tokens
- Interfaces: web portal via HTTP-only cookies, CLI via bearer tokens
- Authorization: `admin` and `analyst` roles enforced with ASP.NET policies
- Data: profile records plus users, OAuth sessions, and refresh tokens
- Deployment: backend and web portal can run together with `docker-compose.stage3.yml` from the parent folder

## Authentication Flow

Browser login:

1. Web redirects to `GET /api/v1/auth/github/start?client=web`.
2. Backend creates OAuth state and PKCE verifier/challenge.
3. GitHub redirects to `/api/v1/auth/github/callback`.
4. Backend exchanges the code, creates/updates the user, sets HTTP-only auth cookies, and redirects to the web portal.

CLI login:

1. CLI calls `POST /api/v1/auth/cli/start`.
2. Backend returns a GitHub authorization URL and state.
3. User completes GitHub login in the browser.
4. CLI polls `POST /api/v1/auth/cli/exchange`.
5. CLI stores credentials at `~/.insighta/credentials.json`.

## Token Handling Approach

- Access tokens are JWTs with a short lifetime, default `3` minutes.
- Refresh tokens are random secrets stored only as SHA-256 hashes in the database.
- Refresh tokens rotate on every refresh.
- Refresh tokens expire after `5` minutes by default.
- Web clients receive tokens in HTTP-only cookies.
- Web unsafe requests must send `X-CSRF-TOKEN` matching the readable `XSRF-TOKEN` cookie.
- CLI clients send `Authorization: Bearer <access_token>` and refresh automatically.

## Role Enforcement Logic

Roles:

- `admin`: can read, search, export, create, and delete profiles
- `analyst`: can read, search, and export profiles

Admin users are assigned by GitHub username through:

```bash
Auth__AdminGitHubUsernames=your-github-username,another-admin
```

Protected profile routes:

```text
GET    /api/v1/profiles              analyst or admin
GET    /api/v1/profiles/{id}         analyst or admin
GET    /api/v1/profiles/search       analyst or admin
GET    /api/v1/profiles/export.csv   analyst or admin
POST   /api/v1/profiles              admin only
DELETE /api/v1/profiles/{id}         admin only
```

Legacy `/api/profiles` routes are also protected with the same policies while retaining the Stage 2 response behavior.

## Natural Language Parsing Approach

Natural language search remains rule-based in `NaturalLanguageProfileQueryParser`. The parser converts supported plain-English phrases into the same structured query options used by filtered profile listing.

Examples:

```text
young males from nigeria
females above 30
adult males from kenya
male and female teenagers above 17
```

Unsupported phrases return the existing standardized error response.

## API Versioning And Pagination

Profile requests must include:

```text
X-API-Version: 1
```

Requests without the header return `400 Bad Request`.

The canonical Stage 3 profile routes live under `/api/profiles`; `/api/v1/profiles` remains available for compatibility.

The list/search response uses:

```json
{
  "status": "success",
  "page": 1,
  "limit": 10,
  "total": 2026,
  "total_pages": 203,
  "links": {
    "self": "/api/profiles?page=1&limit=10",
    "next": "/api/profiles?page=2&limit=10",
    "prev": null
  },
  "data": [],
}
```

## CSV Export

```text
GET /api/profiles/export?format=csv
```

The export supports the same filters and sorting as profile listing. `/api/v1/profiles/export.csv` remains available for compatibility.

## CLI Usage

From the sibling `stage-3-cli` repo:

```bash
dotnet pack
dotnet tool install --global --add-source ./bin/Release Insighta.Cli
insighta config set-backend https://your-backend-url.com
insighta login
insighta profiles list --gender male --page 1 --limit 10
insighta profiles search "young males from nigeria"
insighta profiles export profiles.csv
```

## Environment Variables

Copy `.env.example` and fill in:

```bash
Auth__BackendPublicUrl=
Auth__WebPortalUrl=
Auth__AdminGitHubUsernames=
Auth__OAuthSessionMinutes=10
ALLOWED_ORIGINS=
GitHub__ClientId=
GitHub__ClientSecret=
Jwt__SigningKey=
Jwt__AccessTokenMinutes=3
Jwt__RefreshTokenMinutes=5
RateLimit__AuthPermitLimit=10
RateLimit__ApiPermitLimit=60
RateLimit__WindowMinutes=1
ExternalApis__GenderizeBaseUrl=https://api.genderize.io
ExternalApis__AgifyBaseUrl=https://api.agify.io
ExternalApis__NationalizeBaseUrl=https://api.nationalize.io
```

GitHub OAuth callback:

```text
https://your-backend-url.com/api/v1/auth/github/callback
```

## Local Run

```bash
dotnet build src/HngStageOne.Api/HngStageOne.Api.csproj
dotnet run --project src/HngStageOne.Api/HngStageOne.Api.csproj
```

Swagger in development:

```text
http://localhost:5048/swagger
```

Docker:

```bash
docker compose up --build
```

## Tests

```bash
dotnet test
```
