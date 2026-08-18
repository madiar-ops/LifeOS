import { PlugZap, Sparkles } from 'lucide-react';

import { Button, Card, CardBody } from '@/components/ui';
import { ApiError, describeError } from '@/types/errors';

interface AiStateNoticeProps {
  error: unknown;
  onRetry?: () => void;
}

/**
 * Объяснение, почему AI не дал результата.
 *
 * Здесь три РАЗНЫХ ситуации, и смешивать их в «что-то пошло не так» нельзя:
 *
 *  1. Данных мало (`finance.no_data`, `health.no_data`) — виноват не сбой, а
 *     пустая история. Пользователю нужно добавить записей, а не жать «повторить».
 *  2. AI-сервис недоступен (`ai.*`) — приложение работает, недоступен только
 *     канал анализа. Бэкенд специально отвечает 400 с понятным кодом, а не 500
 *     (ADR 68), чтобы этот экран мог сказать правду.
 *  3. Всё остальное — настоящая ошибка.
 */
export function AiStateNotice({ error, onRetry }: AiStateNoticeProps) {
  const apiError = error instanceof ApiError ? error : null;
  const noData = apiError?.isNoData === true;
  const aiDown = apiError?.isAiUnavailable === true;

  return (
    <Card className={noData ? undefined : 'border-warning/45'}>
      <CardBody className="flex flex-col items-start gap-3 sm:flex-row sm:items-center">
        <span
          className={
            noData
              ? 'flex size-10 shrink-0 items-center justify-center rounded-xl bg-surface-2 text-fg-subtle'
              : 'flex size-10 shrink-0 items-center justify-center rounded-xl bg-warning-soft text-warning'
          }
        >
          {aiDown ? <PlugZap size={19} /> : <Sparkles size={19} />}
        </span>

        <div className="min-w-0 flex-1">
          <p className="text-[13.5px] font-medium text-fg">
            {noData
              ? 'Данных пока недостаточно'
              : aiDown
                ? 'AI-сервис недоступен'
                : 'Анализ не выполнен'}
          </p>
          <p className="mt-0.5 text-[12.5px] leading-relaxed text-fg-muted">
            {describeError(error)}
          </p>
        </div>

        {onRetry !== undefined && !noData && (
          <Button variant="secondary" size="sm" onClick={onRetry}>
            Повторить
          </Button>
        )}
      </CardBody>
    </Card>
  );
}
