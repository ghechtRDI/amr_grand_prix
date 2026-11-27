# Racer Matching

The authoritative description of how uploaded results are matched to existing racers (`Runner`
records). **Keep this file in step with the code**: any change to thresholds, weights, name
candidates or age handling in the files below should update this document in the same commit.
Planned changes live in [RUNNER_MATCHING_IMPROVEMENTS_PLAN.md](RUNNER_MATCHING_IMPROVEMENTS_PLAN.md).

| Concern | Code |
|---|---|
| Scoring a row against every runner | `Services/ResultsProcessing/RunnerMatchingService.cs` (`FindMatchesForResultsAsync`, `ScoreMatches`) |
| Name candidates and alternate-name cleanup | `Services/Runners/RunnerNames.cs` |
| Estimated birth date range (windows, intersecting, estimate) | `Common/BirthDateRange.cs` |
| Age from DOB / estimated range | `Common/AgeCalculator.cs` |
| Stored columns | `Runner.EstimatedDobEarliest` / `EstimatedDobLatest`; `EstimatedBirthYear` is computed |
| Saving: new runners, birth-year estimates, alternate names | `Controllers/ResultsController.cs` (`SaveResults`) |
| Review UI | `AmrGrandPrix.Client/src/components/upload/DataReviewStep.jsx` (`RunnerMatchCell`, `rowHasIssue`) |
| Related: claim matching | `RunnerMatchingService.FindMatchesForProfileAsync`, `Controllers/RunnerClaimsController.cs` |
| Related: duplicate finder | `Services/Runners/RunnerMergeService.cs` |

## Where it runs

After the LLM extraction and `ResultsProcessingService` turn a file into `ResultRow`s, the upload
endpoint calls `FindMatchesForResultsAsync(rows, raceDate)`. It loads every runner once and
scores each row with a non-blank name against all of them. Ages are evaluated **as of the race
date**, not today.

## Name similarity

1. **Normalize** both names: lowercase, trim, collapse whitespace. Nothing else (punctuation,
   accents and word order are compared as-is).
2. **Similarity** = `1 − Levenshtein(a, b) / max(len(a), len(b))`, from 0 to 1. Identical
   normalized names score 1.0.
3. **Name candidates** for a runner (`RunnerNames.NameCandidates`), in order:
   - legal name (`FirstName LastName`)
   - each `AlternateNames` entry
   - `PreferredName` as entered
   - `PreferredName LastName`, when the preferred name is a single word (e.g. "Bob" → "Bob Smith")

   The row's score is the **best** similarity across all candidates (an exact legal-name match
   returns 1.0 immediately).
4. Runners scoring **below 0.75** are dropped. `NameMatch` is reported as true at **0.90+**.

## Estimated birth date range

Race results almost never carry a date of birth, only an age (or an age category) on the race
date. Each one pins the birth date to a window, and every result saved for a runner narrows
their **estimated birth date range** (`Runner.EstimatedBirthRange`, stored as
`EstimatedDobEarliest`/`EstimatedDobLatest`; a null bound is open-ended, both null = unknown).

- **Window for an exact age** `A` on race date `D`: born `D − (A+1) years + 1 day` …
  `D − A years`, inclusive (365–366 days). E.g. 40 on 2026-06-15 → 1985-06-16 … 1986-06-15.
- **Window for an age category** `[lo, hi]`: born `D − (hi+1) years + 1 day` … `D − lo years`.
  The oldest category ("90+") has no earliest bound. Unrecognized categories say nothing.
- **Combining** (`BirthDateRange.Combine`): an unknown side takes the other; otherwise the
  intersection. A window that doesn't overlap the range at all is a **conflict**: the range is
  kept and the result ignored for the estimate.
- **Estimate**: when the range is closed and at most 366 days wide (one exact age, or tighter),
  its midpoint is the estimated date of birth. `Runner.EstimatedBirthYear` is computed as the
  midpoint's year and is null for wider (category-only) ranges.
- A verified `DateOfBirth` always takes precedence when reading an age. The range is still kept
  up to date from results.

`AgeCalculator`:
- `GetBirthRange`: verified DOB as a one-day range, otherwise the estimated range.
- `GetRunnerAge`: age from the verified DOB, otherwise from the estimated midpoint, otherwise null.
- `GetRunnerAgeRange`: youngest and oldest the runner could be on a date.
- `IsAgeEstimated`: true when there's no verified DOB and the range is narrow.

## Age check

The row's window (from its exact age, or its category) is compared with the runner's range from
`AgeCalculator.GetBirthRange`:

| Row window | Runner range | Result |
|---|---|---|
| known | known | match if they overlap once the runner's range is widened by **1 year** each side (`AgeToleranceYears`) |
| unknown | — | neutral |
| — | unknown | neutral |

The 1-year tolerance absorbs results that report age as of Dec 31 or another reference date.
Category rows compare by overlap, so an estimate that straddles a category boundary isn't
penalized. Against a verified DOB, the uploaded age must be within ±1 of the true age.

"Neutral" earns the same confidence as a match, but `AgeMatch` is reported as `false`: it means
"confirmed", so a candidate whose age matches sorts ahead of an equally scored one with no age.

## Gender check

Matches when the row has no gender or the genders are equal.

## Confidence, display and auto-selection

```
confidence = name × 0.70 + (age match or neutral ? 0.15 : 0) + (gender match ? 0.15 : 0)   // capped at 1.0
```

- Candidates are sorted by confidence, then `NameMatch`, then `AgeMatch`.
- Only candidates at **0.70+** are returned to the client.
- The top candidate is **auto-selected at 0.95+** (in practice: name ≥ ~0.93 with the age and
  gender checks passing). Data Review still flags an auto-match below 1.0 for review.
- Each candidate carries `knownNames` (its name candidates) for the review UI, plus `age`
  (verified or estimated midpoint), `ageMin`/`ageMax` (youngest/oldest possible on the race
  date) and `hasVerifiedDateOfBirth`.

## Data Review (client)

- Each row's dropdown lists "+ New Runner" plus the candidates, showing the possible age span
  on the race date ("40", "40–41 (est.)", "40+ (est.)", "?") and confidence.
- Status pills: Auto-matched / Confirm match / Confirmed / New runner.
- "Issues only" view includes unconfirmed partial matches, auto-matches below 100%, and age
  disagreements with the matched runner that haven't been resolved.
- Hints under the dropdown:
  - **Update estimated age (X → Y)** checkbox when the uploaded exact age is outside
    `ageMin`…`ageMax`, i.e. saving would treat it as a conflict and ignore it (sets
    `UpdateRunnerAge`). A runner with a verified DOB shows a note instead. Ages inside the span
    narrow the range silently.
  - **No age on file — will be set to Y** when the runner's range is unknown.
  - **Will save “Name” as an alternate name** when the row's name isn't one of the runner's
    `knownNames` (case- and whitespace-insensitive).

## What saving changes (`SaveResults`)

For each row (after duplicate-runner and missing-gender skips):

- **Matched to an existing runner**
  - Age: the row's window (exact age, else category) is combined into the runner's estimated
    range: intersected, or ignored on conflict. If the admin ticked "Update estimated age" and
    the row has an exact age, the range is **replaced** with that age's window. A verified DOB
    is never changed.
  - Name: if the row's name isn't already one of the runner's name candidates, add it to
    `AlternateNames` (cleaned by `RunnerNames.Normalize`).
- **New runner**: `FirstName` = first word, `LastName` = the rest, gender from the row,
  estimated range = the row's window (exact age or category; unknown if neither).

Editing a single result later (race results page) doesn't change the runner's range.

## Known limitations

- Conflicts are only resolved by hand (the Data Review checkbox). The range never "forgets" an
  earlier result, so one bad age in the first upload can hold a runner's estimate until an
  admin replaces it.
- Ranges are folded in upload order, not race order. That only matters when results conflict.

## Backfill (migration `RunnerEstimatedBirthRange`)

Ranges for existing runners were computed in SQL. Each was seeded from the old
`EstimatedBirthYear` (`race year − age`, so born that year or the year before; this keeps admin
overrides as the anchor), then every result was folded in by race date using the same rules. A
seeded range that never narrowed to one age fell back to exactly the old year. On the dev data
(5,160 runners), no estimate was lost: 3,306 kept their year, 1,336 moved one year earlier (the
old formula's late bias), 22 gained an estimate, and 496 are known only by category. Down
migration restores `EstimatedBirthYear` from the range midpoint.

## Related matchers (same name logic, different weights)

- **Claim matching** (`FindMatchesForProfileAsync`): user's legal, preferred (also paired with
  their last name) and alternate names vs each runner's name candidates. Confidence =
  name × 0.60 + 0.30 (birth match) + 0.10 (gender matches or user has none). A birth match means
  the runner's verified DOB equals the user's, or (no verified DOB) the user's DOB falls inside a
  **narrow** estimated range; category-only ranges don't count. Claims auto-approve at 0.90 and
  queue for review at 0.80.
- **Duplicate finder** (`RunnerMergeService.FindPotentialDuplicatesAsync`): best similarity
  across both runners' name candidates × 0.70 + 0.15 gender + 0.15 birth year (DOB year or the
  computed `EstimatedBirthYear`) within 2 years (unknown is neutral). Pairs at 0.75+ are listed.
- **Merging** intersects the two estimated ranges. If they conflict, the kept runner's range is
  used and the preview warns.
- **Search indexing** (`SearchIndexing.IsRunnerIndexable`): without a verified DOB, a runner is
  indexable only if they're 18+ at the youngest their range allows (`EstimatedDobLatest`). So
  runners known only by an adult category are indexable, while "17 and Under" and unknown ages
  are not.
