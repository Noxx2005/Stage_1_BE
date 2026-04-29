# Quick Command Reference

## Local Development

```bash
# Build Docker image
docker build -t hng-stage-one:latest .

# Start with docker-compose (recommended)
docker-compose up -d
docker-compose logs -f api
docker-compose down

# Run without compose
docker run -d -p 8080:8080 -v $(pwd)/data:/app/data hng-stage-one:latest
curl http://localhost:8080/
```

## AWS EC2 Setup (One-Time)

```bash
# SSH into EC2
ssh -i /path/to/key.pem ubuntu@<EC2_PUBLIC_IP>

# Install Docker
sudo apt-get update
sudo apt-get install -y docker.io
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose
sudo usermod -aG docker ubuntu
newgrp docker
```

## AWS EC2 Deployment

```bash
# Clone and setup
mkdir -p ~/hng-api && cd ~/hng-api
git clone https://github.com/your-username/HNG-INTERNSHIP.git .
cd HNG-BACKEND/hng-task-one
cp .env.example .env
mkdir -p data && chmod 777 data

# Build and start
docker-compose up -d --build
docker-compose ps

# Test
curl http://localhost:8080/
curl http://<EC2_PUBLIC_IP>:8080/
curl -X POST http://<EC2_PUBLIC_IP>:8080/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "test"}'
```

## Nginx Setup (Optional)

```bash
# Install
sudo apt-get install -y nginx

# Create config
sudo tee /etc/nginx/sites-available/hng-api > /dev/null <<'EOF'
upstream hng_api { server localhost:8080; }
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

# Enable and start
sudo rm /etc/nginx/sites-enabled/default 2>/dev/null || true
sudo ln -s /etc/nginx/sites-available/hng-api /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
sudo systemctl enable nginx

# Test
curl http://localhost/
curl http://<EC2_PUBLIC_IP>/
```

## Daily Management

```bash
# View logs (real-time)
docker-compose logs -f api

# View logs (last 100 lines)
docker-compose logs --tail 100 api

# Check status
docker-compose ps

# Restart container
docker-compose restart api

# Update application
git pull origin main
docker-compose up -d --build

# Backup database
mkdir -p ~/backups
cp ~/hng-api/data/hng_stage_one.db ~/backups/hng_stage_one.db.backup_$(date +%Y%m%d_%H%M%S)

# Check resource usage
docker stats

# Stop services
docker-compose down

# Stop and remove data (caution!)
docker-compose down -v
```

## Troubleshooting

```bash
# Container not running
docker-compose ps
docker-compose up -d

# Port issues
docker-compose exec api netstat -tuln | grep 8080
sudo lsof -i :8080

# Database locked errors
chmod 777 ~/hng-api/data
chmod 666 ~/hng-api/data/*.db
docker-compose restart api

# Recreate database
rm ~/hng-api/data/hng_stage_one.db
docker-compose restart api

# Check migrations
docker-compose logs api | grep -i migrat

# Fix volume permissions
mkdir -p ~/hng-api/data
chmod 755 ~/hng-api/data
docker-compose exec api ls -la /app/data
```

## Test Endpoints

```bash
# Health check
curl http://localhost:8080/

# Create profile
curl -X POST http://localhost:8080/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "ella"}'

# Get all profiles
curl http://localhost:8080/api/profiles

# Filter by gender
curl http://localhost:8080/api/profiles?gender=female

# Filter by country
curl http://localhost:8080/api/profiles?country_id=DRC

# Filter by age group
curl http://localhost:8080/api/profiles?age_group=adult

# Get specific profile (replace ID)
curl http://localhost:8080/api/profiles/{id}

# Delete profile (replace ID)
curl -X DELETE http://localhost:8080/api/profiles/{id}
```

## Files Reference

```
Root Directory
├── Dockerfile              - Multi-stage container build
├── docker-compose.yml      - Container orchestration config
├── .dockerignore           - Files to exclude from Docker context
├── .env.example            - Environment variables template
├── DOCKER_SUMMARY.md       - Quick summary (this file)
├── DEPLOYMENT_GUIDE.md     - Complete deployment guide
└── README.md               - Full documentation

Source Code
└── src/HngStageOne.Api/
    ├── Program.cs                       - Updated for production
    ├── appsettings.json                 - Default config
    ├── appsettings.Production.json      - Production config
    └── ... (other API files)

Data (created at runtime)
└── data/
    └── hng_stage_one.db                 - SQLite database
```

## Environment Variables

```bash
# Copy template
cp .env.example .env

# Key variables
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:8080
ConnectionStrings__DefaultConnection=Data Source=/app/data/hng_stage_one.db
LOG_LEVEL=Information
```

---

**For detailed instructions, see DEPLOYMENT_GUIDE.md**
