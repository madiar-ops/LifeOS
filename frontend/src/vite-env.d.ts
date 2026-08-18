/// <reference types="vite/client" />

/**
 * Типизация переменных окружения.
 *
 * Без этого объявления `import.meta.env.VITE_API_BASE_URL` имел бы тип `any`
 * и опечатка в имени переменной прошла бы сборку молча.
 */
interface ImportMetaEnv {
  /** Базовый URL API, например https://localhost:7001/api */
  readonly VITE_API_BASE_URL: string;
  /** Отображаемое название приложения. */
  readonly VITE_APP_NAME?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
