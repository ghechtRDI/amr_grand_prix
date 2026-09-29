import { useMemo } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { formatPercentBehind, formatSecondsAsClock, parseTimeSpanToSeconds } from '@/lib/time';

const TIME_COLOR = 'var(--chart-runner-time)';
const GRADED_COLOR = 'var(--chart-runner-graded)';
const AXIS_TICK = { fontSize: 12, fill: 'var(--color-muted-foreground)' };

function TooltipShell({ year, isPersonalRecord, children }) {
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 text-sm text-popover-foreground shadow-md">
      <div className="mb-1 font-medium">{year}{isPersonalRecord && ' · PR'}</div>
      {children}
    </div>
  );
}

function TooltipRow({ color, dashed, label, value }) {
  return (
    <div className="flex items-center gap-2">
      <svg width="14" height="4" aria-hidden="true">
        <line x1="0" y1="2" x2="14" y2="2" stroke={color} strokeWidth="2" strokeDasharray={dashed ? '4 2' : undefined} />
      </svg>
      <span className="text-muted-foreground">{label}</span>
      <span className="ml-auto pl-3 font-mono tabular-nums">{value}</span>
    </div>
  );
}

function TimeTooltip({ active, payload }) {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload;
  return (
    <TooltipShell year={d.year} isPersonalRecord={d.isPersonalRecord}>
      <TooltipRow color={TIME_COLOR} label="Finish time" value={formatSecondsAsClock(d.seconds)} />
      {d.gradedSeconds != null && (
        <TooltipRow color={GRADED_COLOR} dashed label="Age-graded" value={formatSecondsAsClock(d.gradedSeconds)} />
      )}
      <div className="mt-1 text-muted-foreground">
        {d.overallPlace}/{d.overallFinishers} overall · {d.genderPlace}/{d.genderFinishers} gender
      </div>
    </TooltipShell>
  );
}

function BehindTooltip({ active, payload }) {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload;
  return (
    <TooltipShell year={d.year} isPersonalRecord={d.isPersonalRecord}>
      <TooltipRow color={TIME_COLOR} label="Behind winner" value={formatPercentBehind(d.percentBehindWinner)} />
    </TooltipShell>
  );
}

// PR gets a larger marker; the card-colored ring keeps overlapping markers legible.
function TimeDot({ cx, cy, payload }) {
  if (cx == null || cy == null) return null;
  return (
    <circle cx={cx} cy={cy} r={payload.isPersonalRecord ? 7 : 4} fill={TIME_COLOR} stroke="var(--color-card)" strokeWidth={2} />
  );
}

function LegendItem({ color, dashed, children }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <svg width="18" height="4" aria-hidden="true">
        <line x1="0" y1="2" x2="18" y2="2" stroke={color} strokeWidth="2" strokeDasharray={dashed ? '4 2' : undefined} />
      </svg>
      {children}
    </span>
  );
}

function ChartTitle({ children, legend }) {
  return (
    <div className="mb-2 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
      <h3 className="text-sm font-medium">{children}</h3>
      {legend && <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">{legend}</div>}
    </div>
  );
}

/**
 * Two small multiples for one course variant, sharing a numeric year axis so gaps between
 * appearances show as real distance:
 *  1. Finish time, with an age-graded line (dashed) for results at age 45+.
 *  2. Percent behind the gender winner — a separate chart rather than a second y-axis, since it's
 *     a different unit; it normalizes for year-to-year course conditions.
 */
export function RunnerTimeChart({ results }) {
  const data = useMemo(
    () =>
      results
        .filter((r) => r.overallPlace)
        .map((r) => ({
          ...r,
          seconds: parseTimeSpanToSeconds(r.time),
          gradedSeconds: parseTimeSpanToSeconds(r.ageGradedTime),
        }))
        .sort((a, b) => a.year - b.year),
    [results]
  );

  if (data.length === 0) {
    return <p className="py-8 text-center text-sm text-muted-foreground">No finish times to chart yet.</p>;
  }

  const hasGraded = data.some((d) => d.gradedSeconds != null);
  const xAxis = (
    <XAxis
      dataKey="year"
      type="number"
      domain={['dataMin - 1', 'dataMax + 1']}
      allowDecimals={false}
      tick={AXIS_TICK}
      stroke="var(--color-border)"
    />
  );
  const cursor = { stroke: 'var(--color-muted-foreground)', strokeDasharray: '3 3' };
  const activeDot = (color) => ({ r: 6, fill: color, stroke: 'var(--color-card)', strokeWidth: 2 });

  return (
    <div className="flex flex-col gap-6">
      <div>
        <ChartTitle
          legend={
            hasGraded && (
              <>
                <LegendItem color={TIME_COLOR}>Finish time</LegendItem>
                <LegendItem color={GRADED_COLOR} dashed>Age-graded (45+)</LegendItem>
              </>
            )
          }
        >
          Finish time
        </ChartTitle>
        <ResponsiveContainer width="100%" height={280}>
          <LineChart data={data} margin={{ top: 12, right: 16, bottom: 4, left: 8 }}>
            <CartesianGrid vertical={false} stroke="var(--color-border)" />
            {xAxis}
            <YAxis
              domain={[
                (min) => Math.max(0, Math.floor((min * 0.95) / 60) * 60),
                (max) => Math.ceil((max * 1.05) / 60) * 60,
              ]}
              tickFormatter={formatSecondsAsClock}
              tick={AXIS_TICK}
              stroke="var(--color-border)"
              width={64}
            />
            <Tooltip content={<TimeTooltip />} cursor={cursor} />
            <Line
              type="linear"
              dataKey="seconds"
              stroke={TIME_COLOR}
              strokeWidth={2}
              dot={<TimeDot />}
              activeDot={activeDot(TIME_COLOR)}
              isAnimationActive={false}
            />
            {hasGraded && (
              <Line
                type="linear"
                dataKey="gradedSeconds"
                stroke={GRADED_COLOR}
                strokeWidth={2}
                strokeDasharray="6 4"
                dot={{ r: 4, fill: GRADED_COLOR, stroke: 'var(--color-card)', strokeWidth: 2 }}
                activeDot={activeDot(GRADED_COLOR)}
                connectNulls={false}
                isAnimationActive={false}
              />
            )}
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div>
        <ChartTitle>Behind gender winner</ChartTitle>
        <ResponsiveContainer width="100%" height={200}>
          <LineChart data={data} margin={{ top: 12, right: 16, bottom: 4, left: 8 }}>
            <CartesianGrid vertical={false} stroke="var(--color-border)" />
            {xAxis}
            <YAxis
              domain={[0, (max) => Math.max(5, Math.ceil(max / 5) * 5)]}
              tickFormatter={(v) => `+${v}%`}
              tick={AXIS_TICK}
              stroke="var(--color-border)"
              width={64}
            />
            <Tooltip content={<BehindTooltip />} cursor={cursor} />
            <Line
              type="linear"
              dataKey="percentBehindWinner"
              stroke={TIME_COLOR}
              strokeWidth={2}
              dot={<TimeDot />}
              activeDot={activeDot(TIME_COLOR)}
              isAnimationActive={false}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
