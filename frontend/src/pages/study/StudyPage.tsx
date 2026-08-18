import { BookOpen, FileText, Plus, Sparkles, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';

import { PageShell } from '@/components/layout/PageShell';
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Pagination,
  SkeletonRows,
} from '@/components/ui';
import { useDeleteStudyMaterial, useStudyMaterials } from '@/hooks/useStudy';
import { formatDate } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { ROUTES } from '@/router/routes';
import type { StudyMaterial } from '@/types/api';

import { UploadMaterialModal } from './UploadMaterialModal';

export default function StudyPage() {
  const [pageNumber, setPageNumber] = useState(1);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [toDelete, setToDelete] = useState<StudyMaterial | null>(null);

  const materials = useStudyMaterials({ pageNumber, pageSize: 12 });
  const deleteMaterial = useDeleteStudyMaterial();

  const confirmDelete = async () => {
    if (toDelete === null) return;
    try {
      await deleteMaterial.mutateAsync(toDelete.id);
      toast.success('Материал удалён', 'Файл остался в разделе файлов.');
      setToDelete(null);
    } catch {
      /* уведомление показал глобальный обработчик */
    }
  };

  return (
    <PageShell
      title="Учёба"
      description="PDF-материалы, конспекты от AI, заметки и тесты"
      actions={
        <Button variant="primary" leftIcon={<Plus size={15} />} onClick={() => setUploadOpen(true)}>
          Загрузить PDF
        </Button>
      }
    >
      {materials.isPending ? (
        <SkeletonRows rows={5} />
      ) : materials.isError ? (
        <Card>
          <ErrorState error={materials.error} onRetry={() => void materials.refetch()} />
        </Card>
      ) : materials.data.items.length === 0 ? (
        <Card>
          <EmptyState
            icon={<BookOpen size={20} />}
            title="Материалов пока нет"
            description="Загрузи PDF с текстовым слоем — модель сделает конспект и сможет составить тест."
            action={
              <Button
                variant="primary"
                size="sm"
                leftIcon={<Plus size={14} />}
                onClick={() => setUploadOpen(true)}
              >
                Загрузить первый PDF
              </Button>
            }
          />
        </Card>
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {materials.data.items.map((material) => (
              <Card key={material.id} className="flex flex-col">
                <div className="flex-1 p-4">
                  <div className="flex items-start gap-3">
                    <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-accent-soft text-accent">
                      <FileText size={16} />
                    </span>
                    <div className="min-w-0 flex-1">
                      <Link
                        to={ROUTES.studyMaterial(material.id)}
                        className="line-clamp-2 text-[14px] font-medium text-fg hover:text-accent"
                      >
                        {material.title}
                      </Link>
                      <p className="mt-0.5 truncate text-[11.5px] text-fg-subtle">
                        {material.fileName} · {formatDate(material.createdAt)}
                      </p>
                    </div>
                  </div>

                  <p className="mt-3 line-clamp-3 min-h-[3.4rem] text-[13px] leading-relaxed text-fg-muted">
                    {material.summary ?? 'Конспект ещё не сгенерирован.'}
                  </p>

                  <div className="mt-3 flex flex-wrap gap-1.5">
                    <Badge tone={material.summary === null ? 'neutral' : 'success'}>
                      {material.summary === null ? 'без конспекта' : 'конспект готов'}
                    </Badge>
                    <Badge tone="neutral">заметок: {material.notesCount}</Badge>
                    <Badge tone="neutral">тестов: {material.quizzesCount}</Badge>
                  </div>
                </div>

                <div className="flex items-center justify-between gap-2 border-t border-line px-4 py-2.5">
                  <Link
                    to={ROUTES.studyMaterial(material.id)}
                    className="inline-flex items-center gap-1.5 text-[12.5px] font-medium text-accent hover:underline"
                  >
                    <Sparkles size={13} />
                    Открыть материал
                  </Link>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Удалить материал «${material.title}»`}
                    onClick={() => setToDelete(material)}
                  >
                    <Trash2 size={15} />
                  </Button>
                </div>
              </Card>
            ))}
          </div>

          <Card>
            <Pagination page={materials.data} onChange={setPageNumber} />
          </Card>
        </>
      )}

      <UploadMaterialModal open={uploadOpen} onClose={() => setUploadOpen(false)} />

      <ConfirmDialog
        open={toDelete !== null}
        title="Удалить материал?"
        // Реальное поведение бэкенда: заметки и тесты уходят каскадом, а файл
        // остаётся в хранилище и в таблице Files.
        message={`Материал «${toDelete?.title ?? ''}» будет удалён вместе с заметками и тестами. Сам PDF останется в разделе файлов.`}
        loading={deleteMaterial.isPending}
        onConfirm={() => void confirmDelete()}
        onCancel={() => setToDelete(null)}
      />
    </PageShell>
  );
}
