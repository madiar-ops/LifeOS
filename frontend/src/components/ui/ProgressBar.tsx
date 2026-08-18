import { cn } from '@/lib/cn';

interface ProgressBarProps {
  /** Значение 0..100. Выходящие за диапазон значения обрезаются. */
  value: number;
  tone?: 'accent' | 'success' | 'warning' | 'danger';
  className?: string;
  label?: string;
}

const TONES = {
  accent: 'bg-accent',
  success: 'bg-success',
  warning: 'bg-warning',
  danger: 'bg-danger',
};

/**
 * Полоса прогресса.
 *
 * `role="progressbar"` с aria-атрибутами: визуальная полоска ничего не
 * сообщает скринридеру, поэтому значение дублируется в разметке.
 */
export function ProgressBar({ value, tone = 'accent', className, label }: ProgressBarProps) {
  const clamped = Math.min(100, Math.max(0, value));

  return (
    <div
      role="progressbar"
      aria-valuenow={Math.round(clamped)}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={label ?? 'Прогресс'}
      className={cn('h-1.5 w-full overflow-hidden rounded-full bg-surface-3', className)}
    >
      <div
        className={cn('h-full rounded-full transition-[width] duration-500', TONES[tone])}
        style={{ width: `${String(clamped)}%` }}
      />
    </div>
  );
}
