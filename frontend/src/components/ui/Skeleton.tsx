import { cn } from '@/lib/cn';

/**
 * Заглушка на время загрузки.
 *
 * Скелет вместо спиннера там, где известна форма будущего содержимого: он
 * сохраняет высоту блока, и после загрузки страница не «прыгает». Спиннер по
 * центру пустого экрана такой гарантии не даёт.
 */
export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      aria-hidden="true"
      className={cn('animate-shimmer rounded-md bg-surface-3', className)}
    />
  );
}

/** Скелет строк таблицы или списка. */
export function SkeletonRows({ rows = 5, className }: { rows?: number; className?: string }) {
  return (
    <div className={cn('space-y-2', className)}>
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} className="h-14 w-full" />
      ))}
    </div>
  );
}

/** Скелет сетки карточек-показателей. */
export function SkeletonTiles({ count = 4 }: { count?: number }) {
  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      {Array.from({ length: count }, (_, index) => (
        <Skeleton key={index} className="h-28 w-full rounded-card" />
      ))}
    </div>
  );
}
