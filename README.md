# Alaska Mountain Runners Grand Prix

A full-stack web application for managing and displaying Alaska Mountain Runners Grand Prix race results, overall standings, and statistics. Features secure user authentication, role-based access control, and email confirmation.

## 🏃‍♂️ Tech Stack

- **Backend**: .NET 9 Web API with ASP.NET Identity & JWT Authentication
- **Frontend**: React 19 with Vite & React Router
- **Database**: PostgreSQL 16
- **Email**: MailHog (development) / SMTP (production)
- **Containerization**: Docker & Docker Compose

## ✨ Features

### Implemented
- ✅ User registration with email confirmation
- ✅ JWT token-based authentication
- ✅ Role-based authorization (ReadOnly, Manager, Admin)
- ✅ Automatic token refresh
- ✅ Protected routes
- ✅ Responsive UI with modern design

### Planned
- [ ] Race result management
- [ ] Overall Grand Prix standings
- [ ] Race statistics and analytics
- [ ] Runner profiles and history
- [ ] Race registration integration

## 🚀 Quick Start

### Recommended: Local Development with Docker Infrastructure

This setup runs the database and email server in Docker while running the API and frontend locally for the best development experience with hot reload.

```bash
# 1. Start infrastructure services (PostgreSQL + MailHog)
docker-compose up -d db mailhog

# 2. Start the API (Terminal 1)
cd AmrGrandPrix.API
dotnet ef database update  # First time only
dotnet run                  # Runs on http://localhost:8080

# 3. Start the Frontend (Terminal 2)
cd AmrGrandPrix.Client
npm install                 # First time only
npm run dev                 # Runs on http://localhost:5173

# Access the application at http://localhost:5173
```

### Alternative: Full Docker

Run everything in Docker (note: frontend hot reload works, but API requires rebuild for changes):

```bash
# Start all services
./docker-dev.sh start

# Access:
# - Frontend: http://localhost:5173
# - API: http://localhost:8080
# - MailHog UI: http://localhost:8025
```

## 📁 Documentation

- [CLAUDE.md](CLAUDE.md) - Development context & architecture
- [TESTING_AUTH.md](TESTING_AUTH.md) - Authentication testing guide
- [DOCS/AUTH_PLAN.md](DOCS/AUTH_PLAN.md) - Auth implementation details
- [DOCKER.md](DOCKER.md) - Docker setup & deployment

## 🛠️ Development

### Prerequisites
- .NET 9 SDK
- Node.js 20+
- Docker Desktop

### Services & Ports
| Service | URL |
|---------|-----|
| Frontend | http://localhost:5173 |
| API | http://localhost:8080 |
| MailHog UI | http://localhost:8025 |
| PostgreSQL | localhost:5432 |

### Local Development Setup
1. Clone the repository
2. Run `./docker-dev.sh start` for containerized development
3. Or follow manual setup in [DOCKER.md](DOCKER.md)

## 🔐 Authentication
JWT token-based authentication with email confirmation. Three role levels:
- **ReadOnly** (default) - View data
- **Manager** - Manage races & results
- **Admin** - Full system access
## 📄 License

[License information coming soon]
