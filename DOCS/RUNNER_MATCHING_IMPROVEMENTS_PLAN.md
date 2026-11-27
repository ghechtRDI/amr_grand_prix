# Racer Matching Improvements Plan

Findings from a review of the results-upload racer matching (November 2026), and the plan for
fixing them. How matching works is documented in [RUNNER_MATCHING.md](RUNNER_MATCHING.md), which is
the authoritative reference; this file tracks the planned changes and is updated as phases land.

## Findings

### Names

1. **Preferred name is compared on its own (bug).** The preferred-name field is a nickname
   (`autoComplete="nickname"`, next to first/last name fields), so it usually holds "Bob", not
   "Bob Smith". Upload matching compares the whole uploaded name with it: "bob smith" vs "bob"
   scores about 0.33, so the preferred name almost never helps. Profile/claim matching has the
   same problem with the user's preferred name. The duplicate finder (`RunnerMergeService`)
   already pairs a single-word preferred name with the last name.
2. **Confirmed matches don't teach the system anything.** When an admin matches "Rob Smith" to
   Robert Smith, the uploaded spelling isn't saved, so the next upload has to be matched by hand
   again. Merges already save both names as alternates.
3. **Normalization is minimal.** Only lowercasing and whitespace collapsing. Punctuation,
   hyphens and accents ("O'Brien"/"OBrien", "Mary-Kate"/"Mary Kate", "José"/"Jose"), word order
   ("Smith, John"), middle names/initials and suffixes ("Jr.") all cost edit-distance points.
4. **Whole-string Levenshtein favours long names.** "Dan Smith" vs "Don Smith" scores 0.89
   (passes the 0.75 name check), while "Bob" vs "Robert" Smith scores about 0.67 (fails).
   Scoring first and last names separately, with a small nickname table, would separate these
   better.

### Estimated year of birth

5. **Unknown age counts as a mismatch.** A runner with no DOB and no estimated birth year scores
   at most 0.85 against a row that has an age, so they're never auto-matched. Unknown on the
   *row* side already gets full credit, and the duplicate finder treats unknown as neutral.
6. **The estimate always picks the later of two possible years.** Age 40 on 2026-06-15 means
   born between mid-1985 and mid-1986; the code stores `race year − age` = 1986. Reading it back
   makes the same assumption so the errors usually cancel, but it's up to a year off.
7. **Repeat results never sharpen the estimate.** The first estimate sticks unless an admin
   overrides it. Each result with an age narrows the possible birth-date window; age-category
   results narrow it too and are currently ignored.
8. **Conflicting evidence is silently overwritten or ignored**, rather than flagged as "this age
   is inconsistent with this runner's history" (a different person, or a typo in the results).
9. **Age-category comparison is exact.** A runner estimated at 30 against a "19-29" row fails
   the age check even though the estimate is uncertain by a year.
10. **The age check is all-or-nothing.** 0 and 2 years off earn the same +0.15; 3 years off
    earns nothing.

## Phase 1: Quick fixes (findings 1, 2, 5)

- [x] **Preferred name paired with last name.** Move the name-candidate list into
  `RunnerNames.NameCandidates` (legal name, alternate names, preferred name, and preferred name
  + last name when the preferred name is a single word) and use it from upload matching,
  profile/claim matching and the duplicate finder, so all three agree.
  - Side effect: claim matching (`FindMatchesForProfileAsync`) can now score higher for
    nickname users, so a few more claims may cross the 0.90 auto-approve threshold.
- [x] **Save confirmed name variants.** When a result is saved against an existing runner and
  the uploaded name isn't already one of the runner's names (case- and whitespace-insensitive),
  add it to `AlternateNames`. Data Review shows "Will save “X” as an alternate name" under the
  match dropdown, using the candidate's `knownNames` returned with each match.
  - Caveat: a wrong match also records the wrong name. Admins can remove it in Racer Management.
- [x] **Unknown age is neutral.** When the runner has no age on file, the age check neither
  adds nor removes confidence: it gets the same +0.15 as a match, mirroring the row-side and
  duplicate-finder behaviour. `AgeMatch` on the DTO stays `false` (it means "confirmed"), so a
  candidate whose age actually matches still sorts above an equally scored one with no age.
  - Consequence: an exact name + gender match against a runner with no age now reaches 1.0 and
    is auto-matched. The save then records the uploaded age on that runner.

## Phase 2: DOB range model (findings 6-9)

Replaced the single `EstimatedBirthYear` with a range of possible birth dates. The details are
in [RUNNER_MATCHING.md](RUNNER_MATCHING.md) ("Estimated birth date range").

- [x] `Runner.EstimatedDobEarliest` / `EstimatedDobLatest` (stored, nullable, open-ended when
  null), with `Common/BirthDateRange.cs` holding the window/intersect/estimate rules.
- [x] **`EstimatedBirthYear` kept as a computed property** (EF-ignored, like `FullName`): the
  midpoint year of a range no wider than one reported age; null for category-only ranges. Not
  a Postgres generated column, because the EF InMemory provider used by the tests wouldn't
  compute it, so tests and production would diverge. The three SQL projections that read it
  (athlete directory, SEO page resolver, sitemap) now select the range columns.
- [x] Save: each result's window (exact age, else category) is intersected in. A conflict is
  ignored unless the admin ticks "Update estimated age", which replaces the range. Category-only
  results now contribute (finding 7). The range is maintained even for verified-DOB runners;
  the DOB wins on read.
- [x] Matching: overlap with a 1-year tolerance; category rows compare by overlap (finding 9).
  Against a verified DOB this is tighter than before (±1 vs ±2 years).
- [x] Data Review shows the possible age span ("40–41 (est.)") and flags only true conflicts
  (finding 8); Edit Racer shows the range.
- [x] Claim matching: user DOB inside a narrow range (replaces exact-year equality, which the
  old late-year bias got wrong about half the time).
- [x] SEO: indexable only if 18+ at the youngest the range allows. Adults known only by
  category become indexable; runners within a year of 18 stay out until it's certain.
- [x] Merge intersects ranges; the preview warns on conflict.
- [x] Migration with SQL backfill + Down; tested on a copy of the dev DB on `postgres:16-alpine`.
  ERD regenerated.
- Not done: sliding age credit (finding 10). Overlap + tolerance made it less pressing.

### Backfill results (dev data, 5,160 runners)

| | Runners |
|---|---|
| Same estimated year as before | 3,306 |
| One year earlier (old late-year bias corrected) | 1,336 |
| Newly estimated | 22 |
| Known only by age category (no estimated year) | 496 |
| Estimate lost | 0 |
| Range narrowed to under 90 days | 180 |
| Have a result contradicting their range | 105 (16 by more than the 1-year tolerance) |
| SEO: newly indexable (adult category only) / newly noindex (could be 17) | 469 / 24 |

### Follow-ups

- An admin view of runners whose results conflict with their range (the 16 above are the
  likeliest data errors or merged-together people). Could become a Racer Management tab. Until
  then, this lists them (drop the two `interval '1 year'` widenings to include the 1-year cases):

  ```sql
  SELECT DISTINCT r."RunnerId", r."FirstName", r."LastName", r."EstimatedDobEarliest", r."EstimatedDobLatest"
  FROM "RaceResults" rr
  JOIN "Races" ra USING ("RaceId")
  JOIN "Runners" r USING ("RunnerId")
  WHERE rr."Age" IS NOT NULL AND r."EstimatedDobEarliest" IS NOT NULL AND r."EstimatedDobLatest" IS NOT NULL
    AND NOT ((ra."Date" - make_interval(years => rr."Age" + 1))::date + 1 <= (r."EstimatedDobLatest" + interval '1 year')::date
         AND (ra."Date" - make_interval(years => rr."Age"))::date >= (r."EstimatedDobEarliest" - interval '1 year')::date);
  ```

## Later candidates (findings 3, 4)

- Richer normalization: strip punctuation and accents, handle "Last, First", ignore suffixes
  and middle initials.
- Score first and last names separately; nickname table (Bob/Robert, Liz/Elizabeth…).
