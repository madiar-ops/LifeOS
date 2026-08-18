import { api } from '@/lib/httpClient';
import type { DashboardData } from '@/types/api';

/** Дашборд: `DashboardController`. */
export const dashboardService = {
  /**
   * Сводка главного экрана одним запросом.
   *
   * `days` бэкенд обрезает до диапазона 1..365, поэтому опечатка в параметре
   * не приводит к ошибке (ADR 85) — клиенту не нужна своя проверка.
   */
  get(days: number): Promise<DashboardData> {
    return api.get<DashboardData>('/dashboard', { days });
  },
};
