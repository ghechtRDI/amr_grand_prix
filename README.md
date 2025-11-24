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

### Recommended: Local Development with Localhost PostgreSQL

This setup uses your localhost PostgreSQL database and runs MailHog in Docker for email testing, while running the API and frontend locally for the best development experience with hot reload.

**Prerequisites:**
- PostgreSQL 16 running locally with database `amr_grand_prix`
- Connection string configured in .NET user secrets (see below)

```bash
# 1. Configure database connection (first time only)
cd AmrGrandPrix.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=amr_grand_prix;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"

# 2. Start MailHog for email testing
docker-compose up -d mailhog

# 3. Apply database migrations (first time only)
dotnet ef database update

# 4. Start the API (Terminal 1)
dotnet run                  # Runs on http://localhost:8080

# 5. Start the Frontend (Terminal 2)
cd ../AmrGrandPrix.Client
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
| Service | URL | Notes |
|---------|-----|-------|
| Frontend | http://localhost:5173 | Vite dev server |
| API | http://localhost:8080 | .NET Web API |
| MailHog UI | http://localhost:8025 | Email testing interface |
| MailHog SMTP | localhost:1025 | SMTP server |
| PostgreSQL | localhost:5432 | Local PostgreSQL (not Docker) |

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
