import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { healthService } from '@/services/healthService';
import type {
  CreateHealthLogPayload,
  HealthLog,
  HealthLogQuery,
  UpdateHealthLogPayload,
  Uuid,
} from '@/types/api';

import { queryKeys } from './queryKeys';

export function useHealthLogs(query: HealthLogQuery) {
  return useQuery({
    queryKey: queryKeys.health.logs(query),
    queryFn: () => healthService.list(query),
    placeholderData: keepPreviousData,
  });
}

/** AI-оценка самочувствия. Запускается по кнопке — см. пояснение в useFinance. */
export function useHealthAnalysis(params: { daysBack?: number }, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.health.analysis(params),
    queryFn: () => healthService.analysis(params),
    enabled,
    staleTime: Infinity,
    retry: false,
  });
}

function useHealthMutationSideEffects() {
  const client = useQueryClient();
  return async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: queryKeys.health.all }),
      client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
    ]);
  };
}

export function useCreateHealthLog() {
  const onSettled = useHealthMutationSideEffects();
  return useMutation<HealthLog, Error, CreateHealthLogPayload>({
    mutationFn: (payload) => healthService.create(payload),
    onSuccess: onSettled,
  });
}

export function useUpdateHealthLog() {
  const onSettled = useHealthMutationSideEffects();
  return useMutation<HealthLog, Error, { id: Uuid; payload: UpdateHealthLogPayload }>({
    mutationFn: ({ id, payload }) => healthService.update(id, payload),
    onSuccess: onSettled,
  });
}

export function useDeleteHealthLog() {
  const onSettled = useHealthMutationSideEffects();
  return useMutation<void, Error, Uuid>({
    mutationFn: (id) => healthService.remove(id),
    onSuccess: onSettled,
  });
}
