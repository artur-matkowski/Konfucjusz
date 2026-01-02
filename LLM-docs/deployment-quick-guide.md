# Quick Deployment Guide

## Current Situation
- You're using pre-built image: `ghcr.io/artur-matkowski/konfucjusz:dev`
- Local code has the fix, but container needs updating

## Option 1: Quick Local Test (Recommended for Testing)

Temporarily modify `docker-compose.yml` to build locally:

```yaml
services:
  app:
    container_name: konfucjusz_app
    build:
      context: .
      dockerfile: Dockerfile
    # Comment out the image line temporarily
    # image: ghcr.io/artur-matkowski/konfucjusz:dev
    restart: unless-stopped
    depends_on:
      - db
    # ... rest of config
```

Then:
```bash
docker-compose down
docker-compose build app
docker-compose up -d
```

## Option 2: Build and Push to Registry (For Production)

```bash
# Build image
docker build -t ghcr.io/artur-matkowski/konfucjusz:dev .

# Login to GitHub Container Registry (if not already)
# You need a GitHub Personal Access Token with package:write scope
echo $GITHUB_TOKEN | docker login ghcr.io -u artur-matkowski --password-stdin

# Push to registry
docker push ghcr.io/artur-matkowski/konfucjusz:dev

# Restart container to pull new image
docker-compose pull app
docker-compose up -d app
```

## Option 3: Direct Container Replacement (Fastest for Testing)

```bash
# Build locally with specific tag
docker build -t konfucjusz-local:latest .

# Stop current container
docker stop konfucjusz_app
docker rm konfucjusz_app

# Run with local image
docker run -d \
  --name konfucjusz_app \
  --network konfucjusz_default \
  -p 8080:80 \
  -e ConnectionStrings__MyConnection="Host=db;Port=5432;Database=konfucjusz_db;Username=konfucjusz;Password=konfucjuszpass" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  # ... (add other env vars from docker-compose.yml)
  konfucjusz-local:latest
```

## Recommended: Option 1

Option 1 is cleanest for testing - just comment out the `image:` line and add `build:` section.
