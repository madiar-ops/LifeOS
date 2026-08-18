import { api } from '@/lib/httpClient';
import type {
  AiResult,
  FinanceForecast,
  FinanceSummary,
  IsoDate,
  PagedResponse,
  Transaction,
  TransactionPayload,
  TransactionQuery,
  Uuid,
} from '@/types/api';

/** Финансы: `FinanceController`. */
export const financeService = {
  list(query: TransactionQuery): Promise<PagedResponse<Transaction>> {
    return api.get<PagedResponse<Transaction>>('/finance/transactions', query);
  },

  getById(id: Uuid): Promise<Transaction> {
    return api.get<Transaction>(`/finance/transactions/${id}`);
  },

  create(payload: TransactionPayload): Promise<Transaction> {
    return api.post<Transaction>('/finance/transactions', payload);
  },

  update(id: Uuid, payload: TransactionPayload): Promise<Transaction> {
    return api.put<Transaction>(`/finance/transactions/${id}`, payload);
  },

  remove(id: Uuid): Promise<void> {
    return api.delete(`/finance/transactions/${id}`);
  },

  /**
   * Сводка за период.
   *
   * Считается в РАМКАХ ОДНОЙ валюты (ADR 36): конвертации курсов в MVP нет,
   * поэтому смешивать KZT и USD в одном балансе было бы прямой ложью.
   */
  summary(params: {
    from?: IsoDate;
    to?: IsoDate;
    currency?: string;
  }): Promise<FinanceSummary> {
    return api.get<FinanceSummary>('/finance/summary', params);
  },

  /**
   * AI-прогноз расходов. GET, а не POST — запрос ничего не меняет на сервере.
   *
   * В FastAPI уходят только помесячные итоги, отдельных транзакций он не видит
   * (ADR 75). Может вернуть 400 `finance.no_data` (мало данных) или
   * `ai.unavailable` (сервис не поднят) — оба случая интерфейс обрабатывает
   * как объяснимое состояние, а не как поломку.
   */
  analysis(params: {
    monthsBack?: number;
    currency?: string;
  }): Promise<AiResult<FinanceForecast>> {
    return api.get<AiResult<FinanceForecast>>('/finance/analysis', params);
  },
};
