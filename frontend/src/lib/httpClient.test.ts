import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createMockNetwork, isoFromNow, type MockNetwork } from '@/test/mockNetwork';

/**
 * Тесты сетевого слоя.
 *
 * Самый ценный набор во всём проекте: здесь проверяется механизм, поломка
 * которого не видна ни на одном экране, но выбрасывает пользователя из
 * приложения. Речь про single-flight обновление access-токена — бэкенд
 * ротирует refresh-токен при каждом обновлении и трактует повторное
 * использование как кражу (`auth.token_reuse_detected`, ADR 24), отзывая всю
 * цепочку токенов. Два параллельных `POST /auth/refresh` с одним токеном
 * означают мгновенный разлогин на ровном месте.
 */

const FIFTEEN_MINUTES_MS = 15 * 60_000;

/** Успешный ответ `/auth/refresh` со свежей парой токенов. */
function refreshSuccess(accessToken: string, refreshToken: string) {
  return {
    status: 200,
    data: {
      accessToken,
      refreshToken,
      accessTokenExpiresAt: isoFromNow(FIFTEEN_MINUTES_MS),
      user: { id: 'u1' },
    },
  };
}

/**
 * Загружает сетевой слой заново.
 *
 * `resetModules` обязателен: и `refreshInFlight` в httpClient, и access-токен в
 * tokenStore живут в памяти модуля. Без сброса первый тест оставил бы
 * незавершённое обновление или действующий токен следующему, и порядок
 * выполнения тестов начал бы влиять на результат.
 *
 * `ApiError` тоже импортируется динамически — после сброса это новый класс, и
 * `instanceof` от статически импортированного не сработал бы.
 */
async function loadHttpLayer(network: MockNetwork) {
  vi.resetModules();

  const { default: axios } = await import('axios');
  const { ApiError } = await import('@/types/errors');
  const { tokenStore } = await import('@/lib/tokenStore');
  const { api, httpClient, onSessionExpired } = await import('@/lib/httpClient');

  // Два адаптера, потому что обновление токена идёт мимо httpClient:
  // отдельный экземпляр axios нужен, чтобы отказ обновления не запустил
  // перехватчик, который снова полез бы обновляться.
  axios.defaults.adapter = network.adapter;
  httpClient.defaults.adapter = network.adapter;

  return { ApiError, api, httpClient, onSessionExpired, tokenStore };
}

/** Возвращает отклонение промиса как значение — `rejects` не даёт разобрать поля. */
async function captureError(action: Promise<unknown>): Promise<unknown> {
  try {
    await action;
  } catch (error: unknown) {
    return error;
  }
  throw new Error('Ожидалась ошибка, но запрос завершился успешно.');
}

beforeEach(() => {
  // Refresh-токен лежит в localStorage и переживает resetModules.
  window.localStorage.clear();
});

describe('httpClient: обновление токена в единственном экземпляре', () => {
  it('вызывает /auth/refresh ровно один раз при пяти параллельных запросах', async () => {
    const network = createMockNetwork();
    network.on('/auth/refresh', async () => {
      // Задержка принципиальна. Без неё обновление успевало бы завершиться
      // до того, как остальные запросы дойдут до перехватчика, и тест прошёл
      // бы даже на сломанной реализации.
      await new Promise((resolve) => setTimeout(resolve, 20));
      return refreshSuccess('access-2', 'refresh-2');
    });
    network.on('/goals', (request) => ({
      status: 200,
      data: { sentWith: request.authorization },
    }));

    const { api, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(-1000), // токен уже истёк
    });

    const results = await Promise.all(
      Array.from({ length: 5 }, () => api.get<{ sentWith: string }>('/goals')),
    );

    expect(network.callsTo('/auth/refresh')).toHaveLength(1);
    expect(network.callsTo('/goals')).toHaveLength(5);
    // Все пять запросов ушли уже с новым токеном.
    expect(results.map((result) => result.sentWith)).toEqual(
      Array.from({ length: 5 }, () => 'Bearer access-2'),
    );
    expect(tokenStore.getRefreshToken()).toBe('refresh-2');
  });

  it('отправляет на обновление текущий refresh-токен из хранилища', async () => {
    const network = createMockNetwork();
    network.on('/auth/refresh', () => refreshSuccess('access-2', 'refresh-2'));
    network.on('/goals', () => ({ status: 200, data: [] }));

    const { api, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(-1000),
    });

    await api.get('/goals');

    const [refreshCall] = network.callsTo('/auth/refresh');
    expect(refreshCall).toBeDefined();
    expect(JSON.parse(String(refreshCall?.body))).toEqual({ refreshToken: 'refresh-1' });
  });

  it('обновляется заново после завершения предыдущего обновления', async () => {
    const network = createMockNetwork();
    let refreshCount = 0;
    network.on('/auth/refresh', () => {
      refreshCount += 1;
      return refreshSuccess(`access-${String(refreshCount + 1)}`, `refresh-${String(refreshCount + 1)}`);
    });
    network.on('/goals', () => ({ status: 200, data: [] }));

    const { api, tokenStore } = await loadHttpLayer(network);

    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(-1000),
    });
    await api.get('/goals');

    // Второй раз токен снова протух — промис обновления должен быть сброшен,
    // иначе приложение навсегда осталось бы с первым результатом.
    tokenStore.set({
      accessToken: 'access-2',
      refreshToken: 'refresh-2',
      accessTokenExpiresAt: isoFromNow(-1000),
    });
    await api.get('/goals');

    expect(network.callsTo('/auth/refresh')).toHaveLength(2);
  });
});

describe('httpClient: разбор 401', () => {
  it('обновляет токен и повторяет запрос при 401 с заголовком X-Token-Expired: true', async () => {
    const network = createMockNetwork();
    network.on('/auth/refresh', () => refreshSuccess('access-2', 'refresh-2'));
    network.on('/goals', (request, callIndex) =>
      callIndex === 0
        ? {
            status: 401,
            headers: { 'X-Token-Expired': 'true' },
            data: { code: 'auth.unauthorized', detail: 'Токен истёк.' },
          }
        : { status: 200, data: { sentWith: request.authorization } },
    );

    const { api, tokenStore } = await loadHttpLayer(network);
    // Токен считается свежим — упреждающее обновление не сработает,
    // проверяется именно реакция на ответ сервера.
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(FIFTEEN_MINUTES_MS),
    });

    const result = await api.get<{ sentWith: string }>('/goals');

    const goalsCalls = network.callsTo('/goals');
    expect(goalsCalls).toHaveLength(2);
    expect(goalsCalls[0]?.authorization).toBe('Bearer access-1');
    expect(goalsCalls[1]?.authorization).toBe('Bearer access-2');
    expect(result.sentWith).toBe('Bearer access-2');
    expect(network.callsTo('/auth/refresh')).toHaveLength(1);
  });

  it('разлогинивает без попытки обновления при 401 с X-Token-Expired: false', async () => {
    const network = createMockNetwork();
    network.on('/auth/refresh', () => refreshSuccess('access-2', 'refresh-2'));
    network.on('/goals', () => ({
      status: 401,
      headers: { 'X-Token-Expired': 'false' },
      data: { code: 'auth.unauthorized', detail: 'Токен неверен.' },
    }));

    const { ApiError, api, onSessionExpired, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'broken-token',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(FIFTEEN_MINUTES_MS),
    });

    const reasons: string[] = [];
    const unsubscribe = onSessionExpired((reason) => reasons.push(reason));
    const error = await captureError(api.get('/goals'));
    unsubscribe();

    expect(error).toBeInstanceOf(ApiError);
    expect((error as InstanceType<typeof ApiError>).status).toBe(401);
    // Обновление бессмысленно: сервер сказал, что токен не истёк, а неверен.
    expect(network.callsTo('/auth/refresh')).toHaveLength(0);
    expect(reasons).toEqual(['invalid_token']);
    expect(tokenStore.getRefreshToken()).toBeNull();
    expect(tokenStore.getAccessToken()).toBeNull();
  });

  it('пробует обновление, если заголовок X-Token-Expired не виден браузеру', async () => {
    // Заголовок мог не попасть в Access-Control-Expose-Headers. Тогда отличить
    // «истёк» от «неверен» нельзя, и попытка обновления допускается (ADR 92):
    // она безопасна и упирается в проверку refresh-токена на сервере.
    const network = createMockNetwork();
    network.on('/auth/refresh', () => refreshSuccess('access-2', 'refresh-2'));
    network.on('/goals', (request, callIndex) =>
      callIndex === 0
        ? { status: 401, data: { code: 'auth.unauthorized' } }
        : { status: 200, data: { sentWith: request.authorization } },
    );

    const { api, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(FIFTEEN_MINUTES_MS),
    });

    const result = await api.get<{ sentWith: string }>('/goals');

    expect(network.callsTo('/auth/refresh')).toHaveLength(1);
    expect(result.sentWith).toBe('Bearer access-2');
  });

  it('разлогинивает при 401, если refresh-токена нет вовсе', async () => {
    const network = createMockNetwork();
    network.on('/goals', () => ({
      status: 401,
      headers: { 'X-Token-Expired': 'true' },
      data: { code: 'auth.unauthorized' },
    }));

    const { api, onSessionExpired } = await loadHttpLayer(network);

    const reasons: string[] = [];
    const unsubscribe = onSessionExpired((reason) => reasons.push(reason));
    await captureError(api.get('/goals'));
    unsubscribe();

    expect(reasons).toEqual(['no_refresh_token']);
    expect(network.callsTo('/auth/refresh')).toHaveLength(0);
  });

  it('не пытается обновлять токен и не разлогинивает при 401 на /auth/login', async () => {
    const network = createMockNetwork();
    network.on('/auth/login', () => ({
      status: 401,
      data: { code: 'auth.invalid_credentials', detail: 'Неверный email или пароль.' },
    }));

    const { ApiError, api, onSessionExpired, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(FIFTEEN_MINUTES_MS),
    });

    const reasons: string[] = [];
    const unsubscribe = onSessionExpired((reason) => reasons.push(reason));
    const error = await captureError(api.post('/auth/login', { email: 'a@b.c', password: 'x' }));
    unsubscribe();

    expect((error as InstanceType<typeof ApiError>).code).toBe('auth.invalid_credentials');
    expect(reasons).toEqual([]);
    // Ошибка входа не должна стирать уже существующую сессию другого окна.
    expect(tokenStore.getRefreshToken()).toBe('refresh-1');
    expect(network.callsTo('/auth/login')[0]?.authorization).toBeUndefined();
  });
});

describe('httpClient: неудачное обновление токена', () => {
  it('разлогинивает, если сервер отклонил refresh-токен', async () => {
    const network = createMockNetwork();
    network.on('/auth/refresh', () => ({
      status: 401,
      data: { code: 'auth.token_reuse_detected', detail: 'Токен уже использован.' },
    }));
    network.on('/goals', () => ({
      status: 401,
      headers: { 'X-Token-Expired': 'true' },
      data: { code: 'auth.unauthorized', detail: 'Токен истёк.' },
    }));

    const { ApiError, api, onSessionExpired, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(FIFTEEN_MINUTES_MS),
    });

    const reasons: string[] = [];
    const unsubscribe = onSessionExpired((reason) => reasons.push(reason));
    const error = await captureError(api.get('/goals'));
    unsubscribe();

    expect(reasons).toEqual(['refresh_failed']);
    expect(tokenStore.getRefreshToken()).toBeNull();
    expect(error).toBeInstanceOf(ApiError);
    // Запрос повторён не был — второго обращения к /goals нет.
    expect(network.callsTo('/goals')).toHaveLength(1);
  });

  it('различает отказ сервера (400) от прочих сбоёв обновления', async () => {
    const network = createMockNetwork();
    network.on('/auth/refresh', () => ({
      status: 400,
      data: { code: 'auth.invalid_refresh_token' },
    }));
    network.on('/goals', () => ({ status: 200, data: [] }));

    const { api, onSessionExpired, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(-1000),
    });

    const reasons: string[] = [];
    const unsubscribe = onSessionExpired((reason) => reasons.push(reason));
    // Упреждающее обновление провалилось, но запрос всё равно уходит: если
    // сервер вдруг его примет, пользователь не пострадает от нашей ошибки.
    await api.get('/goals');
    unsubscribe();

    expect(reasons).toEqual(['refresh_rejected']);
    expect(tokenStore.getRefreshToken()).toBeNull();
    expect(network.callsTo('/goals')).toHaveLength(1);
  });
});

describe('httpClient: приведение ошибок к ApiError', () => {
  it('переносит code, detail, errors и traceId из ProblemDetails', async () => {
    const network = createMockNetwork();
    network.on('/finance/transactions', () => ({
      status: 400,
      data: {
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        detail: 'Проверь заполнение полей.',
        code: 'validation.failed',
        traceId: '00-abc-def-01',
        errors: {
          Amount: ['Сумма должна быть больше нуля.'],
          Currency: ['Код валюты по ISO 4217.'],
        },
      },
    }));

    const { ApiError, api } = await loadHttpLayer(network);
    const error = (await captureError(
      api.post('/finance/transactions', {}),
    )) as InstanceType<typeof ApiError>;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.code).toBe('validation.failed');
    // detail важнее title: он описывает конкретный случай, а не класс ошибок.
    expect(error.message).toBe('Проверь заполнение полей.');
    expect(error.traceId).toBe('00-abc-def-01');
    expect(error.fieldErrors).toEqual({
      Amount: ['Сумма должна быть больше нуля.'],
      Currency: ['Код валюты по ISO 4217.'],
    });
    expect(error.isValidation).toBe(true);
  });

  it('подставляет код http.<status>, если сервер не прислал code', async () => {
    const network = createMockNetwork();
    network.on('/goals/missing', () => ({ status: 404, data: { title: 'Not Found' } }));

    const { ApiError, api } = await loadHttpLayer(network);
    const error = (await captureError(
      api.get('/goals/missing'),
    )) as InstanceType<typeof ApiError>;

    expect(error.code).toBe('http.404');
    expect(error.message).toBe('Not Found');
    expect(error.isValidation).toBe(false);
  });

  it('описывает ответ без тела статусом, а не пустой строкой', async () => {
    const network = createMockNetwork();
    network.on('/goals', () => ({ status: 500, data: '' }));

    const { ApiError, api } = await loadHttpLayer(network);
    const error = (await captureError(api.get('/goals'))) as InstanceType<typeof ApiError>;

    expect(error.code).toBe('http.500');
    expect(error.message).toBe('Запрос завершился со статусом 500.');
  });

  it('превращает отсутствие ответа в network.unreachable со статусом 0', async () => {
    const network = createMockNetwork();
    network.on('/goals', () => ({ status: 0 }));

    const { ApiError, api } = await loadHttpLayer(network);
    const error = (await captureError(api.get('/goals'))) as InstanceType<typeof ApiError>;

    expect(error.status).toBe(0);
    expect(error.code).toBe('network.unreachable');
  });

  it('распознаёт коды недоступности AI-канала', async () => {
    const network = createMockNetwork();
    network.on('/ai/recommendations', () => ({
      status: 503,
      data: { code: 'ai.unavailable', detail: 'AI-сервис недоступен.' },
    }));

    const { ApiError, api } = await loadHttpLayer(network);
    const error = (await captureError(
      api.get('/ai/recommendations'),
    )) as InstanceType<typeof ApiError>;

    expect(error.isAiUnavailable).toBe(true);
    expect(error.isNoData).toBe(false);
  });
});

describe('httpClient: подготовка запроса', () => {
  it('снимает Content-Type с FormData, чтобы тело не превратилось в JSON', async () => {
    const network = createMockNetwork();
    network.on('/files', () => ({ status: 200, data: { id: 'f1' } }));

    const { httpClient, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(FIFTEEN_MINUTES_MS),
    });

    const form = new FormData();
    form.append('file', new Blob(['текст'], { type: 'text/plain' }), 'note.txt');
    // Content-Type задан явно — это худший случай: увидев application/json,
    // axios сериализовал бы FormData в JSON и загрузка файла молча сломалась бы.
    await httpClient.post('/files', form, { headers: { 'Content-Type': 'application/json' } });

    const [call] = network.callsTo('/files');
    expect(call?.headers.get('Content-Type')).not.toBe('application/json');
    expect(call?.body).toBeInstanceOf(FormData);
    expect(call?.authorization).toBe('Bearer access-1');
  });

  it('не добавляет Authorization на анонимные пути', async () => {
    const network = createMockNetwork();
    network.on('/ping', () => ({ status: 200, data: { status: 'ok' } }));

    const { api, tokenStore } = await loadHttpLayer(network);
    tokenStore.set({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: isoFromNow(FIFTEEN_MINUTES_MS),
    });

    await api.get('/ping');

    expect(network.callsTo('/ping')[0]?.authorization).toBeUndefined();
  });
});
