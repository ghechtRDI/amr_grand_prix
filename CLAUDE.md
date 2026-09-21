# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview
Full-stack app for managing Alaska Mountain Runners Grand Prix race results, standings, and statistics.

## Tech Stack
- **Frontend**: React 19 + Vite (rolldown-vite), React Router, Tailwind CSS v4 + shadcn/ui (`src/components/ui/`), TanStack Table, react-hook-form
- **Backend**: .NET 9 Web API, ASP.NET Identity, JWT auth, EF Core + PostgreSQL
- **LLM**: Anthropic API (prod) / Ollama (dev) for race results extraction
- **Testing**: xUnit, Moq, FluentAssertions, EF Core InMemory, `Microsoft.AspNetCore.Mvc.Testing`

## Development Commands

### Running locally
```bash
docker-compose up -d mailhog          # Terminal 1 - email testing

cd AmrGrandPrix.API                   # Terminal 2 - API (Postgres must be running)
dotnet ef database update             # first time only
dotnet run                            # http://localhost:8080

cd AmrGrandPrix.Client                # Terminal 3 - Client
npm install                           # first time only
npm run dev                           # http://localhost:5173
```

### LLM configuration (dev)
`Llm:Provider` config defaults to `"Anthropic"`.
```bash
cd AmrGrandPrix.API
dotnet user-secrets set "Llm:Anthropic:ApiKey" "sk-ant-..."
```
For Ollama instead: `docker-compose up -d ollama`, pull a model, then set `"Llm": { "Provider": "Ollama" }` in `appsettings.Development.json`.

### Build, lint, test
```bash
cd AmrGrandPrix.API && dotnet build
cd AmrGrandPrix.Client && npm run lint

dotnet test
dotnet test --filter "FullyQualifiedName~GrandPrixCalculationServiceTests"
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
- `Race` — race event with `IsGrandPrixRace`, `Year`, `Date`
- `Runner` — person with gender, DOB, name
- `RaceResult` — links Runner to Race with time, place, age, gender, status (Finished/DNF/DNS/DQ)
- `GrandPrixPoints` — computed points per runner per race, split by Division (OpenMale/OpenFemale/OpenNonbinary/AgeMale/AgeFemale/AgeNonbinary); Nonbinary runners score in their own division, ranked only against other nonbinary finishers
- `GrandPrixStanding` — season standings per runner per division, best-4-races logic
- `UploadBatch` — tracks file uploads; includes LLM audit fields (RawLlmJson, LlmModel, LlmInputTokens, LlmOutputTokens)

**Scoring rules** (`Models/GrandPrixConstants.cs`, `Services/GrandPrix/GrandPrixCalculationService.cs`):
- Open Division: top 20 finishers per gender score points (100/90/85…1); +10 bonus for course record
- Age Division: top 5 per age category score 5/4/3/2/1
- Standings: best 4 races count; 7+ races earns "Run the Gamut" flag
- Age categories: "17 and Under", "19-29", "30-39", "40-49", "50-59", "60-69", "70-79", "80-89"

**Results upload pipeline** (`Services/`):
1. `ResultsController.UploadResults` receives file upload
2. `ILlmExtractionService` extracts text (PdfPig / plain text / ClosedXML) then calls the configured `ILlmProvider`
3. LLM returns structured JSON: `sections[].{name, gender, rows[].{place, name, age, gender, time_string, status, notes}}`
4. `ResultsProcessingService` converts `List<ExtractedSection>` → `List<ResultRow>` (validates, parses times, resolves gender from section header)
5. `RunnerMatchingService` fuzzy-matches names to existing `Runner` records
6. User reviews in the wizard and saves via `ResultsController.SaveResults`
7. `GrandPrixCalculationService` recalculates standings for Grand Prix races

**LLM services** (`Services/LlmExtraction/`):
- `ILlmExtractionService` — orchestrates text extraction + LLM call + JSON parsing
- `ILlmProvider` — abstraction over Anthropic (`AnthropicLlmProvider`, forced tool use) and Ollama (`OllamaLlmProvider`, `/api/chat` with `format: "json"`)
- Text extractors: `PdfTextExtractor` (PdfPig), `CsvTextExtractor`, `XlsxTextExtractor` (ClosedXML)
- Config keys: `Llm:Provider`, `Llm:Anthropic:ApiKey`/`Model`, `Llm:Ollama:BaseUrl`/`Model`

**Controllers**: `AuthController`, `RacesController`, `ResultsController`, `RunnersController`, `StandingsController`, `UserManagementController`

**Auth**: JWT bearer tokens, ASP.NET Identity, email confirmation via MailHog in dev. `ResultsController` requires `Admin`/`Manager` role; standings/results reads are `[AllowAnonymous]`.

**Database**: `ApplicationDbContext` extends `IdentityDbContext<ApplicationUser>`. Connection string via user secrets: `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=amr_grand_prix;..."`

### Frontend (`AmrGrandPrix.Client/src/`)

- **UI system**: Tailwind CSS v4 + shadcn/ui primitives in `components/ui/` (button, card, table, tabs, select, alert-dialog, etc.), styled with `class-variance-authority`; `lib/utils.js` has the `cn()` class merger. Light/dark theme via `hooks/useTheme.js`.
- **Layout & feature components**: `components/layout/`, `components/auth/`, `components/upload/`
- **Results upload wizard** (`pages/admin/ResultsUpload.jsx`): 4 steps — Race Selection → File Upload → Data Review → Confirmation, each in `components/upload/`. LLM handles extraction automatically; no column-mapping step.
- **Auth** (`contexts/AuthContext.jsx`, `services/authService.js`, `hooks/useAuth.js`, `useRequireAuth.js`, `useRequireRole.js`): JWT stored in context, auto-refresh.
- Vite proxies `/api` to the backend in development.

## Key Implementation Notes

- `GrandPrixCalculationService` has its own local scoring table that duplicates `GrandPrixConstants` — the constants file is the source of truth
- `Runner.FullName` is a computed property; `entity.Ignore(r => r.FullName)` in `ApplicationDbContext`
- New runners created during save use a birth year estimate from age; update once real DOB is known
- `RaceSeedingService` seeds initial race data on startup in development
- LLM audit data (raw JSON, model name, token counts) is stored on `UploadBatch` for every upload
