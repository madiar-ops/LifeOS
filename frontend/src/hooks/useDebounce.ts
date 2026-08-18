import { useEffect, useState } from 'react';

/**
 * Отложенное значение.
 *
 * Нужен для полей поиска: без него каждая нажатая буква — отдельный запрос к
 * серверу и отдельная запись в кэше. При задержке 350 мс запрос уходит один,
 * когда пользователь закончил печатать.
 */
export function useDebounce<T>(value: T, delayMs = 350): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
