import { Menu } from 'lucide-react';
import type { ReactNode } from 'react';

import { Button } from '@/components/ui';

import { ThemeToggle } from './ThemeToggle';

interface TopbarProps {
  title: string;
  description?: string;
  /** Кнопки действий, специфичные для страницы. */
  actions?: ReactNode;
  onOpenMenu: () => void;
}

export function Topbar({ title, description, actions, onOpenMenu }: TopbarProps) {
  return (
    <header
      className={
        // sticky + backdrop-blur: заголовок остаётся на месте при прокрутке
        // длинных таблиц, но не выглядит отдельной плитой поверх содержимого.
        'sticky top-0 z-20 flex min-h-14 flex-wrap items-center gap-3 border-b border-line bg-bg/85 px-4 py-2.5 backdrop-blur-md sm:px-6'
      }
    >
      <Button
        variant="ghost"
        size="icon"
        className="lg:hidden"
        onClick={onOpenMenu}
        aria-label="Открыть меню"
      >
        <Menu size={18} />
      </Button>

      <div className="min-w-0 flex-1">
        <h1 className="truncate text-[15px] font-semibold tracking-tight text-fg">{title}</h1>
        {description !== undefined && (
          <p className="truncate text-[12.5px] text-fg-muted">{description}</p>
        )}
      </div>

      <div className="flex items-center gap-2">
        {actions}
        <ThemeToggle />
      </div>
    </header>
  );
}
