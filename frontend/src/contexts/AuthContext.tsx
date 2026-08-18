import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';

import { queryKeys } from '@/hooks/queryKeys';
import { onSessionExpired } from '@/lib/httpClient';
import { toast } from '@/lib/toastBus';
import { tokenStore } from '@/lib/tokenStore';
import { authService } from '@/services/authService';
import type { LoginRequest, RegisterRequest } from '@/types/api';
import { ERROR_MESSAGES } from '@/types/errors';

import { AuthContext, type AuthContextValue, type AuthStatus } from './auth-context';

/**
 * Аутентификация.
 *
 * Здесь Context на своём месте: «кто вошёл» нужно всему дереву, значение
 * меняется редко и определяет структуру маршрутов.
 *
 * Сами данные пользователя лежат в кэше TanStack Query, а не в useState.
 * Причина: `GET /auth/me` — обычный серверный запрос, и дублировать его
 * результат в состояние компонента означало бы иметь две копии одной правды.
 * После смены профиля достаточно инвалидировать один ключ.
 *
 * ВОССТАНОВЛЕНИЕ СЕССИИ ПОСЛЕ ПЕРЕЗАГРУЗКИ.
 * Access-токен живёт в памяти и после F5 его нет. Отдельного вызова
 * `/auth/refresh` здесь нет намеренно: запрос `/auth/me` уходит с пустым
 * токеном, перехватчик httpClient видит, что access-токен просрочен, а
 * refresh-токен есть, и обновляет пару ДО отправки запроса. Логика обновления
 * остаётся в одном месте вместо двух.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();

  // Признак «есть что восстанавливать». Инициализируется из localStorage,
  // поэтому первый кадр после F5 сразу показывает состояние «восстанавливаем»,
  // а не форму входа, которая тут же исчезнет.
  const [sessionPresent, setSessionPresent] = useState(() => tokenStore.hasSession());

  const userQuery = useQuery({
    queryKey: queryKeys.currentUser,
    queryFn: () => authService.me(),
    enabled: sessionPresent,
    // Личные данные не устаревают сами по себе — обновляем их явно.
    staleTime: Infinity,
    // Один отказ здесь означает недействительную сессию. Повторять нечего:
    // обновление токена уже попробовал перехватчик httpClient.
    retry: false,
  });

  /**
   * Реакция на окончательную потерю сессии.
   *
   * Событие приходит из httpClient — слой сети не знает про React и сообщает о
   * проблеме через подписку. Кэш очищается полностью: в нём лежат данные
   * предыдущего пользователя, и показать их следующему вошедшему нельзя.
   */
  useEffect(
    () =>
      onSessionExpired((reason: string) => {
        setSessionPresent(false);
        queryClient.clear();
        toast.error(
          'Сессия завершена',
          reason === 'refresh_rejected'
            ? ERROR_MESSAGES['auth.token_reuse_detected']
            : ERROR_MESSAGES['auth.unauthorized'],
        );
      }),
    [queryClient],
  );

  const login = useCallback(
    async (payload: LoginRequest) => {
      const auth = await authService.login(payload);
      // Пользователь приходит вместе с токенами — лишний запрос /auth/me
      // не нужен, кладём его прямо в кэш.
      queryClient.setQueryData(queryKeys.currentUser, auth.user);
      setSessionPresent(true);
    },
    [queryClient],
  );

  const register = useCallback(
    async (payload: RegisterRequest) => {
      const auth = await authService.register(payload);
      queryClient.setQueryData(queryKeys.currentUser, auth.user);
      setSessionPresent(true);
    },
    [queryClient],
  );

  const logout = useCallback(async () => {
    try {
      await authService.logout();
    } finally {
      // Даже если запрос не дошёл, локально пользователь должен выйти.
      setSessionPresent(false);
      queryClient.clear();
    }
  }, [queryClient]);

  const refreshUser = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.currentUser });
  }, [queryClient]);

  const user = userQuery.data ?? null;

  const status: AuthStatus = !sessionPresent
    ? 'anonymous'
    : userQuery.isError
      ? 'anonymous'
      : user === null
        ? 'restoring'
        : 'authenticated';

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      isAdmin: user?.role === 'Admin',
      login,
      register,
      logout,
      refreshUser,
    }),
    [status, user, login, register, logout, refreshUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
