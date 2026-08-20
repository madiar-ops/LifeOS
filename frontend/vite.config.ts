import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
// defineConfig берётся из 'vitest/config', а не из 'vite': только эта версия
// знает про секцию `test`. Отдельного vitest.config.ts нет намеренно — иначе
// алиас `@` пришлось бы описывать дважды и однажды он разошёлся бы со сборкой.
import { defineConfig } from 'vitest/config';

/**
 * Конфигурация сборки фронтенда LifeOS.
 *
 * Обращение к API идёт НАПРЯМУЮ на https://localhost:7001, без dev-proxy.
 * Это осознанное решение: прокси сделал бы запросы одноисточниковыми и
 * скрыл механизм CORS, поэтому ошибка в политике CORS обнаружилась бы
 * только после деплоя. Прямое обращение прогоняет в разработке тот же
 * путь, что и в продакшене: preflight, Authorization, CORS-заголовки.
 *
 * Цена решения — самоподписанный сертификат ASP.NET нужно один раз
 * сделать доверенным: `dotnet dev-certs https --trust`.
 * Если это по каким-то причинам невозможно, раскомментируй блок server.proxy
 * ниже и выставь VITE_API_BASE_URL=/api.
 */
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    // Порт зафиксирован намеренно: он прописан в Cors:AllowedOrigins
    // на бэкенде. Если Vite выберет другой порт, браузер отклонит ответы.
    strictPort: true,
    // proxy: {
    //   '/api': {
    //     target: 'https://localhost:7001',
    //     changeOrigin: true,
    //     secure: false, // самоподписанный сертификат разработки
    //   },
    // },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  test: {
    // jsdom, а не node: тестируются компоненты React и tokenStore, который
    // обращается к window.localStorage прямо на этапе загрузки модуля.
    environment: 'jsdom',
    globals: false, // describe/it/expect импортируются явно — так виден источник
    setupFiles: ['./src/test/setup.ts'],
    // Часовой пояс фиксируется, иначе тесты форматирования дат зелёные на
    // машине разработчика и красные в CI. Взят рабочий пояс проекта (UTC+5):
    // так проверка `isoToDatetimeLocal` действительно ловит сдвиг времени,
    // а не проходит вырожденно из-за совпадения с UTC.
    env: { TZ: 'Asia/Almaty' },
    css: false, // Tailwind не влияет на поведение, а его сборка — секунды на каждый прогон
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      // В отчёт входит только то, что вообще имеет смысл покрывать:
      // страницы и графики проверяются вручную, а не снимками разметки.
      include: ['src/lib/**', 'src/schemas/**', 'src/components/ui/**'],
      exclude: ['src/**/index.ts'],
    },
  },
});
