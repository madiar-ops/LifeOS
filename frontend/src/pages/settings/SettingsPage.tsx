import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, FileText, Monitor, Moon, Server, Sun, Trash2, XCircle } from 'lucide-react';
import { useState } from 'react';

import { PageShell } from '@/components/layout/PageShell';
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Pagination,
  SegmentedControl,
  Select,
  SkeletonRows,
} from '@/components/ui';
import { queryKeys } from '@/hooks/queryKeys';
import { API_BASE_URL } from '@/lib/config';
import { useDeleteFile, useFiles } from '@/hooks/useFiles';
import { useTheme } from '@/hooks/useTheme';
import { formatDate, formatFileSize } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { pingService } from '@/services/pingService';
import type { StoredFile } from '@/types/api';
import { MODULE_LABELS, MODULE_VALUES, type ModuleType } from '@/types/enums';
import { describeError } from '@/types/errors';

const THEME_OPTIONS = [
  { value: 'light' as const, label: 'Светлая' },
  { value: 'dark' as const, label: 'Тёмная' },
];

export default function SettingsPage() {
  const { theme, set } = useTheme();
  const [module, setModule] = useState<ModuleType | ''>('');
  const [pageNumber, setPageNumber] = useState(1);
  const [toDelete, setToDelete] = useState<StoredFile | null>(null);

  const files = useFiles({
    pageNumber,
    pageSize: 15,
    ...(module !== '' ? { module } : {}),
  });
  const deleteFile = useDeleteFile();

  /**
   * Проверка связи с API.
   *
   * `/api/ping` — единственный публичный эндпоинт бэкенда, поэтому по нему
   * видно именно доступность сервера, а не действительность токена. Для
   * пользователя это разница между «backend не запущен / сертификат не
   * доверенный» и «ошибка в приложении».
   */
  const ping = useQuery({
    queryKey: queryKeys.ping,
    queryFn: () => pingService.ping(),
    retry: false,
    staleTime: 0,
  });

  const confirmDelete = async () => {
    if (toDelete === null) return;
    try {
      await deleteFile.mutateAsync(toDelete.id);
      toast.success('Файл удалён');
      setToDelete(null);
    } catch {
      // 409 приходит, если файл используется материалом или резюме, — текст
      // об этом показал глобальный обработчик мутаций.
    }
  };

  return (
    <PageShell title="Настройки" description="Оформление, состояние API и загруженные файлы">
      {/* ---- Оформление -------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={theme === 'dark' ? <Moon size={15} /> : <Sun size={15} />}
          title="Оформление"
          description="Выбор сохраняется в браузере; по умолчанию берётся системная тема"
          actions={
            <SegmentedControl
              options={THEME_OPTIONS}
              value={theme}
              onChange={set}
              ariaLabel="Тема оформления"
            />
          }
        />
      </Card>

      {/* ---- Состояние API ----------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<Server size={15} />}
          title="Связь с API"
          description={API_BASE_URL}
          actions={
            <Button
              variant="secondary"
              size="sm"
              loading={ping.isFetching}
              onClick={() => void ping.refetch()}
            >
              Проверить
            </Button>
          }
        />
        <CardBody>
          {ping.isPending ? (
            <p className="text-[13px] text-fg-muted">Проверяем…</p>
          ) : ping.isError ? (
            <div className="flex items-start gap-3">
              <XCircle size={18} className="mt-0.5 shrink-0 text-danger" />
              <div>
                <p className="text-[13.5px] font-medium text-danger">Сервер недоступен</p>
                <p className="mt-0.5 text-[12.5px] leading-relaxed text-fg-muted">
                  {describeError(ping.error)}
                </p>
                <p className="mt-1.5 text-[12.5px] leading-relaxed text-fg-subtle">
                  Проверь, что запущен профиль LifeOS.API, и что сертификат разработки
                  доверенный: <code className="font-mono">dotnet dev-certs https --trust</code>
                </p>
              </div>
            </div>
          ) : (
            <div className="flex flex-wrap items-center gap-3">
              <CheckCircle2 size={18} className="shrink-0 text-success" />
              <div className="min-w-0 flex-1">
                <p className="text-[13.5px] font-medium text-fg">
                  {ping.data.service} · {ping.data.status}
                </p>
                <p className="text-[12.5px] text-fg-subtle">
                  окружение: {ping.data.environment}
                </p>
              </div>
              <Badge tone="success">соединение есть</Badge>
            </div>
          )}
        </CardBody>
      </Card>

      {/* ---- Файлы ------------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<FileText size={15} />}
          title="Загруженные файлы"
          description="Метаданные хранятся в PostgreSQL, сами файлы — в настроенном хранилище"
          actions={
            <Select
              value={module}
              onChange={(event) => {
                setModule(event.target.value as ModuleType | '');
                setPageNumber(1);
              }}
              className="w-40"
              aria-label="Фильтр по модулю"
            >
              <option value="">Все модули</option>
              {MODULE_VALUES.map((value) => (
                <option key={value} value={value}>
                  {MODULE_LABELS[value]}
                </option>
              ))}
            </Select>
          }
        />

        {files.isPending ? (
          <div className="p-5">
            <SkeletonRows rows={5} />
          </div>
        ) : files.isError ? (
          <ErrorState error={files.error} onRetry={() => void files.refetch()} />
        ) : files.data.items.length === 0 ? (
          <EmptyState
            icon={<FileText size={20} />}
            title="Файлов нет"
            description="Здесь появятся PDF учебных материалов, резюме и загруженные аватары."
          />
        ) : (
          <ul className="divide-y divide-line">
            {files.data.items.map((file) => (
              <li key={file.id} className="flex items-center gap-3 px-5 py-3">
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-surface-2 text-fg-subtle">
                  <FileText size={15} />
                </span>
                <div className="min-w-0 flex-1">
                  <a
                    href={file.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="block truncate text-[13.5px] text-fg hover:text-accent hover:underline"
                  >
                    {file.fileName}
                  </a>
                  <p className="text-[11.5px] text-fg-subtle">
                    {MODULE_LABELS[file.module]} · {formatFileSize(file.sizeBytes)} ·{' '}
                    {formatDate(file.createdAt)}
                  </p>
                </div>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Удалить файл ${file.fileName}`}
                  onClick={() => setToDelete(file)}
                >
                  <Trash2 size={15} />
                </Button>
              </li>
            ))}
          </ul>
        )}

        {files.data !== undefined && <Pagination page={files.data} onChange={setPageNumber} />}
      </Card>

      {/* ---- О приложении ------------------------------------------------ */}
      <Card>
        <CardHeader icon={<Monitor size={15} />} title="О приложении" />
        <CardBody>
          <dl className="grid gap-3 text-[13px] sm:grid-cols-2">
            <div>
              <dt className="text-fg-subtle">Клиент</dt>
              <dd className="text-fg">React 19 · TypeScript · Vite · Tailwind CSS 4</dd>
            </div>
            <div>
              <dt className="text-fg-subtle">Серверное состояние</dt>
              <dd className="text-fg">TanStack Query · Axios с ротацией токенов</dd>
            </div>
            <div>
              <dt className="text-fg-subtle">Backend</dt>
              <dd className="text-fg">ASP.NET Core 8 · PostgreSQL · Clean Architecture</dd>
            </div>
            <div>
              <dt className="text-fg-subtle">AI</dt>
              <dd className="text-fg">FastAPI · scikit-learn · PyTorch (инференс)</dd>
            </div>
          </dl>
        </CardBody>
      </Card>

      <ConfirmDialog
        open={toDelete !== null}
        title="Удалить файл?"
        // Реальное поведение: если файл используется материалом или резюме,
        // бэкенд ответит 409 и удаления не будет.
        message={`Файл «${toDelete?.fileName ?? ''}» будет удалён из хранилища. Если он используется учебным материалом или резюме, сервер откажет — сначала удали связанную запись.`}
        loading={deleteFile.isPending}
        onConfirm={() => void confirmDelete()}
        onCancel={() => setToDelete(null)}
      />
    </PageShell>
  );
}
