import {
  Area,
  AreaChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { formatDate, formatDayShort, formatNumber } from '@/lib/format';
import type { HealthPoint } from '@/types/api';

import { useChartTheme } from './useChartTheme';

interface HealthTrendChartProps {
  data: HealthPoint[];
  height?: number;
}

/**
 * Динамика сна и шагов.
 *
 * Две оси Y обязательны: часы сна измеряются единицами, шаги — тысячами. На
 * общей оси линия сна легла бы на нулевую отметку и выглядела бы прямой.
 * Заливка под линией подчёркивает объём, но остаётся полупрозрачной, чтобы
 * второй ряд не терялся под первым.
 */
export function HealthTrendChart({ data, height = 260 }: HealthTrendChartProps) {
  const colors = useChartTheme();

  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={data} margin={{ top: 4, right: 4, bottom: 0, left: -16 }}>
        <defs>
          <linearGradient id="sleepFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={colors.accent} stopOpacity={0.35} />
            <stop offset="100%" stopColor={colors.accent} stopOpacity={0.02} />
          </linearGradient>
          <linearGradient id="stepsFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={colors.income} stopOpacity={0.3} />
            <stop offset="100%" stopColor={colors.income} stopOpacity={0.02} />
          </linearGradient>
        </defs>

        <CartesianGrid stroke={colors.grid} vertical={false} />
        <XAxis
          dataKey="date"
          tickFormatter={formatDayShort}
          tick={{ fill: colors.axis, fontSize: 11 }}
          axisLine={{ stroke: colors.grid }}
          tickLine={false}
          minTickGap={24}
        />
        <YAxis
          yAxisId="sleep"
          tick={{ fill: colors.axis, fontSize: 11 }}
          axisLine={false}
          tickLine={false}
          width={44}
          domain={[0, 12]}
        />
        <YAxis
          yAxisId="steps"
          orientation="right"
          tickFormatter={(value: number) => formatNumber(value / 1000) + 'к'}
          tick={{ fill: colors.axis, fontSize: 11 }}
          axisLine={false}
          tickLine={false}
          width={40}
        />
        <Tooltip
          labelFormatter={(label) => formatDate(String(label))}
          contentStyle={{
            backgroundColor: colors.tooltipBg,
            border: `1px solid ${colors.tooltipBorder}`,
            borderRadius: 10,
            fontSize: 12,
            color: colors.tooltipText,
          }}
        />
        <Legend
          wrapperStyle={{ fontSize: 12, color: colors.axis, paddingTop: 8 }}
          iconType="circle"
          iconSize={8}
        />
        <Area
          yAxisId="sleep"
          type="monotone"
          dataKey="sleepHours"
          name="Сон, ч"
          stroke={colors.accent}
          strokeWidth={2}
          fill="url(#sleepFill)"
          // Пропуск дня не должен превращаться в ноль часов сна: разрыв линии
          // честнее, чем выдуманное значение.
          connectNulls={false}
        />
        <Area
          yAxisId="steps"
          type="monotone"
          dataKey="steps"
          name="Шаги"
          stroke={colors.income}
          strokeWidth={2}
          fill="url(#stepsFill)"
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}
