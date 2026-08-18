import type { ComponentPropsWithRef, ReactNode } from 'react';

import { cn } from '@/lib/cn';

import { controlClasses } from './controlClasses';

interface InputProps extends ComponentPropsWithRef<'input'> {
  /** Иконка слева внутри поля. */
  icon?: ReactNode;
  /** Приписка справа: единица измерения, код валюты. */
  suffix?: ReactNode;
}

export function Input({ className, icon, suffix, ...rest }: InputProps) {
  // Без иконки и приписки обёртка не нужна: лишний div ломал бы вертикальные
  // отступы в сетках форм.
  if (icon === undefined && suffix === undefined) {
    return <input className={cn(controlClasses, 'h-9.5', className)} {...rest} />;
  }

  return (
    <div className="relative">
      {icon !== undefined && (
        <span
          className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-fg-subtle"
          aria-hidden="true"
        >
          {icon}
        </span>
      )}
      <input
        className={cn(
          controlClasses,
          'h-9.5',
          icon !== undefined && 'pl-9',
          suffix !== undefined && 'pr-14',
          className,
        )}
        {...rest}
      />
      {suffix !== undefined && (
        <span
          className="pointer-events-none absolute top-1/2 right-3 -translate-y-1/2 text-[12px] text-fg-subtle"
          aria-hidden="true"
        >
          {suffix}
        </span>
      )}
    </div>
  );
}
