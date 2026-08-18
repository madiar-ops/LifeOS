import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { aiService } from '@/services/aiService';
import type { PaginationParams, Uuid } from '@/types/api';
import type { ModuleType } from '@/types/enums';

import { queryKeys } from './queryKeys';

export function useRecommendations(query: PaginationParams & { module?: ModuleType }) {
  return useQuery({
    queryKey: queryKeys.ai.recommendations(query),
    queryFn: () => aiService.listRecommendations(query),
    placeholderData: keepPreviousData,
  });
}

export function useAiHistory(query: PaginationParams) {
  return useQuery({
    queryKey: queryKeys.ai.history(query),
    queryFn: () => aiService.history(query),
    placeholderData: keepPreviousData,
  });
}

export function useDismissRecommendation() {
  const client = useQueryClient();
  return useMutation<void, Error, Uuid>({
    mutationFn: (id) => aiService.removeRecommendation(id),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.ai.all }),
        // Дашборд показывает свежие рекомендации отдельным виджетом.
        client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      ]);
    },
  });
}
