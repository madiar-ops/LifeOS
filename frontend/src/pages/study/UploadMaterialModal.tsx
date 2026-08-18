import { zodResolver } from '@hookform/resolvers/zod';
import { FileUp } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';

import { Button, Field, Input, Modal, ProgressBar } from '@/components/ui';
import { useUploadFile } from '@/hooks/useFiles';
import { useCreateStudyMaterial } from '@/hooks/useStudy';
import { applyServerErrors } from '@/lib/formErrors';
import { formatFileSize } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { studyMaterialSchema, type StudyMaterialFormValues } from '@/schemas/study';
import { describeError } from '@/types/errors';

interface UploadMaterialModalProps {
  open: boolean;
  onClose: () => void;
}

/** Соответствует FileValidationRules: модуль Study принимает только PDF. */
const ACCEPT = 'application/pdf';

/** Лимит тела запроса на бэкенде — 15 МБ (FormOptions в Program.cs). */
const MAX_SIZE_BYTES = 15 * 1024 * 1024;

/**
 * Загрузка учебного материала.
 *
 * Процесс из ДВУХ запросов, и это не усложнение ради усложнения:
 *   1. POST /api/files/upload?module=Study — файл уходит в хранилище,
 *      метаданные пишутся в таблицу Files;
 *   2. POST /api/study/materials { fileId, title } — материал создаётся из
 *      уже проверенного файла.
 *
 * Так валидация файла (MIME → расширение → сигнатура) живёт в модуле Files и
 * не дублируется в Study и Career (ADR 71).
 *
 * ВАЖНОЕ СЛЕДСТВИЕ: если первый шаг прошёл, а второй упал, в хранилище
 * остаётся загруженный файл. Он не «сирота» — запись в Files существует, файл
 * виден в списке файлов и его можно удалить или использовать повторно.
 * Интерфейс сообщает об этом честно вместо того, чтобы делать вид, что ничего
 * не произошло.
 */
export function UploadMaterialModal({ open, onClose }: UploadMaterialModalProps) {
  const uploadFile = useUploadFile();
  const createMaterial = useCreateStudyMaterial();

  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [progress, setProgress] = useState(0);

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<StudyMaterialFormValues>({
    resolver: zodResolver(studyMaterialSchema),
    defaultValues: { title: '' },
  });

  useEffect(() => {
    if (!open) return;
    reset({ title: '' });
    setFile(null);
    setFileError(null);
    setProgress(0);
  }, [open, reset]);

  const pickFile = (selected: File | null) => {
    setFileError(null);
    if (selected === null) {
      setFile(null);
      return;
    }

    // Проверка на клиенте до отправки: незачем гонять 40 МБ по сети, чтобы
    // получить отказ. Сервер проверит то же самое ещё раз.
    if (selected.type !== ACCEPT) {
      setFileError('Модуль «Учёба» принимает только PDF.');
      return;
    }
    if (selected.size > MAX_SIZE_BYTES) {
      setFileError(`Файл больше ${formatFileSize(MAX_SIZE_BYTES)}.`);
      return;
    }

    setFile(selected);
    // Имя файла без расширения — разумная заготовка названия материала.
    setValue('title', selected.name.replace(/\.pdf$/i, '').slice(0, 200));
  };

  const onSubmit = handleSubmit(async (values) => {
    if (file === null) {
      setFileError('Выбери PDF-файл.');
      return;
    }

    try {
      const uploaded = await uploadFile.mutateAsync({
        file,
        module: 'Study',
        onProgress: setProgress,
      });

      try {
        await createMaterial.mutateAsync({ fileId: uploaded.id, title: values.title.trim() });
        toast.success('Материал добавлен', 'Теперь можно сгенерировать конспект.');
        onClose();
      } catch (error) {
        applyServerErrors<StudyMaterialFormValues>(error, setError, ['title']);
        toast.error(
          'Материал не создан',
          `Файл загружен и остался в разделе файлов. ${describeError(error)}`,
        );
      }
    } catch {
      // Ошибку загрузки уже показал глобальный обработчик мутаций.
      setProgress(0);
    }
  });

  const busy = isSubmitting || uploadFile.isPending || createMaterial.isPending;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Новый учебный материал"
      description="PDF с текстовым слоем — сканы без текста AI прочитать не сможет."
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Отмена
          </Button>
          <Button
            variant="primary"
            onClick={() => void onSubmit()}
            loading={busy}
            disabled={file === null}
          >
            Загрузить
          </Button>
        </>
      }
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void onSubmit();
        }}
        noValidate
        className="space-y-4"
      >
        <div>
          <label
            htmlFor="material-file"
            className="flex cursor-pointer flex-col items-center gap-2 rounded-xl border border-dashed border-line-strong bg-surface-2 px-4 py-8 text-center transition-colors hover:border-accent hover:bg-accent-soft/40"
          >
            <FileUp size={22} className="text-fg-subtle" />
            <span className="text-[13.5px] font-medium text-fg">
              {file === null ? 'Выбрать PDF' : file.name}
            </span>
            <span className="text-[12px] text-fg-subtle">
              {file === null
                ? `PDF до ${formatFileSize(MAX_SIZE_BYTES)}`
                : formatFileSize(file.size)}
            </span>
          </label>
          <input
            id="material-file"
            type="file"
            accept={ACCEPT}
            className="sr-only"
            onChange={(event) => pickFile(event.target.files?.[0] ?? null)}
          />
          {fileError !== null && (
            <p role="alert" className="mt-1.5 text-[12px] text-danger">
              {fileError}
            </p>
          )}
        </div>

        {uploadFile.isPending && progress > 0 && (
          <div className="space-y-1">
            <div className="flex justify-between text-[12px] text-fg-muted">
              <span>Загрузка файла</span>
              <span className="tabular">{progress} %</span>
            </div>
            <ProgressBar value={progress} label="Загрузка файла" />
          </div>
        )}

        <Field label="Название материала" error={errors.title?.message} required>
          {(field) => (
            <Input {...field} {...register('title')} placeholder="Конспект по машинному обучению" />
          )}
        </Field>
      </form>
    </Modal>
  );
}
