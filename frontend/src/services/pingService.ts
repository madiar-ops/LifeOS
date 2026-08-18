import { api } from '@/lib/httpClient';

/** Ответ `GET /api/ping` — анонимный объект из `PingController`. */
export interface PingResponse {
  service: string;
  status: string;
  environment: string;
  utcTime: string;
}

/**
 * Проверка живости API: `PingController`.
 *
 * Маршрут именно `/api/ping`, а не `/api/health`: последний занял модуль
 * здоровья пользователя (ADR 41). Используется на странице настроек, чтобы
 * пользователь мог отличить «backend не запущен» от «ошибка в приложении».
 */
export const pingService = {
  ping(): Promise<PingResponse> {
    return api.get<PingResponse>('/ping');
  },
};
