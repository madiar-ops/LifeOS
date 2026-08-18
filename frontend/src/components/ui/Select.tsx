import { ChevronDown } from 'lucide-react';
import type { ComponentPropsWithRef } from 'react';

import { cn } from '@/lib/cn';

import { controlClasses } from './controlClasses';

/**
 * Выпадающий список.
 *
 * Нативный `<select>`, а не самописный компонент на div'ах. Обоснование:
 * нативный элемент бесплатно получает клавиатурную навигацию, поиск по первой
 * букве, корректное поведение на мобильных (системное колесо выбора) и работу
 * со скринридерами. Самописный список пришлось бы всё это реализовывать
 * заново, и он почти наверняка оказался бы хуже.
 */
export function Select({ className, children, ...rest }: ComponentPropsWithRef<'select'>) {
  return (
    <div className="relative">
      <select
        className={cn(
          controlClasses,
          'h-9.5 cursor-pointer appearance-none pr-9',
          className,
        )}
        {...rest}
      >
        {children}
      </select>
      <ChevronDown
        size={15}
        aria-hidden="true"
        className="pointer-events-none absolute top-1/2 right-3 -translate-y-1/2 text-fg-subtle"
      />
    </div>
  );
}
