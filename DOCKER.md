# 🐳 Docker Setup for AMR Grand Prix

This document explains how to run the AMR Grand Prix application using Docker containers.

## 📋 Prerequisites

- **Docker Desktop** (or Docker Engine + Docker Compose)
- **Git** (to clone the repository)
- At least **4GB RAM** available for Docker

## 🏗️ Architecture

The application uses a multi-container architecture:

### Development Mode
- **API Container**: .NET 9 Web API with hot reload
- **Client Container**: Vite React development server with hot reload
- **Database Container**: SQL Server Express (optional)

### Production Mode
- **App Container**: Single container with built React app served by .NET API
- **Database Container**: SQL Server Standard/Express

## 🚀 Quick Start

### Development Environment

1. **Start the development environment:**
   ```bash
   ./docker-dev.sh start
   ```

2. **Access the application:**
   - **Frontend**: http://localhost:5173
   - **API**: http://localhost:5000
   - **Database**: localhost:1433

3. **View logs:**
   ```bash
   ./docker-dev.sh logs
   ```

4. **Stop the environment:**
   ```bash
   ./docker-dev.sh stop
   ```

### Production Deployment

1. **Configure environment:**
   ```bash
   cp .env.production .env
   # Edit .env with your production values
   ```

2. **Deploy to production:**
   ```bash
   ./docker-prod.sh deploy
   ```

3. **Access the application:**
   - **Application**: http://localhost
   - **HTTPS**: https://localhost

## 🛠️ Available Scripts

### Development Scripts (`./docker-dev.sh`)
- `start` - Start development environment
- `stop` - Stop all services
- `restart` - Restart all services
- `logs [service]` - View logs (optional service: api, client, db)
- `rebuild` - Rebuild and restart services
- `status` - Show service status
- `shell [service]` - Open shell in container
- `cleanup` - Remove all containers and images

### Production Scripts (`./docker-prod.sh`)
- `deploy` - Deploy to production
- `stop` - Stop production services
- `restart` - Restart production
- `update` - Pull latest images and redeploy
- `logs [service]` - View production logs
- `status` - Show production status
- `backup` - Create database backup
- `shell [service]` - Open shell in production container

## 📁 File Structure

```
├── docker-compose.yml          # Development compose file
├── docker-compose.prod.yml     # Production compose file
├── Dockerfile.production       # Production multi-stage build
├── docker-dev.sh              # Development helper script
├── docker-prod.sh             # Production helper script
├── .env.development           # Development environment template
├── .env.production           # Production environment template
├── AmrGrandPrix.API/
│   ├── Dockerfile            # API development container
│   └── .dockerignore        # API build context exclusions
└── AmrGrandPrix.Client/
    ├── Dockerfile           # Client development container
    └── .dockerignore       # Client build context exclusions
```

## ⚙️ Configuration

### Environment Variables

#### Development (.env.development)
```bash
ASPNETCORE_ENVIRONMENT=Development
DOTNET_RUNNING_IN_CONTAINER=true
VITE_API_URL=http://api:5000
DB_SA_PASSWORD=YourStrong@Passw0rd
```

#### Production (.env.production)
```bash
ASPNETCORE_ENVIRONMENT=Production
DB_SA_PASSWORD=YourVeryStrong@ProductionPassword123
CONNECTION_STRING=Server=db;Database=AmrGrandPrix;...
```

### Port Mapping

| Service | Development | Production |
|---------|-------------|------------|
| API HTTP | 8080 | 80 |
| API HTTPS | NA | 443 |
| Client | 5173 | - |
| Database | 1433 | 1433 |

## 🔧 Development Workflow

### Hot Reload
Both API and Client support hot reload in development:
- **API**: Uses `dotnet watch run`
- **Client**: Uses Vite dev server

### Volume Mounts
Development containers mount source code as volumes:
```yaml
volumes:
  - ./AmrGrandPrix.API:/app:delegated
  - ./AmrGrandPrix.Client:/app:delegated
```

### Debugging
1. **API Debugging**: Attach to process in container or use remote debugging
2. **Client Debugging**: Use browser dev tools as normal
3. **Database**: Connect using SQL Server Management Studio to `localhost:1433`

## 🏭 Production Optimizations

### Multi-stage Build
The production Dockerfile uses multi-stage builds:
1. **Stage 1**: Build React application
2. **Stage 2**: Build .NET application
3. **Stage 3**: Runtime with minimal footprint

### Security Features
- Non-root user in production containers
- Minimal base images (Alpine Linux)
- Health checks for container orchestration
- Secure secret management via environment variables

### Performance
- Optimized build caching
- Minimal image layers
- Built-in health checks
- Resource limits configured

## 🩺 Health Checks

### API Health Check
```bash
curl http://localhost:8080/health
```

### Container Health
```bash
docker-compose ps
```

## 🛡️ Security Considerations

### Development
- Uses development certificates
- CORS enabled for local development
- Database with default password (change for production)

### Production
- HTTPS enforced
- Strong database passwords
- Non-root container execution
- Environment-based configuration

## 🐛 Troubleshooting

### Common Issues

#### Port Already in Use
```bash
# Find and kill process using port
lsof -ti:5000 | xargs kill -9
```

#### Container Won't Start
```bash
# Check logs for specific service
./docker-dev.sh logs api
```

#### Volume Mount Issues (macOS/Windows)
- Ensure Docker Desktop has access to project directory
- Try absolute paths in docker-compose volumes

#### Database Connection Issues
```bash
# Test database connectivity
docker-compose exec api curl -f http://localhost:8080/health
```

### Performance Issues

#### Slow File Watching (macOS)
Add to docker-compose.yml:
```yaml
volumes:
  - ./AmrGrandPrix.API:/app:cached
```

#### Memory Issues
Increase Docker Desktop memory allocation:
- Go to Docker Desktop → Settings → Resources
- Increase memory to at least 4GB

## 📊 Monitoring

### View Resource Usage
```bash
docker stats
```

### Container Logs
```bash
# All services
./docker-dev.sh logs

# Specific service
./docker-dev.sh logs api
```

## 🔄 CI/CD Integration

### GitHub Actions Example
```yaml
- name: Build and test
  run: |
    docker-compose build
    docker-compose up -d
    # Run tests
    docker-compose down
```

### Production Deployment
```bash
# Automated deployment
./docker-prod.sh update
```

## 🆘 Support

For issues related to Docker setup:
1. Check the troubleshooting section above
2. Review container logs: `./docker-dev.sh logs`
3. Verify Docker Desktop is running and has sufficient resources
4. Check firewall settings for port access

## 📈 Next Steps

1. **Container Orchestration**: Consider Kubernetes for production scale
2. **Monitoring**: Add Prometheus/Grafana for metrics
3. **Logging**: Centralize logs with ELK stack
4. **Security**: Implement container scanning and secrets management
5. **Backup**: Automated database backup strategies