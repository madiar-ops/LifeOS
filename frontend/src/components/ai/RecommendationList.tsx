import { Sparkles, X } from 'lucide-react';

import { Badge, Button, EmptyState } from '@/components/ui';
import { formatPercent, formatRelative } from '@/lib/format';
import type { Recommendation } from '@/types/api';
import { MODULE_LABELS } from '@/types/enums';
import type { BadgeTone } from '@/components/ui';
import type { ModuleType } from '@/types/enums';

/** Цвет метки модуля — чтобы лента читалась без чтения текста. */
const MODULE_TONES: Record<ModuleType, BadgeTone> = {
  General: 'neutral',
  Study: 'info',
  Finance: 'success',
  Career: 'accent',
  Health: 'warning',
  Avatar: 'neutral',
};

interface RecommendationListProps {
  items: Recommendation[];
  /** Если передан — у каждой рекомендации появляется кнопка «скрыть». */
  onDismiss?: (id: string) => void;
  dismissingId?: string | null;
  emptyHint?: string;
}

/**
 * Лента рекомендаций AI.
 *
 * Уверенность показывается у каждой записи, хотя в ленту попадают только
 * достаточно уверенные выводы (ADR 73). Это не избыточность: пользователь
 * видит РАЗНИЦУ между «уверенность 62 %» и «уверенность 94 %» и может
 * взвешивать советы, а не воспринимать их как одинаково достоверные.
 */
export function RecommendationList({
  items,
  onDismiss,
  dismissingId,
  emptyHint,
}: RecommendationListProps) {
  if (items.length === 0) {
    return (
      <EmptyState
        icon={<Sparkles size={20} />}
        title="Рекомендаций пока нет"
        description={
          emptyHint ??
          'Запусти анализ финансов, здоровья или разбор резюме — уверенные выводы модели появятся здесь.'
        }
      />
    );
  }

  return (
    <ul className="divide-y divide-line">
      {items.map((item) => (
        <li key={item.id} className="flex items-start gap-3 py-3 first:pt-0 last:pb-0">
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone={MODULE_TONES[item.module]} dot>
                {MODULE_LABELS[item.module]}
              </Badge>
              <span className="tabular text-[11.5px] text-fg-subtle">
                уверенность {formatPercent(item.confidence, false)}
              </span>
              <span className="text-[11.5px] text-fg-subtle">·</span>
              <span className="text-[11.5px] text-fg-subtle">
                {formatRelative(item.createdAt)}
              </span>
            </div>
            <p className="mt-1.5 text-[13.5px] leading-relaxed text-fg">{item.content}</p>
          </div>

          {onDismiss !== undefined && (
            <Button
              variant="ghost"
              size="icon"
              aria-label="Скрыть рекомендацию"
              loading={dismissingId === item.id}
              onClick={() => onDismiss(item.id)}
            >
              <X size={15} />
            </Button>
          )}
        </li>
      ))}
    </ul>
  );
}
