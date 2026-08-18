import type { ComponentPropsWithRef, ReactNode } from 'react';

import { cn } from '@/lib/cn';

import { Spinner } from './Spinner';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'subtle';
export type ButtonSize = 'sm' | 'md' | 'lg' | 'icon';

interface ButtonProps extends ComponentPropsWithRef<'button'> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Показывает спиннер и блокирует кнопку — защита от двойной отправки формы. */
  loading?: boolean;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
}

const VARIANTS: Record<ButtonVariant, string> = {
  primary:
    'bg-accent text-accent-fg hover:bg-accent-hover shadow-sm active:translate-y-px',
  secondary:
    'bg-surface text-fg border border-line hover:bg-surface-2 hover:border-line-strong',
  ghost: 'text-fg-muted hover:bg-surface-2 hover:text-fg',
  danger: 'bg-danger text-white hover:brightness-110 active:translate-y-px',
  subtle: 'bg-surface-2 text-fg hover:bg-surface-3',
};

const SIZES: Record<ButtonSize, string> = {
  sm: 'h-8 px-3 text-[13px] gap-1.5 rounded-lg',
  md: 'h-9.5 px-4 text-sm gap-2 rounded-lg',
  lg: 'h-11 px-5 text-[15px] gap-2 rounded-xl',
  icon: 'h-9 w-9 rounded-lg',
};

/**
 * Кнопка.
 *
 * `loading` блокирует кнопку, а не просто рисует спиннер: иначе пользователь
 * успевает нажать «Создать» второй раз, пока идёт первый запрос, и получает
 * две одинаковые записи. Это самая частая ошибка в CRUD-интерфейсах.
 */
export function Button({
  variant = 'secondary',
  size = 'md',
  loading = false,
  leftIcon,
  rightIcon,
  className,
  children,
  disabled,
  type = 'button',
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled === true || loading}
      aria-busy={loading}
      className={cn(
        'inline-flex shrink-0 items-center justify-center font-medium whitespace-nowrap',
        'transition-[background-color,border-color,color,filter,transform] duration-150',
        'disabled:pointer-events-none disabled:opacity-50',
        VARIANTS[variant],
        SIZES[size],
        className,
      )}
      {...rest}
    >
      {/*
        Во время ожидания спиннер ЗАМЕНЯЕТ левую иконку, а не добавляется к ней:
        иначе кнопка меняет ширину и «дёргается» при каждом нажатии.
        Содержимое (`children`) отрисовывается всегда, включая size="icon" —
        именно там иконка и передаётся как children.
      */}
      {loading ? <Spinner size={size === 'lg' ? 18 : 15} /> : leftIcon}
      {/* У квадратной кнопки иконка передаётся через children, поэтому во время
          ожидания её нужно убрать — иначе спиннер и иконка наложатся. */}
      {loading && size === 'icon' ? null : children}
      {rightIcon}
    </button>
  );
}
