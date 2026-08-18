import {
  Briefcase,
  Brain,
  FileText,
  HeartPulse,
  ListChecks,
  Target,
  Wallet,
} from 'lucide-react';
import { Link } from 'react-router-dom';

import { CategoryDonutChart } from '@/components/charts/CategoryDonutChart';
import { HealthTrendChart } from '@/components/charts/HealthTrendChart';
import { MonthlyBarChart } from '@/components/charts/MonthlyBarChart';
import { RecommendationList } from '@/components/ai/RecommendationList';
import {
  Badge,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  ProgressBar,
} from '@/components/ui';
import { cn } from '@/lib/cn';
import {
  formatDate,
  formatFileSize,
  formatMoney,
  formatNumber,
  formatPercent,
} from '@/lib/format';
import { ROUTES } from '@/router/routes';
import type {
  CareerWidget,
  FinanceWidget,
  GoalsWidget,
  HealthWidget,
  Recommendation,
  RecentFileItem,
  StudyWidget,
  TasksWidget,
} from '@/types/api';
import { GOAL_STATUS_LABELS, MODULE_LABELS, PRIORITY_LABELS, moodFromScore, MOOD_EMOJI, MOOD_LABELS } from '@/types/enums';

/** Ссылка «смотреть всё» в шапке карточки. */
function MoreLink({ to, children }: { to: string; children: string }) {
  return (
    <Link to={to} className="text-[12.5px] font-medium text-accent hover:underline">
      {children}
    </Link>
  );
}

// ========================================================================
// Цели
// ========================================================================

export function GoalsWidgetCard({ data }: { data: GoalsWidget }) {
  return (
    <Card>
      <CardHeader
        icon={<Target size={15} />}
        title="Цели"
        description={`${formatNumber(data.completed)} из ${formatNumber(data.total)} завершено`}
        actions={<MoreLink to={ROUTES.goals}>Все цели</MoreLink>}
      />
      <CardBody className="space-y-4">
        <div>
          <div className="mb-1.5 flex items-baseline justify-between">
            <span className="text-[12.5px] text-fg-muted">
              {/* Отменённые цели исключены из знаменателя на бэкенде (ADR 82):
                  отмена — это решение, а не провал. */}
              Выполнение без учёта отменённых
            </span>
            <span className="tabular text-sm font-semibold">
              {formatPercent(data.completionRate)}
            </span>
          </div>
          <ProgressBar value={data.completionRate} tone="accent" label="Выполнение целей" />
        </div>

        <div className="flex flex-wrap gap-1.5">
          <Badge tone="neutral">{GOAL_STATUS_LABELS.NotStarted}: {data.notStarted}</Badge>
          <Badge tone="info">{GOAL_STATUS_LABELS.InProgress}: {data.inProgress}</Badge>
          <Badge tone="success">{GOAL_STATUS_LABELS.Completed}: {data.completed}</Badge>
          {data.cancelled > 0 && (
            <Badge tone="neutral">{GOAL_STATUS_LABELS.Cancelled}: {data.cancelled}</Badge>
          )}
          {data.overdueCount > 0 && <Badge tone="danger">Просрочено: {data.overdueCount}</Badge>}
        </div>

        {data.upcoming.length === 0 ? (
          <EmptyState
            icon={<Target size={18} />}
            title="Ближайших целей нет"
            description="Создай цель со сроком — она появится здесь."
            className="py-8"
          />
        ) : (
          <ul className="space-y-3">
            {data.upcoming.map((goal) => (
              <li key={goal.id} className="space-y-1.5">
                <div className="flex items-baseline justify-between gap-3">
                  <span className="min-w-0 truncate text-[13.5px] font-medium text-fg">
                    {goal.title}
                  </span>
                  <span
                    className={cn(
                      'tabular shrink-0 text-[12px]',
                      goal.isOverdue ? 'font-medium text-danger' : 'text-fg-subtle',
                    )}
                  >
                    {formatDate(goal.deadline)}
                  </span>
                </div>
                <ProgressBar
                  value={goal.progress}
                  tone={goal.isOverdue ? 'danger' : 'accent'}
                  label={`Прогресс цели «${goal.title}»`}
                />
                <div className="flex items-center gap-2 text-[11.5px] text-fg-subtle">
                  <span>{PRIORITY_LABELS[goal.priority]}</span>
                  <span>·</span>
                  <span className="tabular">
                    {goal.completedTasks}/{goal.totalTasks} задач
                  </span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
    </Card>
  );
}

// ========================================================================
// Задачи
// ========================================================================

export function TasksWidgetCard({ data }: { data: TasksWidget }) {
  return (
    <Card>
      <CardHeader
        icon={<ListChecks size={15} />}
        title="Задачи"
        description={`${formatNumber(data.pending)} в работе`}
        actions={<MoreLink to={ROUTES.tasks}>Все задачи</MoreLink>}
      />
      <CardBody className="space-y-4">
        <div className="grid grid-cols-3 gap-2 text-center">
          <div className="rounded-lg bg-surface-2 py-2.5">
            <p className="tabular text-lg font-semibold">{data.dueTodayCount}</p>
            <p className="text-[11.5px] text-fg-muted">сегодня</p>
          </div>
          <div className="rounded-lg bg-surface-2 py-2.5">
            <p className="tabular text-lg font-semibold">{data.dueThisWeekCount}</p>
            <p className="text-[11.5px] text-fg-muted">на неделе</p>
          </div>
          <div
            className={cn(
              'rounded-lg py-2.5',
              data.overdueCount > 0 ? 'bg-danger-soft' : 'bg-surface-2',
            )}
          >
            <p
              className={cn(
                'tabular text-lg font-semibold',
                data.overdueCount > 0 && 'text-danger',
              )}
            >
              {data.overdueCount}
            </p>
            <p className="text-[11.5px] text-fg-muted">просрочено</p>
          </div>
        </div>

        {data.urgent.length === 0 ? (
          <EmptyState
            icon={<ListChecks size={18} />}
            title="Срочных задач нет"
            description="Всё под контролем."
            className="py-8"
          />
        ) : (
          <ul className="divide-y divide-line">
            {data.urgent.map((task) => (
              <li key={task.id} className="flex items-baseline justify-between gap-3 py-2.5">
                <div className="min-w-0">
                  <p className="truncate text-[13.5px] text-fg">{task.title}</p>
                  {task.goalTitle !== null && (
                    <p className="truncate text-[11.5px] text-fg-subtle">{task.goalTitle}</p>
                  )}
                </div>
                <span
                  className={cn(
                    'tabular shrink-0 text-[12px]',
                    task.isOverdue ? 'font-medium text-danger' : 'text-fg-subtle',
                  )}
                >
                  {formatDate(task.deadline)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
    </Card>
  );
}

// ========================================================================
// Финансы
// ========================================================================

export function FinanceWidgetCard({ data }: { data: FinanceWidget }) {
  return (
    <Card className="xl:col-span-2">
      <CardHeader
        icon={<Wallet size={15} />}
        title="Финансы"
        description={
          // Валюта дашборда — самая частая у пользователя (ADR 83).
          // Об этом стоит сказать прямо: иначе непонятно, почему именно она.
          `Расчёт в ${data.currency} — валюте большинства операций`
        }
        actions={<MoreLink to={ROUTES.finance}>Все операции</MoreLink>}
      />
      <CardBody className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-3">
          <div>
            <p className="text-[12px] text-fg-muted">Доходы</p>
            <p className="tabular mt-0.5 text-lg font-semibold text-success">
              {formatMoney(data.totalIncome, data.currency)}
            </p>
          </div>
          <div>
            <p className="text-[12px] text-fg-muted">Расходы</p>
            <p className="tabular mt-0.5 text-lg font-semibold text-danger">
              {formatMoney(data.totalExpense, data.currency)}
            </p>
          </div>
          <div>
            <p className="text-[12px] text-fg-muted">Баланс</p>
            <p
              className={cn(
                'tabular mt-0.5 text-lg font-semibold',
                data.balance < 0 ? 'text-danger' : 'text-fg',
              )}
            >
              {formatMoney(data.balance, data.currency)}
            </p>
          </div>
        </div>

        {data.monthlyTrend.length > 0 && (
          <div>
            <p className="mb-2 text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
              {/* Тренд всегда за 6 месяцев независимо от выбранного периода
                  (ADR 84): график из двух точек не показывает тенденцию. */}
              Тренд за 6 месяцев
            </p>
            <MonthlyBarChart data={data.monthlyTrend} currency={data.currency} />
          </div>
        )}

        {data.topExpenseCategories.length > 0 && (
          <div>
            <p className="mb-2 text-[11px] font-semibold tracking-wide text-fg-subtle uppercase">
              Крупнейшие категории расходов
            </p>
            <CategoryDonutChart
              data={data.topExpenseCategories}
              currency={data.currency}
              height={200}
            />
          </div>
        )}

        {data.transactionCount === 0 && (
          <EmptyState
            icon={<Wallet size={18} />}
            title="Операций за период нет"
            description="Добавь доход или расход, чтобы увидеть баланс и структуру трат."
            className="py-8"
          />
        )}
      </CardBody>
    </Card>
  );
}

// ========================================================================
// Здоровье
// ========================================================================

export function HealthWidgetCard({ data }: { data: HealthWidget }) {
  const mood = moodFromScore(data.averageMood);

  return (
    <Card className="xl:col-span-2">
      <CardHeader
        icon={<HeartPulse size={15} />}
        title="Здоровье"
        description={`${formatNumber(data.entriesCount)} записей за период`}
        actions={<MoreLink to={ROUTES.health}>Дневник</MoreLink>}
      />
      <CardBody className="space-y-5">
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div>
            <p className="text-[12px] text-fg-muted">Сон</p>
            <p className="tabular mt-0.5 text-lg font-semibold">
              {data.averageSleepHours === null
                ? '—'
                : `${formatNumber(data.averageSleepHours, 1)} ч`}
            </p>
          </div>
          <div>
            <p className="text-[12px] text-fg-muted">Шаги</p>
            <p className="tabular mt-0.5 text-lg font-semibold">
              {formatNumber(data.averageSteps)}
            </p>
          </div>
          <div>
            <p className="text-[12px] text-fg-muted">Вода</p>
            <p className="tabular mt-0.5 text-lg font-semibold">
              {formatNumber(data.averageWaterMl)} мл
            </p>
          </div>
          <div>
            <p className="text-[12px] text-fg-muted">Настроение</p>
            <p className="mt-0.5 text-lg font-semibold">
              <span aria-hidden="true">{MOOD_EMOJI[mood]}</span>{' '}
              <span className="text-[13px] font-medium text-fg-muted">{MOOD_LABELS[mood]}</span>
            </p>
          </div>
        </div>

        {data.latestWeight !== null && (
          <p className="text-[12.5px] text-fg-muted">
            Последний вес{' '}
            <span className="tabular font-medium text-fg">
              {formatNumber(data.latestWeight, 1)} кг
            </span>
            {data.weightChange !== null && data.weightChange !== 0 && (
              <>
                {' · изменение '}
                <span
                  className={cn(
                    'tabular font-medium',
                    data.weightChange > 0 ? 'text-warning' : 'text-success',
                  )}
                >
                  {data.weightChange > 0 ? '+' : ''}
                  {formatNumber(data.weightChange, 1)} кг
                </span>
              </>
            )}
          </p>
        )}

        {data.trend.length > 0 ? (
          <HealthTrendChart data={data.trend} />
        ) : (
          <EmptyState
            icon={<HeartPulse size={18} />}
            title="Записей за период нет"
            description="Одна запись в день: сон, вода, шаги и настроение."
            className="py-8"
          />
        )}
      </CardBody>
    </Card>
  );
}

// ========================================================================
// Учёба и карьера
// ========================================================================

export function StudyCareerWidgetCard({
  study,
  career,
}: {
  study: StudyWidget;
  career: CareerWidget;
}) {
  return (
    <Card>
      <CardHeader
        icon={<Brain size={15} />}
        title="Учёба и карьера"
        actions={<MoreLink to={ROUTES.study}>Материалы</MoreLink>}
      />
      <CardBody className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <p className="text-[12px] text-fg-muted">Материалов</p>
            <p className="tabular mt-0.5 text-lg font-semibold">{study.materialsCount}</p>
            <p className="text-[11.5px] text-fg-subtle">
              с конспектом: {study.summarizedCount}
            </p>
          </div>
          <div>
            <p className="text-[12px] text-fg-muted">Тестов пройдено</p>
            <p className="tabular mt-0.5 text-lg font-semibold">
              {study.completedQuizzesCount}
              <span className="text-[13px] font-normal text-fg-subtle">
                {' '}
                / {study.quizzesCount}
              </span>
            </p>
            <p className="text-[11.5px] text-fg-subtle">
              средний балл:{' '}
              {study.averageQuizScore === null
                ? '—'
                : formatNumber(study.averageQuizScore, 1)}
            </p>
          </div>
        </div>

        <div className="border-t border-line pt-4">
          <div className="flex items-start gap-3">
            <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-accent-soft text-accent">
              <Briefcase size={15} />
            </span>
            <div className="min-w-0 flex-1">
              <p className="text-[13.5px] font-medium text-fg">
                {career.desiredPosition ?? 'Желаемая позиция не указана'}
              </p>
              <div className="mt-1.5 flex flex-wrap gap-1.5">
                <Badge tone={career.hasResume ? 'success' : 'neutral'}>
                  {career.hasResume ? 'Резюме загружено' : 'Резюме не загружено'}
                </Badge>
                <Badge tone={career.hasAiReview ? 'accent' : 'neutral'}>
                  {career.hasAiReview ? 'Разбор AI готов' : 'Разбор AI не сделан'}
                </Badge>
              </div>
            </div>
            <MoreLink to={ROUTES.career}>Карьера</MoreLink>
          </div>
        </div>

        <p className="text-[12px] text-fg-subtle">Заметок: {formatNumber(study.notesCount)}</p>
      </CardBody>
    </Card>
  );
}

// ========================================================================
// Рекомендации и файлы
// ========================================================================

export function RecommendationsWidgetCard({ items }: { items: Recommendation[] }) {
  return (
    <Card>
      <CardHeader
        icon={<Brain size={15} />}
        title="Рекомендации AI"
        description="Только выводы, в которых модель уверена"
        actions={<MoreLink to={ROUTES.ai}>Вся лента</MoreLink>}
      />
      <CardBody>
        <RecommendationList items={items} />
      </CardBody>
    </Card>
  );
}

export function RecentFilesWidgetCard({ items }: { items: RecentFileItem[] }) {
  return (
    <Card>
      <CardHeader icon={<FileText size={15} />} title="Последние файлы" />
      <CardBody>
        {items.length === 0 ? (
          <EmptyState
            icon={<FileText size={18} />}
            title="Файлов пока нет"
            description="PDF учебных материалов и резюме появятся здесь."
            className="py-8"
          />
        ) : (
          <ul className="divide-y divide-line">
            {items.map((file) => (
              <li key={file.id} className="flex items-center gap-3 py-2.5">
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-surface-2 text-fg-subtle">
                  <FileText size={15} />
                </span>
                <div className="min-w-0 flex-1">
                  <a
                    href={file.url}
                    target="_blank"
                    // noreferrer вместе с noopener: без них открытая страница
                    // получает доступ к window.opener и адрес источника.
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
              </li>
            ))}
          </ul>
        )}
      </CardBody>
    </Card>
  );
}
