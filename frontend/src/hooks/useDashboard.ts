import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { dashboardService } from '@/services/dashboardService';

import { queryKeys } from './queryKeys';

/**
 * Данные главного экрана.
 *
 * Один запрос вместо восьми — так спроектирован бэкенд (ADR 79). Смена
 * периода не показывает пустой экран: старая сводка остаётся, пока считается
 * новая.
 */
export function useDashboard(days: number) {
  return useQuery({
    queryKey: queryKeys.dashboard.byDays(days),
    queryFn: () => dashboardService.get(days),
    placeholderData: keepPreviousData,
    // Сводка агрегируется в PostgreSQL и достаточно тяжела, чтобы не
    // перезапрашивать её чаще, чем раз в полминуты.
    staleTime: 30_000,
  });
}
