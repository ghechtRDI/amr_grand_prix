# LLM File Parser Plan

Replace the existing PDF/CSV/XLSX parsers with an LLM-driven extraction step that
converts any uploaded results file into a canonical structure. Downstream
validation, runner matching, and the wizard's review step remain the safety net.

## Constraints & decisions

- **Volume**: 10–15 uploads/year — cost is effectively irrelevant.
- **No scanned/image PDFs** — text extraction only, no OCR or vision LLM.
- **Local dev**: Ollama in `docker-compose`.
- **Prod**: direct Anthropic API (not Bedrock).
- **All formats route through the LLM** — no "fast path" for CSV/XLSX.

## Architecture

```
Upload → text extraction → LlmExtractionService → typed rows
       → ResultsProcessingService (validation + name splitting + runner match)
       → user review → save → GrandPrixCalculationService
```

The LLM emits canonical fields directly, so column mapping and section-selection
steps are no longer needed.

## What gets deleted

- `IFileParserService`, `FileParserFactory`
- `CsvParserService`, `ExcelParserService`
- `PdfParserService` (already deleted) and its helpers:
  `PdfWordExtractor`, `PdfHeaderDetector`, `PdfSectionDetector`, `PdfColumnDetector`
- The `sectionsAvailable` branch in `ResultsController` (LLM handles multi-section
  files in one call and returns section labels in its JSON)
- `CsvHelper`, `ExcelDataReader` NuGet refs (assuming no other consumers)
- `ColumnMappingStep.jsx` and the dynamic section-selection step in the wizard
- Header normalization, section detection, and column-mapping code in
  `ResultsProcessingService` (kept: "Last, First" splitting, time parsing,
  validation, runner matching)

## What gets added

### Text extraction (pre-LLM, deterministic)

- **PDF** → `PdfPig` (already a dep), layout-preserved text.
- **CSV** → `File.ReadAllText`.
- **XLSX** → one minimal reader (`ClosedXML` or `EPPlus`) that dumps cells to
  a plain-text table.

Output: a single "page-oriented" string that goes to the LLM.

### `ILlmExtractionService`

```csharp
Task<ExtractionResult> ExtractAsync(Stream file, string fileName, CancellationToken ct);

record ExtractionResult(
    List<ExtractedSection> Sections,
    string RawModelJson,
    string LlmModel,
    int InputTokens,
    int OutputTokens);

record ExtractedSection(string? Name, string? Gender, List<ExtractedRow> Rows);

record ExtractedRow(
    int? Place,
    string Name,
    int? Age,
    string? Gender,
    string? TimeString,
    string? Status,
    string? Notes);
```

### Provider abstraction

`ILlmProvider` with two implementations:

- **`OllamaLlmProvider`** — HTTP to `http://ollama:11434`, model
  `qwen2.5:14b`, `format: "json"`.
- **`AnthropicLlmProvider`** — `Anthropic.SDK` NuGet, Claude Haiku 4.5 default,
  structured output via forced tool use
  (`tool_choice: {type: "tool", name: "extract_results"}`).

Selected via `Llm:Provider` config (`Ollama` | `Anthropic`).

**Credentials**:
- Dev: Anthropic key in .NET user secrets (`Anthropic:ApiKey`).
- Prod: env var / AWS Secrets Manager.

### Prompt shape

- **System**: schema definition, status codes (`Finished`/`DNF`/`DNS`/`DQ`),
  AMR-specific conventions (Male/Female section labels, age category strings).
- **User**: `"filename: X.pdf\n---\n<extracted text>"`.
- **Tool schema (JSON)**: `sections[].{name, gender, rows[].{place, name, age,
  gender, time_string, status, notes}}`.
- **Temperature**: 0.
- **Chunking**: for files >~20k tokens of text, chunk by page and carry the
  last-seen section header as prompt context; merge outputs. Most files will
  fit in a single call.

### Persistence

Extend `UploadBatch`:

- `RawLlmJson` (string)
- `LlmModel` (string)
- `LlmInputTokens` (int)
- `LlmOutputTokens` (int)

New EF Core migration.

### `docker-compose.yml`

```yaml
ollama:
  image: ollama/ollama:latest
  ports: ["11434:11434"]
  volumes: [ollama_data:/root/.ollama]
```

One-time: `docker compose exec ollama ollama pull gwen2.5:14b-instruct`.

## Wizard changes (`ResultsUpload.jsx`)

New flow: **Race Selection → File Upload → Data Review → Confirmation**.

- `ColumnMappingStep` removed.
- Dynamic section-selection step removed.
- `hasSectionStep` flag removed.

## Golden-set tests

`AmrGrandPrix.API.Tests/LlmExtraction/` with 8–10 files chosen from
`ResultsArchive/` covering the format zoo:

- Multi-section (e.g., `2022-Grand-Prix-Results.pdf`)
- "Last, First" comma names
- Ancient formatting (e.g., `Bird-Ridge-1989.pdf`)
- Messy XLSX exports
- TATR-style narrow tables

Tests run against Ollama; skipped when `OLLAMA_URL` env var is unset. Assert
field-level accuracy on a snapshot.

## Build order

1. Delete old parsers + their tests.
2. Add `ILlmExtractionService`, `ILlmProvider`, `AnthropicLlmProvider`,
   `OllamaLlmProvider`.
3. Wire into `ResultsController.UploadAsync` (replaces `FileParserFactory`).
4. Strip `ResultsProcessingService` down to typed-input post-processing.
5. `UploadBatch` migration for audit fields.
6. `docker-compose` addition + README update.
7. Wizard cleanup: remove `ColumnMappingStep` and section step.
8. Golden-set tests.

## Cost sanity check (prod)

Claude Haiku 4.5 at ~$1/M input, $5/M output. A typical race file is 5–30k
input tokens and 10–50k output tokens → ~$0.10–$0.30 per upload → **~$1.50–$4.50
per year** at 15 uploads. Switching to Sonnet 4.6 (~5× cost) would still be
under $25/year.
