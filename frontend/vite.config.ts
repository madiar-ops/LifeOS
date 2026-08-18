import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

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
});
