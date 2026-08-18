/**
 * Ошибки API.
 *
 * Бэкенд отвечает в формате ProblemDetails (RFC 7807) и добавляет расширение
 * `code` — машиночитаемый код ошибки (ADR 20). Фронтенд реагирует именно на
 * `code`, а не на текст `detail`: текст можно переписать или перевести, и любая
 * проверка вида `if (message.includes('не найдено'))` сломается молча.
 */

/** Тело ответа при ошибке. Совпадает с ProblemDetails + расширениями бэкенда. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Расширение бэкенда: машиночитаемый код. */
  code?: string;
  /** Расширение бэкенда: идентификатор запроса для сопоставления с логами. */
  traceId?: string;
  /** Заполняется только для ValidationProblemDetails: поле → список сообщений. */
  errors?: Record<string, string[]>;
}

/**
 * Нормализованная ошибка, единая для всего приложения.
 *
 * Наследование от Error, а не отдельная структура: так ошибку можно бросать,
 * она попадает в `onError` TanStack Query и сохраняет стек вызовов.
 */
export class ApiError extends Error {
  /** HTTP-статус. 0 означает, что ответа не было вовсе (сеть, CORS, обрыв). */
  readonly status: number;

  /** Код из расширения `code`. Для сетевых сбоев — `network.unreachable`. */
  readonly code: string;

  /** Ошибки по полям формы, если сервер вернул ValidationProblemDetails. */
  readonly fieldErrors: Record<string, string[]> | undefined;

  readonly traceId: string | undefined;

  constructor(params: {
    status: number;
    code: string;
    message: string;
    fieldErrors?: Record<string, string[]> | undefined;
    traceId?: string | undefined;
  }) {
    super(params.message);
    this.name = 'ApiError';
    this.status = params.status;
    this.code = params.code;
    this.fieldErrors = params.fieldErrors;
    this.traceId = params.traceId;
  }

  /** Ошибка валидации — есть смысл подсветить конкретные поля формы. */
  get isValidation(): boolean {
    return this.status === 400 && this.fieldErrors !== undefined;
  }

  /** Ошибка на стороне AI-сервиса, а не пользователя. */
  get isAiUnavailable(): boolean {
    return AI_ERROR_CODES.includes(this.code);
  }

  /** Данных для анализа не хватает — это не сбой, а пустое состояние. */
  get isNoData(): boolean {
    return NO_DATA_CODES.includes(this.code);
  }
}

/**
 * Коды, означающие недоступность AI-канала (ADR 68).
 *
 * Тип `readonly string[]`, а не литеральный union: список проверяется на
 * ВХОДЯЩИЙ код с сервера, то есть на произвольную строку. Литеральный union
 * заставлял бы приводить тип в каждой проверке — ровно там, где типизация и
 * должна работать.
 */
export const AI_ERROR_CODES: readonly string[] = [
  'ai.unavailable',
  'ai.unauthorized',
  'ai.model_unavailable',
  'ai.timeout',
  'study.quiz_unavailable',
];

/** Коды «нет данных для анализа» — показываем подсказку, а не красную ошибку. */
export const NO_DATA_CODES: readonly string[] = [
  'finance.no_data',
  'health.no_data',
  'study.no_text_layer',
  'career.no_resume',
];

/**
 * Человеческие формулировки для кодов, которые пользователь может встретить.
 *
 * Словарь неполный намеренно: для неизвестного кода берётся `detail` с сервера.
 * Дублировать здесь все сообщения бэкенда — значит поддерживать два перевода
 * одной ошибки и однажды разойтись с ним.
 */
export const ERROR_MESSAGES: Record<string, string> = {
  'network.unreachable':
    'Не удалось связаться с сервером. Проверь, что backend запущен и его сертификат доверенный.',
  'auth.unauthorized': 'Сессия истекла. Войди заново.',
  'auth.token_reuse_detected':
    'Обнаружено повторное использование токена — все сессии отозваны в целях безопасности. Войди заново.',
  'resource.not_found': 'Запись не найдена.',
  'resource.conflict': 'Такая запись уже существует.',
  'access.forbidden': 'Недостаточно прав для этого действия.',
  'validation.failed': 'Проверь заполнение полей.',
  'server.error': 'Внутренняя ошибка сервера. Мы записали её в лог.',

  'ai.unavailable': 'AI-сервис недоступен. Запусти ai-service и повтори попытку.',
  'ai.unauthorized':
    'Ключи backend и ai-service не совпадают: AiService:InternalApiKey ≠ INTERNAL_API_KEY.',
  'ai.model_unavailable': 'Модель не обучена. Выполни скрипты обучения в ai-service.',
  'ai.timeout': 'AI-сервис не ответил за отведённое время.',

  'finance.no_data': 'Недостаточно транзакций за период, чтобы построить прогноз.',
  'health.no_data': 'Недостаточно записей о здоровье за период для анализа.',
  'study.no_text_layer':
    'В PDF нет текстового слоя — вероятно, это скан. OCR в проекте не используется.',
  'study.quiz_unavailable':
    'Генерация тестов требует ключа LLM в ai-service. Без него сервис отказывается выдавать вопросы, а не придумывает их.',

  'file.type_not_allowed': 'Этот тип файла не разрешён для выбранного модуля.',
  'file.signature_mismatch':
    'Содержимое файла не совпадает с его расширением — загрузка отклонена.',
  'file.too_large': 'Файл превышает допустимый размер.',
  'file.empty': 'Файл пустой.',
  'file.in_use': 'Файл используется другим модулем, сначала удали связанную запись.',
};

/** Текст ошибки для показа пользователю. */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    return ERROR_MESSAGES[error.code] ?? error.message;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return 'Неизвестная ошибка.';
}
