# Grand Prix Points Breakdown — Plan

## Goal

Add a third **Points Breakdown** tab to the Standings page. It should match the published
"Alaska Mountain Running Grand Prix Results — Overall and Age Groups" sheet: for one gender, show
every runner's points in every Grand Prix race so far, first for the **Overall (Open)** division
and then for **each age group**. A Male/Female toggle switches gender.

Separately, make **Female** the default gender on every Standings tab (it is Male today).

## Reference: the current spreadsheet

The published sheet (2026 tab) is one long page per gender:

```
2026 Alaska Mountain Running Grand Prix Results Overall WOMEN
Rank | Name              | Total | C Lazy | Knoya | Blueberry | Bird | Juneau RR | Mt Marathon | Alyeska | Mat Peak | Veins of Gold | Totals
1    | Severson, Shauna  | 430   | 90     | 80    | 90        | 80   | 110       | 85          | 100     | 110      | 110           | 430
2    | Marvin, Christy   | 380   | 100    | 90    | 100       |      |           | 90          |         | 90       |               | 380
5    | Taylor, Gail      | 280   | 60     | 45    | 70        | 60   |           |             |         | 80       | 70            | 280
     | Neale, Megan      | 280   |        | 75    |           | 45   |           |             | 60      | 65       | 80            | 280
...
Female Age Group 17 and Under
Rank | Name              | Total | Crazy Lazy | Knoya | ... | Veins of Gold | Total
1    | Ruggles, Rylee    | 17    | 5          |       | ... |               | 17
...
Female Age Group 18-29 / 30-39 / ... / 70-79
(then the same for Overall MEN and the Male age groups)
```

What to note:
- There is one column per GP race, in calendar order. A blank cell means no points in that race.
- **Total** is the sum of the best 4 races. The cells still show every race the runner scored in,
  including the ones that don't count (e.g. Severson has 9 scoring races and a 430 total).
- Record bonuses are added into the cell (110 = 100 + 10). Nothing marks them.
- Tied runners have a blank Rank cell, meaning "same rank as the row above".
- Age groups with no scorers (e.g. Female 80-89) are left out.

## Current state

- `Standings.jsx` has two tabs, **Open Division** and **Age Divisions**. Each keeps its own gender
  state (`genderTab`, `ageGender`), and both default to `'male'`.
- `GET /api/standings/{year}/open/{gender}` and `/age/{category}/{gender}` already return a
  `PointsBreakdown` per standing. They run one points query per runner (an N+1 pattern), and they
  return a single division or category per call. Showing the full breakdown with these would take
  1 + 9 calls per gender, each with N+1 queries.
- `GrandPrixStanding` stores `Rank` (unique, tiebreakers applied), `TotalPoints`,
  `Best/Second/Third/FourthBestRacePoints`, `RacesCompleted` and `RunTheGamutQualified`.
- `GrandPrixPoints` stores one row per runner, race, division and age category, with
  `IsRecordBonus`.

## Design

### Backend: one breakdown endpoint

`GET /api/standings/{year}/breakdown/{gender}` — `[AllowAnonymous]`

It returns everything for one gender in a single response, using a fixed number of queries
(no N+1):

```jsonc
{
  "year": 2026,
  "gender": "Female",
  "isFinalized": false,
  "races": [                          // GP races for the year, ordered by Date
    { "raceId": "…", "label": "Crazy Lazy", "fullName": "Crazy Lazy", "date": "2026-05-16" }
  ],
  "overall": { "division": "OpenFemale", "ageCategory": null, "rows": [ /* BreakdownRow */ ] },
  "ageGroups": [                      // GrandPrixConstants.AgeCategories order, empty groups omitted
    { "division": "AgeFemale", "ageCategory": "17 and Under", "rows": [ /* BreakdownRow */ ] }
  ]
}
```

```jsonc
// BreakdownRow
{
  "runnerId": "…",
  "runnerName": "Shauna Severson",
  "rank": 1,
  "isTiedWithPrevious": false,
  "totalPoints": 430,
  "racesScored": 9,
  "runTheGamut": true,
  "points": {                         // keyed by raceId; races with no points are absent
    "<raceId>": { "points": 110, "isRecordBonus": true, "counted": true }
  }
}
```

Implementation (in `StandingsController`, or a small `GrandPrixBreakdownService` to keep the
controller thin):
1. Parse the gender with the same switch the other endpoints use. Consider moving that switch into
   a shared helper, since it now appears three times.
2. Load the year's GP races, ordered by `Date`, and project their labels.
3. Load all `GrandPrixStandings` for the year where the division is `GetOpenDivision(g)` or
   `GetAgeDivision(g)`, including `Runner`. This is one query.
4. Load all `GrandPrixPoints` for the same year and divisions. This is one query.
5. Group the points by `(Division, AgeCategory, RunnerId)` and attach them to the standings.
6. Set **`counted`**: sort a runner's points within that division by points (descending) and mark
   the top 4 as counted. Break ties by race date so the result is deterministic. This uses the same
   ordering as `CalculateDivisionStandings`. Consider moving that ordering into a shared static
   helper so the calculation and the display can't drift apart.
7. Set **`isTiedWithPrevious`**: true when `TotalPoints` and all four `*BestRacePoints` match the
   previous row (a true tie under the rules). See open question 2.
8. Read `IsFinalized` from `GrandPrixSeasons`.

**Column labels.** `RaceProjections.DisplayName` gives long names such as
"Mount Marathon Race — Adult". These make a 9-column table very wide. The recommendation is to add
an optional `ShortName` (max 30 characters) to `RaceSeries` and fall back to `DisplayName` when it
is empty. This needs a migration, an edit field in the series admin form, and seed values in
`RaceSeedingService` ("Crazy Lazy", "Knoya", "Gov't Peak", "Bird Ridge", "Juneau RR",
"Mt Marathon", "Alyeska", "Mat Peak", "Veins of Gold"). The header tooltip shows `fullName`.
If one series has more than one GP race in a year, add the variant name to the label.

DTOs go in `Models/DTOs/StandingsDto.cs` (or a new `StandingsBreakdownDto.cs`):
`StandingsBreakdownResponse`, `BreakdownRaceColumnDto`, `BreakdownSectionDto`, `BreakdownRowDto`
and `BreakdownCellDto`.

### Frontend

**Default to Female, with one shared gender state** (`pages/Standings.jsx`)
- Replace `genderTab` and `ageGender` with a single `gender` state that starts as `'female'`.
  Switching tabs then keeps the gender the user picked. This is simpler and matches how the sheet
  is organised (by gender first).
- Reorder `GenderToggle` so **Female** comes first.
- Optionally, keep `?gender=` and `?tab=` in the URL query (next to the existing `/standings/:year`
  route) so links can be shared.

**Third tab: "Points Breakdown"**
- Add `const VIEW_BREAKDOWN = 'breakdown'` and a third `TabsTrigger`.
- The breakdown response has a different shape, so give it its own fetch and state. The cleanest
  way is a new component, `components/standings/PointsBreakdown.jsx`, that takes `year`, `gender`
  and `searchTerm` and fetches on its own. `Standings.jsx` then renders it instead of
  `StandingsTable` when that tab is active. The existing `loadStandings` should skip fetching on
  this tab.
- Move `RankBadge`, `STICKY_HEAD` and `GenderToggle` into `components/standings/` so both views can
  use them.

**Layout of the breakdown view**
- Toolbar: the gender toggle and search box, plus a row of jump links (chips): *Overall · 17 and
  Under · 18-29 · …*. Only groups that have rows appear, and each chip scrolls to its section.
- Sections are stacked: **Overall {Women|Men}** first, then one card per age group, each with an
  `h3` heading and a row count.
- Each section is a `BreakdownTable`:
  - Columns: Rank | Runner | Total | one column per race | Races.
  - **Rank and Runner are sticky on the left** (`sticky left-0`, with a background), so the table
    can scroll sideways on phones while names stay visible. The table sits in an
    `overflow-x-auto` wrapper. The header row stays sticky vertically, as it does today.
  - Race cells: points that count are shown at normal weight. Points that don't count are muted
    (`text-muted-foreground`, optionally struck through) and have a "Not counted (best 4 only)"
    title. This explains why the Total is less than the row's sum, which the spreadsheet never
    shows.
  - Record bonus: a small superscript marker (e.g. `110ᴿ` or a `Trophy` icon), with the title
    "Includes +10 course-record bonus".
  - A tied rank shows the same number in a muted badge, or is left blank like the sheet (see open
    question 2).
  - A Gamut check (the existing `Check` badge) next to the runner's name.
  - Names link to `/runners/{id}`, the same as `StandingsTable`.
  - Race headers use the short label, with `title={fullName}`. They could link to the race results
    page.
- Search filters rows in every section at once and hides sections with no matches.
- A legend under the toolbar explains: best 4 count, muted = dropped, ᴿ = record bonus,
  ✓ = Run the Gamut.
- When the season isn't finalized, show a "Tentative — season in progress" badge. This matches
  how runner pages already treat unfinalized years.
- Loading uses `Skeleton` blocks; errors use `Alert`. Both follow the existing pattern.

## Implementation steps

1. **Female default and shared gender state**: a small, standalone change to `Standings.jsx`.
   It can ship first.
2. **Backend**: add the DTOs, then the endpoint (or service plus endpoint), with the
   counted/tied/isFinalized logic.
3. **Optional `RaceSeries.ShortName`**: migration, series DTO and controller update, admin form
   field, seed values, and the label fallback in the endpoint.
4. **Frontend**: move the shared pieces into `components/standings/`, then add
   `PointsBreakdown.jsx`, `BreakdownTable.jsx` and the third tab.
5. **Tests** (in `AmrGrandPrix.API.Tests/Controllers/`, following `StandingsControllerAuthTests` and
   `GrandPrixSeasonFinalizationTests`):
   - The endpoint is anonymous and returns 200. An invalid gender returns 400.
   - Race columns are GP races only, in date order. Non-GP races are left out.
   - Overall and age-group rows match the stored standings (totals, ranks, Gamut).
   - A runner with 5 or more scoring races has exactly 4 cells with `counted: true`, and their sum
     equals `totalPoints`.
   - `isRecordBonus` comes through, and empty age groups are omitted.
   - A runner who changes age group mid-season appears in both groups with the right races.
   - `isTiedWithPrevious` is set only for full ties.
6. Run `dotnet build`, `dotnet test` and `npm run lint`, then compare by hand against the 2026 sheet
   once the 2026 results are loaded.
7. Update `CLAUDE.md`: add the new endpoint under Controllers, and add Points Breakdown to the
   Standings description.

## Decisions (2026-10-31)

1. **Juniors in the Overall table.** Juniors who run the main (adult) race score Open points.
   Junior-only courses score age-group points only. These are marked with a new
   `RaceVariant.IsAgeGroupOnly` flag (Jack's Run, MMR Junior and Junior Blueberry Knoll are
   flagged by the migration and the seed data). A series' junior course shares the series' column
   in the breakdown, so the breakdown has **one column per race series**, not one per race.
2. **Ties.** Show tiebreaker ranks. Runners who are still level on total and all four best races
   share a rank (1, 2, 2, 4). The calculation service applies this, so every tab agrees.
3. **Knoya Dome / Gov't Peak Uphill for 17U and 60+.** Deferred.
4. **Short race labels.** Not done for now. The column headers wrap the series name.
5. **Nonbinary.** The toggle stays Male/Female. The endpoint accepts `nonbinary`.
