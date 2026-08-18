import { api, httpClient } from '@/lib/httpClient';
import type { ModuleType } from '@/types/enums';
import type { FileQuery, PagedResponse, StoredFile, Uuid } from '@/types/api';

/** Файлы: `FilesController`. */
export const fileService = {
  list(query: FileQuery): Promise<PagedResponse<StoredFile>> {
    return api.get<PagedResponse<StoredFile>>('/files', query);
  },

  getById(id: Uuid): Promise<StoredFile> {
    return api.get<StoredFile>(`/files/${id}`);
  },

  /**
   * Загрузка файла: POST /api/files/upload?module=Study, поле формы `file`.
   *
   * Модуль передаётся в query, а не в теле: от него зависят разрешённые типы
   * (Study и Career принимают только PDF), и бэкенду он нужен до чтения файла.
   *
   * Прогресс загрузки прокидывается наружу — PDF на несколько мегабайт без
   * индикатора выглядит как зависший интерфейс.
   */
  async upload(
    file: File,
    module: ModuleType,
    onProgress?: (percent: number) => void,
  ): Promise<StoredFile> {
    const form = new FormData();
    form.append('file', file);

    const { data } = await httpClient.post<StoredFile>('/files/upload', form, {
      params: { module },
      onUploadProgress: (event) => {
        if (onProgress && event.total !== undefined && event.total > 0) {
          onProgress(Math.round((event.loaded / event.total) * 100));
        }
      },
    });
    return data;
  },

  /**
   * Удаление файла.
   *
   * Вернёт 409, если файл используется учебным материалом или резюме: у связи
   * Files → StudyMaterials/CareerProfiles стоит NoAction, и бэкенд проверяет
   * ссылки заранее, чтобы вместо ошибки внешнего ключа отдать понятный код
   * (ADR 50).
   */
  remove(id: Uuid): Promise<void> {
    return api.delete(`/files/${id}`);
  },
};
