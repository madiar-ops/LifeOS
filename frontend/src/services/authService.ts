import { api } from '@/lib/httpClient';
import { tokenStore } from '@/lib/tokenStore';
import type { AuthResponse, LoginRequest, RegisterRequest, User } from '@/types/api';

/**
 * Аутентификация: `AuthController`.
 *
 * Сервис — единственное место, которое имеет право писать в tokenStore после
 * входа. Компоненты вызывают его через AuthContext и о токенах не знают.
 */
export const authService = {
  async register(payload: RegisterRequest): Promise<AuthResponse> {
    const auth = await api.post<AuthResponse>('/auth/register', payload);
    tokenStore.set(auth);
    return auth;
  },

  async login(payload: LoginRequest): Promise<AuthResponse> {
    const auth = await api.post<AuthResponse>('/auth/login', payload);
    tokenStore.set(auth);
    return auth;
  },

  /**
   * Выход. Отзывает refresh-токен на сервере.
   *
   * Локальные токены чистятся в `finally`: если сеть отвалилась, пользователь
   * всё равно должен оказаться разлогиненным в браузере. Оставить его «внутри»
   * из-за неудачного запроса — худший из вариантов.
   */
  async logout(): Promise<void> {
    const refreshToken = tokenStore.getRefreshToken();
    try {
      if (refreshToken !== null) {
        await api.post('/auth/logout', { refreshToken });
      }
    } finally {
      tokenStore.clear();
    }
  },

  me(): Promise<User> {
    return api.get<User>('/auth/me');
  },
};
