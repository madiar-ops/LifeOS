import type { ReactNode } from 'react';

import { useShell } from '@/contexts/ShellContext';

import { Topbar } from './Topbar';

interface PageShellProps {
  title: string;
  description?: string;
  actions?: ReactNode;
  children: ReactNode;
}

/**
 * Каркас страницы: закреплённая шапка и область содержимого.
 *
 * Каждая страница оборачивает себя в него сама, а не получает заголовок от
 * маршрутизатора. Так заголовок и кнопки действий описаны там же, где логика
 * экрана, и не нужно поддерживать отдельную таблицу «маршрут → заголовок»,
 * которая неизбежно разойдётся с реальностью.
 */
export function PageShell({ title, description, actions, children }: PageShellProps) {
  const { openMenu } = useShell();

  return (
    <>
      <Topbar
        title={title}
        {...(description !== undefined ? { description } : {})}
        {...(actions !== undefined ? { actions } : {})}
        onOpenMenu={openMenu}
      />
      <div className="animate-fade-in mx-auto w-full max-w-[1400px] space-y-5 px-4 py-5 sm:px-6 sm:py-6">
        {children}
      </div>
    </>
  );
}
