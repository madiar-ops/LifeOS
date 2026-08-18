/**
 * Хранилище токенов.
 *
 * РЕШЕНИЕ О ХРАНЕНИИ (готовый ответ на вопрос преподавателя):
 *
 *  - Access-токен живёт ТОЛЬКО в памяти модуля. В localStorage его нет: любой
 *    XSS прочитал бы оттуда действующий токен, а отозвать access-токен нельзя
 *    по устройству (ADR 22) — его можно только пережить. Перезагрузка страницы
 *    память очищает, и это правильно.
 *
 *  - Refresh-токен лежит в localStorage. Иначе пользователь разлогинивался бы
 *    при каждом F5, что для SPA неприемлемо. Риск осознан и компенсирован на
 *    бэкенде: refresh одноразовый, при обновлении ротируется, а повторное
 *    использование трактуется как кража и отзывает ВСЮ цепочку токенов
 *    пользователя (ADR 24). То есть украденный refresh-токен обнаруживается.
 *
 *  - Идеальное решение — refresh в httpOnly+Secure+SameSite cookie: JavaScript
 *    его не видит вовсе. Оно требует правок бэкенда (установка cookie, CSRF-
 *    защита, CORS с credentials), а PROMPTS_GUIDE запрещает менять backend в
 *    фазе фронтенда. Отмечено как задача Фазы 10.
 *
 * Срок жизни access-токена хранится рядом с ним, чтобы обновлять токен ДО
 * запроса, а не после отказа: на бэкенде `ClockSkew = TimeSpan.Zero` (ADR 25),
 * поэтому запаса по времени нет вообще.
 */

const REFRESH_STORAGE_KEY = 'lifeos.refreshToken';

/** Обновляем токен заранее: 15 секунд запаса перекрывают сетевую задержку. */
const EXPIRY_SAFETY_MARGIN_MS = 15_000;

let accessToken: string | null = null;
let accessTokenExpiresAt: number | null = null;

/** localStorage недоступен в приватном режиме некоторых браузеров. */
function safeLocalStorage(): Storage | null {
  try {
    const probe = '__lifeos_probe__';
    window.localStorage.setItem(probe, '1');
    window.localStorage.removeItem(probe);
    return window.localStorage;
  } catch {
    return null;
  }
}

const storage = safeLocalStorage();

export const tokenStore = {
  getAccessToken(): string | null {
    return accessToken;
  },

  getRefreshToken(): string | null {
    return storage?.getItem(REFRESH_STORAGE_KEY) ?? null;
  },

  /** Признак «пользователь когда-то входил» — по нему решаем, пробовать ли restore. */
  hasSession(): boolean {
    return this.getRefreshToken() !== null;
  },

  set(tokens: { accessToken: string; refreshToken: string; accessTokenExpiresAt: string }): void {
    accessToken = tokens.accessToken;
    accessTokenExpiresAt = Date.parse(tokens.accessTokenExpiresAt);
    storage?.setItem(REFRESH_STORAGE_KEY, tokens.refreshToken);
  },

  clear(): void {
    accessToken = null;
    accessTokenExpiresAt = null;
    storage?.removeItem(REFRESH_STORAGE_KEY);
  },

  /** true, если токена нет или он истекает в ближайшие секунды. */
  isAccessTokenStale(): boolean {
    if (accessToken === null) return true;
    if (accessTokenExpiresAt === null || Number.isNaN(accessTokenExpiresAt)) return false;
    return Date.now() >= accessTokenExpiresAt - EXPIRY_SAFETY_MARGIN_MS;
  },
};
