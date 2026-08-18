import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowLeft, ExternalLink, FileText, NotebookPen, Pencil, Sparkles, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useParams } from 'react-router-dom';

import { AiResultCard } from '@/components/ai/AiResultCard';
import { AiStateNotice } from '@/components/ai/AiStateNotice';
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
  Field,
  Input,
  Skeleton,
  SkeletonRows,
  Textarea,
} from '@/components/ui';
import {
  useCreateStudyNote,
  useDeleteStudyNote,
  useGenerateQuiz,
  useStudyMaterial,
  useStudyNotes,
  useSummarizeMaterial,
  useUpdateStudyNote,
} from '@/hooks/useStudy';
import { formatDate, formatRelative } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { ROUTES } from '@/router/routes';
import { generateQuizSchema, studyNoteSchema, type GenerateQuizFormValues, type StudyNoteFormValues } from '@/schemas/study';
import type { AiResult, Quiz, StudyNote, StudySummary } from '@/types/api';

import { QuizRunner } from './QuizRunner';

/** Расшифровка поля `source`: какой алгоритм сформировал результат. */
const SOURCE_LABELS: Record<string, string> = {
  llm: 'сгенерировано языковой моделью',
  extractive: 'извлекающая суммаризация (без LLM)',
  local: 'локальный алгоритм',
  unavailable: 'недоступно',
};

export default function StudyMaterialPage() {
  const { id } = useParams<{ id: string }>();
  const materialId = id ?? null;

  const material = useStudyMaterial(materialId);
  const notes = useStudyNotes(materialId);
  const summarize = useSummarizeMaterial();
  const generateQuiz = useGenerateQuiz();
  const createNote = useCreateStudyNote();
  const updateNote = useUpdateStudyNote();
  const deleteNote = useDeleteStudyNote();

  const [summaryResult, setSummaryResult] = useState<AiResult<StudySummary> | null>(null);
  const [summaryError, setSummaryError] = useState<unknown>(null);
  const [quizResult, setQuizResult] = useState<AiResult<Quiz> | null>(null);
  const [quizError, setQuizError] = useState<unknown>(null);
  const [editingNote, setEditingNote] = useState<StudyNote | null>(null);
  const [noteToDelete, setNoteToDelete] = useState<StudyNote | null>(null);

  const noteForm = useForm<StudyNoteFormValues>({
    resolver: zodResolver(studyNoteSchema),
    defaultValues: { content: '' },
  });

  const quizForm = useForm<GenerateQuizFormValues>({
    resolver: zodResolver(generateQuizSchema),
    defaultValues: { questionCount: 5 },
  });

  const runSummarize = async () => {
    if (materialId === null) return;
    setSummaryError(null);
    try {
      setSummaryResult(await summarize.mutateAsync(materialId));
    } catch (error) {
      // Ошибка сохраняется в состоянии, чтобы AiStateNotice объяснил причину:
      // скан без текстового слоя и недоступный AI-сервис — разные ситуации.
      setSummaryError(error);
    }
  };

  const runGenerateQuiz = quizForm.handleSubmit(async (values) => {
    if (materialId === null) return;
    setQuizError(null);
    try {
      setQuizResult(
        await generateQuiz.mutateAsync({
          studyMaterialId: materialId,
          questionCount: values.questionCount,
        }),
      );
    } catch (error) {
      setQuizError(error);
    }
  });

  const submitNote = noteForm.handleSubmit(async (values) => {
    if (materialId === null) return;
    try {
      if (editingNote !== null) {
        await updateNote.mutateAsync({ id: editingNote.id, content: values.content.trim() });
        toast.success('Заметка обновлена');
        setEditingNote(null);
      } else {
        await createNote.mutateAsync({
          studyMaterialId: materialId,
          content: values.content.trim(),
        });
        toast.success('Заметка добавлена');
      }
      noteForm.reset({ content: '' });
    } catch {
      /* уведомление показал глобальный обработчик */
    }
  });

  const confirmDeleteNote = async () => {
    if (noteToDelete === null) return;
    try {
      await deleteNote.mutateAsync(noteToDelete.id);
      toast.success('Заметка удалена');
      setNoteToDelete(null);
    } catch {
      /* уведомление показал глобальный обработчик */
    }
  };

  if (material.isPending) {
    return (
      <PageShell title="Материал">
        <Skeleton className="h-40 rounded-card" />
        <Skeleton className="h-64 rounded-card" />
      </PageShell>
    );
  }

  if (material.isError) {
    return (
      <PageShell title="Материал">
        <Card>
          <ErrorState error={material.error} onRetry={() => void material.refetch()} />
        </Card>
      </PageShell>
    );
  }

  const data = material.data;
  const savedSummary = data.summary;

  return (
    <PageShell
      title={data.title}
      description={`Добавлен ${formatDate(data.createdAt)}`}
      actions={
        <Link
          to={ROUTES.study}
          className="inline-flex h-9.5 items-center gap-1.5 rounded-lg border border-line px-3 text-sm font-medium text-fg transition-colors hover:bg-surface-2"
        >
          <ArrowLeft size={15} />
          К материалам
        </Link>
      }
    >
      {/* ---- Файл -------------------------------------------------------- */}
      <Card>
        <CardBody className="flex flex-wrap items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-accent-soft text-accent">
            <FileText size={19} />
          </span>
          <div className="min-w-0 flex-1">
            <p className="truncate text-[13.5px] font-medium text-fg">{data.fileName}</p>
            <p className="text-[12px] text-fg-subtle">
              Заметок: {data.notesCount} · Тестов: {data.quizzesCount}
            </p>
          </div>
          <a
            href={data.fileUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line px-3 text-[13px] font-medium text-fg transition-colors hover:bg-surface-2"
          >
            <ExternalLink size={14} />
            Открыть PDF
          </a>
        </CardBody>
      </Card>

      {/* ---- Конспект ---------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<Sparkles size={15} />}
          title="Конспект"
          description="Текст извлекается из PDF на бэкенде и отправляется в AI-сервис"
          actions={
            <Button
              variant="primary"
              size="sm"
              loading={summarize.isPending}
              onClick={() => void runSummarize()}
            >
              {savedSummary === null ? 'Сгенерировать' : 'Перегенерировать'}
            </Button>
          }
        />
        <CardBody className="space-y-4">
          {summaryError !== null ? (
            <AiStateNotice error={summaryError} onRetry={() => void runSummarize()} />
          ) : summaryResult !== null ? (
            <AiResultCard
              title="Свежий конспект"
              envelope={summaryResult}
              actions={
                <Badge tone="neutral">
                  {SOURCE_LABELS[summaryResult.result.source] ?? summaryResult.result.source}
                </Badge>
              }
            >
              <p className="text-[13.5px] leading-relaxed whitespace-pre-line text-fg">
                {summaryResult.result.summary}
              </p>
              {summaryResult.result.keyPoints.length > 0 && (
                <div>
                  <p className="text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
                    Ключевые тезисы
                  </p>
                  <ul className="mt-2 space-y-1.5">
                    {summaryResult.result.keyPoints.map((point) => (
                      <li key={point} className="flex gap-2 text-[13px] leading-relaxed text-fg-muted">
                        <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-accent" />
                        {point}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </AiResultCard>
          ) : savedSummary !== null ? (
            <p className="text-[13.5px] leading-relaxed whitespace-pre-line text-fg">
              {savedSummary}
            </p>
          ) : (
            <EmptyState
              icon={<Sparkles size={20} />}
              title="Конспекта пока нет"
              description="Модель прочитает текстовый слой PDF и выделит главное. Скан без текста обработать нельзя — OCR в проекте не используется."
              className="py-8"
            />
          )}
        </CardBody>
      </Card>

      {/* ---- Тест -------------------------------------------------------- */}
      {quizResult !== null ? (
        <QuizRunner quiz={quizResult.result} onClose={() => setQuizResult(null)} />
      ) : (
        <Card>
          <CardHeader
            icon={<Sparkles size={15} />}
            title="Тест по материалу"
            description="Правильные ответы не приходят на клиент — проверка только на сервере"
          />
          <CardBody className="space-y-4">
            {quizError !== null ? (
              <AiStateNotice error={quizError} onRetry={() => void runGenerateQuiz()} />
            ) : null}

            <form
              onSubmit={(event) => {
                event.preventDefault();
                void runGenerateQuiz();
              }}
              className="flex flex-wrap items-end gap-3"
            >
              <Field
                label="Количество вопросов"
                error={quizForm.formState.errors.questionCount?.message}
                hint="От 1 до 15"
                className="w-40"
              >
                {(field) => (
                  <Input
                    {...field}
                    {...quizForm.register('questionCount', { valueAsNumber: true })}
                    type="number"
                    min={1}
                    max={15}
                    inputMode="numeric"
                  />
                )}
              </Field>
              <Button type="submit" variant="primary" loading={generateQuiz.isPending}>
                Сгенерировать тест
              </Button>
            </form>

            <p className="text-[12.5px] leading-relaxed text-fg-subtle">
              Генерация требует ключа LLM в ai-service. Без него сервис отвечает отказом,
              а не выдумывает вопросы — это осознанное поведение, а не сбой.
            </p>
          </CardBody>
        </Card>
      )}

      {/* ---- Заметки ----------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<NotebookPen size={15} />}
          title="Заметки"
          description="Свои формулировки запоминаются лучше готового конспекта"
        />
        <CardBody className="space-y-4">
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void submitNote();
            }}
            noValidate
            className="space-y-3"
          >
            <Field
              label={editingNote === null ? 'Новая заметка' : 'Правка заметки'}
              error={noteForm.formState.errors.content?.message}
              required
            >
              {(field) => (
                <Textarea
                  {...field}
                  {...noteForm.register('content')}
                  rows={3}
                  placeholder="Важно: переобучение лечится регуляризацией."
                />
              )}
            </Field>
            <div className="flex gap-2">
              <Button
                type="submit"
                variant="primary"
                size="sm"
                loading={createNote.isPending || updateNote.isPending}
              >
                {editingNote === null ? 'Добавить' : 'Сохранить'}
              </Button>
              {editingNote !== null && (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => {
                    setEditingNote(null);
                    noteForm.reset({ content: '' });
                  }}
                >
                  Отмена
                </Button>
              )}
            </div>
          </form>

          {notes.isPending ? (
            <SkeletonRows rows={2} />
          ) : notes.isError ? (
            <ErrorState error={notes.error} onRetry={() => void notes.refetch()} />
          ) : notes.data.length === 0 ? (
            <p className="text-[13px] text-fg-subtle">Заметок пока нет.</p>
          ) : (
            <ul className="divide-y divide-line">
              {notes.data.map((note) => (
                <li key={note.id} className="flex items-start gap-3 py-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-[13.5px] leading-relaxed whitespace-pre-line text-fg">
                      {note.content}
                    </p>
                    <p className="mt-1 text-[11.5px] text-fg-subtle">
                      {formatRelative(note.updatedAt)}
                    </p>
                  </div>
                  <div className="flex shrink-0 gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label="Изменить заметку"
                      onClick={() => {
                        setEditingNote(note);
                        noteForm.reset({ content: note.content });
                      }}
                    >
                      <Pencil size={15} />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label="Удалить заметку"
                      onClick={() => setNoteToDelete(note)}
                    >
                      <Trash2 size={15} />
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </CardBody>
      </Card>

      <ConfirmDialog
        open={noteToDelete !== null}
        title="Удалить заметку?"
        message="Заметка будет удалена безвозвратно."
        loading={deleteNote.isPending}
        onConfirm={() => void confirmDeleteNote()}
        onCancel={() => setNoteToDelete(null)}
      />
    </PageShell>
  );
}
