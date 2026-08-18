import { Activity, Droplets, HeartPulse, Moon, Pencil, Plus, Sparkles, Trash2 } from 'lucide-react';
import { useState } from 'react';

import { AiResultCard } from '@/components/ai/AiResultCard';
import { AiStateNotice } from '@/components/ai/AiStateNotice';
import { HealthTrendChart } from '@/components/charts/HealthTrendChart';
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
  ProgressBar,
  SegmentedControl,
  SkeletonRows,
  StatTile,
} from '@/components/ui';
import { useDeleteHealthLog, useHealthAnalysis, useHealthLogs } from '@/hooks/useHealth';
import { formatDate, formatNumber, isoDateDaysAgo, todayIsoDate } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import type { HealthLog, HealthLogQuery, HealthPoint } from '@/types/api';
import { MOOD_EMOJI, MOOD_LABELS, MOOD_SCORES, moodFromScore } from '@/types/enums';

import { HealthLogFormModal } from './HealthLogFormModal';

const PERIODS = [
  { value: 7, label: '7 дней' },
  { value: 30, label: '30 дней' },
  { value: 90, label: '90 дней' },
] as const;

export default function HealthPage() {
  const [pageNumber, setPageNumber] = useState(1);
  const [days, setDays] = useState<number>(30);

  const [editing, setEditing] = useState<HealthLog | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [toDelete, setToDelete] = useState<HealthLog | null>(null);
  const [analysisEnabled, setAnalysisEnabled] = useState(false);

  const query: HealthLogQuery = {
    pageNumber,
    pageSize: 31,
    from: isoDateDaysAgo(days),
    to: todayIsoDate(),
  };

  const logs = useHealthLogs(query);
  const analysis = useHealthAnalysis({ daysBack: days }, analysisEnabled);
  const deleteLog = useDeleteHealthLog();

  const items = logs.data?.items ?? [];

  /*
   * Средние считаются по ЗАПОЛНЕННЫМ значениям, а не по всем записям.
   *
   * Если делить сумму часов сна на количество записей, дни без измерения
   * занижают среднее. Знаменатель должен совпадать с числителем — иначе это
   * не среднее, а произвольное число.
   */
  const withSleep = items.filter((item) => item.sleepHours !== null);
  const averageSleep =
    withSleep.length === 0
      ? null
      : withSleep.reduce((sum, item) => sum + (item.sleepHours ?? 0), 0) / withSleep.length;
  const averageSteps =
    items.length === 0 ? 0 : items.reduce((sum, item) => sum + item.steps, 0) / items.length;
  const averageWater =
    items.length === 0 ? 0 : items.reduce((sum, item) => sum + item.waterMl, 0) / items.length;
  const averageMood =
    items.length === 0
      ? 3
      : items.reduce((sum, item) => sum + MOOD_SCORES[item.mood], 0) / items.length;

  /*
   * График строится в хронологическом порядке.
   *
   * Бэкенд отдаёт список свежими записями вперёд, что верно для таблицы, но на
   * оси времени дало бы линию, идущую справа налево. Копия массива обязательна:
   * `reverse` меняет исходный массив, а он лежит в кэше TanStack Query.
   */
  const trend: HealthPoint[] = [...items].reverse().map((item) => ({
    date: item.date,
    sleepHours: item.sleepHours,
    steps: item.steps,
    waterMl: item.waterMl,
    mood: MOOD_SCORES[item.mood],
  }));

  const confirmDelete = async () => {
    if (toDelete === null) return;
    try {
      await deleteLog.mutateAsync(toDelete.id);
      toast.success('Запись удалена');
      setToDelete(null);
    } catch {
      /* уведомление показал глобальный обработчик */
    }
  };

  return (
    <PageShell
      title="Здоровье"
      description="Дневник самочувствия и AI-оценка по нему"
      actions={
        <div className="flex items-center gap-2">
          <SegmentedControl
            options={PERIODS}
            value={days}
            onChange={(value) => {
              setDays(value);
              setPageNumber(1);
            }}
            ariaLabel="Период дневника"
            className="hidden sm:inline-flex"
          />
          <Button
            variant="primary"
            leftIcon={<Plus size={15} />}
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
          >
            Запись
          </Button>
        </div>
      }
    >
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatTile
          label="Средний сон"
          value={averageSleep === null ? '—' : `${formatNumber(averageSleep, 1)} ч`}
          hint={`по ${formatNumber(withSleep.length)} записям`}
          icon={<Moon size={15} />}
        />
        <StatTile
          label="Шаги в день"
          value={formatNumber(averageSteps)}
          icon={<Activity size={15} />}
        />
        <StatTile
          label="Вода в день"
          value={`${formatNumber(averageWater)} мл`}
          icon={<Droplets size={15} />}
        />
        <StatTile
          label="Настроение"
          value={
            <span>
              <span aria-hidden="true">{MOOD_EMOJI[moodFromScore(averageMood)]}</span>{' '}
              <span className="text-base font-medium">{MOOD_LABELS[moodFromScore(averageMood)]}</span>
            </span>
          }
          hint={`средний балл ${formatNumber(averageMood, 1)} из 5`}
          icon={<HeartPulse size={15} />}
        />
      </div>

      {/* ---- Динамика ---------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<Activity size={15} />}
          title="Динамика"
          description="Сон и шаги на разных осях: часы и тысячи шагов несопоставимы напрямую"
        />
        <CardBody>
          {logs.isPending ? (
            <SkeletonRows rows={4} />
          ) : trend.length === 0 ? (
            <EmptyState
              icon={<Activity size={20} />}
              title="Данных за период нет"
              description="Добавь запись — график появится со второй точки."
              className="py-8"
            />
          ) : (
            <HealthTrendChart data={trend} />
          )}
        </CardBody>
      </Card>

      {/* ---- AI-оценка --------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<Sparkles size={15} />}
          title="Оценка самочувствия"
          description="Интегральный балл, прогноз настроения и факторы риска"
          actions={
            <Button
              variant="primary"
              size="sm"
              loading={analysis.isFetching}
              onClick={() => {
                setAnalysisEnabled(true);
                if (analysisEnabled) void analysis.refetch();
              }}
            >
              {analysisEnabled ? 'Пересчитать' : 'Проанализировать'}
            </Button>
          }
        />
        <CardBody>
          {!analysisEnabled ? (
            <p className="text-[13px] leading-relaxed text-fg-muted">
              Оценку считает модель RandomForest в ai-service. Запуск ручной: результат
              попадает в историю AI и при достаточной уверенности создаёт рекомендацию.
            </p>
          ) : analysis.isPending ? (
            <SkeletonRows rows={3} />
          ) : analysis.isError ? (
            <AiStateNotice error={analysis.error} onRetry={() => void analysis.refetch()} />
          ) : (
            <AiResultCard title="Оценка за период" envelope={analysis.data}>
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <p className="text-[12px] text-fg-muted">Индекс самочувствия</p>
                  <p className="tabular mt-0.5 text-2xl font-semibold">
                    {formatNumber(analysis.data.result.wellbeingScore, 1)}
                    <span className="text-sm font-normal text-fg-subtle"> / 100</span>
                  </p>
                  <ProgressBar
                    className="mt-2"
                    value={analysis.data.result.wellbeingScore}
                    tone={
                      analysis.data.result.wellbeingScore >= 70
                        ? 'success'
                        : analysis.data.result.wellbeingScore >= 45
                          ? 'warning'
                          : 'danger'
                    }
                    label="Индекс самочувствия"
                  />
                </div>
                <div>
                  <p className="text-[12px] text-fg-muted">Прогноз настроения</p>
                  <p className="mt-0.5 text-2xl font-semibold">
                    <span aria-hidden="true">
                      {MOOD_EMOJI[moodFromScore(analysis.data.result.predictedMood)]}
                    </span>{' '}
                    <span className="text-base font-medium">
                      {MOOD_LABELS[moodFromScore(analysis.data.result.predictedMood)]}
                    </span>
                  </p>
                  <p className="mt-2 text-[12px] text-fg-subtle">
                    проанализировано дней: {analysis.data.result.daysAnalyzed}
                  </p>
                </div>
              </div>

              {analysis.data.result.riskFactors.length > 0 && (
                <div>
                  <p className="text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
                    Факторы риска
                  </p>
                  <div className="mt-2 flex flex-wrap gap-1.5">
                    {analysis.data.result.riskFactors.map((factor) => (
                      <Badge key={factor} tone="warning">
                        {factor}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}

              {analysis.data.result.recommendations.length > 0 && (
                <div>
                  <p className="text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
                    Что можно сделать
                  </p>
                  <ul className="mt-2 space-y-1.5">
                    {analysis.data.result.recommendations.map((item) => (
                      <li key={item} className="flex gap-2 text-[13px] leading-relaxed text-fg-muted">
                        <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-accent" />
                        {item}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </AiResultCard>
          )}
        </CardBody>
      </Card>

      {/* ---- Дневник ----------------------------------------------------- */}
      <Card>
        <CardHeader title="Дневник" description={`Записи за последние ${String(days)} дней`} />

        {logs.isPending ? (
          <div className="p-4">
            <SkeletonRows rows={6} />
          </div>
        ) : logs.isError ? (
          <ErrorState error={logs.error} onRetry={() => void logs.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            icon={<HeartPulse size={20} />}
            title="Записей нет"
            description="Дневник — источник данных для health-модели. Чем больше дней, тем точнее прогноз."
            action={
              <Button
                variant="primary"
                size="sm"
                leftIcon={<Plus size={14} />}
                onClick={() => {
                  setEditing(null);
                  setFormOpen(true);
                }}
              >
                Добавить запись
              </Button>
            }
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-[13px]">
              <thead className="border-b border-line text-[11.5px] tracking-wide text-fg-subtle uppercase">
                <tr>
                  <th scope="col" className="px-4 py-2.5 font-medium">Дата</th>
                  <th scope="col" className="px-4 py-2.5 font-medium">Настроение</th>
                  <th scope="col" className="px-4 py-2.5 text-right font-medium">Сон</th>
                  <th scope="col" className="px-4 py-2.5 text-right font-medium">Шаги</th>
                  <th scope="col" className="px-4 py-2.5 text-right font-medium">Вода</th>
                  <th scope="col" className="px-4 py-2.5 text-right font-medium">Вес</th>
                  <th scope="col" className="px-4 py-2.5">
                    <span className="sr-only">Действия</span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {items.map((item) => (
                  <tr key={item.id} className="transition-colors hover:bg-surface-2">
                    <td className="tabular px-4 py-2.5 whitespace-nowrap text-fg-muted">
                      {formatDate(item.date)}
                    </td>
                    <td className="px-4 py-2.5 whitespace-nowrap">
                      <span aria-hidden="true">{MOOD_EMOJI[item.mood]}</span>{' '}
                      <span className="text-fg-muted">{MOOD_LABELS[item.mood]}</span>
                    </td>
                    <td className="tabular px-4 py-2.5 text-right">
                      {item.sleepHours === null ? '—' : `${formatNumber(item.sleepHours, 1)} ч`}
                    </td>
                    <td className="tabular px-4 py-2.5 text-right">{formatNumber(item.steps)}</td>
                    <td className="tabular px-4 py-2.5 text-right">
                      {formatNumber(item.waterMl)} мл
                    </td>
                    <td className="tabular px-4 py-2.5 text-right">
                      {item.weight === null ? '—' : `${formatNumber(item.weight, 1)} кг`}
                    </td>
                    <td className="px-4 py-2.5">
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Изменить запись за ${formatDate(item.date)}`}
                          onClick={() => {
                            setEditing(item);
                            setFormOpen(true);
                          }}
                        >
                          <Pencil size={15} />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Удалить запись за ${formatDate(item.date)}`}
                          onClick={() => setToDelete(item)}
                        >
                          <Trash2 size={15} />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {logs.data !== undefined && <Pagination page={logs.data} onChange={setPageNumber} />}
      </Card>

      <HealthLogFormModal open={formOpen} log={editing} onClose={() => setFormOpen(false)} />

      <ConfirmDialog
        open={toDelete !== null}
        title="Удалить запись?"
        message={
          toDelete === null
            ? ''
            : `Запись за ${formatDate(toDelete.date)} будет удалена. После этого дату можно использовать заново.`
        }
        loading={deleteLog.isPending}
        onConfirm={() => void confirmDelete()}
        onCancel={() => setToDelete(null)}
      />
    </PageShell>
  );
}
