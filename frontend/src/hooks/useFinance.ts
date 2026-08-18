import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { financeService } from '@/services/financeService';
import type { IsoDate, Transaction, TransactionPayload, TransactionQuery, Uuid } from '@/types/api';

import { queryKeys } from './queryKeys';

export function useTransactions(query: TransactionQuery) {
  return useQuery({
    queryKey: queryKeys.finance.transactions(query),
    queryFn: () => financeService.list(query),
    placeholderData: keepPreviousData,
  });
}

export function useFinanceSummary(params: {
  from?: IsoDate;
  to?: IsoDate;
  currency?: string;
}) {
  return useQuery({
    queryKey: queryKeys.finance.summary(params),
    queryFn: () => financeService.summary(params),
  });
}

/**
 * AI-прогноз расходов.
 *
 * `enabled` управляется снаружи: прогноз запускается по кнопке, а не при
 * открытии экрана. Причина не в производительности фронтенда — каждый вызов
 * идёт в FastAPI, пишется в AIHistory и может создать рекомендацию. Дёргать
 * его при каждом монтировании компонента означало бы засорять ленту
 * рекомендаций и историю без действия пользователя.
 *
 * `staleTime: Infinity` — результат прогноза не устаревает сам по себе:
 * повторный запрос должен быть явным.
 */
export function useFinanceAnalysis(
  params: { monthsBack?: number; currency?: string },
  enabled: boolean,
) {
  return useQuery({
    queryKey: queryKeys.finance.analysis(params),
    queryFn: () => financeService.analysis(params),
    enabled,
    staleTime: Infinity,
    retry: false,
  });
}

function useFinanceMutationSideEffects() {
  const client = useQueryClient();
  return async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: queryKeys.finance.all }),
      client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
    ]);
  };
}

export function useCreateTransaction() {
  const onSettled = useFinanceMutationSideEffects();
  return useMutation<Transaction, Error, TransactionPayload>({
    mutationFn: (payload) => financeService.create(payload),
    onSuccess: onSettled,
  });
}

export function useUpdateTransaction() {
  const onSettled = useFinanceMutationSideEffects();
  return useMutation<Transaction, Error, { id: Uuid; payload: TransactionPayload }>({
    mutationFn: ({ id, payload }) => financeService.update(id, payload),
    onSuccess: onSettled,
  });
}

export function useDeleteTransaction() {
  const onSettled = useFinanceMutationSideEffects();
  return useMutation<void, Error, Uuid>({
    mutationFn: (id) => financeService.remove(id),
    onSuccess: onSettled,
  });
}
