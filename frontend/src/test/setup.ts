/**
 * Подготовка окружения для всех тестовых файлов.
 *
 * Подключается один раз через `test.setupFiles` в vite.config.ts, поэтому
 * матчеры и заглушки браузерных API не нужно повторять в каждом тесте.
 */

// Импорт из '/vitest', а не из корня пакета: эта точка входа не только
// регистрирует матчеры, но и расширяет типы `expect` из vitest — без неё
// `toBeInTheDocument()` не проходит проверку типов при `npm run typecheck`.
import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// jsdom не реализует matchMedia, а ThemeContext и компоненты графиков
// спрашивают системную тему прямо при монтировании.
if (typeof window.matchMedia !== 'function') {
  window.matchMedia = (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }) as unknown as MediaQueryList;
}

// Размонтирование после каждого теста. Без него дерево предыдущего теста
// остаётся в документе, и `getByText` находит два совпадения вместо одного.
afterEach(() => {
  cleanup();
});
