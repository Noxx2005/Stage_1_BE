# Quick Deployment Summary

## Step 1: Prepare GitHub OAuth App

1. Go to https://github.com/settings/developers
2. New OAuth App
3. Application name: `Insighta Labs+`
4. Homepage URL: `https://your-web-portal.vercel.app` (update after web deploy)
5. Authorization callback URL: `https://your-backend.up.railway.app/auth/github/callback`
6. Save Client ID and Client Secret

## Step 2: Deploy Backend to Railway

```bash
# From backend directory (Stage_3_BE)
git add .
git commit -m "feat: stage 3 complete"
git push origin main

# Then on Railway dashboard:
# 1. New Project → Deploy from GitHub
# 2. Add these env vars:
```

**Railway Environment Variables:**
```
GITHUB_CLIENT_ID=your_github_client_id
GITHUB_CLIENT_SECRET=your_github_client_secret
GITHUB_REDIRECT_URI=https://your-app.up.railway.app/auth/github/callback
JWT_SECRET_KEY=generate_a_random_32_char_string
FRONTEND_URL=https://your-web-portal.vercel.app
CORS_ORIGINS=https://your-web-portal.vercel.app
```

Get backend URL from Railway dashboard (e.g., `https://insighta-api.up.railway.app`)

## Step 3: Deploy Web Portal to Vercel

```bash
cd web

# Create separate repo for web
git init
git add .
git commit -m "Initial web portal"
# Create GitHub repo and push

# Deploy to Vercel
npm i -g vercel
vercel --prod

# Set env var
vercel env add VITE_API_URL production
# Enter your Railway backend URL
```

Get web URL from Vercel (e.g., `https://insighta-web.vercel.app`)

## Step 4: Update GitHub OAuth Settings

Update the GitHub OAuth app with production URLs:
- Homepage URL: `https://insighta-web.vercel.app`
- Authorization callback URL: `https://insighta-api.up.railway.app/auth/github/callback`

## Step 5: Update Backend CORS

Add your Vercel web URL to Railway backend CORS_ORIGINS:
```
CORS_ORIGINS=https://insighta-web.vercel.app
```

## Step 6: Test Everything

**Test Backend:**
```bash
curl https://insighta-api.up.railway.app/
```

**Test Web Portal:**
- Open `https://insighta-web.vercel.app`
- Click "Continue with GitHub"
- Should redirect to Railway backend OAuth
- Complete login → redirect back to web portal

**Test CLI with Production:**
```bash
cd cli
npm link  # or npm install -g .

# Use production backend
export INSIGHTA_API_URL=https://insighta-api.up.railway.app

insighta login
insighta profiles list
```

## URLs for Submission

| Component | URL |
|-----------|-----|
| Backend Repo | `https://github.com/yourusername/Stage_3_BE` |
| CLI Repo | `https://github.com/yourusername/insighta-cli` |
| Web Repo | `https://github.com/yourusername/insighta-web` |
| Live Backend | `https://insighta-api.up.railway.app` |
| Live Web Portal | `https://insighta-web.vercel.app` |

## Troubleshooting

**CORS errors in browser:**
- Check `CORS_ORIGINS` includes exact web URL
- Must include `https://` not `http://`

**OAuth redirect errors:**
- Check `GITHUB_REDIRECT_URI` matches GitHub OAuth app settings exactly
- Must use same protocol (https) and path

**CLI can't connect:**
- Verify `INSIGHTA_API_URL` is set correctly
- Test with `curl $INSIGHTA_API_URL`

**Database resets:**
- SQLite is ephemeral on some platforms
- For persistent data, use Railway's managed PostgreSQL
