import { X } from 'lucide-react';
import { useEffect, useRef, type ReactNode } from 'react';

import { cn } from '@/lib/cn';

import { Button } from './Button';

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: ReactNode;
  /** Кнопки внизу. Обычно «Отмена» и основное действие. */
  footer?: ReactNode;
  size?: 'sm' | 'md' | 'lg';
}

const SIZES = {
  sm: 'max-w-md',
  md: 'max-w-xl',
  lg: 'max-w-3xl',
};

/**
 * Модальное окно на нативном `<dialog>`.
 *
 * Нативный элемент даёт то, что вручную делается плохо и почти всегда с
 * ошибками: перехват фокуса внутри окна, закрытие по Escape, слой поверх
 * всего остального без войны z-index и блокировка фона для скринридеров.
 * Собственная реализация на div с position: fixed выглядит так же, но
 * пользователь на клавиатуре уходит фокусом за пределы окна.
 */
export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  size = 'md',
}: ModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (dialog === null) return;

    if (open && !dialog.open) {
      dialog.showModal();
      // Прокрутка фона под открытым окном дезориентирует.
      document.body.style.overflow = 'hidden';
    } else if (!open && dialog.open) {
      dialog.close();
      document.body.style.overflow = '';
    }

    return () => {
      document.body.style.overflow = '';
    };
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      // Escape вызывает событие cancel — окно должно закрываться через тот же
      // обработчик, что и кнопка, иначе внешнее состояние `open` рассинхронится
      // с реальным состоянием диалога, и второй раз он не откроется.
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        // Клик по подложке: цель события — сам dialog, а не его содержимое.
        if (event.target === dialogRef.current) onClose();
      }}
      className={cn(
        'w-[calc(100%-2rem)] rounded-card border border-line bg-surface p-0 text-fg shadow-pop',
        'backdrop:bg-black/55 backdrop:backdrop-blur-[2px]',
        'open:animate-slide-up m-auto',
        SIZES[size],
      )}
      aria-labelledby="modal-title"
    >
      <header className="flex items-start justify-between gap-4 border-b border-line px-5 py-4">
        <div className="min-w-0">
          <h2 id="modal-title" className="text-[15px] font-semibold">
            {title}
          </h2>
          {description !== undefined && (
            <p className="mt-0.5 text-[13px] text-fg-muted">{description}</p>
          )}
        </div>
        <Button variant="ghost" size="icon" onClick={onClose} aria-label="Закрыть">
          <X size={17} />
        </Button>
      </header>

      <div className="max-h-[65vh] overflow-y-auto px-5 py-4">{children}</div>

      {footer !== undefined && (
        <footer className="flex flex-wrap justify-end gap-2 border-t border-line bg-surface-2 px-5 py-3.5">
          {footer}
        </footer>
      )}
    </dialog>
  );
}
