import { zodResolver } from '@hookform/resolvers/zod';
import { AtSign, Lock } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router-dom';

import { Button, Field, Input } from '@/components/ui';
import { useAuth } from '@/hooks/useAuth';
import { applyServerErrors } from '@/lib/formErrors';
import { ROUTES } from '@/router/routes';
import { registerSchema, type RegisterFormValues } from '@/schemas/auth';
import { describeError } from '@/types/errors';

/** Требования к паролю показаны СРАЗУ, а не после первой неудачной попытки. */
const PASSWORD_RULES = 'Минимум 8 символов, заглавная и строчная буквы, цифра.';

export default function RegisterPage() {
  const { register: signUp } = useAuth();
  const navigate = useNavigate();

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { name: '', surname: '', email: '', password: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await signUp(values);
      navigate(ROUTES.dashboard, { replace: true });
    } catch (error) {
      // Занятый email приходит как 409 без словаря полей — привязываем его к
      // полю email вручную, потому что исправлять пользователю нужно именно его.
      const matched = applyServerErrors<RegisterFormValues>(error, setError, [
        'name',
        'surname',
        'email',
        'password',
      ]);
      if (!matched) {
        setError('root', { message: describeError(error) });
      }
    }
  });

  return (
    <div>
      <header className="mb-7">
        <h1 className="text-xl font-semibold tracking-tight">Создать аккаунт</h1>
        <p className="mt-1 text-[13.5px] text-fg-muted">
          Личное пространство LifeOS — бесплатно и за минуту.
        </p>
      </header>

      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Имя" error={errors.name?.message} required>
            {(field) => (
              <Input {...field} {...register('name')} autoComplete="given-name" placeholder="Мадияр" />
            )}
          </Field>
          <Field label="Фамилия" error={errors.surname?.message} required>
            {(field) => (
              <Input
                {...field}
                {...register('surname')}
                autoComplete="family-name"
                placeholder="Абубек"
              />
            )}
          </Field>
        </div>

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

        <Field
          label="Пароль"
          error={errors.password?.message}
          hint={PASSWORD_RULES}
          required
        >
          {(field) => (
            <Input
              {...field}
              {...register('password')}
              type="password"
              autoComplete="new-password"
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
          Зарегистрироваться
        </Button>
      </form>

      <p className="mt-6 text-center text-[13px] text-fg-muted">
        Уже есть аккаунт?{' '}
        <Link to={ROUTES.login} className="font-medium text-accent hover:underline">
          Войти
        </Link>
      </p>
    </div>
  );
}
