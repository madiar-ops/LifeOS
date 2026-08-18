import { zodResolver } from '@hookform/resolvers/zod';
import { AtSign, Lock } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { Link, useLocation, useNavigate } from 'react-router-dom';

import { Button, Field, Input } from '@/components/ui';
import { useAuth } from '@/hooks/useAuth';
import { applyServerErrors } from '@/lib/formErrors';
import { ROUTES } from '@/router/routes';
import { loginSchema, type LoginFormValues } from '@/schemas/auth';
import { describeError } from '@/types/errors';

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await login(values);
      // Возврат туда, куда пользователь шёл до перехвата ProtectedRoute.
      const from = (location.state as { from?: string } | null)?.from;
      navigate(from ?? ROUTES.dashboard, { replace: true });
    } catch (error) {
      /*
       * Бэкенд отвечает ОДИНАКОВОЙ ошибкой на неверный email и неверный пароль
       * (ADR 26) — это защита от перебора существующих адресов. Интерфейс
       * обязан сохранить это свойство: сообщение вешается на форму целиком,
       * а не на конкретное поле. Подсветка поля «email» выдала бы, что такой
       * пользователь существует.
       */
      const matched = applyServerErrors<LoginFormValues>(error, setError, ['email', 'password']);
      if (!matched) {
        setError('root', { message: describeError(error) });
      }
    }
  });

  return (
    <div>
      <header className="mb-7">
        <h1 className="text-xl font-semibold tracking-tight">Вход в LifeOS</h1>
        <p className="mt-1 text-[13.5px] text-fg-muted">
          Продолжи работу с целями, финансами и учёбой.
        </p>
      </header>

      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <Field label="Email" error={errors.email?.message} required>
          {(field) => (
            <Input
              {...field}
              {...register('email')}
              type="email"
              autoComplete="email"
              placeholder="you@example.com"
              icon={<AtSign size={15} />}
            />
          )}
        </Field>

        <Field label="Пароль" error={errors.password?.message} required>
          {(field) => (
            <Input
              {...field}
              {...register('password')}
              type="password"
              autoComplete="current-password"
              placeholder="••••••••"
              icon={<Lock size={15} />}
            />
          )}
        </Field>

        {errors.root?.message !== undefined && (
          <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2.5 text-[12.5px] text-danger">
            {errors.root.message}
          </p>
        )}

        <Button type="submit" variant="primary" size="lg" loading={isSubmitting} className="w-full">
          Войти
        </Button>
      </form>

      <p className="mt-6 text-center text-[13px] text-fg-muted">
        Нет аккаунта?{' '}
        <Link to={ROUTES.register} className="font-medium text-accent hover:underline">
          Зарегистрироваться
        </Link>
      </p>
    </div>
  );
}
