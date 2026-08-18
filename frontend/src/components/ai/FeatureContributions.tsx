import { formatNumber } from '@/lib/format';
import type { AiContribution } from '@/types/api';

/**
 * Вклад признаков в прогноз.
 *
 * Значения приходят из `feature_importances_` модели: именно они превращают
 * ответ из «поверь мне» в объяснимый результат. Показать их важнее, чем
 * красиво оформить — на защите это прямое доказательство, что модель не
 * чёрный ящик.
 *
 * Длина полосы считается относительно МАКСИМАЛЬНОГО вклада, а не суммы:
 * важности sklearn не обязаны давать в сумме единицу, и нормировка по сумме
 * зрительно занижала бы все признаки сразу.
 */
export function FeatureContributions({ contributions }: { contributions: AiContribution[] }) {
  if (contributions.length === 0) return null;

  const maxImpact = Math.max(...contributions.map((item) => Math.abs(item.impact)));
  if (maxImpact === 0) return null;

  return (
    <div>
      <p className="text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
        Что повлияло на вывод
      </p>
      <ul className="mt-2 space-y-2">
        {contributions.map((item) => {
          const width = (Math.abs(item.impact) / maxImpact) * 100;
          return (
            <li key={item.feature} className="space-y-1">
              <div className="flex items-baseline justify-between gap-3 text-[12.5px]">
                <span className="min-w-0 truncate text-fg">{item.feature}</span>
                <span className="tabular shrink-0 text-fg-subtle">
                  {formatNumber(item.value, 2)}
                </span>
              </div>
              <div className="h-1.5 overflow-hidden rounded-full bg-surface-3">
                <div
                  className="h-full rounded-full bg-accent/70"
                  style={{ width: `${String(width)}%` }}
                />
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
