import { Info, Sparkles } from 'lucide-react';
import type { ReactNode } from 'react';

import { Card, CardBody, CardHeader } from '@/components/ui';
import { cn } from '@/lib/cn';
import type { AiResult } from '@/types/api';

import { ConfidenceBadge } from './ConfidenceBadge';
import { FeatureContributions } from './FeatureContributions';

interface AiResultCardProps<T> {
  title: string;
  envelope: AiResult<T>;
  /** Отрисовка полезной нагрузки — она у каждого модуля своя. */
  children: ReactNode;
  actions?: ReactNode;
}

/**
 * Оболочка любого результата AI.
 *
 * Все AI-эндпоинты бэкенда возвращают один конверт `AiResultResponse<T>`, и
 * этот компонент отрисовывает его одинаково во всех модулях: уверенность,
 * объяснение, вклад признаков, версия модели. Меняется только содержимое.
 *
 * Благодаря этому «AI не молчит о своей неуверенности» соблюдается по
 * построению: невозможно показать результат модели, обойдя бейдж уверенности,
 * не написав отдельный компонент.
 */
export function AiResultCard<T>({ title, envelope, children, actions }: AiResultCardProps<T>) {
  return (
    <Card
      className={cn(
        // Неуверенный результат обведён предупреждающим цветом: пользователь
        // должен заметить это до того, как начнёт читать цифры.
        !envelope.isConfident && 'border-warning/45',
      )}
    >
      <CardHeader
        icon={<Sparkles size={15} />}
        title={title}
        description={`Модель ${envelope.modelVersion}`}
        actions={
          <div className="flex items-center gap-2">
            <ConfidenceBadge
              confidence={envelope.confidence}
              isConfident={envelope.isConfident}
            />
            {actions}
          </div>
        }
      />

      <CardBody className="space-y-4">
        {!envelope.isConfident && (
          <p className="flex gap-2 rounded-lg bg-warning-soft px-3 py-2.5 text-[12.5px] leading-relaxed text-warning">
            <Info size={15} className="mt-px shrink-0" />
            Модель не уверена в этом выводе. Используй его как ориентир, а не как
            основание для решения.
          </p>
        )}

        {children}

        {envelope.explanation !== '' && (
          <div className="rounded-lg bg-surface-2 px-3 py-2.5">
            <p className="text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
              Как модель это объясняет
            </p>
            <p className="mt-1 text-[13px] leading-relaxed text-fg-muted">
              {envelope.explanation}
            </p>
          </div>
        )}

        <FeatureContributions contributions={envelope.contributions} />
      </CardBody>
    </Card>
  );
}
