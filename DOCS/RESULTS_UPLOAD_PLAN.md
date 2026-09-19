# Results Upload Tool - Implementation Plan

## Implementation Status

**Last Updated**: April 28, 2026

### Progress Overview
- ✅ **Phase 1**: Database & Core Models (COMPLETED)
- ✅ **Phase 2**: File Parsing Backend (COMPLETED + TESTED)
- ✅ **Phase 3**: Results Processing Backend (COMPLETED + TESTED)
- ✅ **Phase 4**: Grand Prix Calculation Backend (COMPLETED + TESTED)
- ✅ **Phase 5**: API Controllers (COMPLETED)
- ✅ **Phase 6**: Frontend - Upload Wizard (COMPLETED)
- 🔄 **Phase 7**: Frontend - Results & Standings (IN PROGRESS)
- ⏳ **Phase 8**: Testing & Refinement (NOT STARTED)
- ⏳ **Phase 9**: Documentation & Deployment (NOT STARTED)

### Test Coverage Summary
- **Total Tests**: 212 (all passing ✅)
  - Auth & Identity Tests: 82
  - CSV Parser Tests: 14
  - Results Processing Tests: 63
  - Runner Matching Tests: 36
  - Grand Prix Calculation Tests: 58

### Current Sprint Focus
**Phase 7: Frontend - Results & Standings**
- Standings dashboard (`/standings/:year`)
- Race results display page (`/races/:raceId/results`)
- Results management page for admins (`/admin/results`)
- Navigation links from Home page

### Next Steps
1. **Immediate** (Phase 7): Implement results & standings display UI
   - Standings dashboard (/standings/:year)
   - Race results page (/races/:raceId/results)
   - Admin results management (/admin/results)
2. **Short-term** (Phase 8): Testing & refinement
3. **Medium-term** (Phase 9): Documentation & deployment

---

## Overview
A comprehensive results upload and management system for Alaska Mountain Runners Grand Prix races. The tool allows Managers and Admins to upload race results in CSV/Excel format or by pasting text, review and validate the data, and automatically calculate Grand Prix standings.

## Table of Contents
- [Core Requirements](#core-requirements)
- [Grand Prix Rules Reference](#grand-prix-rules-reference)
- [Data Models](#data-models)
- [Architecture](#architecture)
- [Implementation Phases](#implementation-phases)
- [Technical Details](#technical-details)
- [User Interface](#user-interface)
- [Testing Strategy](#testing-strategy)

---

## Core Requirements

### Functional Requirements
1. **File Upload / Text Paste**
   - Accept CSV and Excel (.xlsx, .xls) file uploads
   - Accept pasted text (tab- or comma-separated) — replaces PDF support
   - Paste modes: single mixed-gender block, or separate male/female blocks
   - Required fields: Name, Age, Place, Time, Gender
   - Optional fields: Bib, Category, Notes

2. **Data Parsing**
   - Intelligent header detection (case-insensitive, flexible naming)
   - Handle gender from row data OR from pasted-gender-block mode
   - Parse times in various formats (HH:MM:SS, MM:SS, H:MM:SS.mmm)
   - Handle DNF (Did Not Finish), DNS (Did Not Start), DQ (Disqualified)

3. **Data Review & Validation**
   - Display parsed data in editable table
   - Highlight validation issues (missing data, invalid formats)
   - Allow manual corrections before saving
   - Duplicate detection (same runner, same race)
   - Age/gender verification against existing runner profiles

4. **Race Configuration**
   - Select race from predefined list (9 Grand Prix races + custom races)
   - Mark as Grand Prix race or non-GP race
   - Specify race date and course variant (e.g., Knoya Full Monty vs Original)
   - Associate with specific year

5. **Grand Prix Calculation**
   - Automatically calculate GP points for GP races
   - Update overall standings after results save
   - Handle Open Division (Top 20, FIS scoring)
   - Handle Age Division (Top 5, simplified scoring) — supports specific ages and age group categories
   - Count best 4 races per runner
   - Track "Run the Gamut" qualification (7 of 9 races)

6. **Access Control**
   - Only Managers and Admins can upload results
   - ReadOnly users can view results but not upload

### Non-Functional Requirements
- **Performance**: Handle files up to 10,000 rows
- **Usability**: Clear UI with progress indicators
- **Reliability**: Transaction-based saves (all or nothing)
- **Auditability**: Track who uploaded which results and when

---

## Grand Prix Rules Reference

### Scoring Systems

#### Open Division
- Top 20 finishers (male and female separately)
- Scoring:
  - 1st: 100 points
  - 2nd: 90 points
  - 3rd: 85 points
  - 4th: 80 points
  - 5th: 75 points
  - 6th-19th: Decrement by 5 each
  - 20th: 1 point
- New Record: +10 bonus points
- Count best 4 races (of 9 max)
- Must finish Top 20 in at least 1 race to be eligible

#### Age Division
- Top 5 finishers per age category
- Simple scoring:
  - 1st: 5 points
  - 2nd: 4 points
  - 3rd: 3 points
  - 4th: 2 points
  - 5th: 1 point
- Count best 4 races
- Must finish Top 5 in at least 1 race to be eligible

#### Age Categories
- 17 and Under
- 19-29
- 30-39
- 40-49
- 50-59
- 60-69
- 70-79
- 80-89

*Note: Runners can accumulate points in 2 age groups if birthday falls during GP season*

### Grand Prix Races
1. Crazy Lazy
2. Kal's Knoya Ridge Run
   - GP Race: The Full Monty
   - Age 17-under & 60+: Dome/Original also counts
3. Government Peak Climb
   - GP Race: Up-and-Down
   - Age 17-under & 60+: Uphill Only also counts
4. Robert Spurr Memorial Hill Climb (Bird Ridge)
5. Juneau Ridge Race
6. Mount Marathon Race
7. Cirque Series Alyeska
8. Matanuska Peak Challenge
9. Veins of Gold

### Special Awards
- **Overall Winners**: Each GP race winner (M/F) earns Mount Marathon bib
- **Run the Gamut**: Complete 7 of 9 GP events

---

## Data Models

### Database Schema

#### **Races** Table
```sql
- RaceId (PK, GUID)
- Name (string, required)
- IsGrandPrixRace (bool)
- GrandPrixRaceOrder (int, nullable) -- 1-9 for GP races
- Date (date, required)
- Year (int, required, indexed)
- CourseVariant (string, nullable) -- e.g., "Full Monty", "Uphill Only"
- Location (string)
- RecordTimeMale (TimeSpan, nullable)
- RecordTimeFemale (TimeSpan, nullable)
- RecordHolderMale (string, nullable)
- RecordHolderFemale (string, nullable)
- CreatedAt (datetime)
- CreatedBy (userId, FK)
```

#### **Runners** Table
```sql
- RunnerId (PK, GUID)
- FirstName (string, required)
- LastName (string, required)
- DateOfBirth (date, nullable)
- Gender (enum: Male, Female, Nonbinary)
- Email (string, nullable)
- CreatedAt (datetime)
- UpdatedAt (datetime)
```

#### **RaceResults** Table
```sql
- ResultId (PK, GUID)
- RaceId (FK, required)
- RunnerId (FK, required)
- Bib (int, nullable)
- Place (int, nullable) -- null for DNF/DNS/DQ
- PlaceGender (int, nullable) -- place within gender
- PlaceAgeCategory (int, nullable) -- place within age category
- Time (TimeSpan, nullable) -- null for DNF/DNS/DQ
- Age (int, required)
- Gender (enum, required)
- Status (enum: Finished, DNF, DNS, DQ)
- Notes (string, nullable)
- IsNewRecord (bool)
- CreatedAt (datetime)
- UploadedBy (userId, FK)
- UploadBatchId (GUID) -- groups results from same upload
```

#### **GrandPrixPoints** Table
```sql
- PointsId (PK, GUID)
- RunnerId (FK, required)
- RaceId (FK, required)
- ResultId (FK, required)
- Year (int, required, indexed)
- Division (enum: OpenMale, OpenFemale, AgeMale, AgeFemale)
- AgeCategory (string, nullable) -- e.g., "30-39"
- Points (int, required)
- IsRecordBonus (bool)
- CreatedAt (datetime)
```

#### **GrandPrixStandings** (Calculated View/Table)
```sql
- StandingId (PK, GUID)
- RunnerId (FK, required)
- Year (int, required, indexed)
- Division (enum)
- AgeCategory (string, nullable)
- TotalPoints (int) -- sum of best 4 races
- RacesCompleted (int)
- RacesCounted (int) -- up to 4
- BestRacePoints (int) -- for tiebreaker
- SecondBestRacePoints (int) -- for tiebreaker
- RunTheGamutQualified (bool) -- 7+ races
- Rank (int)
- LastUpdated (datetime)
```

#### **UploadBatches** Table
```sql
- UploadBatchId (PK, GUID)
- RaceId (FK, required)
- FileName (string, required)
- FileType (enum: CSV, Excel, Text)
- RecordsUploaded (int)
- UploadedBy (userId, FK)
- UploadedAt (datetime)
- Status (enum: Pending, Validated, Saved, Cancelled)
```

---

## Architecture

### Backend Components

#### 1. **File Parsing Service**
- `IFileParserService`
  - `ParseAsync(Stream file, string filename)`
- Returns: `List<RawResultRow>`

> **Note**: PDF parsing was evaluated and dropped due to the complexity of extracting structured data from varied PDF layouts. Instead, a text-paste workflow was implemented (see below).

**Libraries:**
- CSV/Text: CsvHelper (handles comma and tab delimiters via auto-detection)
- Excel: ClosedXML

#### 2. **Text Paste Endpoint** (`/api/results/parse-text`)
- Accepts raw text content (tab- or comma-separated) with an optional gender override
- Routes through `CsvParserService` (auto-delimiter detection)
- Caller can submit male and female blocks separately; results are merged by the frontend
- Cached results keyed by `UploadBatchId` via `IMemoryCache` (30-min TTL)

#### 3. **Results Processing Service**
- `IResultsProcessingService`
  - `ProcessResultsAsync(RawResultRow[])` — normalize, validate, detect gender
  - `ValidateRow(ResultRow)` — re-validate a single row

#### 4. **Grand Prix Calculation Service**
- `IGrandPrixCalculationService`
  - `CalculateRacePointsAsync(RaceId)` — after results saved
  - `UpdateStandingsAsync(int year)` — recalculate season standings
- Supports both specific ages and age group categories (e.g., runners categorized into "30-39")

#### 5. **Results Upload Controller**
- `POST /api/results/upload` — upload CSV/Excel file, return parsed data
- `POST /api/results/parse-text` — parse pasted text, return parsed data
- `POST /api/results/validate` — validate parsed data
- `POST /api/results/save` — save to database
- `GET /api/results/race/{raceId}` — get results for race
- `GET /api/results/batches` — list upload batches (admin)
- `DELETE /api/results/batch/{batchId}` — delete upload batch

#### 6. **Races Controller**
- `GET /api/races` — list all races
- `GET /api/races/{year}` — races for year
- `GET /api/races/detail/{id}` — get single race
- `POST /api/races` — create race
- `PUT /api/races/{id}` — update race
- `DELETE /api/races/{id}` — delete race (no results only)

#### 7. **Standings Controller**
- `GET /api/standings/{year}` — all standings for year
- `GET /api/standings/{year}/open/{gender}` — open male/female standings
- `GET /api/standings/{year}/age/{category}/{gender}` — age category standings
- `GET /api/standings/runner/{runnerId}` — runner's GP history
- `GET /api/standings/years` — years with data
- `GET /api/standings/{year}/categories` — age categories for year
- `POST /api/standings/{year}/recalculate` — manual recalc

### Frontend Components

#### 1. **Upload Wizard** (`pages/admin/ResultsUpload.jsx`) ✅
Multi-step wizard: Race Selection → Input Method → Column Mapping → Data Review → Confirmation

**Step 2: Input Method** (replaces old "File Upload" step)
- Method toggle: "Upload File" or "Paste Text"
- File upload: drag-and-drop for CSV/Excel (PDF no longer supported)
- Paste text: supports "All genders in one block" or "Separate by gender"
  - Separate-by-gender allows submitting male and female result blocks independently, each with their own header row

#### 2. **Standings Dashboard** (`pages/Standings.jsx`) 🔄
- `/standings/:year` (defaults to current year)
- Year selector
- Division navigation: Open (Male/Female) and Age Divisions (category + gender selector)
- Standings table: rank, name, total points, races completed, run the gamut badge
- Points breakdown per runner expandable

#### 3. **Race Results Page** (`pages/RaceResults.jsx`) 🔄
- `/races/:raceId/results`
- Race header: name, date, location, GP badge
- Results table: place, name, age, gender, time, status
- GP points column if race is a Grand Prix race

#### 4. **Admin Results Management** (`pages/admin/ResultsManagement.jsx`) 🔄
- `/admin/results`
- Table of races with result counts
- Navigate to race results
- Delete upload batches
- Trigger GP recalculation

---

## Implementation Phases

### Phase 1: Database & Core Models ✅ COMPLETED

### Phase 2: File Parsing Backend ✅ COMPLETED

**Actual deliverables** (differs from original plan):
- ✅ `CsvParserService.cs` — handles CSV and pasted text (auto-delimiter detection)
- ✅ `ExcelParserService.cs` — .xlsx and .xls
- ✅ `FileParserFactory.cs` — routes by file extension
- ❌ `PdfParserService.cs` — **REMOVED**: PDF parsing dropped in favor of text-paste workflow
- ✅ `ResultsController` — added `POST /api/results/parse-text` endpoint for pasted text

**Why PDF was dropped**: PDF layouts vary too much between race organizers (scanned images, columnar PDFs, HTML-to-PDF exports). A copy-paste workflow is simpler, handles all source formats, and lets the user correct any parsing issues before they enter the review step.

---

### Phase 3: Results Processing Backend ✅ COMPLETED

---

### Phase 4: Grand Prix Calculation Backend ✅ COMPLETED

**Age division support**: Results include both a specific `Age` field and a computed age category (e.g., "30-39"). The `GrandPrixCalculationService` determines the appropriate age bracket from the runner's age at race time and assigns age division points accordingly.

---

### Phase 5: API Controllers ✅ COMPLETED

---

### Phase 6: Frontend - Upload Wizard ✅ COMPLETED

**Actual deliverables** (differs from original plan):
- ✅ `ResultsUpload.jsx` — wizard container, 5-step progress indicator
- ✅ `RaceSelectionStep.jsx` — race dropdown + new race form
- ✅ `InputMethodStep.jsx` — **replaces** `FileUploadStep.jsx`; supports file upload (CSV/Excel) AND text paste (mixed/by-gender modes)
- ❌ `FileUploadStep.jsx` — **REMOVED**: merged into InputMethodStep
- ✅ `ColumnMappingStep.jsx` — column detection and mapping
- ✅ `DataReviewStep.jsx` — editable table with validation highlighting
- ✅ `ConfirmationStep.jsx` — summary and save
- ✅ `upload.css` — dark theme styling

---

### Phase 7: Frontend - Results & Standings 🔄 IN PROGRESS

**Goal**: Display results and standings so uploads can be verified and the public can view GP data.

1. **Standings Dashboard** (`src/pages/Standings.jsx`)
   - Year selector (fetched from `/api/standings/years`)
   - Open division: male and female tabs
   - Age division: category dropdown + gender toggle
   - Standings table with rank, name, points, races, run-the-gamut badge

2. **Race Results Page** (`src/pages/RaceResults.jsx`)
   - Race metadata header
   - Results table sorted by place
   - GP points column if applicable

3. **Admin Results Management** (`src/pages/admin/ResultsManagement.jsx`)
   - List of races with uploaded results
   - Delete batch
   - Recalculate standings trigger

4. **Home Page** — add navigation links to standings, upload, and results

**New backend endpoint needed**:
- `GET /api/results/batches?year={year}` — list upload batches for admin management

---

### Phase 8: Testing & Refinement ⏳ NOT STARTED

### Phase 9: Documentation & Deployment ⏳ NOT STARTED

---

## Technical Details

### Column Header Normalization
Flexible pattern matching maps variations like "First Name", "F Name", "Finish Time", "Clock Time", "M/F" to canonical field names (Name, Age, Place, Time, Gender, Bib).

### Time Parsing
Supports: `H:MM:SS`, `H:MM:SS.mmm`, `MM:SS`, `MM:SS.mmm`, plus plain seconds. DNF/DNS/DQ strings are recognized and set `Status` accordingly.

### Runner Matching (Fuzzy)
Levenshtein distance with age (±2 years) and gender confirmation. High-confidence matches (>95%) are auto-applied; lower matches surface in the Data Review step for manual resolution.

### Text Paste Workflow
1. User pastes text into `InputMethodStep` (mixed or separate-by-gender)
2. Frontend sends `POST /api/results/parse-text` with `{ raceId, textContent, gender? }`
3. Backend parses with `CsvParserService` (auto-detects comma/tab delimiter)
4. Gender override applied to all rows if provided
5. For separate-by-gender: frontend calls endpoint twice (male + female) and merges results; the older batch is deleted so only one batch ID is carried forward
6. Remainder of wizard (column mapping, data review, confirmation) proceeds identically to file upload path

---

## User Interface

### Upload Wizard Steps
1. **Race Selection** — pick existing race or create new
2. **Input Method** — file upload (CSV/Excel) or paste text
3. **Column Mapping** — map detected columns to required fields
4. **Data Review** — edit table, resolve warnings, match runners
5. **Confirmation** — summary + save button

### Standings Dashboard
```
┌──────────────────────────────────────────────────────────┐
│ Grand Prix Standings                [Year: 2025 ▼]       │
├──────────────────────────────────────────────────────────┤
│  [Open Division]  [Age Divisions]                        │
│                                                          │
│    [Male]  [Female]                                      │
│                                                          │
│  Open Male Division                                      │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ Rank │ Runner         │ Total │ Races │ Gamut        │ │
│  │   1  │ John Doe       │  355  │  7/9  │  ✓          │ │
│  │   2  │ Mike Johnson   │  340  │  5/9  │             │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

---

## Testing Strategy

### Backend Testing
- Unit tests for CSV parser, results processing, GP calculation
- Integration tests for API endpoints
- Edge cases: duplicate names, DNF/DNS, record bonus, tiebreakers

### Frontend Testing
- Manual testing of upload flow with real race result files
- Verify GP calculations match expected values

### Manual Testing Checklist
- [ ] Upload small CSV (< 50 results)
- [ ] Upload large CSV (1000+ results)
- [ ] Upload Excel file
- [ ] Paste mixed-gender results
- [ ] Paste separate male/female results
- [ ] Edit results in review step
- [ ] Match existing runners
- [ ] Verify GP points calculation
- [ ] Verify standings update
- [ ] View standings dashboard
- [ ] View race results page
- [ ] Delete upload batch
- [ ] Recalculate standings
- [ ] Test as Manager role
- [ ] Test as Admin role
- [ ] Verify ReadOnly users cannot upload

---

## Security Considerations

1. **File Upload Security**:
   - Validate file size (max 10MB)
   - Validate file type (whitelist: .csv, .xlsx, .xls only — PDF removed)
   - Sanitize all user input

2. **Input Validation**:
   - Parameterized queries via EF Core
   - HTML-escape runner names in frontend output

3. **Authorization**:
   - `[Authorize(Roles = "Admin,Manager")]` on all write endpoints
   - `[AllowAnonymous]` on standings/results read endpoints

4. **Audit Trail**:
   - `UploadBatch` records track file name, type, user, timestamp
   - `UploadedBy` on each `RaceResult`

---

## Performance Considerations

1. **Large File Handling**: Stream parsing, batch database inserts
2. **GP Calculations**: Only recalculate affected year; indexed on Year + Division
3. **Frontend**: TanStack Table with client-side sorting for results tables

---

## Timeline Summary

| Phase | Task | Status |
|-------|------|--------|
| 1 | Database & Core Models | ✅ COMPLETE |
| 2 | File Parsing Backend | ✅ COMPLETE (PDF dropped, text-paste added) |
| 3 | Results Processing Backend | ✅ COMPLETE |
| 4 | Grand Prix Calculation Backend | ✅ COMPLETE |
| 5 | API Controllers | ✅ COMPLETE |
| 6 | Frontend Upload Wizard | ✅ COMPLETE (InputMethodStep replaces FileUploadStep) |
| 7 | Frontend Results & Standings | 🔄 IN PROGRESS |
| 8 | Testing & Refinement | ⏳ NOT STARTED |
| 9 | Documentation & Deployment | ⏳ NOT STARTED |
