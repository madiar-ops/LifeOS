import { cn } from '@/lib/cn';

interface SpinnerProps {
  size?: number;
  className?: string;
  label?: string;
}

/**
 * Индикатор ожидания.
 *
 * `role="status"` и текст для скринридера обязательны: без них слепой
 * пользователь не узнаёт, что интерфейс чего-то ждёт, — вращающаяся картинка
 * для него просто не существует.
 */
export function Spinner({ size = 16, className, label = 'Загрузка' }: SpinnerProps) {
  return (
    <span role="status" aria-live="polite" className={cn('inline-flex', className)}>
      <svg
        width={size}
        height={size}
        viewBox="0 0 24 24"
        fill="none"
        aria-hidden="true"
        className="animate-spin"
      >
        <circle cx="12" cy="12" r="9" stroke="currentColor" strokeOpacity="0.25" strokeWidth="3" />
        <path
          d="M21 12a9 9 0 0 0-9-9"
          stroke="currentColor"
          strokeWidth="3"
          strokeLinecap="round"
        />
      </svg>
      <span className="sr-only">{label}</span>
    </span>
  );
}

/** Полноэкранное ожидание — для восстановления сессии при первом входе. */
export function FullPageSpinner({ label = 'Загрузка' }: { label?: string }) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-bg">
      <Spinner size={26} className="text-accent" label={label} />
      <p className="text-sm text-fg-muted">{label}…</p>
    </div>
  );
}
