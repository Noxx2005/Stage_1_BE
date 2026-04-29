# Deployment Guide - Insighta Labs+

## Backend Deployment

### Option 1: Railway (Recommended)

1. **Push code to GitHub:**
```bash
git add .
git commit -m "feat: stage 3 complete - auth, rbac, cli, web"
git push origin main
```

2. **Create Railway account:**
   - Go to https://railway.app
   - Sign up with GitHub

3. **Create new project:**
   - Click "New Project"
   - Select "Deploy from GitHub repo"
   - Choose your Stage_3_BE repository

4. **Add environment variables:**
   In Railway dashboard → Variables, add:
   ```
   GITHUB_CLIENT_ID=your_github_client_id
   GITHUB_CLIENT_SECRET=your_github_client_secret
   GITHUB_REDIRECT_URI=https://your-app.up.railway.app/auth/github/callback
   JWT_SECRET_KEY=your_random_secret_key_at_least_32_chars
   FRONTEND_URL=https://your-web-portal-url.vercel.app
   CORS_ORIGINS=https://your-web-portal-url.vercel.app
   PYTHON_VERSION=3.11.0
   ```

5. **Deploy:**
   - Railway auto-deploys on push
   - Get your backend URL from dashboard (e.g., `https://insighta-api.up.railway.app`)

### Option 2: Heroku

1. **Install Heroku CLI and login:**
```bash
heroku login
```

2. **Create app:**
```bash
heroku create insighta-api
```

3. **Set environment variables:**
```bash
heroku config:set GITHUB_CLIENT_ID=your_id
heroku config:set GITHUB_CLIENT_SECRET=your_secret
heroku config:set GITHUB_REDIRECT_URI=https://insighta-api.herokuapp.com/auth/github/callback
heroku config:set JWT_SECRET_KEY=your_secret
heroku config:set FRONTEND_URL=your_web_url
```

4. **Deploy:**
```bash
git push heroku main
```

### Option 3: Vercel (Serverless)

Install `vercel.json` is already configured. Just:

1. **Install Vercel CLI:**
```bash
npm i -g vercel
```

2. **Deploy:**
```bash
vercel --prod
```

3. **Set environment variables in Vercel dashboard**

## Web Portal Deployment

### Option 1: Vercel (Recommended for React)

1. **Push web code to separate repo (or deploy from subdirectory):**
```bash
cd web
git init
git add .
git commit -m "Initial web portal"
# Create new GitHub repo and push
```

2. **Deploy to Vercel:**
```bash
npm i -g vercel
vercel --prod
```

3. **Set environment variable:**
```bash
vercel env add VITE_API_URL production
# Enter: https://your-backend-url.railway.app
```

4. **Update CORS:**
   - Add your Vercel URL to backend CORS_ORIGINS

### Option 2: Netlify

1. **Build locally:**
```bash
cd web
npm install
npm run build
```

2. **Deploy:**
   - Drag & drop `dist/` folder to https://app.netlify.com/drop
   - Or use Netlify CLI

3. **Set environment variables in Netlify dashboard**

## GitHub OAuth App Configuration

You need to update your GitHub OAuth app settings for production:

1. Go to https://github.com/settings/developers
2. Click on your OAuth app
3. Update **Authorization callback URL**:
   - Backend: `https://your-backend-url.up.railway.app/auth/github/callback`
4. Add **Homepage URL**:
   - Web portal: `https://your-web-url.vercel.app`

## Environment Variables Summary

### Backend (.env)
```
GITHUB_CLIENT_ID=Ov23liXXXXXXXXXX
GITHUB_CLIENT_SECRET=XXXXXXXXXXXXXXXXXXXXXXXX
GITHUB_REDIRECT_URI=https://insighta-api.up.railway.app/auth/github/callback
JWT_SECRET_KEY=your_super_secret_random_key_32chars
FRONTEND_URL=https://insighta-web.vercel.app
CORS_ORIGINS=https://insighta-web.vercel.app,https://insighta-dev.vercel.app
```

### Web Portal (.env)
```
VITE_API_URL=https://insighta-api.up.railway.app
```

### CLI (stored in ~/.insighta/credentials.json)
The CLI will auto-detect the backend URL or you can set it in config.

## Post-Deployment Checklist

- [ ] Backend health check: `GET https://your-api.com/`
- [ ] Auth endpoint: `GET https://your-api.com/auth/github`
- [ ] API with version header works:
  ```bash
  curl -H "X-API-Version: 1" https://your-api.com/api/profiles
  ```
- [ ] Web portal loads and shows login page
- [ ] GitHub OAuth flow completes successfully
- [ ] CLI can connect: `insighta login` with production API URL

## Troubleshooting

### CORS Errors
Update `CORS_ORIGINS` in backend to include your exact web URL:
```
CORS_ORIGINS=https://insighta-web.vercel.app
```

### OAuth Callback Errors
Ensure `GITHUB_REDIRECT_URI` matches exactly what's in GitHub OAuth app settings (including https).

### Database Issues
SQLite is file-based and persists on Railway/Heroku. For production scale, consider:
- Railway's managed PostgreSQL
- Supabase
- Render's PostgreSQL

### Token/Auth Issues
- Check JWT_SECRET_KEY is set and consistent
- Verify GITHUB_CLIENT_ID and SECRET are correct
- Ensure FRONTEND_URL matches your actual web URL

## URLs to Submit

For your submission, you need:
1. **Backend Repo:** https://github.com/yourusername/stage_3_be
2. **CLI Repo:** https://github.com/yourusername/insighta-cli
3. **Web Repo:** https://github.com/yourusername/insighta-web
4. **Live Backend:** https://insighta-api.up.railway.app
5. **Live Web Portal:** https://insighta-web.vercel.app
