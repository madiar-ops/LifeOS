import {
  AxiosError,
  AxiosHeaders,
  type AxiosAdapter,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from 'axios';

/**
 * Сеть-заглушка на уровне адаптера axios.
 *
 * ПОЧЕМУ адаптер, а не `vi.mock('axios')`. Проверяется поведение самих
 * перехватчиков httpClient: подстановка Authorization, разбор ProblemDetails,
 * повтор запроса. Подменив модуль axios целиком, мы выбросили бы ровно тот код,
 * который тестируем, и проверяли бы собственную заглушку. Адаптер — последнее
 * звено конвейера axios: перехватчики, слияние конфигов, преобразование тела и
 * нормализация заголовков остаются настоящими, подменяется только выход в сеть.
 */

export interface RecordedRequest {
  /** Путь без baseURL — ровно то, что видят перехватчики. */
  url: string;
  method: string;
  authorization: string | undefined;
  /** Тело уже после transformRequest, то есть для JSON — строка. */
  body: unknown;
  headers: AxiosHeaders;
}

export interface MockReply {
  /** 0 означает «ответа не было вовсе»: обрыв сети, CORS, недоверенный сертификат. */
  status: number;
  data?: unknown;
  headers?: Record<string, string>;
}

type MockHandler = (request: RecordedRequest, callIndex: number) => MockReply | Promise<MockReply>;

export interface MockNetwork {
  adapter: AxiosAdapter;
  /** Маршрут задаётся окончанием пути: так один обработчик ловит и `/auth/refresh`, и `/api/auth/refresh`. */
  on: (pathSuffix: string, handler: MockHandler) => void;
  requests: RecordedRequest[];
  callsTo: (pathSuffix: string) => RecordedRequest[];
}

export function createMockNetwork(): MockNetwork {
  const routes = new Map<string, MockHandler>();
  const requests: RecordedRequest[] = [];

  const callsTo = (pathSuffix: string): RecordedRequest[] =>
    requests.filter((request) => request.url.endsWith(pathSuffix));

  const adapter: AxiosAdapter = async (config: InternalAxiosRequestConfig) => {
    const url = config.url ?? '';
    const headers = AxiosHeaders.from(config.headers);
    const rawAuthorization = headers.get('Authorization');

    const record: RecordedRequest = {
      url,
      method: (config.method ?? 'get').toLowerCase(),
      authorization: typeof rawAuthorization === 'string' ? rawAuthorization : undefined,
      body: config.data,
      headers,
    };
    requests.push(record);

    const routeKey = [...routes.keys()].find((key) => url.endsWith(key));
    const handler = routeKey === undefined ? undefined : routes.get(routeKey);
    if (routeKey === undefined || handler === undefined) {
      // Падаем громко: незаявленный запрос — это почти всегда ошибка в тесте,
      // а «тихий» ответ-заглушка спрятал бы её за непонятным падением ниже.
      throw new Error(`Нет заглушки для ${record.method.toUpperCase()} ${url}`);
    }

    // Порядковый номер обращения к маршруту — им обработчик различает
    // «первый раз ответь 401, второй раз 200».
    const callIndex = callsTo(routeKey).length - 1;
    const reply = await handler(record, callIndex);

    if (reply.status === 0) {
      throw new AxiosError('Network Error', AxiosError.ERR_NETWORK, config, {});
    }

    const response: AxiosResponse = {
      status: reply.status,
      statusText: String(reply.status),
      data: reply.data,
      // Имена заголовков приводятся к нижнему регистру, потому что так их отдаёт
      // настоящий axios: `parseHeaders` нормализует ключи ответа, и код
      // приложения читает `headers['x-token-expired']`. Сохранив исходный
      // регистр, заглушка вела бы себя иначе, чем браузер.
      headers: AxiosHeaders.from(
        Object.fromEntries(
          Object.entries(reply.headers ?? {}).map(([name, value]) => [name.toLowerCase(), value]),
        ),
      ),
      config,
      request: {},
    };

    if (reply.status >= 200 && reply.status < 300) return response;

    throw new AxiosError(
      `Request failed with status code ${String(reply.status)}`,
      AxiosError.ERR_BAD_REQUEST,
      config,
      {},
      response,
    );
  };

  return {
    adapter,
    on: (pathSuffix, handler) => routes.set(pathSuffix, handler),
    requests,
    callsTo,
  };
}

/** Момент в будущем/прошлом в формате `accessTokenExpiresAt` бэкенда. */
export function isoFromNow(offsetMs: number): string {
  return new Date(Date.now() + offsetMs).toISOString();
}
