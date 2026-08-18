import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';

import { formatMoney, formatPercent } from '@/lib/format';

import { useChartTheme } from './useChartTheme';

interface CategorySlice {
  category: string;
  amount: number;
  percentage: number;
}

interface CategoryDonutChartProps {
  data: CategorySlice[];
  currency: string;
  height?: number;
}

/**
 * Расходы по категориям.
 *
 * Кольцевая диаграмма, а не круговая: пустой центр даёт место для итога и
 * визуально легче читается. Показываем максимум шесть категорий — на большем
 * количестве секторов человек всё равно не различает доли, а подписи
 * перекрываются.
 *
 * Рядом с диаграммой ОБЯЗАТЕЛЬНА текстовая легенда с числами: доли, различимые
 * только по цвету, недоступны части пользователей и не читаются при печати.
 */
export function CategoryDonutChart({ data, currency, height = 260 }: CategoryDonutChartProps) {
  const colors = useChartTheme();
  const slices = data.slice(0, 6);

  return (
    <div className="grid items-center gap-4 sm:grid-cols-[minmax(0,220px)_1fr]">
      <ResponsiveContainer width="100%" height={height}>
        <PieChart>
          <Pie
            data={slices}
            dataKey="amount"
            nameKey="category"
            innerRadius="58%"
            outerRadius="88%"
            paddingAngle={2}
            stroke="none"
          >
            {slices.map((slice, index) => (
              <Cell
                key={slice.category}
                fill={colors.categories[index % colors.categories.length]}
              />
            ))}
          </Pie>
          <Tooltip
            formatter={(value) => formatMoney(Number(value), currency)}
            contentStyle={{
              backgroundColor: colors.tooltipBg,
              border: `1px solid ${colors.tooltipBorder}`,
              borderRadius: 10,
              fontSize: 12,
              color: colors.tooltipText,
            }}
          />
        </PieChart>
      </ResponsiveContainer>

      <ul className="space-y-2">
        {slices.map((slice, index) => (
          <li key={slice.category} className="flex items-center gap-2.5 text-[13px]">
            <span
              aria-hidden="true"
              className="size-2.5 shrink-0 rounded-full"
              style={{ backgroundColor: colors.categories[index % colors.categories.length] }}
            />
            <span className="min-w-0 flex-1 truncate text-fg">{slice.category}</span>
            <span className="tabular shrink-0 text-fg-muted">
              {formatPercent(slice.percentage)}
            </span>
            <span className="tabular shrink-0 font-medium text-fg">
              {formatMoney(slice.amount, currency)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
