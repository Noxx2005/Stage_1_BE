# Docker & AWS EC2 Deployment Guide

## Files Created/Updated

### Docker-Related Files (Added to Project Root)

1. **Dockerfile** - Multi-stage build configuration
2. **.dockerignore** - Files to exclude from Docker context
3. **docker-compose.yml** - Production-like container orchestration
4. **.env.example** - Environment variable template
5. **.gitignore** - Git ignore patterns (updated)

### Application Configuration (Updated)

1. **Program.cs** - Added forwarded headers middleware for reverse proxy support
2. **appsettings.Production.json** - Production-specific database configuration

## Complete File Tree

```
HNG-BACKEND/hng-task-one/
├── Dockerfile                          ✓ Multi-stage build
├── .dockerignore                        ✓ Docker context filter
├── docker-compose.yml                  ✓ Container orchestration
├── .env.example                         ✓ Environment variables template
├── .gitignore                           ✓ Git ignore patterns
├── README.md                            ✓ Updated with Docker/deployment guide
├── src/
│   └── HngStageOne.Api/
│       ├── Program.cs                   ✓ Updated for production
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── appsettings.Production.json  ✓ New
│       ├── Controllers/
│       ├── Data/
│       ├── Domain/
│       ├── DTOs/
│       ├── Services/
│       ├── Repositories/
│       ├── Clients/
│       ├── Middleware/
│       ├── Helpers/
│       ├── Constants/
│       └── HngStageOne.Api.csproj
└── tests/
```

## Local Testing Commands

### Prerequisites

- Docker Desktop installed and running
- Docker Compose (included with Docker Desktop)
- Git installed

### Build the Docker Image

```bash
# Navigate to project root
cd HNG-BACKEND/hng-task-one

# Build the image (first time takes ~2-3 minutes)
docker build -t hng-stage-one:latest .

# Verify image created
docker images | grep hng-stage-one
```

### Run with Docker Compose (Recommended)

```bash
# Create .env from template (if not exists)
cp .env.example .env

# Start services in background
docker-compose up -d

# View logs
docker-compose logs -f api

# Test API
curl http://localhost:8080/
curl -X POST http://localhost:8080/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "ella"}'

# List running containers
docker-compose ps

# Stop all services
docker-compose down

# Completely remove everything (including data)
docker-compose down -v
```

### Run without Compose (Direct Docker)

```bash
# Create data directory
mkdir -p ./data
chmod 777 ./data

# Run container
docker run -d \
  --name hng-api \
  -p 8080:8080 \
  -v "$(pwd)/data:/app/data" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/hng_stage_one.db" \
  hng-stage-one:latest

# View logs
docker logs -f hng-api

# Test API
curl http://localhost:8080/

# Stop container
docker stop hng-api

# Remove container
docker rm hng-api
```

## AWS EC2 Deployment Steps

### Phase 1: AWS Setup

#### 1. Launch EC2 Instance

```
1. Go to AWS EC2 Dashboard
2. Click "Launch Instance"
3. Choose: Ubuntu Server 24.04 LTS (or latest Ubuntu LTS)
4. Instance Type: t3.micro (free tier) or t3.small (better performance)
5. Network Settings:
   - VPC: Default
   - Auto-assign public IP: Enable
   - Security Group: Create new or select existing
6. Storage: 20GB (minimum) or 30GB (recommended)
7. Launch with your SSH key pair
```

**Save your EC2 Public IP**: You'll need this for SSH and API access

#### 2. Configure Security Group

After launching, go to AWS EC2 Security Groups and add these Inbound Rules:

| Type       | Protocol | Port | Source    | Purpose                |
| ---------- | -------- | ---- | --------- | ---------------------- |
| SSH        | TCP      | 22   | Your IP   | SSH access             |
| HTTP       | TCP      | 80   | 0.0.0.0/0 | Web traffic (Nginx)    |
| HTTPS      | TCP      | 443  | 0.0.0.0/0 | Secure traffic (Nginx) |
| Custom TCP | TCP      | 8080 | 0.0.0.0/0 | Direct API access      |

### Phase 2: Install Docker on EC2

```bash
# SSH into your EC2 instance
ssh -i /path/to/key.pem ubuntu@<EC2_PUBLIC_IP>

# Update system
sudo apt-get update
sudo apt-get upgrade -y

# Install Docker
sudo apt-get install -y docker.io

# Install Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# Verify installations
docker --version
docker-compose --version

# Add user to docker group (allows running docker without sudo)
sudo usermod -aG docker ubuntu

# Apply group changes (log out and back in, or use:)
newgrp docker

# Test without sudo
docker ps

# Install Git (optional, if pulling from GitHub)
sudo apt-get install -y git
```

### Phase 3: Deploy Application

```bash
# Create deployment directory
mkdir -p ~/hng-api && cd ~/hng-api

# Option A: Clone from GitHub
git clone https://github.com/your-username/HNG-INTERNSHIP.git .
cd HNG-BACKEND/hng-task-one

# Option B: Copy files from local machine
# (From your local machine)
scp -i /path/to/key.pem -r HNG-BACKEND/hng-task-one/* ubuntu@<EC2_IP>:~/hng-api/

# Back on EC2
cd ~/hng-api

# Create .env file
cp .env.example .env

# (Optional) Customize .env
# nano .env

# Create data directory
mkdir -p data
chmod 777 data
```

### Phase 4: Build and Start Container

```bash
# Build the Docker image (first time takes ~3-5 minutes)
docker-compose up -d --build

# Monitor build progress
docker-compose logs -f api

# Wait for output like:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://0.0.0.0:8080

# Verify container is running
docker-compose ps

# Expected output:
# hng_stage_one_api    Running
```

### Phase 5: Verify API is Reachable

```bash
# From EC2 (local test)
curl http://localhost:8080/
# Expected: {"status":"success","message":"API is running"}

# From local machine (using EC2 public IP)
curl http://<EC2_PUBLIC_IP>:8080/

# Create a profile to fully test
curl -X POST http://<EC2_PUBLIC_IP>:8080/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "test"}'

# Expected response with profile data
```

### Phase 6: Setup Nginx Reverse Proxy (Recommended)

```bash
# Install Nginx
sudo apt-get install -y nginx

# Create Nginx configuration
sudo tee /etc/nginx/sites-available/hng-api > /dev/null <<'EOF'
upstream hng_api {
    server localhost:8080;
}

server {
    listen 80;
    server_name _;
    client_max_body_size 10M;

    location / {
        proxy_pass http://hng_api;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_http_version 1.1;
        proxy_set_header Connection "";
    }
}
EOF

# Enable configuration
sudo rm /etc/nginx/sites-enabled/default 2>/dev/null || true
sudo ln -s /etc/nginx/sites-available/hng-api /etc/nginx/sites-enabled/

# Test configuration
sudo nginx -t

# Start/restart Nginx
sudo systemctl restart nginx
sudo systemctl enable nginx

# Verify Nginx is running
sudo systemctl status nginx
```

**Now test through Nginx** (port 80):

```bash
# From EC2
curl http://localhost/

# From local machine
curl http://<EC2_PUBLIC_IP>/
curl -X POST http://<EC2_PUBLIC_IP>/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "ella"}'
```

### Phase 7: Optional - Enable HTTPS with Let's Encrypt

```bash
# Install Certbot
sudo apt-get install -y certbot python3-certbot-nginx

# Generate certificate (replace api.example.com with your domain)
sudo certbot certonly --nginx \
  -d api.example.com \
  --non-interactive \
  --agree-tos \
  -m your-email@example.com

# Verify certificate
sudo ls -la /etc/letsencrypt/live/api.example.com/

# Update Nginx with SSL
sudo tee /etc/nginx/sites-available/hng-api > /dev/null <<'EOF'
upstream hng_api {
    server localhost:8080;
}

server {
    listen 80;
    server_name api.example.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.example.com;

    ssl_certificate /etc/letsencrypt/live/api.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.example.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    client_max_body_size 10M;

    location / {
        proxy_pass http://hng_api;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_http_version 1.1;
        proxy_set_header Connection "";
    }
}
EOF

# Test and restart
sudo nginx -t
sudo systemctl restart nginx

# Setup auto-renewal
sudo systemctl enable certbot.timer
sudo systemctl start certbot.timer

# Verify auto-renewal
sudo certbot renew --dry-run
```

## Ongoing EC2 Management

### View Logs

```bash
# Real-time logs
docker-compose logs -f api

# Last 100 lines
docker-compose logs --tail 100 api

# Logs from last 2 hours
docker-compose logs --since 2h api
```

### Restart Services

```bash
# From ~/hng-api directory
docker-compose restart api

# Or full restart
docker-compose down
docker-compose up -d
```

### Update Application

```bash
# Pull latest code
cd ~/hng-api
git pull origin main

# Rebuild and restart
docker-compose up -d --build

# View logs
docker-compose logs -f api
```

### Monitor Container

```bash
# Resource usage (memory, CPU)
docker stats

# Container status
docker-compose ps

# Detailed inspection
docker inspect hng_stage_one_api
```

### Backup SQLite Database

```bash
# Create backup
mkdir -p ~/backups
cp ~/hng-api/data/hng_stage_one.db ~/backups/hng_stage_one.db.backup_$(date +%Y%m%d_%H%M%S)

# List backups
ls -lh ~/backups/

# Download to local machine
scp -i /path/to/key.pem ubuntu@<EC2_IP>:~/backups/hng_stage_one.db.* ~/

# Restore from backup
cp ~/backups/hng_stage_one.db.backup_20260415_120000 ~/hng-api/data/hng_stage_one.db
docker-compose restart api
```

## Troubleshooting

### Container Starts but API is Unreachable

**Check 1: Container Running?**

```bash
docker-compose ps
# Should show: hng_stage_one_api   Up
```

**Check 2: Port Binding?**

```bash
docker-compose exec api netstat -tuln | grep 8080
# Should show: tcp 0 0 0.0.0.0:8080
```

**Check 3: Logs for Errors?**

```bash
docker-compose logs api
# Should show: Now listening on: http://0.0.0.0:8080
```

**Check 4: Security Group Allows Port 8080?**

```bash
# Test locally from EC2
curl http://localhost:8080/

# If that works, check AWS security group in Console
```

**Check 5: Restart Container**

```bash
docker-compose restart api
docker-compose logs -f api
```

### SQLite File Permission Issues

**Symptom**: "database is locked" or "readonly database"

**Solution**:

```bash
# Check permissions
ls -la ~/hng-api/data/

# Fix directory permissions
chmod 777 ~/hng-api/data

# Fix database file permissions
chmod 666 ~/hng-api/data/*.db

# Restart container
docker-compose down
docker-compose up -d
```

### Migrations Failing

**Symptom**: "The model for context has pending changes"

**Causes**:

- Database schema doesn't match code model
- Migrations weren't run

**Solution**:

```bash
# Migrations run automatically on container start
# Check logs
docker-compose logs api | grep -i migrat

# If needed, delete and recreate database
rm ~/hng-api/data/hng_stage_one.db
docker-compose restart api
docker-compose logs -f api
```

### Port Binding Errors

**Symptom**: "bind: address already in use 0.0.0.0:8080"

**Solution**:

```bash
# Find what's using port 8080
sudo netstat -tulpn | grep 8080
sudo lsof -i :8080

# Kill the process (if safe)
sudo kill -9 <PID>

# Or use different port in docker-compose.yml
# Change "8080:8080" to "8081:8080"
nano docker-compose.yml
docker-compose up -d
```

### Volume Mount Issues

**Symptom**: "mkdir /app/data: permission denied"

**Solution**:

```bash
# Ensure directory exists
mkdir -p ~/hng-api/data

# Check permissions
ls -la ~/hng-api/

# Fix if needed
chmod 755 ~/hng-api/data

# Verify mount in container
docker-compose exec api ls -la /app/data
```

### Out of Disk Space

**Solution**:

```bash
# Check disk usage
df -h

# Clean up Docker resources
docker system prune -a

# Remove old images
docker rmi hng-stage-one:old_tag
```

## Performance Tips

1. **Monitor Resource Usage**:

   ```bash
   docker stats --no-stream
   ```

2. **Increase Log Level Only When Debugging**:

   ```bash
   # Edit .env
   LOG_LEVEL=Information
   ```

3. **Regular Database Backups**:

   ```bash
   # Create cron job
   (crontab -l 2>/dev/null; echo "0 2 * * * /home/ubuntu/backup-db.sh") | crontab -
   ```

4. **Monitor Logs for Errors**:

   ```bash
   docker-compose logs api | grep -i error
   ```

5. **Setup Uptime Monitoring**:
   - Use AWS CloudWatch
   - Or third-party service (Uptime Robot, Pingdom, etc.)

## Production Rollout Checklist

- [ ] Application builds successfully locally
- [ ] Docker image builds without errors
- [ ] docker-compose.yml tested locally
- [ ] .env.example created with all required variables
- [ ] EC2 instance launched and security groups configured
- [ ] Docker and Docker Compose installed on EC2
- [ ] Application deployed and running
- [ ] API is reachable from external IP
- [ ] Health check endpoint working
- [ ] Nginx reverse proxy configured
- [ ] HTTPS certificates (optional but recommended)
- [ ] Database backup procedure documented
- [ ] Logs monitored for errors
- [ ] Performance baseline established
- [ ] Disaster recovery procedures tested

---

**Last Updated**: April 15, 2026
**Status**: Deployment Ready
