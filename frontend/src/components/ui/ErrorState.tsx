import { RefreshCw, ServerCrash } from 'lucide-react';

import { cn } from '@/lib/cn';
import { ApiError, describeError } from '@/types/errors';

import { Button } from './Button';

interface ErrorStateProps {
  error: unknown;
  onRetry?: () => void;
  className?: string;
}

/**
 * Показ ошибки загрузки.
 *
 * Кроме текста показывается `traceId` из ProblemDetails. Это не украшение:
 * по нему запись в логах Serilog находится за одну команду, и пользователь
 * (или преподаватель на защите) может назвать конкретный идентификатор вместо
 * «у меня что-то не работает».
 */
export function ErrorState({ error, onRetry, className }: ErrorStateProps) {
  const apiError = error instanceof ApiError ? error : null;

  return (
    <div
      role="alert"
      className={cn(
        'flex flex-col items-center justify-center gap-3 px-6 py-12 text-center',
        className,
      )}
    >
      <span className="flex size-12 items-center justify-center rounded-2xl bg-danger-soft text-danger">
        <ServerCrash size={22} />
      </span>

      <div className="space-y-1">
        <p className="text-sm font-medium text-fg">Не удалось загрузить данные</p>
        <p className="mx-auto max-w-md text-[13px] leading-relaxed text-fg-muted">
          {describeError(error)}
        </p>
        {apiError?.traceId !== undefined && (
          <p className="pt-1 font-mono text-[11px] text-fg-subtle">
            traceId: {apiError.traceId}
          </p>
        )}
      </div>

      {onRetry !== undefined && (
        <Button variant="secondary" size="sm" onClick={onRetry} leftIcon={<RefreshCw size={14} />}>
          Повторить
        </Button>
      )}
    </div>
  );
}
