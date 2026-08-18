import { api, httpClient } from '@/lib/httpClient';
import type {
  AvatarUploadResponse,
  ChangePasswordRequest,
  UpdateProfileRequest,
  User,
  Uuid,
} from '@/types/api';

/** Профиль пользователя: `UsersController`. */
export const userService = {
  getById(id: Uuid): Promise<User> {
    return api.get<User>(`/users/${id}`);
  },

  updateProfile(payload: UpdateProfileRequest): Promise<User> {
    return api.put<User>('/users/profile', payload);
  },

  /**
   * Смена пароля.
   *
   * После успеха бэкенд отзывает ВСЕ refresh-токены пользователя. Значит,
   * текущая сессия тоже мертва — вызывающий код обязан отправить пользователя
   * на страницу входа, иначе первый же запрос упадёт с 401.
   */
  changePassword(payload: ChangePasswordRequest): Promise<void> {
    return api.put<void>('/users/password', payload);
  },

  /**
   * Загрузка аватара: PUT /api/users/avatar, поле формы `file`.
   *
   * Имя поля должно быть ровно `file` — так называется параметр `IFormFile file`
   * в контроллере, по нему ASP.NET и связывает файл.
   */
  async uploadAvatar(file: File): Promise<AvatarUploadResponse> {
    const form = new FormData();
    form.append('file', file);
    const { data } = await httpClient.put<AvatarUploadResponse>('/users/avatar', form);
    return data;
  },
};
