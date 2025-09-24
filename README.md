# Alaska Mountain Runners Grand Prix

This is a .NET/React application used to manage and publicize Alaska Mountain Runners Grand Prix race result history, overall standings, and race statistics.

## 🏃‍♂️ Tech Stack

- **Backend**: .NET 9 Web API with SPA proxy
- **Frontend**: React 19 with Vite
- **Database**: SQL Server
- **Containerization**: Docker & Docker Compose

## 🚀 Quick Start

### Option 1: Docker (Recommended)
```bash
# Start development environment
./docker-dev.sh start

# Access the application
# Frontend: http://localhost:5173
# API: http://localhost:5000
```

### Option 2: Local Development
```bash
# Start the .NET API
cd AmrGrandPrix.API
dotnet run

# Start the React client (in another terminal)
cd AmrGrandPrix.Client
npm install
npm run dev
```

## 🐳 Docker Setup

For detailed Docker setup and deployment instructions, see [DOCKER.md](DOCKER.md).

### Quick Commands
- **Development**: `./docker-dev.sh start`
- **Production**: `./docker-prod.sh deploy`
- **View Logs**: `./docker-dev.sh logs`
- **Stop Services**: `./docker-dev.sh stop`

## 📁 Project Structure

```
├── AmrGrandPrix.API/          # .NET 9 Web API
├── AmrGrandPrix.Client/       # React + Vite frontend
├── docker-compose.yml         # Development containers
├── docker-compose.prod.yml    # Production containers
├── Dockerfile.production      # Production build
├── docker-dev.sh             # Development helper
├── docker-prod.sh            # Production helper
└── DOCKER.md                 # Docker documentation
```

## 🛠️ Development

The application uses SPA proxy configuration where the .NET API serves as the primary entry point and proxies frontend requests to the Vite development server.

### Prerequisites
- .NET 9 SDK
- Node.js 20+
- Docker Desktop (for containerized development)

### Local Development Setup
1. Clone the repository
2. Run `./docker-dev.sh start` for containerized development
3. Or follow manual setup in [DOCKER.md](DOCKER.md)

## 🏭 Production Deployment

See [DOCKER.md](DOCKER.md) for comprehensive production deployment instructions.

## 📊 Features (Planned)

- [ ] Race result management
- [ ] Overall Grand Prix standings
- [ ] Race statistics and analytics
- [ ] Runner profiles and history
- [ ] Race registration integration
- [ ] Mobile-responsive design

## 🤝 Contributing

[Work in progress - contributing guidelines coming soon]

## 📄 License

[License information coming soon]
