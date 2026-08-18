import { CheckCircle2, HeartPulse, RefreshCw, Target, Wallet } from 'lucide-react';
import { useState } from 'react';

import { PageShell } from '@/components/layout/PageShell';
import {
  Button,
  ErrorState,
  SegmentedControl,
  SkeletonTiles,
  Skeleton,
  StatTile,
} from '@/components/ui';
import { useAuth } from '@/hooks/useAuth';
import { useDashboard } from '@/hooks/useDashboard';
import { formatDate, formatMoney, formatNumber, formatPercent } from '@/lib/format';

import {
  FinanceWidgetCard,
  GoalsWidgetCard,
  HealthWidgetCard,
  RecentFilesWidgetCard,
  RecommendationsWidgetCard,
  StudyCareerWidgetCard,
  TasksWidgetCard,
} from './DashboardWidgets';

/**
 * Варианты периода.
 *
 * Максимум 365 — верхняя граница, которую принимает бэкенд. Значения за её
 * пределами он обрезает молча (ADR 85), но предлагать пользователю
 * недопустимое значение всё равно неправильно.
 */
const PERIODS = [
  { value: 7, label: '7 дней' },
  { value: 30, label: '30 дней' },
  { value: 90, label: '90 дней' },
  { value: 365, label: 'Год' },
] as const;

export default function DashboardPage() {
  const { user } = useAuth();
  const [days, setDays] = useState<number>(30);
  const { data, isPending, isError, error, refetch, isFetching } = useDashboard(days);

  const greeting = user === null ? 'Обзор' : `Привет, ${user.name}`;

  return (
    <PageShell
      title={greeting}
      description={
        data === undefined
          ? 'Сводка по всем модулям'
          : `Период: ${formatDate(data.period.from)} — ${formatDate(data.period.to)}`
      }
      actions={
        <div className="flex items-center gap-2">
          <SegmentedControl
            options={PERIODS}
            value={days}
            onChange={setDays}
            ariaLabel="Период сводки"
            className="hidden sm:inline-flex"
          />
          <Button
            variant="ghost"
            size="icon"
            onClick={() => void refetch()}
            loading={isFetching && !isPending}
            aria-label="Обновить сводку"
            title="Обновить сводку"
          >
            <RefreshCw size={16} />
          </Button>
        </div>
      }
    >
      {/* На узких экранах переключатель периода переезжает под шапку. */}
      <div className="sm:hidden">
        <SegmentedControl
          options={PERIODS}
          value={days}
          onChange={setDays}
          ariaLabel="Период сводки"
        />
      </div>

      {isPending ? (
        <div className="space-y-5">
          <SkeletonTiles />
          <div className="grid gap-5 xl:grid-cols-2">
            <Skeleton className="h-80 rounded-card" />
            <Skeleton className="h-80 rounded-card" />
          </div>
        </div>
      ) : isError ? (
        <ErrorState error={error} onRetry={() => void refetch()} />
      ) : (
        <>
          {/* Четыре ключевых показателя — по одному на каждый модуль с числами. */}
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <StatTile
              label="Баланс за период"
              value={formatMoney(data.finance.balance, data.finance.currency)}
              hint={`норма сбережений ${formatPercent(data.finance.savingsRate)}`}
              icon={<Wallet size={15} />}
            />
            <StatTile
              label="Цели выполнены"
              value={formatPercent(data.goals.completionRate)}
              hint={`${formatNumber(data.goals.completed)} из ${formatNumber(data.goals.total)}`}
              icon={<Target size={15} />}
            />
            <StatTile
              label="Задачи в работе"
              value={formatNumber(data.tasks.pending)}
              hint={
                data.tasks.overdueCount > 0
                  ? `просрочено ${formatNumber(data.tasks.overdueCount)}`
                  : 'без просрочек'
              }
              icon={<CheckCircle2 size={15} />}
            />
            <StatTile
              label="Средний сон"
              value={
                data.health.averageSleepHours === null
                  ? '—'
                  : `${formatNumber(data.health.averageSleepHours, 1)} ч`
              }
              hint={`${formatNumber(data.health.averageSteps)} шагов в день`}
              icon={<HeartPulse size={15} />}
            />
          </div>

          <div className="grid gap-5 xl:grid-cols-2">
            <FinanceWidgetCard data={data.finance} />
            <GoalsWidgetCard data={data.goals} />
            <TasksWidgetCard data={data.tasks} />
            <HealthWidgetCard data={data.health} />
            <StudyCareerWidgetCard study={data.study} career={data.career} />
            <RecommendationsWidgetCard items={data.recommendations} />
            <RecentFilesWidgetCard items={data.recentFiles} />
          </div>

          <p className="text-center text-[11.5px] text-fg-subtle">
            {/* Время генерации на сервере: показывает, что данные агрегированы
                в PostgreSQL, а не собраны клиентом из отдельных запросов. */}
            Сводка сформирована на сервере {formatDate(data.generatedAt)}
          </p>
        </>
      )}
    </PageShell>
  );
}
