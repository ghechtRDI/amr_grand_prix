# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
Full-stack web application for managing Alaska Mountain Runners Grand Prix race results, standings, and statistics.

## Tech Stack
- **Frontend**: React 19 + Vite (rolldown-vite 7.1.12), React Router, TanStack Table, react-hook-form
- **Backend**: .NET 9 Web API, ASP.NET Identity, JWT auth, EF Core + PostgreSQL
- **LLM**: Anthropic API (prod) / Ollama (dev) for race results extraction
- **Testing**: xUnit, Moq, FluentAssertions, EF Core InMemory, `Microsoft.AspNetCore.Mvc.Testing`

## Development Commands

### Running locally (recommended)
```bash
# Terminal 1 - MailHog for email testing
docker-compose up -d mailhog

# Terminal 2 - API (PostgreSQL must be running on localhost)
cd AmrGrandPrix.API
dotnet ef database update   # first time only
dotnet run                  # http://localhost:8080

# Terminal 3 - Client
cd AmrGrandPrix.Client
npm install                 # first time only
npm run dev                 # http://localhost:5173
```

### LLM configuration (dev)
The API reads `Llm:Provider` from config. Default is `"Anthropic"`.

**Anthropic (recommended for dev):**
```bash
cd AmrGrandPrix.API
dotnet user-secrets set "Llm:Anthropic:ApiKey" "sk-ant-..."
```

**Ollama (local, no cost):**
```bash
docker-compose up -d ollama
docker compose exec ollama ollama pull gwen2.5:14b-instruct
# Then set in appsettings.Development.json:
# "Llm": { "Provider": "Ollama" }
```

### Build & lint
```bash
cd AmrGrandPrix.API && dotnet build
cd AmrGrandPrix.Client && npm run lint
```

### Tests
```bash
# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~GrandPrixCalculationServiceTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~GrandPrixCalculationServiceTests.MethodName"
```

### Database migrations
```bash
cd AmrGrandPrix.API
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Architecture

### Backend (`AmrGrandPrix.API/`)

**Data model** (`Models/`):
- `Race` — a race event with `IsGrandPrixRace`, `Year`, `Date`
- `Runner` — a person with gender, DOB, name
- `RaceResult` — links Runner to Race with time, place, age, gender, status (Finished/DNF/DNS/DQ)
- `GrandPrixPoints` — computed points per runner per race, split by Division (OpenMale/OpenFemale/AgeMale/AgeFemale)
- `GrandPrixStanding` — computed season standings per runner per division, with best-4-races logic
- `UploadBatch` — tracks file uploads; includes LLM audit fields (RawLlmJson, LlmModel, LlmInputTokens, LlmOutputTokens)

**Scoring rules** (`Models/GrandPrixConstants.cs`, `Services/GrandPrix/GrandPrixCalculationService.cs`):
- Open Division: top 20 finishers per gender score points (100/90/85…1); +10 bonus for course record
- Age Division: top 5 per age category score 5/4/3/2/1
- Standings: best 4 races count; 7+ races earns "Run the Gamut" flag
- Age categories: "17 and Under", "19-29", "30-39", "40-49", "50-59", "60-69", "70-79", "80-89"

**Results upload pipeline** (`Services/`):
1. `ResultsController.UploadResults` receives file upload
2. `ILlmExtractionService` extracts text (PdfPig / plain text / ClosedXML) then calls the configured `ILlmProvider`
3. LLM (Anthropic or Ollama) returns a structured JSON with `sections[].{name, gender, rows[].{place, name, age, gender, time_string, status, notes}}`
4. `ResultsProcessingService` converts `List<ExtractedSection>` → `List<ResultRow>` (validates, parses times, resolves gender from section header)
5. `RunnerMatchingService` fuzzy-matches names to existing `Runner` records
6. User reviews in the wizard and saves via `ResultsController.SaveResults`
7. `GrandPrixCalculationService` recalculates standings for Grand Prix races

**LLM services** (`Services/LlmExtraction/`):
- `ILlmExtractionService` / `LlmExtractionService` — orchestrates text extraction + LLM call + JSON parsing
- `ILlmProvider` — abstraction over Anthropic and Ollama
- `AnthropicLlmProvider` — HTTP calls to `api.anthropic.com/v1/messages` with forced tool use
- `OllamaLlmProvider` — HTTP calls to Ollama `/api/chat` with `format: "json"`
- Text extractors: `PdfTextExtractor` (PdfPig), `CsvTextExtractor` (plain text), `XlsxTextExtractor` (ClosedXML)
- Config: `Llm:Provider` (`"Anthropic"` | `"Ollama"`), `Llm:Anthropic:ApiKey` (user secrets), `Llm:Anthropic:Model`, `Llm:Ollama:BaseUrl`, `Llm:Ollama:Model`

**Controllers** (`Controllers/`): `AuthController`, `RacesController`, `ResultsController`, `RunnersController`, `StandingsController`, `UserManagementController`

**Auth**: JWT bearer tokens, ASP.NET Identity, email confirmation via MailHog in dev. `ResultsController` requires `Admin` or `Manager` role; standings/results reads are `[AllowAnonymous]`.

**Database**: `ApplicationDbContext` extends `IdentityDbContext<ApplicationUser>`. Connection string in .NET user secrets (not appsettings.json): `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=amr_grand_prix;..."`

### Frontend (`AmrGrandPrix.Client/src/`)

**Results upload wizard** (`pages/admin/ResultsUpload.jsx`):
4-step wizard: **Race Selection → File Upload → Data Review → Confirmation**. The LLM handles extraction automatically — no column mapping or section selection steps.

**Auth** (`contexts/AuthContext.jsx`, `services/authService.js`): JWT stored in context, auto-refresh.

**API proxy**: Vite proxies `/api` to backend during development.

## Key Implementation Notes

- `GrandPrixCalculationService` has its own local scoring table that duplicates `GrandPrixConstants` — the constants file is the source of truth
- `Runner.FullName` is a computed property; `entity.Ignore(r => r.FullName)` in `ApplicationDbContext`
- New runners created during save use a birth year estimate from age; update once real DOB is known
- `RaceSeedingService` seeds initial race data on startup in development
- LLM audit data (raw JSON, model name, token counts) is stored on `UploadBatch` for every upload
