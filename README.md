# Alaska Mountain Runners Grand Prix

A full-stack web application for managing and displaying Alaska Mountain Runners Grand Prix race results, overall standings, and statistics. Features secure user authentication, role-based access control, and AI-powered results extraction.

## 🏃‍♂️ Tech Stack

- **Backend**: .NET 9 Web API with ASP.NET Identity & JWT Authentication
- **Frontend**: React 19 with Vite & React Router
- **Database**: PostgreSQL 16
- **AI Extraction**: Anthropic Claude (production) / Ollama (local development)
- **Email**: MailHog (development) / SMTP (production)
- **Containerization**: Docker & Docker Compose

## ✨ Features

- ✅ User registration with email confirmation
- ✅ JWT token-based authentication
- ✅ Role-based authorization (ReadOnly, Manager, Admin)
- ✅ Automatic token refresh
- ✅ Protected routes
- ✅ Responsive UI with dark theme
- ✅ Race result upload (PDF, CSV, Excel) with AI-powered extraction
- ✅ Grand Prix points calculation and standings
- ✅ Runner profiles and history
- ✅ 4-step upload wizard: Race Selection → File Upload → Data Review → Confirmation

## 🚀 Quick Start

### Recommended: Local Development with Localhost PostgreSQL

**Prerequisites:**
- PostgreSQL 16 running locally
- .NET 9 SDK, Node.js 20+, Docker Desktop

```bash
# 1. Configure database connection (first time only)
cd AmrGrandPrix.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Database=amr_grand_prix;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"

# 2. Configure Anthropic API key (for results upload)
dotnet user-secrets set "Llm:Anthropic:ApiKey" "sk-ant-..."

# 3. Start MailHog for email testing
docker-compose up -d mailhog

# 4. Apply database migrations (first time only)
dotnet ef database update

# 5. Start the API (Terminal 1)
dotnet run                  # http://localhost:8080

# 6. Start the Frontend (Terminal 2)
cd ../AmrGrandPrix.Client
npm install                 # first time only
npm run dev                 # http://localhost:5173
```

### Alternative: Use Ollama for Local LLM (no API cost)

```bash
# Start Ollama container
docker-compose up -d ollama

# Pull the model (one time, ~9 GB download)
docker compose exec ollama ollama pull gwen2.5:14b-instruct

# Switch the API to Ollama by adding to AmrGrandPrix.API/appsettings.Development.json:
# "Llm": { "Provider": "Ollama" }
```

### Full Docker

```bash
./docker-dev.sh start

# Frontend: http://localhost:5173
# API:      http://localhost:8080
# MailHog:  http://localhost:8025
# Ollama:   http://localhost:11434
```

## 📁 Documentation

- [CLAUDE.md](CLAUDE.md) — Development context & architecture
- [DOCS/LLM_FILE_PARSER_PLAN.md](DOCS/LLM_FILE_PARSER_PLAN.md) — LLM extraction design
- [TESTING_AUTH.md](TESTING_AUTH.md) — Authentication testing guide
- [DOCKER.md](DOCKER.md) — Docker setup & deployment

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
| PostgreSQL | localhost:5432 | Local PostgreSQL |
| Ollama | http://localhost:11434 | Local LLM (optional) |

### Results Upload

Race results can be uploaded as **PDF, CSV, or Excel** files. The file is sent to an LLM (Anthropic Claude Haiku in production, Ollama locally) which extracts all runner data into a structured format regardless of layout variation. The admin then reviews and corrects any issues before saving.

LLM audit data (raw JSON, model name, token counts) is stored on every `UploadBatch` record for traceability.

## 🔐 Authentication

JWT token-based authentication with email confirmation. Three role levels:
- **ReadOnly** (default) — View data
- **Manager** — Manage races & results
- **Admin** — Full system access

## 📄 License

[License information coming soon]
