import { api } from '@/lib/httpClient';
import type {
  CreateTaskPayload,
  PagedResponse,
  TaskItem,
  TaskQuery,
  UpdateTaskPayload,
  Uuid,
} from '@/types/api';

/** Задачи: `TasksController`. */
export const taskService = {
  list(query: TaskQuery): Promise<PagedResponse<TaskItem>> {
    return api.get<PagedResponse<TaskItem>>('/tasks', query);
  },

  getById(id: Uuid): Promise<TaskItem> {
    return api.get<TaskItem>(`/tasks/${id}`);
  },

  create(payload: CreateTaskPayload): Promise<TaskItem> {
    return api.post<TaskItem>('/tasks', payload);
  },

  update(id: Uuid, payload: UpdateTaskPayload): Promise<TaskItem> {
    return api.put<TaskItem>(`/tasks/${id}`, payload);
  },

  /**
   * Переключение выполнения: PATCH /api/tasks/{id}/complete.
   *
   * Отдельный эндпоинт, а не PUT со всем объектом: клиенту не нужно знать
   * остальные поля, чтобы поставить галочку, и две одновременные правки
   * разных полей не перезаписывают друг друга.
   */
  toggleComplete(id: Uuid): Promise<TaskItem> {
    return api.patch<TaskItem>(`/tasks/${id}/complete`);
  },

  remove(id: Uuid): Promise<void> {
    return api.delete(`/tasks/${id}`);
  },
};
