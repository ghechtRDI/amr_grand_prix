import { Link } from 'react-router-dom';
import { createColumnHelper } from '@tanstack/react-table';
import { Star } from 'lucide-react';
import { DataTable } from '@/components/ui/data-table';
import { Badge } from '@/components/ui/badge';
import { formatDateOnly } from '@/lib/utils';
import { formatPercentBehind, formatTime, parseTimeSpanToSeconds } from '@/lib/time';

const columnHelper = createColumnHelper();

function raceLabel(result) {
  return result.courseVariant ? `${result.raceName} - ${result.courseVariant}` : result.raceName;
}

function Place({ place, of }) {
  if (place == null) return <span className="text-muted-foreground">—</span>;
  return (
    <span className="tabular-nums">
      {place}
      <span className="text-muted-foreground">/{of}</span>
    </span>
  );
}

// Places are null for non-finishers; returning undefined lets `sortUndefined: 'last'` keep them at
// the bottom in both sort directions.
const placeColumn = (id, header, placeKey, ofKey) =>
  columnHelper.accessor((r) => r[placeKey] ?? undefined, {
    id,
    header,
    sortUndefined: 'last',
    cell: ({ row }) => <Place place={row.original[placeKey]} of={row.original[ofKey]} />,
  });

const RACE_COLUMN = columnHelper.accessor(raceLabel, {
  id: 'race',
  header: 'Race',
  cell: ({ row, getValue }) => (
    <Link to={`/races/${row.original.raceId}/results`} className="font-medium text-foreground hover:text-primary">
      {getValue()}
    </Link>
  ),
});

const COLUMNS = [
  columnHelper.accessor('date', {
    header: 'Date',
    cell: (info) => (
      <span className="whitespace-nowrap">
        {formatDateOnly(info.getValue(), { year: 'numeric', month: 'short', day: 'numeric' })}
      </span>
    ),
  }),
  placeColumn('overallPlace', 'Overall', 'overallPlace', 'overallFinishers'),
  placeColumn('genderPlace', 'Gender', 'genderPlace', 'genderFinishers'),
  placeColumn('ageGroupPlace', 'Age Group', 'ageGroupPlace', 'ageGroupFinishers'),
  columnHelper.accessor((r) => (r.overallPlace ? parseTimeSpanToSeconds(r.time) : undefined), {
    id: 'time',
    header: 'Time',
    sortUndefined: 'last',
    cell: ({ row }) =>
      row.original.overallPlace ? (
        <span className="font-mono text-sm tabular-nums">{formatTime(row.original.time)}</span>
      ) : (
        <Badge variant="outline" className="text-muted-foreground">{row.original.status}</Badge>
      ),
  }),
];

// Series-page-only columns: how far back of the gender winner, and the open-age equivalent time.
const BEHIND_COLUMN = columnHelper.accessor((r) => r.percentBehindWinner ?? undefined, {
  id: 'percentBehindWinner',
  header: 'Behind Winner',
  sortUndefined: 'last',
  cell: ({ row }) => <span className="tabular-nums">{formatPercentBehind(row.original.percentBehindWinner)}</span>,
});

const AGE_GRADED_COLUMN = columnHelper.accessor((r) => parseTimeSpanToSeconds(r.ageGradedTime) ?? undefined, {
  id: 'ageGradedTime',
  header: 'Age-Graded',
  sortUndefined: 'last',
  cell: ({ row }) =>
    row.original.ageGradedTime ? (
      <span className="font-mono text-sm tabular-nums">{formatTime(row.original.ageGradedTime)}</span>
    ) : (
      <span className="text-muted-foreground">—</span>
    ),
});

const PR_COLUMN = columnHelper.accessor('isPersonalRecord', {
    header: 'PR',
    cell: (info) =>
      info.getValue() ? (
        <Star className="size-4 fill-primary text-primary" aria-label="Personal record" />
      ) : null,
});

/**
 * A runner's results, newest first. `showRace` adds the "Race - Variant" column for the all-results
 * table; the per-series page hides it (each table there is scoped to one variant) and instead adds
 * "Behind Winner" and, when any result qualifies, "Age-Graded".
 */
export function RunnerResultsTable({ results, showRace = true }) {
  const hasAgeGraded = results.some((r) => r.ageGradedTime);
  const columns = showRace
    ? [RACE_COLUMN, ...COLUMNS, PR_COLUMN]
    : [...COLUMNS, BEHIND_COLUMN, ...(hasAgeGraded ? [AGE_GRADED_COLUMN] : []), PR_COLUMN];

  return (
    <DataTable
      columns={columns}
      data={results}
      searchable={showRace}
      searchPlaceholder="Search races…"
      emptyMessage="No results recorded yet."
    />
  );
}
