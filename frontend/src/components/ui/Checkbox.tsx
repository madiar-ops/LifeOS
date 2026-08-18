import { Check } from 'lucide-react';
import type { ComponentPropsWithRef } from 'react';

import { cn } from '@/lib/cn';

interface CheckboxProps extends Omit<ComponentPropsWithRef<'input'>, 'type'> {
  label?: string;
}

/**
 * Флажок.
 *
 * Настоящий `<input type="checkbox">` остаётся в разметке и лишь визуально
 * скрыт (`sr-only`), а не заменён на div: так сохраняются фокус, пробел для
 * переключения и связь с формой. Галочка рисуется поверх через `peer`.
 */
export function Checkbox({ label, className, ...rest }: CheckboxProps) {
  return (
    <label className={cn('inline-flex cursor-pointer items-center gap-2 select-none', className)}>
      <span className="relative inline-flex size-4.5 shrink-0">
        <input type="checkbox" className="peer sr-only" {...rest} />
        <span
          aria-hidden="true"
          className={cn(
            'inline-flex size-4.5 items-center justify-center rounded-[5px] border border-line-strong bg-surface',
            'transition-colors duration-150',
            'peer-hover:border-accent',
            'peer-checked:border-accent peer-checked:bg-accent peer-checked:text-accent-fg',
            'peer-focus-visible:ring-2 peer-focus-visible:ring-accent/35',
            'peer-disabled:opacity-50',
            // Галочка внутри — не сосед input'а, а потомок соседа, поэтому
            // обычный peer-checked: до неё не доходит: селектор `~ *` matches
            // только братьев. Произвольный вариант добавляет вложенность.
            'peer-checked:[&>svg]:opacity-100',
          )}
        >
          <Check size={12} strokeWidth={3.5} className="opacity-0" />
        </span>
      </span>
      {label !== undefined && <span className="text-sm text-fg">{label}</span>}
    </label>
  );
}
