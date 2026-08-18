import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { taskService } from '@/services/taskService';
import type { CreateTaskPayload, TaskItem, TaskQuery, UpdateTaskPayload, Uuid } from '@/types/api';

import { queryKeys } from './queryKeys';

export function useTasks(query: TaskQuery) {
  return useQuery({
    queryKey: queryKeys.tasks.list(query),
    queryFn: () => taskService.list(query),
    placeholderData: keepPreviousData,
  });
}

/**
 * Инвалидация после правки задачи.
 *
 * Цели тоже сбрасываются: `GoalResponse` содержит `totalTasks` и
 * `completedTasks`, посчитанные на сервере, — отметка галочки меняет прогресс
 * цели, а не только строку задачи.
 */
function useTaskMutationSideEffects() {
  const client = useQueryClient();
  return async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: queryKeys.tasks.all }),
      client.invalidateQueries({ queryKey: queryKeys.goals.all }),
      client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
    ]);
  };
}

export function useCreateTask() {
  const onSettled = useTaskMutationSideEffects();
  return useMutation<TaskItem, Error, CreateTaskPayload>({
    mutationFn: (payload) => taskService.create(payload),
    onSuccess: onSettled,
  });
}

export function useUpdateTask() {
  const onSettled = useTaskMutationSideEffects();
  return useMutation<TaskItem, Error, { id: Uuid; payload: UpdateTaskPayload }>({
    mutationFn: ({ id, payload }) => taskService.update(id, payload),
    onSuccess: onSettled,
  });
}

/**
 * Переключение галочки.
 *
 * Здесь нет оптимистичного обновления сознательно: сервер возвращает
 * пересчитанные `updatedAt` и прогресс связанной цели, а подделать их на
 * клиенте — значит на секунду показать неправильный процент выполнения.
 * Задержка одного PATCH меньше, чем цена рассинхронизации.
 */
export function useToggleTask() {
  const onSettled = useTaskMutationSideEffects();
  return useMutation<TaskItem, Error, Uuid>({
    mutationFn: (id) => taskService.toggleComplete(id),
    onSuccess: onSettled,
  });
}

export function useDeleteTask() {
  const onSettled = useTaskMutationSideEffects();
  return useMutation<void, Error, Uuid>({
    mutationFn: (id) => taskService.remove(id),
    onSuccess: onSettled,
  });
}
