import type { ReactNode } from 'react';

import { cn } from '@/lib/cn';

interface EmptyStateProps {
  icon?: ReactNode;
  title: string;
  description?: string;
  action?: ReactNode;
  className?: string;
}

/**
 * Пустое состояние.
 *
 * Отдельный осмысленный экран, а не пустая таблица. У нового пользователя
 * данных нет по определению — и первое, что он видит в каждом модуле, это
 * пустое состояние. Если оно молчит, приложение выглядит сломанным; если
 * объясняет и предлагает действие — работает как онбординг.
 */
export function EmptyState({ icon, title, description, action, className }: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-3 px-6 py-14 text-center',
        className,
      )}
    >
      {icon !== undefined && (
        <span className="flex size-12 items-center justify-center rounded-2xl bg-surface-2 text-fg-subtle">
          {icon}
        </span>
      )}
      <div className="space-y-1">
        <p className="text-sm font-medium text-fg">{title}</p>
        {description !== undefined && (
          <p className="mx-auto max-w-sm text-[13px] leading-relaxed text-fg-muted">
            {description}
          </p>
        )}
      </div>
      {action}
    </div>
  );
}
