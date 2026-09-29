/**
 * Summary numbers for the runner page's stats row, derived from the profile's result list.
 */

export function ordinal(n) {
  const mod100 = n % 100;
  if (mod100 >= 11 && mod100 <= 13) return `${n}th`;
  return `${n}${{ 1: 'st', 2: 'nd', 3: 'rd' }[n % 10] ?? 'th'}`;
}

// Lowest place wins; ties go to the most recent result (results arrive newest-first).
function best(results, key) {
  return results.reduce((acc, r) => (r[key] != null && (acc == null || r[key] < acc[key]) ? r : acc), null);
}

// Longest run of consecutive calendar years at the same event (series, or race name if ungrouped).
function longestStreak(results) {
  const yearsByEvent = new Map();
  for (const r of results) {
    const key = r.raceSeriesId ?? r.raceName;
    if (!yearsByEvent.has(key)) yearsByEvent.set(key, { name: r.raceSeriesName ?? r.raceName, years: new Set() });
    yearsByEvent.get(key).years.add(r.year);
  }

  let top = null;
  for (const { name, years } of yearsByEvent.values()) {
    const sorted = [...years].sort((a, b) => a - b);
    let run = 1;
    for (let i = 0; i < sorted.length; i++) {
      run = i > 0 && sorted[i] === sorted[i - 1] + 1 ? run + 1 : 1;
      if (!top || run > top.years) top = { name, years: run, endYear: sorted[i] };
    }
  }
  return top;
}

export function computeRunnerStats(results) {
  const years = [...new Set(results.map((r) => r.year))].sort((a, b) => a - b);
  const bestAgeGroup = best(results, 'ageGroupPlace');

  return {
    races: results.length,
    finishes: results.filter((r) => r.overallPlace).length,
    yearsActive: years.length,
    firstYear: years[0],
    lastYear: years[years.length - 1],
    streak: longestStreak(results),
    bestOverall: best(results, 'overallPlace'),
    bestAgeGroup,
    ageGroupWins: results.filter((r) => r.ageGroupPlace === 1).length,
  };
}
