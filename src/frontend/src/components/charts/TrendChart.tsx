import { useId } from 'react';
import type { ReactNode } from 'react';
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  CHART_AXIS_COLOR,
  CHART_COLOR_ORDER,
  CHART_COLOR_VARS,
  CHART_GRID_COLOR,
  CHART_TOOLTIP_BACKGROUND,
  CHART_TOOLTIP_BORDER,
  CHART_TOOLTIP_TEXT,
} from './chart-tokens';
import type { ChartColorToken } from './chart-tokens';

export interface TrendChartSeries<TDatum extends object> {
  /** Key into each data point rendered by this series. */
  key: Extract<keyof TDatum, string>;
  label: string;
  /** Defaults to the next color in `CHART_COLOR_ORDER` when omitted. */
  color?: ChartColorToken;
}

export interface TrendChartProps<TDatum extends object> {
  /**
   * Only these three shapes are supported deliberately - per the Module 37
   * charting conventions, no pie/3D/decorative chart types exist here.
   */
  variant: 'line' | 'area' | 'bar';
  data: TDatum[];
  series: TrendChartSeries<TDatum>[];
  /** Key into each data point used for the x-axis (e.g. a date or category label). */
  xKey: Extract<keyof TDatum, string>;
  title?: string;
  description?: string;
  height?: number;
  valueFormatter?: (value: number) => string;
  /** Shown instead of the chart when `data` or `series` is empty. */
  emptyState?: ReactNode;
  className?: string;
}

const sharedAxisProps = {
  stroke: CHART_AXIS_COLOR,
  tick: { fontSize: 12, fill: CHART_AXIS_COLOR },
  tickLine: false,
} as const;

const sharedTooltipStyle = {
  backgroundColor: CHART_TOOLTIP_BACKGROUND,
  borderColor: CHART_TOOLTIP_BORDER,
  color: CHART_TOOLTIP_TEXT,
  borderRadius: 8,
  fontSize: 12,
};

/**
 * TASK-UX-08 (Module 37): themed, token-driven trend chart used for
 * dashboard metrics (processing volume, failed vs completed jobs,
 * agent-run duration, knowledge growth, token use by agent). Charts are an
 * optional accent, never the primary dashboard content - callers should
 * only render one of these for a genuinely actionable trend, and must
 * supply an `emptyState` message rather than fabricating placeholder data.
 */
function TrendChart<TDatum extends object>({
  variant,
  data,
  series,
  xKey,
  title,
  description,
  height = 280,
  valueFormatter,
  emptyState,
  className,
}: TrendChartProps<TDatum>) {
  const gradientId = useId();
  const hasData = data.length > 0 && series.length > 0;

  const resolvedSeries = series.map((entry, index) => ({
    ...entry,
    color: CHART_COLOR_VARS[entry.color ?? CHART_COLOR_ORDER[index % CHART_COLOR_ORDER.length]],
  }));

  return (
    <Card className={className}>
      {(title ?? description) ? (
        <CardHeader>
          {title ? <CardTitle>{title}</CardTitle> : null}
          {description ? <CardDescription>{description}</CardDescription> : null}
        </CardHeader>
      ) : null}
      <CardContent>
        {!hasData ? (
          <div
            role="status"
            className="flex items-center justify-center text-sm text-text-muted"
            style={{ height }}
          >
            {emptyState ?? 'No data yet.'}
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={height}>
            {variant === 'line' ? (
              <LineChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                <CartesianGrid stroke={CHART_GRID_COLOR} strokeDasharray="3 3" vertical={false} />
                <XAxis
                  dataKey={xKey}
                  {...sharedAxisProps}
                  axisLine={{ stroke: CHART_GRID_COLOR }}
                />
                <YAxis
                  {...sharedAxisProps}
                  axisLine={false}
                  tickFormatter={valueFormatter}
                  width={40}
                />
                <Tooltip
                  contentStyle={sharedTooltipStyle}
                  formatter={valueFormatter ? (value) => valueFormatter(Number(value)) : undefined}
                />
                {resolvedSeries.length > 1 ? <Legend wrapperStyle={{ fontSize: 12 }} /> : null}
                {resolvedSeries.map((entry) => (
                  <Line
                    key={entry.key}
                    type="monotone"
                    dataKey={entry.key}
                    name={entry.label}
                    stroke={entry.color}
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4 }}
                  />
                ))}
              </LineChart>
            ) : variant === 'area' ? (
              <AreaChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                <defs>
                  {resolvedSeries.map((entry) => (
                    <linearGradient
                      key={entry.key}
                      id={`${gradientId}-${entry.key}`}
                      x1="0"
                      y1="0"
                      x2="0"
                      y2="1"
                    >
                      <stop offset="5%" stopColor={entry.color} stopOpacity={0.35} />
                      <stop offset="95%" stopColor={entry.color} stopOpacity={0} />
                    </linearGradient>
                  ))}
                </defs>
                <CartesianGrid stroke={CHART_GRID_COLOR} strokeDasharray="3 3" vertical={false} />
                <XAxis
                  dataKey={xKey}
                  {...sharedAxisProps}
                  axisLine={{ stroke: CHART_GRID_COLOR }}
                />
                <YAxis
                  {...sharedAxisProps}
                  axisLine={false}
                  tickFormatter={valueFormatter}
                  width={40}
                />
                <Tooltip
                  contentStyle={sharedTooltipStyle}
                  formatter={valueFormatter ? (value) => valueFormatter(Number(value)) : undefined}
                />
                {resolvedSeries.length > 1 ? <Legend wrapperStyle={{ fontSize: 12 }} /> : null}
                {resolvedSeries.map((entry) => (
                  <Area
                    key={entry.key}
                    type="monotone"
                    dataKey={entry.key}
                    name={entry.label}
                    stroke={entry.color}
                    strokeWidth={2}
                    fill={`url(#${gradientId}-${entry.key})`}
                  />
                ))}
              </AreaChart>
            ) : (
              <BarChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                <CartesianGrid stroke={CHART_GRID_COLOR} strokeDasharray="3 3" vertical={false} />
                <XAxis
                  dataKey={xKey}
                  {...sharedAxisProps}
                  axisLine={{ stroke: CHART_GRID_COLOR }}
                />
                <YAxis
                  {...sharedAxisProps}
                  axisLine={false}
                  tickFormatter={valueFormatter}
                  width={40}
                />
                <Tooltip
                  cursor={{ fill: CHART_GRID_COLOR, opacity: 0.3 }}
                  contentStyle={sharedTooltipStyle}
                  formatter={valueFormatter ? (value) => valueFormatter(Number(value)) : undefined}
                />
                {resolvedSeries.length > 1 ? <Legend wrapperStyle={{ fontSize: 12 }} /> : null}
                {resolvedSeries.map((entry) => (
                  <Bar
                    key={entry.key}
                    dataKey={entry.key}
                    name={entry.label}
                    fill={entry.color}
                    radius={[4, 4, 0, 0]}
                  />
                ))}
              </BarChart>
            )}
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}

export { TrendChart };
