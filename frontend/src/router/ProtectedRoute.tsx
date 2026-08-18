import { Navigate, Outlet, useLocation } from 'react-router-dom';

import { FullPageSpinner } from '@/components/ui';
import { useAuth } from '@/hooks/useAuth';

import { ROUTES } from './routes';

/**
 * Защита приватных маршрутов.
 *
 * ВАЖНО ПОНИМАТЬ ГРАНИЦЫ: это не безопасность, а удобство. Настоящая проверка
 * доступа выполняется на сервере — атрибут `[Authorize]` на контроллерах и
 * фильтр по `UserId` в каждом сервисе (CrudGuard.EnsureOwned). Любой может
 * убрать этот компонент в DevTools и увидеть пустой экран без данных, потому
 * что все запросы вернут 401. Здесь решается только одно: не показывать
 * интерфейс, который всё равно не наполнится данными.
 *
 * Состояние `restoring` обрабатывается отдельно от `anonymous`. Без этого при
 * каждой перезагрузке страницы пользователя на мгновение выбрасывало бы на
 * форму входа — сессия ещё восстанавливается, а маршрутизатор уже решил, что
 * гость.
 */
export function ProtectedRoute() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === 'restoring') {
    return <FullPageSpinner label="Восстанавливаем сессию" />;
  }

  if (status === 'anonymous') {
    // Запомненный адрес возвращает пользователя туда, куда он шёл, — иначе
    // после входа он всегда оказывался бы на главной.
    return <Navigate to={ROUTES.login} replace state={{ from: location.pathname }} />;
  }

  return <Outlet />;
}

/**
 * Обратная защита: вошедшему пользователю нечего делать на форме входа.
 *
 * Без неё возврат «назад» после входа показывал бы форму логина, и это
 * выглядело бы как выход из системы.
 */
export function GuestRoute() {
  const { status } = useAuth();

  if (status === 'restoring') {
    return <FullPageSpinner label="Проверяем сессию" />;
  }

  if (status === 'authenticated') {
    return <Navigate to={ROUTES.dashboard} replace />;
  }

  return <Outlet />;
}
