import { api } from '@/lib/httpClient';
import type {
  AiResult,
  CreateHealthLogPayload,
  HealthAssessment,
  HealthLog,
  HealthLogQuery,
  PagedResponse,
  UpdateHealthLogPayload,
  Uuid,
} from '@/types/api';

/** Здоровье: `HealthController`. Маршрут `/api/health/*`, liveness — `/api/ping`. */
export const healthService = {
  list(query: HealthLogQuery): Promise<PagedResponse<HealthLog>> {
    return api.get<PagedResponse<HealthLog>>('/health/logs', query);
  },

  getById(id: Uuid): Promise<HealthLog> {
    return api.get<HealthLog>(`/health/logs/${id}`);
  },

  /**
   * Создание записи.
   *
   * Вернёт 409, если запись на эту дату уже есть: у HealthLogs уникальный
   * индекс (UserId, Date) — одна запись в день (ADR 15).
   */
  create(payload: CreateHealthLogPayload): Promise<HealthLog> {
    return api.post<HealthLog>('/health/logs', payload);
  },

  /** Правка записи. Дата не передаётся — она часть уникального ключа (ADR 38). */
  update(id: Uuid, payload: UpdateHealthLogPayload): Promise<HealthLog> {
    return api.put<HealthLog>(`/health/logs/${id}`, payload);
  },

  remove(id: Uuid): Promise<void> {
    return api.delete(`/health/logs/${id}`);
  },

  /** AI-оценка самочувствия за последние `daysBack` дней. */
  analysis(params: { daysBack?: number }): Promise<AiResult<HealthAssessment>> {
    return api.get<AiResult<HealthAssessment>>('/health/analysis', params);
  },
};
