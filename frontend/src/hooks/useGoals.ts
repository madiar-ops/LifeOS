import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { goalService } from '@/services/goalService';
import type { Goal, GoalPayload, GoalQuery, Uuid } from '@/types/api';

import { queryKeys } from './queryKeys';

/**
 * Список целей.
 *
 * `placeholderData: keepPreviousData` — при переходе на следующую страницу
 * таблица не мигает пустотой: старые строки остаются на экране, пока грузятся
 * новые. Без этого пагинация выглядит как перезагрузка страницы.
 */
export function useGoals(query: GoalQuery) {
  return useQuery({
    queryKey: queryKeys.goals.list(query),
    queryFn: () => goalService.list(query),
    placeholderData: keepPreviousData,
  });
}

export function useGoal(id: Uuid | null) {
  return useQuery({
    queryKey: queryKeys.goals.detail(id ?? ''),
    queryFn: () => goalService.getById(id as Uuid),
    enabled: id !== null,
  });
}

/**
 * Общая инвалидация после любой правки цели.
 *
 * Сбрасывается не только список целей: показатели дашборда посчитаны в
 * PostgreSQL по этим же строкам, поэтому его кэш тоже устарел. Пропущенная
 * инвалидация даёт самый неприятный класс ошибок — интерфейс показывает
 * старое число, и выглядит это как ошибка бэкенда.
 */
function useGoalMutationSideEffects() {
  const client = useQueryClient();
  return async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: queryKeys.goals.all }),
      client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      // Задачи хранят название цели (goalTitle) и могут потерять привязку
      // при удалении цели — их список тоже устарел.
      client.invalidateQueries({ queryKey: queryKeys.tasks.all }),
    ]);
  };
}

export function useCreateGoal() {
  const onSettled = useGoalMutationSideEffects();
  return useMutation<Goal, Error, GoalPayload>({
    mutationFn: (payload) => goalService.create(payload),
    onSuccess: onSettled,
  });
}

export function useUpdateGoal() {
  const onSettled = useGoalMutationSideEffects();
  return useMutation<Goal, Error, { id: Uuid; payload: GoalPayload }>({
    mutationFn: ({ id, payload }) => goalService.update(id, payload),
    onSuccess: onSettled,
  });
}

export function useDeleteGoal() {
  const onSettled = useGoalMutationSideEffects();
  return useMutation<void, Error, Uuid>({
    mutationFn: (id) => goalService.remove(id),
    onSuccess: onSettled,
  });
}
