import { zodResolver } from '@hookform/resolvers/zod';
import { Camera, KeyRound, UserRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';

import { PageShell } from '@/components/layout/PageShell';
import {
  Avatar,
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  Field,
  Input,
} from '@/components/ui';
import { useAuth } from '@/hooks/useAuth';
import { applyServerErrors } from '@/lib/formErrors';
import { formatDate, formatFileSize } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { userService } from '@/services/userService';
import {
  changePasswordSchema,
  profileSchema,
  type ChangePasswordFormValues,
  type ProfileFormValues,
} from '@/schemas/profile';
import { describeError } from '@/types/errors';

/** Ограничения из FileValidationRules: аватар — изображение до 2 МБ. */
const AVATAR_ACCEPT = ['image/jpeg', 'image/png', 'image/webp'];
const AVATAR_MAX_BYTES = 2 * 1024 * 1024;

export default function ProfilePage() {
  const { user, logout, refreshUser } = useAuth();

  const [avatarError, setAvatarError] = useState<string | null>(null);
  const [avatarUploading, setAvatarUploading] = useState(false);

  const profileForm = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { name: '', surname: '' },
  });

  const passwordForm = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  useEffect(() => {
    if (user === null) return;
    profileForm.reset({ name: user.name, surname: user.surname });
  }, [user, profileForm]);

  const saveProfile = profileForm.handleSubmit(async (values) => {
    try {
      await userService.updateProfile({ name: values.name.trim(), surname: values.surname.trim() });
      await refreshUser();
      toast.success('Профиль обновлён');
    } catch (error) {
      applyServerErrors<ProfileFormValues>(error, profileForm.setError, ['name', 'surname']);
    }
  });

  /**
   * Смена пароля.
   *
   * После успеха бэкенд отзывает ВСЕ refresh-токены пользователя. Значит,
   * текущая сессия уже недействительна, и оставаться в приложении нельзя —
   * первый же запрос упал бы с 401. Поэтому сразу выполняется выход: это не
   * грубость интерфейса, а следствие того, как работает отзыв токенов.
   */
  const changePassword = passwordForm.handleSubmit(async (values) => {
    try {
      await userService.changePassword({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
      });
      toast.success('Пароль изменён', 'Все сессии отозваны — войди заново с новым паролем.');
      passwordForm.reset();
      await logout();
    } catch (error) {
      const matched = applyServerErrors<ChangePasswordFormValues>(error, passwordForm.setError, [
        'currentPassword',
        'newPassword',
      ]);
      if (!matched) {
        passwordForm.setError('currentPassword', {
          type: 'server',
          message: describeError(error),
        });
      }
    }
  });

  const uploadAvatar = async (file: File) => {
    setAvatarError(null);
    if (!AVATAR_ACCEPT.includes(file.type)) {
      setAvatarError('Допустимы JPEG, PNG и WebP.');
      return;
    }
    if (file.size > AVATAR_MAX_BYTES) {
      setAvatarError(`Файл больше ${formatFileSize(AVATAR_MAX_BYTES)}.`);
      return;
    }

    setAvatarUploading(true);
    try {
      await userService.uploadAvatar(file);
      // Профиль перечитывается: URL нового аватара приходит в UserResponse.
      await refreshUser();
      toast.success('Аватар обновлён', 'Предыдущий файл удалён из хранилища.');
    } catch (error) {
      setAvatarError(describeError(error));
    } finally {
      setAvatarUploading(false);
    }
  };

  if (user === null) return null;

  return (
    <PageShell title="Профиль" description="Личные данные, аватар и пароль">
      {/* ---- Карточка пользователя --------------------------------------- */}
      <Card>
        <CardBody className="flex flex-wrap items-center gap-5">
          <div className="relative">
            <Avatar name={user.name} surname={user.surname} url={user.avatarUrl} size="lg" />
            <label
              htmlFor="avatar-file"
              className="absolute -right-1 -bottom-1 flex size-8 cursor-pointer items-center justify-center rounded-full border border-line bg-surface text-fg-muted shadow-card transition-colors hover:text-accent"
              title="Изменить аватар"
            >
              <Camera size={15} />
              <span className="sr-only">Изменить аватар</span>
            </label>
            <input
              id="avatar-file"
              type="file"
              accept={AVATAR_ACCEPT.join(',')}
              className="sr-only"
              disabled={avatarUploading}
              onChange={(event) => {
                const selected = event.target.files?.[0];
                if (selected !== undefined) void uploadAvatar(selected);
              }}
            />
          </div>

          <div className="min-w-0 flex-1">
            <p className="text-lg font-semibold tracking-tight">
              {user.name} {user.surname}
            </p>
            <p className="text-[13px] text-fg-muted">{user.email}</p>
            <div className="mt-2 flex flex-wrap items-center gap-1.5">
              <Badge tone={user.role === 'Admin' ? 'accent' : 'neutral'}>
                {user.role === 'Admin' ? 'Администратор' : 'Пользователь'}
              </Badge>
              <Badge tone="neutral">с {formatDate(user.createdAt)}</Badge>
              {avatarUploading && <Badge tone="info">загрузка аватара…</Badge>}
            </div>
            {avatarError !== null && (
              <p role="alert" className="mt-2 text-[12px] text-danger">
                {avatarError}
              </p>
            )}
          </div>
        </CardBody>
      </Card>

      <div className="grid gap-5 lg:grid-cols-2">
        {/* ---- Данные ---------------------------------------------------- */}
        <Card>
          <CardHeader icon={<UserRound size={15} />} title="Личные данные" />
          <CardBody>
            <form
              onSubmit={(event) => {
                event.preventDefault();
                void saveProfile();
              }}
              noValidate
              className="space-y-4"
            >
              <Field label="Имя" error={profileForm.formState.errors.name?.message} required>
                {(field) => <Input {...field} {...profileForm.register('name')} />}
              </Field>
              <Field label="Фамилия" error={profileForm.formState.errors.surname?.message} required>
                {(field) => <Input {...field} {...profileForm.register('surname')} />}
              </Field>

              {/*
                Email и роль не редактируются. Email — часть уникального
                индекса и идентификатор входа; роль назначается администратором.
                Показать их отключёнными полями честнее, чем скрыть: пользователь
                видит, что данные есть, и понимает, что менять их здесь нельзя.
              */}
              <Field label="Email" hint="Не редактируется: используется для входа.">
                {(field) => <Input {...field} value={user.email} disabled readOnly />}
              </Field>

              <Button
                type="submit"
                variant="primary"
                loading={profileForm.formState.isSubmitting}
                disabled={!profileForm.formState.isDirty}
              >
                Сохранить
              </Button>
            </form>
          </CardBody>
        </Card>

        {/* ---- Пароль ---------------------------------------------------- */}
        <Card>
          <CardHeader
            icon={<KeyRound size={15} />}
            title="Смена пароля"
            description="После смены все сессии отзываются — потребуется войти заново"
          />
          <CardBody>
            <form
              onSubmit={(event) => {
                event.preventDefault();
                void changePassword();
              }}
              noValidate
              className="space-y-4"
            >
              <Field
                label="Текущий пароль"
                error={passwordForm.formState.errors.currentPassword?.message}
                required
              >
                {(field) => (
                  <Input
                    {...field}
                    {...passwordForm.register('currentPassword')}
                    type="password"
                    autoComplete="current-password"
                  />
                )}
              </Field>

              <Field
                label="Новый пароль"
                error={passwordForm.formState.errors.newPassword?.message}
                hint="Минимум 8 символов, заглавная и строчная буквы, цифра."
                required
              >
                {(field) => (
                  <Input
                    {...field}
                    {...passwordForm.register('newPassword')}
                    type="password"
                    autoComplete="new-password"
                  />
                )}
              </Field>

              <Field
                label="Повтори новый пароль"
                error={passwordForm.formState.errors.confirmPassword?.message}
                required
              >
                {(field) => (
                  <Input
                    {...field}
                    {...passwordForm.register('confirmPassword')}
                    type="password"
                    autoComplete="new-password"
                  />
                )}
              </Field>

              <Button type="submit" variant="primary" loading={passwordForm.formState.isSubmitting}>
                Изменить пароль
              </Button>
            </form>
          </CardBody>
        </Card>
      </div>
    </PageShell>
  );
}
