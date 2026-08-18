import { zodResolver } from '@hookform/resolvers/zod';
import { Briefcase, FileUp, Sparkles, TrendingUp } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';

import { AiResultCard } from '@/components/ai/AiResultCard';
import { AiStateNotice } from '@/components/ai/AiStateNotice';
import { PageShell } from '@/components/layout/PageShell';
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  ErrorState,
  Field,
  Input,
  ProgressBar,
  Skeleton,
  Textarea,
} from '@/components/ui';
import { useAnalyzeResume, useCareerProfile, useUpdateCareerProfile } from '@/hooks/useCareer';
import { useUploadFile } from '@/hooks/useFiles';
import { applyServerErrors } from '@/lib/formErrors';
import { formatFileSize, formatNumber } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { careerProfileSchema, type CareerProfileFormValues } from '@/schemas/career';
import type { AiResult, ResumeAnalysis } from '@/types/api';

/** Модуль Career, как и Study, принимает только PDF (FileValidationRules). */
const ACCEPT = 'application/pdf';
const MAX_SIZE_BYTES = 15 * 1024 * 1024;

export default function CareerPage() {
  const profile = useCareerProfile();
  const updateProfile = useUpdateCareerProfile();
  const uploadFile = useUploadFile();
  const analyzeResume = useAnalyzeResume();

  const [analysis, setAnalysis] = useState<AiResult<ResumeAnalysis> | null>(null);
  const [analysisError, setAnalysisError] = useState<unknown>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [progress, setProgress] = useState(0);

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<CareerProfileFormValues>({
    resolver: zodResolver(careerProfileSchema),
    defaultValues: { skills: '', desiredPosition: '' },
  });

  /*
   * Форма заполняется после загрузки профиля.
   *
   * Профиль создаётся лениво при первом GET (ADR 77), поэтому данные приходят
   * асинхронно и `defaultValues` их застать не успевает. `reset` вместо
   * `setValue` — он же сбрасывает флаг isDirty, и кнопка «Сохранить»
   * не выглядит активной сразу после открытия страницы.
   */
  useEffect(() => {
    if (profile.data === undefined) return;
    reset({
      skills: profile.data.skills ?? '',
      desiredPosition: profile.data.desiredPosition ?? '',
    });
  }, [profile.data, reset]);

  const saveProfile = handleSubmit(async (values) => {
    if (profile.data === undefined) return;
    try {
      await updateProfile.mutateAsync({
        skills: values.skills.trim() === '' ? null : values.skills.trim(),
        desiredPosition: values.desiredPosition.trim() === '' ? null : values.desiredPosition.trim(),
        // Привязку резюме не трогаем: она меняется отдельным действием.
        resumeFileId: profile.data.resumeFileId,
      });
      toast.success('Профиль сохранён');
    } catch (error) {
      applyServerErrors<CareerProfileFormValues>(error, setError, ['skills', 'desiredPosition']);
    }
  });

  /**
   * Загрузка резюме — тоже два шага.
   *
   * Файл сначала попадает в модуль Files (валидация PDF по сигнатуре), затем его
   * идентификатор привязывается к карьерному профилю через PUT. Прямого
   * «загрузить резюме» на бэкенде нет намеренно: иначе валидация файлов
   * дублировалась бы в третьем месте.
   */
  const uploadResume = async (file: File) => {
    setFileError(null);
    if (file.type !== ACCEPT) {
      setFileError('Резюме принимается только в PDF.');
      return;
    }
    if (file.size > MAX_SIZE_BYTES) {
      setFileError(`Файл больше ${formatFileSize(MAX_SIZE_BYTES)}.`);
      return;
    }

    try {
      const uploaded = await uploadFile.mutateAsync({
        file,
        module: 'Career',
        onProgress: setProgress,
      });
      await updateProfile.mutateAsync({
        skills: profile.data?.skills ?? null,
        desiredPosition: profile.data?.desiredPosition ?? null,
        resumeFileId: uploaded.id,
      });
      toast.success('Резюме привязано', 'Теперь можно запустить разбор.');
    } catch {
      /* уведомление показал глобальный обработчик */
    } finally {
      setProgress(0);
    }
  };

  const runAnalysis = async () => {
    setAnalysisError(null);
    try {
      setAnalysis(await analyzeResume.mutateAsync());
    } catch (error) {
      setAnalysisError(error);
    }
  };

  if (profile.isPending) {
    return (
      <PageShell title="Карьера">
        <Skeleton className="h-56 rounded-card" />
        <Skeleton className="h-40 rounded-card" />
      </PageShell>
    );
  }

  if (profile.isError) {
    return (
      <PageShell title="Карьера">
        <Card>
          <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />
        </Card>
      </PageShell>
    );
  }

  const data = profile.data;
  const hasResume = data.resumeFileId !== null;

  return (
    <PageShell title="Карьера" description="Профиль, резюме и его разбор моделью">
      {/* ---- Профиль ----------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<Briefcase size={15} />}
          title="Профиль"
          description="Навыки и желаемая позиция — контекст для разбора резюме"
        />
        <CardBody>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void saveProfile();
            }}
            noValidate
            className="space-y-4"
          >
            <Field label="Желаемая позиция" error={errors.desiredPosition?.message}>
              {(field) => (
                <Input
                  {...field}
                  {...register('desiredPosition')}
                  placeholder="Backend Developer"
                />
              )}
            </Field>

            <Field
              label="Навыки"
              error={errors.skills?.message}
              hint="Через запятую. Модель сравнивает их с требованиями к указанной позиции."
            >
              {(field) => (
                <Textarea
                  {...field}
                  {...register('skills')}
                  rows={3}
                  placeholder="C#, ASP.NET Core, React, PostgreSQL"
                />
              )}
            </Field>

            <Button
              type="submit"
              variant="primary"
              loading={isSubmitting || updateProfile.isPending}
              disabled={!isDirty}
            >
              Сохранить
            </Button>
          </form>
        </CardBody>
      </Card>

      {/* ---- Резюме ------------------------------------------------------ */}
      <Card>
        <CardHeader
          icon={<FileUp size={15} />}
          title="Резюме"
          description="PDF с текстовым слоем — из него извлекается текст для анализа"
          actions={
            hasResume ? <Badge tone="success">загружено</Badge> : <Badge tone="neutral">нет файла</Badge>
          }
        />
        <CardBody className="space-y-3">
          {hasResume && (
            <p className="text-[13px] text-fg-muted">
              Текущий файл:{' '}
              <span className="font-medium text-fg">{data.resumeFileName ?? 'резюме.pdf'}</span>
            </p>
          )}

          <label
            htmlFor="resume-file"
            className="flex cursor-pointer flex-col items-center gap-2 rounded-xl border border-dashed border-line-strong bg-surface-2 px-4 py-7 text-center transition-colors hover:border-accent hover:bg-accent-soft/40"
          >
            <FileUp size={20} className="text-fg-subtle" />
            <span className="text-[13.5px] font-medium text-fg">
              {hasResume ? 'Заменить резюме' : 'Загрузить резюме'}
            </span>
            <span className="text-[12px] text-fg-subtle">
              PDF до {formatFileSize(MAX_SIZE_BYTES)}
            </span>
          </label>
          <input
            id="resume-file"
            type="file"
            accept={ACCEPT}
            className="sr-only"
            onChange={(event) => {
              const selected = event.target.files?.[0];
              if (selected !== undefined) void uploadResume(selected);
            }}
          />

          {fileError !== null && (
            <p role="alert" className="text-[12px] text-danger">
              {fileError}
            </p>
          )}

          {uploadFile.isPending && progress > 0 && (
            <div className="space-y-1">
              <div className="flex justify-between text-[12px] text-fg-muted">
                <span>Загрузка резюме</span>
                <span className="tabular">{progress} %</span>
              </div>
              <ProgressBar value={progress} label="Загрузка резюме" />
            </div>
          )}
        </CardBody>
      </Card>

      {/* ---- Разбор ------------------------------------------------------ */}
      <Card>
        <CardHeader
          icon={<Sparkles size={15} />}
          title="Разбор резюме"
          description="Оценка, сильные и слабые места, недостающие навыки"
          actions={
            <Button
              variant="primary"
              size="sm"
              loading={analyzeResume.isPending}
              disabled={!hasResume}
              onClick={() => void runAnalysis()}
            >
              {data.aiReview === null ? 'Разобрать' : 'Разобрать заново'}
            </Button>
          }
        />
        <CardBody className="space-y-4">
          {!hasResume ? (
            <p className="text-[13px] leading-relaxed text-fg-muted">
              Сначала загрузи PDF резюме — анализировать пока нечего.
            </p>
          ) : analysisError !== null ? (
            <AiStateNotice error={analysisError} onRetry={() => void runAnalysis()} />
          ) : analysis !== null ? (
            <AiResultCard
              title="Результат разбора"
              envelope={analysis}
              actions={<Badge tone="neutral">источник: {analysis.result.source}</Badge>}
            >
              <div>
                <div className="flex items-baseline justify-between">
                  <span className="text-[12.5px] text-fg-muted">Общая оценка резюме</span>
                  <span className="tabular text-lg font-semibold">
                    {formatNumber(analysis.result.overallScore, 1)}
                    <span className="text-[13px] font-normal text-fg-subtle"> / 100</span>
                  </span>
                </div>
                <ProgressBar
                  className="mt-2"
                  value={analysis.result.overallScore}
                  tone={
                    analysis.result.overallScore >= 70
                      ? 'success'
                      : analysis.result.overallScore >= 45
                        ? 'warning'
                        : 'danger'
                  }
                  label="Оценка резюме"
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <AnalysisList
                  title="Сильные стороны"
                  items={analysis.result.strengths}
                  tone="success"
                />
                <AnalysisList
                  title="Слабые места"
                  items={analysis.result.weaknesses}
                  tone="danger"
                />
              </div>

              {analysis.result.missingSkills.length > 0 && (
                <div>
                  <p className="text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
                    Чего не хватает для позиции
                  </p>
                  <div className="mt-2 flex flex-wrap gap-1.5">
                    {analysis.result.missingSkills.map((skill) => (
                      <Badge key={skill} tone="warning">
                        {skill}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}

              {analysis.result.suggestions.length > 0 && (
                <AnalysisList
                  title="Что улучшить"
                  items={analysis.result.suggestions}
                  tone="accent"
                />
              )}
            </AiResultCard>
          ) : data.aiReview !== null ? (
            <div className="rounded-lg bg-surface-2 px-4 py-3">
              <p className="mb-1.5 flex items-center gap-1.5 text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
                <TrendingUp size={12} />
                Сохранённый разбор
              </p>
              <p className="text-[13.5px] leading-relaxed whitespace-pre-line text-fg">
                {data.aiReview}
              </p>
            </div>
          ) : (
            <p className="text-[13px] leading-relaxed text-fg-muted">
              Разбора ещё не было. Модель сравнит текст резюме с желаемой позицией и
              указанными навыками.
            </p>
          )}
        </CardBody>
      </Card>
    </PageShell>
  );
}

function AnalysisList({
  title,
  items,
  tone,
}: {
  title: string;
  items: string[];
  tone: 'success' | 'danger' | 'accent';
}) {
  if (items.length === 0) return null;

  const dotColor =
    tone === 'success' ? 'bg-success' : tone === 'danger' ? 'bg-danger' : 'bg-accent';

  return (
    <div>
      <p className="text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">{title}</p>
      <ul className="mt-2 space-y-1.5">
        {items.map((item) => (
          <li key={item} className="flex gap-2 text-[13px] leading-relaxed text-fg-muted">
            <span className={`mt-1.5 size-1.5 shrink-0 rounded-full ${dotColor}`} />
            {item}
          </li>
        ))}
      </ul>
    </div>
  );
}
