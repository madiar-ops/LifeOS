import { api } from '@/lib/httpClient';
import type {
  AiHistoryEntry,
  PagedResponse,
  PaginationParams,
  Recommendation,
  Uuid,
} from '@/types/api';
import type { ModuleType } from '@/types/enums';

/** Рекомендации и аудит AI: `RecommendationsController`. */
export const aiService = {
  /**
   * Лента рекомендаций.
   *
   * Сюда попадают только выводы, в которых модель была достаточно уверена:
   * запись создаётся при `isConfident` и превышении RecommendationThreshold
   * (ADR 73). Поэтому лента не требует фильтрации по уверенности на клиенте.
   */
  listRecommendations(
    query: PaginationParams & { module?: ModuleType },
  ): Promise<PagedResponse<Recommendation>> {
    return api.get<PagedResponse<Recommendation>>('/recommendations', query);
  },

  /** Скрыть рекомендацию. */
  removeRecommendation(id: Uuid): Promise<void> {
    return api.delete(`/recommendations/${id}`);
  },

  /**
   * История обращений к AI.
   *
   * Содержимое запросов и ответов наружу не отдаётся — там могут быть
   * фрагменты личных документов (ADR 74). Доступны только эндпоинт,
   * уверенность и время: этого достаточно для отладки и демонстрации.
   */
  history(query: PaginationParams): Promise<PagedResponse<AiHistoryEntry>> {
    return api.get<PagedResponse<AiHistoryEntry>>('/ai/history', query);
  },
};
