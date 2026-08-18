import axios, {
  AxiosError,
  AxiosHeaders,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from 'axios';

import { ApiError, type ProblemDetails } from '@/types/errors';

import { API_BASE_URL } from './config';
import { tokenStore } from './tokenStore';

/**
 * Единственная точка выхода в сеть.
 *
 * Здесь живёт вся «магия» авторизации: подстановка Bearer-токена и его
 * прозрачное обновление. Ни один компонент не знает про токены — он просто
 * вызывает сервис и получает данные либо ApiError.
 */

const REFRESH_PATH = '/auth/refresh';
const LOGIN_PATH = '/auth/login';
const REGISTER_PATH = '/auth/register';

/** Пути, на которых Authorization не нужен и обновление токена бессмысленно. */
const ANONYMOUS_PATHS = [REFRESH_PATH, LOGIN_PATH, REGISTER_PATH, '/ping'];

/** Расширение конфига: помечаем повторно отправленный запрос. */
interface RetryableConfig extends InternalAxiosRequestConfig {
  _retriedAfterRefresh?: boolean;
}

export const httpClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 120_000, // AI-эндпоинты на бэкенде ждут FastAPI до 90 секунд
  headers: { Accept: 'application/json' },
});

// =========================================================================
// Оповещение приложения о принудительном выходе
// =========================================================================

type UnauthorizedListener = (reason: string) => void;
const unauthorizedListeners = new Set<UnauthorizedListener>();

/**
 * Подписка на «сессия окончательно потеряна».
 *
 * Нужна, чтобы httpClient не импортировал React-контекст: иначе получился бы
 * цикл зависимостей (контекст → сервис → httpClient → контекст) и слой сети
 * стал бы зависеть от слоя UI.
 */
export function onSessionExpired(listener: UnauthorizedListener): () => void {
  unauthorizedListeners.add(listener);
  return () => unauthorizedListeners.delete(listener);
}

function notifySessionExpired(reason: string): void {
  tokenStore.clear();
  for (const listener of unauthorizedListeners) listener(reason);
}

// =========================================================================
// Обновление токена в единственном экземпляре (single-flight)
// =========================================================================

/**
 * Промис текущего обновления. Ключевая деталь всей схемы.
 *
 * Дашборд открывает несколько запросов сразу. Если access-токен истёк, они
 * получат 401 практически одновременно. Наивная реализация отправила бы
 * несколько POST /auth/refresh с ОДНИМ И ТЕМ ЖЕ refresh-токеном. Бэкенд
 * ротирует токен при первом обновлении, поэтому второй запрос попал бы под
 * защиту от повторного использования (ADR 24), получил
 * `auth.token_reuse_detected` и отозвал бы ВСЮ цепочку токенов — пользователя
 * выбросило бы из приложения при штатном обновлении.
 *
 * Поэтому обновление ровно одно: первый запрос его начинает, остальные ждут
 * тот же промис.
 */
let refreshInFlight: Promise<string> | null = null;

async function refreshAccessToken(): Promise<string> {
  const refreshToken = tokenStore.getRefreshToken();
  if (refreshToken === null) {
    throw new ApiError({
      status: 401,
      code: 'auth.unauthorized',
      message: 'Нет refresh-токена — требуется вход.',
    });
  }

  // Отдельный экземпляр axios: обычный прошёл бы через перехватчики и при
  // отказе снова попытался бы обновиться — получилась бы рекурсия.
  const response = await axios.post<{
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAt: string;
    user: unknown;
  }>(
    `${API_BASE_URL}${REFRESH_PATH}`,
    { refreshToken },
    { headers: { 'Content-Type': 'application/json' } },
  );

  tokenStore.set({
    accessToken: response.data.accessToken,
    refreshToken: response.data.refreshToken,
    accessTokenExpiresAt: response.data.accessTokenExpiresAt,
  });

  return response.data.accessToken;
}

/** Возвращает свежий access-токен, гарантируя не более одного обновления за раз. */
export function ensureFreshAccessToken(): Promise<string> {
  refreshInFlight ??= refreshAccessToken()
    .catch((error: unknown) => {
      notifySessionExpired(
        error instanceof AxiosError && error.response?.status === 400
          ? 'refresh_rejected'
          : 'refresh_failed',
      );
      throw error;
    })
    .finally(() => {
      refreshInFlight = null;
    });

  return refreshInFlight;
}

// =========================================================================
// Перехватчик запроса
// =========================================================================

function isAnonymousPath(url: string | undefined): boolean {
  if (url === undefined) return false;
  return ANONYMOUS_PATHS.some((path) => url.startsWith(path));
}

httpClient.interceptors.request.use(async (config) => {
  if (isAnonymousPath(config.url)) return config;

  // Упреждающее обновление: если токен уже истёк, нет смысла отправлять
  // запрос, получать 401 и делать второй круг. На бэкенде ClockSkew = 0.
  let token = tokenStore.getAccessToken();
  if (tokenStore.isAccessTokenStale() && tokenStore.getRefreshToken() !== null) {
    try {
      token = await ensureFreshAccessToken();
    } catch {
      // Обновиться не удалось — отправляем запрос как есть и получим честный
      // 401, который обработает перехватчик ответа.
    }
  }

  if (token !== null) {
    config.headers = AxiosHeaders.from(config.headers);
    config.headers.set('Authorization', `Bearer ${token}`);
  }

  // Для FormData Content-Type должен выставить браузер: только он знает
  // boundary. Прописанный вручную заголовок ломает multipart-разбор на сервере.
  if (config.data instanceof FormData) {
    config.headers = AxiosHeaders.from(config.headers);
    config.headers.delete('Content-Type');
  }

  return config;
});

// =========================================================================
// Перехватчик ответа
// =========================================================================

/** Приводит любой сбой axios к единому ApiError. */
function toApiError(error: AxiosError<ProblemDetails>): ApiError {
  const response = error.response;

  // Ответа нет вовсе: backend не запущен, сертификат не доверен, CORS отклонил
  // preflight или запрос отменён. Для пользователя это одна ситуация.
  if (response === undefined) {
    const cancelled = axios.isCancel(error) || error.code === 'ERR_CANCELED';
    return new ApiError({
      status: 0,
      code: cancelled ? 'request.cancelled' : 'network.unreachable',
      message: cancelled ? 'Запрос отменён.' : 'Сервер недоступен.',
    });
  }

  const problem = response.data;
  const isProblemObject = typeof problem === 'object' && problem !== null;

  return new ApiError({
    status: response.status,
    code: (isProblemObject ? problem.code : undefined) ?? `http.${response.status}`,
    message:
      (isProblemObject ? (problem.detail ?? problem.title) : undefined) ??
      `Запрос завершился со статусом ${String(response.status)}.`,
    fieldErrors: isProblemObject ? problem.errors : undefined,
    traceId: isProblemObject ? problem.traceId : undefined,
  });
}

httpClient.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    if (!(error instanceof AxiosError)) throw error;

    const axiosError = error as AxiosError<ProblemDetails>;
    const config = axiosError.config as RetryableConfig | undefined;
    const status = axiosError.response?.status;

    if (status !== 401 || config === undefined || config._retriedAfterRefresh === true) {
      throw toApiError(axiosError);
    }

    if (isAnonymousPath(config.url) || tokenStore.getRefreshToken() === null) {
      if (!isAnonymousPath(config.url)) notifySessionExpired('no_refresh_token');
      throw toApiError(axiosError);
    }

    /*
     * Заголовок X-Token-Expired ставит бэкенд в OnAuthenticationFailed
     * (ADR 30). Он разделяет два разных 401:
     *   - «токен истёк»    → обновляем и повторяем запрос;
     *   - «токен неверен»  → обновление не поможет, пользователь выходит.
     * Если заголовок недоступен браузеру (не попал в Access-Control-Expose-
     * Headers), считаем, что попытка обновления допустима: она безопасна и
     * упирается в проверку refresh-токена на сервере.
     */
    const tokenExpired = axiosError.response?.headers['x-token-expired'] === 'true';
    const headerVisible = axiosError.response?.headers['x-token-expired'] !== undefined;
    if (headerVisible && !tokenExpired) {
      notifySessionExpired('invalid_token');
      throw toApiError(axiosError);
    }

    try {
      const freshToken = await ensureFreshAccessToken();
      config._retriedAfterRefresh = true;
      config.headers = AxiosHeaders.from(config.headers);
      config.headers.set('Authorization', `Bearer ${freshToken}`);
      return await httpClient.request(config);
    } catch (refreshError) {
      // ensureFreshAccessToken уже оповестил приложение о потере сессии.
      throw refreshError instanceof ApiError
        ? refreshError
        : toApiError(axiosError);
    }
  },
);

/**
 * Обёртка над `httpClient`, возвращающая сразу тело ответа.
 *
 * Без неё каждый метод сервиса заканчивался бы `.then(r => r.data)`, а место,
 * где легко забыть `.data`, — источник ошибок вида «в состоянии лежит
 * AxiosResponse вместо данных».
 */
export const api = {
  async get<T>(url: string, params?: unknown): Promise<T> {
    const { data } = await httpClient.get<T>(url, { params });
    return data;
  },
  async post<T>(url: string, body?: unknown): Promise<T> {
    const { data } = await httpClient.post<T>(url, body);
    return data;
  },
  async put<T>(url: string, body?: unknown): Promise<T> {
    const { data } = await httpClient.put<T>(url, body);
    return data;
  },
  async patch<T>(url: string, body?: unknown): Promise<T> {
    const { data } = await httpClient.patch<T>(url, body);
    return data;
  },
  async delete(url: string): Promise<void> {
    await httpClient.delete(url);
  },
};
