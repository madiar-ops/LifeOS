import { useContext } from 'react';

import { AuthContext, type AuthContextValue } from '@/contexts/auth-context';

/**
 * Доступ к аутентификации.
 *
 * Бросает исключение вне провайдера, а не возвращает «гостя» по умолчанию:
 * молчаливый запасной вариант превратил бы ошибку сборки дерева в загадочный
 * редирект на страницу входа.
 */
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (context === null) {
    throw new Error('useAuth вызван вне AuthProvider.');
  }
  return context;
}
