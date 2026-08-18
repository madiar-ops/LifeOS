import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { formatMoney, formatMonth, formatMonthShort, formatNumber } from '@/lib/format';
import type { MonthlyPoint } from '@/types/api';

import { useChartTheme } from './useChartTheme';

interface MonthlyBarChartProps {
  data: MonthlyPoint[];
  currency: string;
  height?: number;
}

/**
 * Доходы и расходы по месяцам.
 *
 * Столбцы рядом, а не с накоплением: доход и расход — не части одного целого,
 * и складывать их в одну колонку означало бы показывать бессмысленную сумму.
 * Задача графика — сравнить их между собой в каждом месяце.
 */
export function MonthlyBarChart({ data, currency, height = 260 }: MonthlyBarChartProps) {
  const colors = useChartTheme();

  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data} margin={{ top: 4, right: 4, bottom: 0, left: -12 }} barGap={4}>
        <CartesianGrid stroke={colors.grid} vertical={false} />
        <XAxis
          dataKey="month"
          tickFormatter={formatMonthShort}
          tick={{ fill: colors.axis, fontSize: 11 }}
          axisLine={{ stroke: colors.grid }}
          tickLine={false}
        />
        {/*
          На оси — только число в тысячах, без кода валюты.
          Полная запись «120 тыс. KZT» не влезает в отведённую ширину и
          обрезается: подпись превращается в «тыс. KZT» без самого числа.
          Валюта названа один раз в заголовке карточки, а в подсказке при
          наведении сумма показывается полностью.
        */}
        <YAxis
          tickFormatter={(value: number) => (value === 0 ? '0' : `${formatNumber(value / 1000)} к`)}
          tick={{ fill: colors.axis, fontSize: 11 }}
          axisLine={false}
          tickLine={false}
          width={48}
        />
        <Tooltip
          formatter={(value) => formatMoney(Number(value), currency)}
          // Recharts типизирует метку как ReactNode: подпись оси может быть
          // любой. Приводим к строке явно вместо утверждения типа.
          labelFormatter={(label) => formatMonth(String(label))}
          contentStyle={{
            backgroundColor: colors.tooltipBg,
            border: `1px solid ${colors.tooltipBorder}`,
            borderRadius: 10,
            fontSize: 12,
            color: colors.tooltipText,
          }}
          cursor={{ fill: colors.grid, opacity: 0.4 }}
        />
        <Legend
          wrapperStyle={{ fontSize: 12, color: colors.axis, paddingTop: 8 }}
          iconType="circle"
          iconSize={8}
        />
        <Bar dataKey="income" name="Доходы" fill={colors.income} radius={[4, 4, 0, 0]} />
        <Bar dataKey="expense" name="Расходы" fill={colors.expense} radius={[4, 4, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  );
}
