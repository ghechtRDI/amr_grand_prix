#!/bin/bash
# Production Docker script for AMR Grand Prix

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if Docker is running
check_docker() {
    if ! docker --version > /dev/null 2>&1; then
        print_error "Docker is not installed or not running"
        exit 1
    fi
}

# Function to check environment file
check_env() {
    if [ ! -f ".env" ]; then
        if [ -f ".env.production" ]; then
            print_warning "No .env file found. Copying from .env.production"
            cp .env.production .env
        else
            print_error "No .env file found. Please create one from .env.production template"
            exit 1
        fi
    fi
}

# Function to deploy production
deploy() {
    print_status "Deploying AMR Grand Prix to production..."

    check_env

    # Build and start production services
    docker-compose -f docker-compose.prod.yml up --build -d

    print_status "Production deployment started!"
    print_status "Application: http://localhost (port 80)"
    print_status "HTTPS: https://localhost (port 443)"

    print_status "Waiting for services to be healthy..."
    sleep 10

    # Check health
    if curl -f http://localhost/health > /dev/null 2>&1; then
        print_status "✅ Production deployment successful!"
    else
        print_warning "⚠️ Health check failed. Check logs with: $0 logs"
    fi
}

# Function to stop production
stop_prod() {
    print_status "Stopping production services..."
    docker-compose -f docker-compose.prod.yml down
    print_status "Production services stopped."
}

# Function to view production logs
show_logs() {
    if [ -n "$2" ]; then
        docker-compose -f docker-compose.prod.yml logs -f "$2"
    else
        docker-compose -f docker-compose.prod.yml logs -f
    fi
}

# Function to update production
update() {
    print_status "Updating production deployment..."
    docker-compose -f docker-compose.prod.yml down
    docker-compose -f docker-compose.prod.yml pull
    docker-compose -f docker-compose.prod.yml up --build -d
    print_status "Production updated."
}

# Function to backup database
backup_db() {
    print_status "Creating database backup..."
    timestamp=$(date +%Y%m%d_%H%M%S)
    docker-compose -f docker-compose.prod.yml exec db /opt/mssql-tools/bin/sqlcmd \
        -S localhost -U sa -P "${DB_SA_PASSWORD}" \
        -Q "BACKUP DATABASE AmrGrandPrix TO DISK = '/var/opt/mssql/backup/AmrGrandPrix_${timestamp}.bak'"
    print_status "Database backup created: AmrGrandPrix_${timestamp}.bak"
}

# Function to show help
show_help() {
    echo "AMR Grand Prix Docker Production Helper"
    echo "Usage: $0 [COMMAND]"
    echo ""
    echo "Commands:"
    echo "  deploy    Deploy to production (default)"
    echo "  stop      Stop production services"
    echo "  restart   Restart production services"
    echo "  update    Pull latest images and redeploy"
    echo "  logs      Show production logs"
    echo "  logs [service]  Show logs for specific service"
    echo "  status    Show status of production services"
    echo "  backup    Create database backup"
    echo "  shell [service]  Open shell in production container"
    echo "  help      Show this help message"
}

# Function to show status
show_status() {
    docker-compose -f docker-compose.prod.yml ps
}

# Function to open shell in container
open_shell() {
    if [ -n "$2" ]; then
        docker-compose -f docker-compose.prod.yml exec "$2" /bin/bash 2>/dev/null || \
        docker-compose -f docker-compose.prod.yml exec "$2" /bin/sh
    else
        print_error "Please specify a service: app or db"
    fi
}

# Check Docker first
check_docker

# Main command handling
case "${1:-deploy}" in
    "deploy")
        deploy
        ;;
    "stop")
        stop_prod
        ;;
    "restart")
        stop_prod
        deploy
        ;;
    "update")
        update
        ;;
    "logs")
        show_logs "$@"
        ;;
    "status")
        show_status
        ;;
    "backup")
        backup_db
        ;;
    "shell")
        open_shell "$@"
        ;;
    "help"|"-h"|"--help")
        show_help
        ;;
    *)
        print_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac