/**
 * Конфигурация из переменных окружения, собранная в одном месте.
 *
 * Обращаться к `import.meta.env` напрямую из компонентов нельзя по двум
 * причинам: значение подставляется на этапе сборки (и опечатка в имени
 * переменной даст `undefined` без ошибки), а запасное значение приходилось бы
 * дублировать в каждой точке использования.
 */

/**
 * Базовый URL ASP.NET Core Web API.
 *
 * Запасное значение `/api` рассчитано на две ситуации: dev-proxy Vite и
 * развёртывание фронтенда за тем же доменом, что и API. В остальных случаях
 * переменную нужно задать явно — в разработке через `.env.development`,
 * в продакшене через переменные окружения Vercel.
 */
export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '/api';

export const APP_NAME: string = import.meta.env.VITE_APP_NAME ?? 'LifeOS';

if (import.meta.env.DEV && (import.meta.env.VITE_API_BASE_URL as string | undefined) === undefined) {
  // Предупреждение только в разработке: в продакшене консоль пользователя
  // не место для сообщений о конфигурации.
  console.warn(
    '[LifeOS] VITE_API_BASE_URL не задан — используется запасное значение "/api". ' +
      'Скопируй .env.example в .env.development и укажи адрес backend.',
  );
}
