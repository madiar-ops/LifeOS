import { act, renderHook } from '@testing-library/react';
import { useForm, type UseFormSetError } from 'react-hook-form';
import { describe, expect, it, vi } from 'vitest';

import { ApiError } from '@/types/errors';

import { applyServerErrors } from './formErrors';

/**
 * Тесты переноса серверных ошибок валидации в форму.
 *
 * Проверяется граница двух миров именования: FluentValidation берёт имена полей
 * из свойств C# (PascalCase), а поля react-hook-form названы по-JavaScript
 * (camelCase). Ошибка в этом переводе не ломает приложение — она просто ничего
 * не подсвечивает, и пользователь видит форму без объяснения, что исправлять.
 * Именно поэтому такое стоит проверять тестом, а не глазами.
 */

interface TestFormValues {
  email: string;
  newPassword: string;
  date: string;
  goalId: string;
}

const KNOWN_FIELDS = ['email', 'newPassword', 'date', 'goalId'] as const;

function validationError(fieldErrors: Record<string, string[]>): ApiError {
  return new ApiError({
    status: 400,
    code: 'validation.failed',
    message: 'Проверь заполнение полей.',
    fieldErrors,
  });
}

describe('applyServerErrors', () => {
  it('переводит имя поля из PascalCase в camelCase', () => {
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    const matched = applyServerErrors<TestFormValues>(
      validationError({ Email: ['Такой email уже зарегистрирован.'] }),
      setError,
      KNOWN_FIELDS,
    );

    expect(matched).toBe(true);
    expect(setError).toHaveBeenCalledWith('email', {
      type: 'server',
      message: 'Такой email уже зарегистрирован.',
    });
  });

  it('переводит составное имя: NewPassword → newPassword', () => {
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    applyServerErrors<TestFormValues>(
      validationError({ NewPassword: ['Новый пароль должен отличаться от текущего.'] }),
      setError,
      KNOWN_FIELDS,
    );

    expect(setError).toHaveBeenCalledWith('newPassword', expect.objectContaining({
      message: 'Новый пароль должен отличаться от текущего.',
    }));
  });

  it('отбрасывает префикс ключа привязки модели «$.»', () => {
    // Такие ключи приходят не от FluentValidation, а от разбора JSON в ASP.NET:
    // «$.date» означает свойство date в корне тела запроса.
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    const matched = applyServerErrors<TestFormValues>(
      validationError({ '$.date': ['Дата в неверном формате.'] }),
      setError,
      KNOWN_FIELDS,
    );

    expect(matched).toBe(true);
    expect(setError).toHaveBeenCalledWith('date', expect.objectContaining({
      message: 'Дата в неверном формате.',
    }));
  });

  it('берёт последний сегмент вложенного пути: Request.GoalId → goalId', () => {
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    applyServerErrors<TestFormValues>(
      validationError({ 'Request.GoalId': ['Указанная цель не найдена.'] }),
      setError,
      KNOWN_FIELDS,
    );

    expect(setError).toHaveBeenCalledWith('goalId', expect.objectContaining({
      message: 'Указанная цель не найдена.',
    }));
  });

  it('показывает только первое сообщение по полю', () => {
    // Остальные сообщения обычно уточняют то же правило; выводить их стопкой
    // под одним полем — шум, из-за которого не видно главного.
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    applyServerErrors<TestFormValues>(
      validationError({ Email: ['Email обязателен.', 'Email слишком длинный.'] }),
      setError,
      KNOWN_FIELDS,
    );

    expect(setError).toHaveBeenCalledTimes(1);
    expect(setError).toHaveBeenCalledWith('email', expect.objectContaining({
      message: 'Email обязателен.',
    }));
  });

  it('привязывает сразу несколько полей и сообщает об успехе', () => {
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    const matched = applyServerErrors<TestFormValues>(
      validationError({
        Email: ['Некорректный email.'],
        NewPassword: ['Пароль слишком простой.'],
      }),
      setError,
      KNOWN_FIELDS,
    );

    expect(matched).toBe(true);
    expect(setError).toHaveBeenCalledTimes(2);
  });

  it('возвращает false для неизвестного поля, чтобы форма показала общее сообщение', () => {
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    const matched = applyServerErrors<TestFormValues>(
      validationError({ CurrentPassword: ['Текущий пароль неверен.'] }),
      setError,
      KNOWN_FIELDS,
    );

    expect(matched).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });

  it('игнорирует поле с пустым списком сообщений', () => {
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    const matched = applyServerErrors<TestFormValues>(validationError({ Email: [] }), setError, KNOWN_FIELDS);

    expect(matched).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });

  it('не реагирует на ApiError без разбивки по полям', () => {
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    const matched = applyServerErrors<TestFormValues>(
      new ApiError({ status: 409, code: 'resource.conflict', message: 'Уже существует.' }),
      setError,
      KNOWN_FIELDS,
    );

    expect(matched).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });

  it('не реагирует на ошибку, не относящуюся к API', () => {
    const setError = vi.fn<UseFormSetError<TestFormValues>>();

    const matched = applyServerErrors<TestFormValues>(
      new TypeError('Сбой в компоненте'),
      setError,
      KNOWN_FIELDS,
    );

    expect(matched).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });

  it('доводит сообщение сервера до состояния настоящей формы', () => {
    // Проверка с настоящим useForm, а не с заглушкой: она подтверждает, что
    // имя поля действительно принимается react-hook-form и ошибка окажется
    // под нужным полем, а не потеряется в объекте errors.
    const { result } = renderHook(() => {
      const form = useForm<TestFormValues>({
        defaultValues: { email: '', newPassword: '', date: '', goalId: '' },
      });
      // Чтение errors внутри рендера обязательно: formState в react-hook-form —
      // прокси, и без обращения подписки на обновление не возникает.
      return { setError: form.setError, errors: form.formState.errors };
    });

    act(() => {
      applyServerErrors<TestFormValues>(
        validationError({ Email: ['Такой email уже зарегистрирован.'] }),
        result.current.setError,
        KNOWN_FIELDS,
      );
    });

    expect(result.current.errors.email?.message).toBe('Такой email уже зарегистрирован.');
    expect(result.current.errors.email?.type).toBe('server');
    expect(result.current.errors.newPassword).toBeUndefined();
  });
});
