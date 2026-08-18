import { TrendingDown, TrendingUp } from 'lucide-react';
import type { ReactNode } from 'react';

import { cn } from '@/lib/cn';

interface StatTileProps {
  label: string;
  value: ReactNode;
  /** Пояснение под значением: период, единица, доля. */
  hint?: ReactNode;
  icon?: ReactNode;
  /** Изменение в процентах. Знак определяет и цвет, и направление стрелки. */
  delta?: number | null;
  /**
   * Для расходов рост — плохая новость. Флаг переворачивает цветовую
   * трактовку, чтобы «+20 % расходов» не подсвечивалось зелёным как успех.
   */
  invertDelta?: boolean;
  className?: string;
}

export function StatTile({
  label,
  value,
  hint,
  icon,
  delta,
  invertDelta = false,
  className,
}: StatTileProps) {
  const hasDelta = delta !== null && delta !== undefined && Number.isFinite(delta);
  const rising = hasDelta && delta > 0;
  const good = invertDelta ? !rising : rising;

  return (
    <div
      className={cn(
        'rounded-card border border-line bg-surface p-4 shadow-card',
        'transition-[border-color,transform] duration-150 hover:-translate-y-0.5 hover:border-line-strong',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <p className="text-[12.5px] font-medium text-fg-muted">{label}</p>
        {icon !== undefined && <span className="text-fg-subtle">{icon}</span>}
      </div>

      <p className="tabular mt-2 text-2xl leading-tight font-semibold tracking-tight text-fg">
        {value}
      </p>

      <div className="mt-1.5 flex items-center gap-2">
        {hasDelta && delta !== 0 && (
          <span
            className={cn(
              'tabular inline-flex items-center gap-0.5 text-[12px] font-medium',
              good ? 'text-success' : 'text-danger',
            )}
          >
            {rising ? <TrendingUp size={13} /> : <TrendingDown size={13} />}
            {Math.abs(delta).toFixed(1)} %
          </span>
        )}
        {hint !== undefined && <span className="text-[12px] text-fg-subtle">{hint}</span>}
      </div>
    </div>
  );
}
