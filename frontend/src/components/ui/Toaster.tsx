import { CheckCircle2, Info, X, XCircle } from 'lucide-react';
import { useEffect, useState } from 'react';

import { cn } from '@/lib/cn';
import { subscribeToToasts, type ToastKind, type ToastMessage } from '@/lib/toastBus';

const AUTO_DISMISS_MS: Record<ToastKind, number> = {
  success: 3500,
  info: 4000,
  // Ошибка живёт дольше: её нужно успеть прочитать, а иногда и запомнить traceId.
  error: 7000,
};

const TONES: Record<ToastKind, { ring: string; icon: string }> = {
  success: { ring: 'border-success/35', icon: 'text-success' },
  error: { ring: 'border-danger/35', icon: 'text-danger' },
  info: { ring: 'border-info/35', icon: 'text-info' },
};

const ICONS: Record<ToastKind, typeof Info> = {
  success: CheckCircle2,
  error: XCircle,
  info: Info,
};

/** Ограничение очереди: пять сообщений уже перекрывают экран. */
const MAX_VISIBLE = 5;

/**
 * Область вывода уведомлений.
 *
 * Монтируется один раз в корне приложения и подписывается на шину. Компоненты
 * вызывают `toast.success(...)` как обычную функцию и ничего о провайдере не
 * знают — это позволяет показывать уведомления из слоёв без доступа к React,
 * например из глобального обработчика ошибок мутаций.
 */
export function Toaster() {
  const [messages, setMessages] = useState<ToastMessage[]>([]);

  useEffect(
    () =>
      subscribeToToasts((message) => {
        setMessages((current) => [...current, message].slice(-MAX_VISIBLE));
        window.setTimeout(() => {
          setMessages((current) => current.filter((item) => item.id !== message.id));
        }, AUTO_DISMISS_MS[message.kind]);
      }),
    [],
  );

  return (
    <div
      // aria-live="polite" — уведомление озвучивается, но не прерывает
      // текущее чтение. Для assertive это было бы навязчиво.
      aria-live="polite"
      aria-atomic="false"
      className="pointer-events-none fixed inset-x-0 bottom-0 z-50 flex flex-col items-center gap-2 p-4 sm:right-0 sm:left-auto sm:items-end"
    >
      {messages.map((message) => {
        const Icon = ICONS[message.kind];
        return (
          <div
            key={message.id}
            className={cn(
              'animate-slide-up pointer-events-auto flex w-full max-w-sm items-start gap-3',
              'rounded-xl border bg-surface p-3.5 shadow-pop',
              TONES[message.kind].ring,
            )}
          >
            <Icon size={17} className={cn('mt-0.5 shrink-0', TONES[message.kind].icon)} />
            <div className="min-w-0 flex-1">
              <p className="text-[13px] font-medium text-fg">{message.title}</p>
              {message.description !== undefined && message.description !== '' && (
                <p className="mt-0.5 text-[12.5px] leading-relaxed break-words text-fg-muted">
                  {message.description}
                </p>
              )}
            </div>
            <button
              type="button"
              aria-label="Скрыть уведомление"
              onClick={() =>
                setMessages((current) => current.filter((item) => item.id !== message.id))
              }
              className="-m-1 shrink-0 rounded p-1 text-fg-subtle transition-colors hover:text-fg"
            >
              <X size={14} />
            </button>
          </div>
        );
      })}
    </div>
  );
}
