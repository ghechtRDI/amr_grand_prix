#!/bin/bash
# Development Docker script for AMR Grand Prix

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

# Function to start development environment
start_dev() {
    print_status "Starting AMR Grand Prix development environment with Docker..."

    # Use development environment file
    cp .env.development .env 2>/dev/null || true

    # Build and start services
    docker-compose up --build -d

    print_status "Services starting up..."
    sleep 5

    print_status "Development environment is ready!"
    print_status "API: http://localhost:8080"
    print_status "Client: http://localhost:5173"
    print_status "Database: localhost:1433"

    print_status "To view logs, run: ./docker-dev.sh logs"
    print_status "To stop services, run: ./docker-dev.sh stop"
}

# Function to stop services
stop_dev() {
    print_status "Stopping development services..."
    docker-compose down
    print_status "Services stopped."
}

# Function to view logs
show_logs() {
    if [ -n "$2" ]; then
        docker-compose logs -f "$2"
    else
        docker-compose logs -f
    fi
}

# Function to rebuild services
rebuild() {
    print_status "Rebuilding services..."
    docker-compose down
    docker-compose build --no-cache
    docker-compose up -d
    print_status "Services rebuilt and started."
}

# Function to clean up
cleanup() {
    print_warning "This will remove all containers, images, and volumes for this project"
    read -p "Are you sure? (y/N): " -r
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        docker-compose down -v --rmi all --remove-orphans
        docker system prune -f
        print_status "Cleanup completed."
    fi
}

# Function to show help
show_help() {
    echo "AMR Grand Prix Docker Development Helper"
    echo "Usage: $0 [COMMAND]"
    echo ""
    echo "Commands:"
    echo "  start     Start the development environment (default)"
    echo "  stop      Stop all services"
    echo "  restart   Restart all services"
    echo "  logs      Show logs for all services"
    echo "  logs [service]  Show logs for specific service (api, client, db)"
    echo "  rebuild   Rebuild and restart services"
    echo "  cleanup   Remove all containers, images, and volumes"
    echo "  status    Show status of all services"
    echo "  shell [service]  Open shell in service container"
    echo "  help      Show this help message"
}

# Function to show status
show_status() {
    docker-compose ps
}

# Function to open shell in container
open_shell() {
    if [ -n "$2" ]; then
        docker-compose exec "$2" /bin/bash 2>/dev/null || docker-compose exec "$2" /bin/sh
    else
        print_error "Please specify a service: api, client, or db"
    fi
}

# Check Docker first
check_docker

# Main command handling
case "${1:-start}" in
    "start")
        start_dev
        ;;
    "stop")
        stop_dev
        ;;
    "restart")
        stop_dev
        start_dev
        ;;
    "logs")
        show_logs "$@"
        ;;
    "rebuild")
        rebuild
        ;;
    "cleanup")
        cleanup
        ;;
    "status")
        show_status
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