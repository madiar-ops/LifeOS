import { LogOut, X } from 'lucide-react';
import { NavLink } from 'react-router-dom';

import { Avatar, Button } from '@/components/ui';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { NAV_ITEMS, type NavItem } from '@/router/routes';

const GROUP_TITLES: Record<NavItem['group'], string | null> = {
  main: null,
  modules: 'Модули',
  account: 'Аккаунт',
};

interface SidebarProps {
  /** Открыт ли мобильный вариант меню. */
  open: boolean;
  onClose: () => void;
}

function NavGroup({ group, onNavigate }: { group: NavItem['group']; onNavigate: () => void }) {
  const items = NAV_ITEMS.filter((item) => item.group === group);
  const title = GROUP_TITLES[group];

  return (
    <div className="space-y-0.5">
      {title !== null && (
        <p className="px-3 pt-4 pb-1.5 text-[10.5px] font-semibold tracking-wider text-fg-subtle uppercase">
          {title}
        </p>
      )}
      {items.map(({ to, label, icon: Icon }) => (
        <NavLink
          key={to}
          to={to}
          // `end` только для корневого маршрута: иначе «Обзор» подсвечивался бы
          // активным на всех страницах, поскольку каждый путь начинается с «/».
          end={to === '/'}
          onClick={onNavigate}
          className={({ isActive }) =>
            cn(
              'flex items-center gap-2.5 rounded-lg px-3 py-2 text-[13.5px] font-medium',
              'transition-colors duration-150',
              isActive
                ? 'bg-accent-soft text-accent'
                : 'text-fg-muted hover:bg-surface-2 hover:text-fg',
            )
          }
        >
          {({ isActive }) => (
            <>
              <Icon size={16} className={isActive ? 'text-accent' : 'text-fg-subtle'} />
              {label}
            </>
          )}
        </NavLink>
      ))}
    </div>
  );
}

/**
 * Боковое меню.
 *
 * На узких экранах превращается в выдвижную панель поверх содержимого, а не
 * прячется совсем: адаптивность из UI REQUIREMENTS означает, что интерфейс
 * остаётся полнофункциональным на телефоне, а не теряет навигацию.
 */
export function Sidebar({ open, onClose }: SidebarProps) {
  const { user, logout } = useAuth();

  return (
    <>
      {/* Подложка только для мобильного варианта. */}
      <div
        aria-hidden="true"
        onClick={onClose}
        className={cn(
          'fixed inset-0 z-30 bg-black/50 transition-opacity duration-200 lg:hidden',
          open ? 'opacity-100' : 'pointer-events-none opacity-0',
        )}
      />

      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-line bg-surface',
          'transition-transform duration-200 lg:translate-x-0',
          open ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-14 shrink-0 items-center justify-between border-b border-line px-4">
          <span className="flex items-center gap-2">
            <span className="flex size-7 items-center justify-center rounded-lg bg-accent text-[13px] font-bold text-accent-fg">
              L
            </span>
            <span className="text-[15px] font-semibold tracking-tight">LifeOS</span>
          </span>
          <Button
            variant="ghost"
            size="icon"
            className="lg:hidden"
            onClick={onClose}
            aria-label="Закрыть меню"
          >
            <X size={17} />
          </Button>
        </div>

        <nav className="flex-1 overflow-y-auto p-2" aria-label="Основная навигация">
          <NavGroup group="main" onNavigate={onClose} />
          <NavGroup group="modules" onNavigate={onClose} />
          <NavGroup group="account" onNavigate={onClose} />
        </nav>

        {user !== null && (
          <div className="shrink-0 border-t border-line p-3">
            <div className="flex items-center gap-2.5">
              <Avatar name={user.name} surname={user.surname} url={user.avatarUrl} />
              <div className="min-w-0 flex-1">
                <p className="truncate text-[13px] font-medium text-fg">
                  {user.name} {user.surname}
                </p>
                <p className="truncate text-[11.5px] text-fg-subtle">{user.email}</p>
              </div>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => void logout()}
                aria-label="Выйти"
                title="Выйти"
              >
                <LogOut size={16} />
              </Button>
            </div>
          </div>
        )}
      </aside>
    </>
  );
}
