import { useEffect, useMemo, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';

import { Sidebar } from '@/components/layout/Sidebar';
import { ShellContext } from '@/contexts/ShellContext';

/**
 * Оболочка приватной части приложения.
 *
 * Боковое меню закреплено на широких экранах и выдвигается на узких. Состояние
 * этого меню — единственное, что здесь хранится, и оно сбрасывается при смене
 * маршрута: иначе после перехода на телефоне панель осталась бы открытой
 * поверх только что выбранной страницы.
 */
export function DashboardLayout() {
  const [menuOpen, setMenuOpen] = useState(false);
  const location = useLocation();

  useEffect(() => {
    setMenuOpen(false);
  }, [location.pathname]);

  const shell = useMemo(() => ({ openMenu: () => setMenuOpen(true) }), []);

  return (
    <ShellContext.Provider value={shell}>
      <div className="min-h-screen bg-bg">
        <Sidebar open={menuOpen} onClose={() => setMenuOpen(false)} />
        {/* Отступ слева равен ширине меню только на широких экранах. */}
        <main className="lg:pl-64">
          <Outlet />
        </main>
      </div>
    </ShellContext.Provider>
  );
}
