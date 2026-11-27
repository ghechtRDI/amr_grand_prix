# Database ERD

<!-- Generated from the EF Core model by AmrGrandPrix.API.Tests/Infrastructure/ErdGenerator.cs. Do not edit by hand. -->

PostgreSQL schema as built by EF Core migrations. ASP.NET Identity's role/claim/login/token tables are omitted.
Regenerate after changing the model:

```bash
UPDATE_ERD=1 dotnet test --filter "FullyQualifiedName~ErdTests"
```

Relationship labels name the foreign-key column(s) and delete behavior.

```mermaid
erDiagram
    GrandPrixPoints {
        uuid PointsId PK
        uuid RunnerId FK
        uuid RaceId FK
        uuid ResultId FK
        integer Year
        integer Division "enum Division"
        varchar(50) AgeCategory "nullable"
        integer Points
        boolean IsRecordBonus
        timestamptz CreatedAt
    }
    GrandPrixSeasons {
        integer Year PK
        boolean IsFinalized
        timestamptz FinalizedAt "nullable"
        text FinalizedBy "nullable"
    }
    GrandPrixStandings {
        uuid StandingId PK
        uuid RunnerId FK
        integer Year
        integer Division "enum Division"
        varchar(50) AgeCategory "nullable"
        integer TotalPoints
        integer RacesCompleted
        integer RacesCounted
        integer BestRacePoints
        integer SecondBestRacePoints
        integer ThirdBestRacePoints
        integer FourthBestRacePoints
        boolean RunTheGamutQualified
        integer Rank
        timestamptz LastUpdated
    }
    RaceResults {
        uuid ResultId PK
        uuid RaceId FK
        uuid RunnerId FK
        integer Bib "nullable"
        integer Place "nullable"
        integer PlaceGender "nullable"
        integer PlaceAgeCategory "nullable"
        interval Time "nullable"
        integer Age "nullable"
        varchar(50) AgeCategory "nullable"
        integer Gender "enum Gender"
        integer Status "enum ResultStatus"
        varchar(500) Notes "nullable"
        boolean IsNewRecord
        timestamptz CreatedAt
        text UploadedBy "nullable"
        uuid UploadBatchId FK
    }
    RaceSeries {
        uuid RaceSeriesId PK
        varchar(200) Name UK
        varchar(1000) Description "nullable"
        timestamptz CreatedAt
    }
    RaceVariants {
        uuid RaceVariantId PK
        uuid RaceSeriesId FK
        varchar(100) Name
        text[] Aliases
        varchar(1000) Description "nullable"
        boolean IsGrandPrixByDefault
        boolean IsAgeGroupOnly
        boolean CountsTowardGamut
        integer DisplayOrder
        interval RecordTimeMale "nullable"
        interval RecordTimeFemale "nullable"
        varchar(200) RecordHolderMale "nullable"
        varchar(200) RecordHolderFemale "nullable"
        timestamptz CreatedAt
    }
    Races {
        uuid RaceId PK
        uuid RaceVariantId FK
        boolean IsGrandPrixRace
        date Date
        integer Year
        varchar(200) Location "nullable"
        timestamptz CreatedAt
        text CreatedBy "nullable"
    }
    ResultReports {
        uuid ReportId PK
        varchar(200) RunnerNameReported
        date DateOfBirthReported "nullable"
        uuid RaceId FK "nullable"
        varchar(200) RaceName "nullable"
        date RaceDate "nullable"
        varchar(2000) Description
        varchar(200) ReporterEmail
        integer Status "enum ReportStatus"
        timestamptz SubmittedAt
    }
    RunnerClaims {
        uuid ClaimId PK
        text ApplicationUserId FK
        uuid RunnerId FK
        integer Status "enum ClaimStatus"
        timestamptz RequestedAt
        timestamptz ReviewedAt "nullable"
        text ReviewedByUserId "nullable"
        float8 MatchConfidence "nullable"
        varchar(500) Notes "nullable"
    }
    RunnerNotDuplicates {
        uuid Id PK
        uuid RunnerAId FK
        uuid RunnerBId FK
        timestamptz DismissedAt
        text DismissedByUserId "nullable"
    }
    Runners {
        uuid RunnerId PK
        varchar(100) FirstName
        varchar(100) LastName
        date DateOfBirth "nullable"
        date EstimatedDobEarliest "nullable"
        date EstimatedDobLatest "nullable"
        integer Gender "enum Gender"
        varchar(200) Email "nullable"
        varchar(100) PreferredName "nullable"
        text[] AlternateNames
        timestamptz CreatedAt
        timestamptz UpdatedAt
    }
    UploadBatches {
        uuid UploadBatchId PK
        uuid RaceId FK "nullable"
        uuid RaceSeriesId FK "nullable"
        date RaceDate "nullable"
        uuid[] IncludedVariantIds
        varchar(255) FileName
        integer FileType "enum FileType"
        integer RecordsUploaded
        text UploadedBy
        timestamptz UploadedAt
        integer Status "enum UploadStatus"
        text ExtractedText "nullable"
        text[] KnownVariants
        boolean OnlyTheseVariants
        integer ExpectedRows "nullable"
        varchar(2000) ExtractionError "nullable"
        text ReviewDraftJson "nullable"
        timestamptz ReviewDraftSavedAt "nullable"
        text RawLlmJson "nullable"
        text LlmModel "nullable"
        integer LlmInputTokens
        integer LlmOutputTokens
    }
    Users {
        text Id PK
        varchar(256) UserName "nullable"
        varchar(256) NormalizedUserName UK "nullable"
        varchar(256) Email "nullable"
        varchar(256) NormalizedEmail "nullable"
        boolean EmailConfirmed
        text PasswordHash "nullable"
        text SecurityStamp "nullable"
        text ConcurrencyStamp "nullable"
        text PhoneNumber "nullable"
        boolean PhoneNumberConfirmed
        boolean TwoFactorEnabled
        timestamptz LockoutEnd "nullable"
        boolean LockoutEnabled
        integer AccessFailedCount
        text FirstName "nullable"
        text LastName "nullable"
        text PreferredName "nullable"
        text Hometown "nullable"
        text[] AlternateNames
        date DateOfBirth "nullable"
        integer Gender "nullable; enum Gender"
        text RefreshToken "nullable"
        timestamptz RefreshTokenExpiryTime "nullable"
        timestamptz CreatedAt
        timestamptz UpdatedAt "nullable"
        uuid RunnerId FK "nullable"
    }

    RaceResults ||--o{ GrandPrixPoints : "ResultId (Cascade)"
    RaceSeries |o--o{ UploadBatches : "RaceSeriesId (Cascade)"
    RaceSeries ||--o{ RaceVariants : "RaceSeriesId (Cascade)"
    RaceVariants ||--o{ Races : "RaceVariantId (Restrict)"
    Races |o--o{ ResultReports : "RaceId (SetNull)"
    Races |o--o{ UploadBatches : "RaceId (Cascade)"
    Races ||--o{ GrandPrixPoints : "RaceId (Cascade)"
    Races ||--o{ RaceResults : "RaceId (Cascade)"
    Runners |o--o{ Users : "RunnerId (SetNull)"
    Runners ||--o{ GrandPrixPoints : "RunnerId (Cascade)"
    Runners ||--o{ GrandPrixStandings : "RunnerId (Cascade)"
    Runners ||--o{ RaceResults : "RunnerId (Restrict)"
    Runners ||--o{ RunnerClaims : "RunnerId (Cascade)"
    Runners ||--o{ RunnerNotDuplicates : "RunnerAId (Cascade)"
    Runners ||--o{ RunnerNotDuplicates : "RunnerBId (Cascade)"
    UploadBatches ||--o{ RaceResults : "UploadBatchId (Restrict)"
    Users ||--o{ RunnerClaims : "ApplicationUserId (Cascade)"
```
