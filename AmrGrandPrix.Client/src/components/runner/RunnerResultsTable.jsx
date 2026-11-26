import { Link } from 'react-router-dom';
import { createColumnHelper } from '@tanstack/react-table';
import { Star } from 'lucide-react';
import { DataTable } from '@/components/ui/data-table';
import { Badge } from '@/components/ui/badge';
import { formatDateOnly } from '@/lib/utils';
import { formatPercentBehind, formatTime, parseTimeSpanToSeconds } from '@/lib/time';

const columnHelper = createColumnHelper();

const resultHref = (result) => `/races/${result.raceId}/results`;

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
    <Link to={resultHref(row.original)} className="font-medium text-foreground hover:text-primary">
      {getValue()}
    </Link>
  ),
});

const formatResultDate = (date) => formatDateOnly(date, { year: 'numeric', month: 'short', day: 'numeric' });

const DATE_COLUMN = columnHelper.accessor('date', {
  header: 'Date',
  cell: (info) => <span className="whitespace-nowrap">{formatResultDate(info.getValue())}</span>,
});

// Without the Race column (series page) the date is the row's keyboard-reachable link.
const DATE_LINK_COLUMN = columnHelper.accessor('date', {
  header: 'Date',
  cell: ({ row, getValue }) => (
    <Link to={resultHref(row.original)} className="whitespace-nowrap font-medium text-foreground hover:text-primary">
      {formatResultDate(getValue())}
    </Link>
  ),
});

const COLUMNS = [
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
 * A runner's results, newest first; each row links to that race's results. `showRace` adds the "Race - Variant" column for the all-results
 * table; the per-series page hides it (each table there is scoped to one variant) and instead adds
 * "Behind Winner" and, when any result qualifies, "Age-Graded".
 */
export function RunnerResultsTable({ results, showRace = true }) {
  const hasAgeGraded = results.some((r) => r.ageGradedTime);
  const columns = showRace
    ? [RACE_COLUMN, DATE_COLUMN, ...COLUMNS, PR_COLUMN]
    : [DATE_LINK_COLUMN, ...COLUMNS, BEHIND_COLUMN, ...(hasAgeGraded ? [AGE_GRADED_COLUMN] : []), PR_COLUMN];

  return (
    <DataTable
      columns={columns}
      data={results}
      searchable={showRace}
      searchPlaceholder="Search races…"
      emptyMessage="No results recorded yet."
      getRowHref={resultHref}
    />
  );
}
