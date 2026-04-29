# Insighta Labs+ - Stage 3

A secure, multi-interface Profile Intelligence System with GitHub OAuth authentication, Role-Based Access Control (RBAC), and both CLI and Web Portal interfaces.

## System Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   CLI Tool      │     │   Web Portal    │     │   External APIs  │
│   (Node.js)     │     │   (React)       │     │   (Genderize,    │
│                 │     │                 │     │   Agify,         │
│  - PKCE Auth    │     │  - HTTP-only    │     │   Nationalize)   │
│  - Local Server │     │    Cookies      │     │                  │
│  - ~/.insighta/ │     │  - CSRF Prot.   │     │                  │
│    credentials  │     │                 │     │                  │
└────────┬────────┘     └────────┬────────┘     └─────────────────┘
         │                       │                            │
         │    Bearer Token      │    HTTP-only Cookies       │
         └───────────┬───────────┴────────────┬─────────────┘
                     │                          │
                     └──────────┬───────────────┘
                                │
                    ┌─────────────▼─────────────┐
                    │      Backend API          │
                    │      (FastAPI)            │
                    │                           │
                    │  - JWT Auth (3min)        │
                    │  - Refresh Tokens (5min)  │
                    │  - RBAC (Admin/Analyst)   │
                    │  - Rate Limiting          │
                    │  - API Versioning         │
                    └─────────────┬─────────────┘
                                  │
                    ┌─────────────▼─────────────┐
                    │      SQLite Database        │
                    │                             │
                    │  - Users Table              │
                    │  - Profiles Table           │
                    │  - Refresh Tokens Table     │
                    └─────────────────────────────┘
```

## Repository Structure

This is a monorepo with three separate git repositories:

```
Stage_3_BE/
├── .git/                      # Main backend repository
├── main.py                    # Backend application
├── auth.py                    # GitHub OAuth + PKCE
├── jwt_handler.py             # Token management
├── middleware.py              # Rate limiting, logging, auth
├── database.py                # Database models
├── requirements.txt           # Python dependencies
├── cli/                       # CLI tool (separate git repo)
│   ├── .git/
│   ├── bin/insighta.js
│   └── src/
└── web/                       # Web portal (separate git repo)
    ├── .git/
    ├── src/
    └── package.json
```

## Authentication Flow

### CLI Authentication Flow (PKCE)

```
1. User runs: insighta login
2. CLI generates:
   - code_verifier (random secret)
   - code_challenge (SHA256 hash)
   - state (random string)
3. CLI starts local HTTP server (localhost:8765)
4. CLI opens browser with GitHub OAuth URL
5. User authenticates with GitHub
6. GitHub redirects to localhost:8765/callback
7. CLI receives code + state
8. CLI sends POST to /auth/github/callback:
   {code, state, code_verifier}
9. Backend exchanges code with GitHub
10. Backend issues:
    - Access token (3 min expiry)
    - Refresh token (5 min expiry)
11. CLI stores tokens in ~/.insighta/credentials.json
12. CLI displays: "Logged in as @username"
```

### Web Authentication Flow

```
1. User clicks "Continue with GitHub"
2. Backend generates PKCE params and stores state
3. Backend redirects to GitHub OAuth
4. User authenticates with GitHub
5. GitHub redirects to /auth/github/callback
6. Backend validates state, exchanges code
7. Backend sets HTTP-only cookies:
   - access_token (3 min)
   - refresh_token (5 min)
8. Backend redirects to frontend
9. Frontend calls /auth/me to get user info
```

## Token Handling

### Access Token
- **Expiry**: 3 minutes
- **Storage**: CLI (credentials.json) / Web (HTTP-only cookie)
- **Usage**: Bearer token in Authorization header

### Refresh Token
- **Expiry**: 5 minutes
- **Storage**: Database (hashed) + cookie/local storage
- **Rotation**: Single-use, invalidated on refresh
- **Flow**: POST /auth/refresh → new access + refresh tokens

### Token Refresh (CLI)
- Automatic: API client detects expired token
- Manual: User prompted to re-login if refresh fails

## Role Enforcement

### Roles
| Role | Permissions |
|------|-------------|
| admin | Create profiles, delete profiles, view all, search, export |
| analyst | View all, search, export |

### Enforcement Strategy
All endpoints under `/api/*` require authentication via `AuthMiddleware`.

Role checks use the `require_role()` decorator:

```python
@app.post("/api/profiles")
async def create_profile(
    current_user: UserDB = Depends(require_role("admin"))
):
    # Only admins can create
    pass

@app.delete("/api/profiles/{id}")
async def delete_profile(
    current_user: UserDB = Depends(require_role("admin"))
):
    # Only admins can delete
    pass
```

## API Versioning

All profile endpoints require header:
```
X-API-Version: 1
```

Missing header returns:
```json
{
  "status": "error",
  "message": "API version header required"
}
```

## Pagination Response Format

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
  "data": [...]
}
```

## Natural Language Parsing

Rule-based parsing (no AI):

| Keyword | Filter |
|---------|--------|
| male/female | gender |
| young | min_age=16, max_age=24 |
| adult/teenager/senior/child | age_group |
| above {n} | min_age=n |
| below {n} | max_age=n |
| from {country} | country_id |

Example: "young males from nigeria" →
```json
{
  "gender": "male",
  "min_age": 16,
  "max_age": 24,
  "country_id": "NG"
}
```

## Setup & Installation

### Backend

1. Install dependencies:
```bash
pip install -r requirements.txt
```

2. Create `.env` file:
```bash
cp .env.example .env
# Edit with your GitHub OAuth credentials
```

3. Run the server:
```bash
uvicorn main:app --reload
```

### CLI Tool

1. Navigate to CLI directory:
```bash
cd cli
```

2. Install globally:
```bash
npm install -g .
```

3. Login:
```bash
insighta login
```

### Web Portal

1. Navigate to web directory:
```bash
cd web
```

2. Install dependencies:
```bash
npm install
```

3. Create `.env` file:
```bash
cp .env.example .env
```

4. Run development server:
```bash
npm run dev
```

## CLI Commands

```bash
# Authentication
insighta login                    # Login with GitHub
insighta logout                   # Logout
insighta whoami                   # Show current user

# Profile commands
insighta profiles list            # List all profiles
insighta profiles list --gender male --country NG
insighta profiles list --min-age 25 --max-age 40
insighta profiles list --sort-by age --order desc
insighta profiles list --page 2 --limit 20

insighta profiles get <id>      # Get single profile

insighta profiles search "young males from nigeria"

insighta profiles create --name "Harriet Tubman"  # Admin only

insighta profiles export --format csv
insighta profiles export --format csv --gender male --country NG
```

## API Endpoints

### Authentication
- `GET /auth/github` - Initiate OAuth flow
- `GET /auth/github/callback` - OAuth callback (web)
- `POST /auth/github/callback` - OAuth callback (CLI)
- `POST /auth/refresh` - Refresh tokens
- `POST /auth/logout` - Logout
- `GET /auth/me` - Get current user

### Profiles (requires auth + X-API-Version: 1)
- `GET /api/profiles` - List all (admin & analyst)
- `GET /api/profiles/search?q=...` - Natural language search
- `GET /api/profiles/:id` - Get single profile
- `POST /api/profiles` - Create profile (admin only)
- `DELETE /api/profiles/:id` - Delete profile (admin only)
- `GET /api/profiles/export?format=csv` - Export CSV

## Security Features

1. **PKCE for CLI OAuth** - Prevents authorization code interception
2. **HTTP-only Cookies (Web)** - JavaScript cannot access tokens
3. **CSRF Protection** - For cookie-based authentication
4. **Rate Limiting** - Auth: 10/min, API: 60/min per user
5. **Token Rotation** - Refresh tokens are single-use
6. **Short Token Lifetimes** - 3 min access, 5 min refresh
7. **Role-Based Access Control** - Enforced on all endpoints
8. **Request Logging** - All requests logged with user info

## Environment Variables

### Backend (.env)
```
GITHUB_CLIENT_ID=your_client_id
GITHUB_CLIENT_SECRET=your_client_secret
GITHUB_REDIRECT_URI=http://localhost:8000/auth/github/callback
JWT_SECRET_KEY=your_random_secret
FRONTEND_URL=http://localhost:3000
CORS_ORIGINS=http://localhost:3000,http://localhost:5173
```

### Web (.env)
```
VITE_API_URL=http://localhost:8000
```

## Deployment

### Backend
Deploy to Railway, Render, or similar. Ensure environment variables are set.

### Web Portal
Build with `npm run build` and deploy to Vercel, Netlify, or similar.

### CLI
Published to npm registry:
```bash
npm publish
```

## Tech Stack

- **Backend**: FastAPI, SQLAlchemy, SQLite, python-jose
- **CLI**: Node.js, Commander.js, Axios, cli-table3
- **Web**: React, React Router, React Query, Tailwind CSS, Vite
- **Auth**: GitHub OAuth, PKCE, JWT, HTTP-only cookies

## License

MIT
