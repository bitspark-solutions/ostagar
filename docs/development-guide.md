# Development Guide - Hot Reload Setup

## Overview
This guide explains how to use the hot reload development environment for the Ostagar system.

## Quick Start

### First Time Setup
```bash
# Build and start development environment
make docker-dev
```

### Daily Development (Hot Reload)
```bash
# Start development with hot reload (no rebuild)
make docker-dev-hot
```

### When Dependencies Change
```bash
# Rebuild development environment
make docker-dev-rebuild
```

## Development Workflow

### 1. Initial Setup
- Run `make docker-dev` to build and start all services
- This will create development containers with hot reload support

### 2. Daily Development
- Run `make docker-dev-hot` to start existing containers
- Code changes will automatically reload without rebuilding containers
- Frontend: Next.js dev server with hot reload
- Backend: .NET watch with hot reload

### 3. When to Rebuild
- Added/removed npm packages in frontend
- Added/removed NuGet packages in backend
- Changed Dockerfile.dev files
- Run `make docker-dev-rebuild`

## Services and Ports

### Main Services
- **Frontend**: http://localhost:3000 (Next.js dev server)
- **Backend**: http://localhost:8080 (API with hot reload)
- **Nginx**: http://localhost (reverse proxy)

### Supporting Services
- **PostgreSQL**: localhost:5432
- **Redis**: localhost:6380
- **pgAdmin**: http://localhost:5050
- **Redis Commander**: http://localhost:8082
- **MailHog**: http://localhost:8025

## Hot Reload Features

### Frontend (Next.js)
- **File Watching**: All files in ./frontend are mounted as volumes
- **Auto Reload**: Changes to React components, pages, styles automatically reload
- **Node Modules**: Cached in container for performance
- **Build Cache**: .next folder cached for faster rebuilds

### Backend (.NET)
- **File Watching**: All files in ./backend are mounted as volumes
- **Auto Reload**: Changes to C# files trigger automatic compilation and restart
- **Build Cache**: obj/ and bin/ folders cached for performance
- **Watch Mode**: Uses `dotnet watch` for optimal development experience

## Volume Mounting

### Frontend Volumes
```yaml
volumes:
  - ./frontend:/app           # Source code
  - /app/node_modules         # Dependencies (cached)
  - /app/.next               # Build cache
```

### Backend Volumes
```yaml
volumes:
  - ./backend:/app           # Source code
  - /app/obj                 # Build cache
  - /app/bin                 # Output cache
```

## Development Commands

### Container Management
```bash
# Start with hot reload
make docker-dev-hot

# Stop all services
make docker-down

# View logs
docker logs garments-frontend-dev
docker logs garments-backend-dev

# Access container shell
docker exec -it garments-frontend-dev sh
docker exec -it garments-backend-dev bash
```

### Debugging
```bash
# View all running containers
docker ps

# View container logs in real-time
docker logs -f garments-frontend-dev
docker logs -f garments-backend-dev

# Check container health
docker inspect garments-frontend-dev | grep Health
docker inspect garments-backend-dev | grep Health
```

## Performance Tips

### 1. Use Hot Reload Commands
- Use `make docker-dev-hot` for daily development
- Only use `make docker-dev` or `make docker-dev-rebuild` when needed

### 2. Container Optimization
- Development containers are optimized for fast rebuilds
- Volume mounting reduces I/O overhead
- Cached dependencies improve startup time

### 3. File Watching
- Frontend: Next.js watches all files in /app
- Backend: dotnet watch monitors .cs, .csproj files
- Changes trigger automatic recompilation

## Troubleshooting

### Hot Reload Not Working

1. **Check Container Status**
   ```bash
   docker ps
   ```

2. **Restart Development Environment**
   ```bash
   make docker-dev-rebuild
   ```

3. **Clear Docker Cache**
   ```bash
   docker system prune -a
   make docker-dev
   ```

### Port Conflicts
- Frontend: Port 3000 should be available
- Backend: Port 8080 should be available
- Check Windows port exclusions if needed

### File Permission Issues
- Ensure Docker has access to project directories
- Check Docker Desktop file sharing settings

## Best Practices

### 1. Development Workflow
- Start with `make docker-dev-hot` daily
- Use `make docker-dev-rebuild` after package changes
- Monitor logs for compilation errors

### 2. Code Changes
- Frontend changes reload automatically
- Backend changes trigger recompilation
- Database migrations may require container restart

### 3. Performance Monitoring
- Use browser dev tools for frontend performance
- Monitor backend logs for API performance
- Check Redis and PostgreSQL performance via admin tools

## Environment Variables

### Frontend Development
- `NODE_ENV=development` - Enables development mode
- `NEXT_PUBLIC_API_URL=http://localhost` - API endpoint

### Backend Development
- `ASPNETCORE_ENVIRONMENT=Development` - Enables development mode
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection
- `ConnectionStrings__Redis` - Redis connection

## Next Steps
- Code changes will automatically reflect in running containers
- Use the admin tools (pgAdmin, Redis Commander) for database management
- Monitor logs for any compilation or runtime errors
