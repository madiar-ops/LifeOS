import { cn } from '@/lib/cn';

interface SegmentedControlProps<T extends string | number> {
  options: readonly { value: T; label: string }[];
  value: T;
  onChange: (value: T) => void;
  className?: string;
  ariaLabel: string;
}

/**
 * Переключатель из нескольких взаимоисключающих вариантов.
 *
 * Реализован как `role="tablist"` из кнопок, а не как набор радиокнопок:
 * визуально это один элемент управления, и переключение должно происходить
 * по одному нажатию. `aria-selected` сообщает скринридеру, какой вариант
 * активен, — без него это просто пять кнопок без состояния.
 */
export function SegmentedControl<T extends string | number>({
  options,
  value,
  onChange,
  className,
  ariaLabel,
}: SegmentedControlProps<T>) {
  return (
    <div
      role="tablist"
      aria-label={ariaLabel}
      className={cn(
        'inline-flex items-center gap-0.5 rounded-lg border border-line bg-surface-2 p-0.5',
        className,
      )}
    >
      {options.map((option) => {
        const active = option.value === value;
        return (
          <button
            key={String(option.value)}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onChange(option.value)}
            className={cn(
              'rounded-[7px] px-2.5 py-1 text-[12.5px] font-medium whitespace-nowrap',
              'transition-colors duration-150',
              active
                ? 'bg-surface text-fg shadow-card'
                : 'text-fg-muted hover:text-fg',
            )}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
