import { api } from '@/lib/httpClient';
import type { Goal, GoalPayload, GoalQuery, PagedResponse, Uuid } from '@/types/api';

/** Цели: `GoalsController`. */
export const goalService = {
  list(query: GoalQuery): Promise<PagedResponse<Goal>> {
    return api.get<PagedResponse<Goal>>('/goals', query);
  },

  getById(id: Uuid): Promise<Goal> {
    return api.get<Goal>(`/goals/${id}`);
  },

  create(payload: GoalPayload): Promise<Goal> {
    return api.post<Goal>('/goals', payload);
  },

  update(id: Uuid, payload: GoalPayload): Promise<Goal> {
    return api.put<Goal>(`/goals/${id}`, payload);
  },

  /**
   * Удаление цели.
   *
   * Задачи цели НЕ удаляются: у связи Goals → Tasks правило SetNull, поэтому
   * `goalId` задач станет null, а сами задачи выживут. Интерфейс обязан
   * предупредить об этом в подтверждении — иначе поведение выглядит как баг.
   */
  remove(id: Uuid): Promise<void> {
    return api.delete(`/goals/${id}`);
  },
};
