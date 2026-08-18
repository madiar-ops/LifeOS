import { useId, type ReactElement, type ReactNode } from 'react';

import { cn } from '@/lib/cn';

interface FieldProps {
  label: string;
  /** Текст ошибки. Обычно `errors.field?.message` из react-hook-form. */
  error?: string | undefined;
  /** Подсказка под полем. Скрывается, когда показана ошибка. */
  hint?: ReactNode;
  required?: boolean;
  className?: string;
  /**
   * Функция получает готовые атрибуты доступности.
   *
   * Так `id`, `aria-invalid` и `aria-describedby` физически невозможно забыть:
   * они не «рекомендуются к добавлению», а приходят вместе с разметкой поля.
   */
  children: (props: {
    id: string;
    'aria-invalid': boolean;
    'aria-describedby': string | undefined;
  }) => ReactElement;
}

export function Field({
  label,
  error,
  hint,
  required = false,
  className,
  children,
}: FieldProps) {
  const id = useId();
  const messageId = `${id}-message`;
  const hasMessage = error !== undefined || hint !== undefined;

  return (
    <div className={cn('space-y-1.5', className)}>
      <label htmlFor={id} className="block text-[13px] font-medium text-fg">
        {label}
        {required && (
          <span className="ml-0.5 text-danger" aria-hidden="true">
            *
          </span>
        )}
      </label>

      {children({
        id,
        'aria-invalid': error !== undefined,
        'aria-describedby': hasMessage ? messageId : undefined,
      })}

      {hasMessage && (
        <p
          id={messageId}
          // role="alert" только для ошибки: скринридер должен прервать чтение
          // и озвучить проблему, а нейтральная подсказка этого не требует.
          {...(error !== undefined ? { role: 'alert' as const } : {})}
          className={cn('text-[12px]', error !== undefined ? 'text-danger' : 'text-fg-subtle')}
        >
          {error ?? hint}
        </p>
      )}
    </div>
  );
}
