import type { ComponentPropsWithRef, ReactNode } from 'react';

import { cn } from '@/lib/cn';

/**
 * Карточка — базовая поверхность интерфейса.
 *
 * Глубина задаётся и границей, и тенью одновременно: в светлой теме работает
 * тень, в тёмной она невидима и роль разделителя берёт на себя граница.
 * Один компонент вместо двух вариантов под каждую тему.
 */
export function Card({ className, children, ...rest }: ComponentPropsWithRef<'section'>) {
  return (
    <section
      className={cn(
        'rounded-card border border-line bg-surface shadow-card',
        'transition-colors duration-150',
        className,
      )}
      {...rest}
    >
      {children}
    </section>
  );
}

interface CardHeaderProps {
  title: ReactNode;
  description?: ReactNode;
  /** Кнопки или фильтры справа от заголовка. */
  actions?: ReactNode;
  icon?: ReactNode;
  className?: string;
}

export function CardHeader({ title, description, actions, icon, className }: CardHeaderProps) {
  return (
    <header
      className={cn(
        'flex flex-wrap items-start justify-between gap-3 border-b border-line px-5 py-4',
        className,
      )}
    >
      <div className="flex min-w-0 items-start gap-3">
        {icon !== undefined && (
          <span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg bg-accent-soft text-accent">
            {icon}
          </span>
        )}
        <div className="min-w-0">
          <h2 className="truncate text-[15px] font-semibold text-fg">{title}</h2>
          {description !== undefined && (
            <p className="mt-0.5 text-[13px] text-fg-muted">{description}</p>
          )}
        </div>
      </div>
      {actions !== undefined && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </header>
  );
}

export function CardBody({ className, children, ...rest }: ComponentPropsWithRef<'div'>) {
  return (
    <div className={cn('p-5', className)} {...rest}>
      {children}
    </div>
  );
}
