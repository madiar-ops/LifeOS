import { createContext } from 'react';

import type { LoginRequest, RegisterRequest, User } from '@/types/api';

/**
 * `restoring` — отдельное состояние, а не разновидность `anonymous`.
 *
 * После перезагрузки страницы access-токен потерян, и приложение обновляет его
 * по refresh-токену. В этот момент пользователь ещё не «гость»: показать ему
 * форму входа значило бы мигнуть ею и тут же убрать.
 */
export type AuthStatus = 'restoring' | 'authenticated' | 'anonymous';

export interface AuthContextValue {
  status: AuthStatus;
  user: User | null;
  isAdmin: boolean;
  login: (payload: LoginRequest) => Promise<void>;
  register: (payload: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
  /** Перечитать данные пользователя — после смены имени или аватара. */
  refreshUser: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
