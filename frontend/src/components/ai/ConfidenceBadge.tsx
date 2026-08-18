import { ShieldAlert, ShieldCheck } from 'lucide-react';

import { Badge } from '@/components/ui';
import { formatPercent } from '@/lib/format';

interface ConfidenceBadgeProps {
  /** Значение 0..1, как его отдаёт AI-сервис. */
  confidence: number;
  /** Решение о достаточности уверенности принимает СЕРВЕР, а не интерфейс. */
  isConfident: boolean;
}

/**
 * Бейдж уверенности модели.
 *
 * Прямая реализация требования MASTER_GUIDE «если AI не уверен — он сообщает
 * об этом». Ключевая деталь: порог уверенности НЕ вычисляется здесь. Клиент
 * получает готовый флаг `isConfident` от бэкенда, где порог задан настройкой
 * `AiService:RecommendationThreshold`. Считать порог на фронтенде значило бы
 * иметь два разных мнения о том, доверять ли модели.
 */
export function ConfidenceBadge({ confidence, isConfident }: ConfidenceBadgeProps) {
  return (
    <Badge tone={isConfident ? 'success' : 'warning'}>
      {isConfident ? <ShieldCheck size={12.5} /> : <ShieldAlert size={12.5} />}
      Уверенность {formatPercent(confidence, false)}
    </Badge>
  );
}
