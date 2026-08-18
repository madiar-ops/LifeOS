import { Pencil, Plus, Search, Target, Trash2 } from 'lucide-react';
import { useState } from 'react';

import { PageShell } from '@/components/layout/PageShell';
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Input,
  Pagination,
  ProgressBar,
  Select,
  SkeletonRows,
  type BadgeTone,
} from '@/components/ui';
import { useDebounce } from '@/hooks/useDebounce';
import { useDeleteGoal, useGoals } from '@/hooks/useGoals';
import { cn } from '@/lib/cn';
import { deadlineState, formatDate } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import type { Goal, GoalQuery } from '@/types/api';
import {
  GOAL_STATUS_LABELS,
  GOAL_STATUS_VALUES,
  PRIORITY_LABELS,
  PRIORITY_VALUES,
  type GoalStatus,
  type PriorityLevel,
} from '@/types/enums';

import { GoalFormModal } from './GoalFormModal';

const STATUS_TONES: Record<GoalStatus, BadgeTone> = {
  NotStarted: 'neutral',
  InProgress: 'info',
  Completed: 'success',
  Cancelled: 'neutral',
};

const PRIORITY_TONES: Record<PriorityLevel, BadgeTone> = {
  Low: 'neutral',
  Medium: 'info',
  High: 'danger',
};

export default function GoalsPage() {
  const [pageNumber, setPageNumber] = useState(1);
  const [status, setStatus] = useState<GoalStatus | ''>('');
  const [priority, setPriority] = useState<PriorityLevel | ''>('');
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search);

  const [editing, setEditing] = useState<Goal | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [toDelete, setToDelete] = useState<Goal | null>(null);

  /*
   * Пустая строка означает «фильтр не выбран», и такой ключ в запрос не
   * попадает. Отправить `status=''` нельзя: ASP.NET не сможет привязать пустую
   * строку к GoalStatus? и вернёт 400 через ValidationFilter.
   */
  const query: GoalQuery = {
    pageNumber,
    pageSize: 20,
    ...(status !== '' ? { status } : {}),
    ...(priority !== '' ? { priority } : {}),
    ...(debouncedSearch.trim() !== '' ? { search: debouncedSearch.trim() } : {}),
  };

  const { data, isPending, isError, error, refetch } = useGoals(query);
  const deleteGoal = useDeleteGoal();

  const confirmDelete = async () => {
    if (toDelete === null) return;
    try {
      await deleteGoal.mutateAsync(toDelete.id);
      toast.success('Цель удалена', 'Её задачи сохранены и остались без цели.');
      setToDelete(null);
    } catch {
      // Сообщение показал глобальный обработчик мутаций.
    }
  };

  return (
    <PageShell
      title="Цели"
      description="Долгосрочные результаты, к которым привязываются задачи"
      actions={
        <Button
          variant="primary"
          leftIcon={<Plus size={15} />}
          onClick={() => {
            setEditing(null);
            setFormOpen(true);
          }}
        >
          Новая цель
        </Button>
      }
    >
      <Card>
        <div className="flex flex-wrap gap-2 border-b border-line p-4">
          <Input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              // Сброс на первую страницу: иначе после фильтрации пользователь
              // остался бы на пятой странице результата из двух записей.
              setPageNumber(1);
            }}
            placeholder="Поиск по названию"
            icon={<Search size={15} />}
            className="w-full sm:w-64"
            aria-label="Поиск по названию цели"
          />

          <Select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value as GoalStatus | '');
              setPageNumber(1);
            }}
            className="w-full sm:w-44"
            aria-label="Фильтр по статусу"
          >
            <option value="">Все статусы</option>
            {GOAL_STATUS_VALUES.map((value) => (
              <option key={value} value={value}>
                {GOAL_STATUS_LABELS[value]}
              </option>
            ))}
          </Select>

          <Select
            value={priority}
            onChange={(event) => {
              setPriority(event.target.value as PriorityLevel | '');
              setPageNumber(1);
            }}
            className="w-full sm:w-48"
            aria-label="Фильтр по приоритету"
          >
            <option value="">Любой приоритет</option>
            {PRIORITY_VALUES.map((value) => (
              <option key={value} value={value}>
                {PRIORITY_LABELS[value]}
              </option>
            ))}
          </Select>
        </div>

        {isPending ? (
          <div className="p-4">
            <SkeletonRows rows={6} />
          </div>
        ) : isError ? (
          <ErrorState error={error} onRetry={() => void refetch()} />
        ) : data.items.length === 0 ? (
          <EmptyState
            icon={<Target size={20} />}
            title="Целей пока нет"
            description="Цель — это результат, к которому ведут задачи. Начни с одной большой."
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
                Создать цель
              </Button>
            }
          />
        ) : (
          <ul className="divide-y divide-line">
            {data.items.map((goal) => {
              const state = deadlineState(goal.deadline);
              const progress =
                goal.totalTasks === 0 ? 0 : (goal.completedTasks / goal.totalTasks) * 100;

              return (
                <li key={goal.id} className="p-4 transition-colors hover:bg-surface-2">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <h3 className="text-[14px] font-medium text-fg">{goal.title}</h3>
                        <Badge tone={STATUS_TONES[goal.status]} dot>
                          {GOAL_STATUS_LABELS[goal.status]}
                        </Badge>
                        <Badge tone={PRIORITY_TONES[goal.priority]}>
                          {PRIORITY_LABELS[goal.priority]}
                        </Badge>
                        {state === 'overdue' && goal.status !== 'Completed' && (
                          <Badge tone="danger">Просрочена</Badge>
                        )}
                      </div>

                      {goal.description !== null && (
                        <p className="mt-1 line-clamp-2 text-[13px] leading-relaxed text-fg-muted">
                          {goal.description}
                        </p>
                      )}

                      <div className="mt-2.5 max-w-md space-y-1.5">
                        <ProgressBar
                          value={progress}
                          tone={goal.status === 'Completed' ? 'success' : 'accent'}
                          label={`Прогресс цели «${goal.title}»`}
                        />
                        <div className="flex items-center gap-2 text-[11.5px] text-fg-subtle">
                          <span className="tabular">
                            {goal.completedTasks}/{goal.totalTasks} задач
                          </span>
                          {goal.deadline !== null && (
                            <>
                              <span>·</span>
                              <span
                                className={cn(
                                  'tabular',
                                  state === 'overdue' && 'font-medium text-danger',
                                  (state === 'today' || state === 'soon') && 'text-warning',
                                )}
                              >
                                срок {formatDate(goal.deadline)}
                              </span>
                            </>
                          )}
                        </div>
                      </div>
                    </div>

                    <div className="flex shrink-0 gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`Изменить цель «${goal.title}»`}
                        onClick={() => {
                          setEditing(goal);
                          setFormOpen(true);
                        }}
                      >
                        <Pencil size={15} />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`Удалить цель «${goal.title}»`}
                        onClick={() => setToDelete(goal)}
                      >
                        <Trash2 size={15} />
                      </Button>
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        )}

        {data !== undefined && <Pagination page={data} onChange={setPageNumber} />}
      </Card>

      <GoalFormModal open={formOpen} goal={editing} onClose={() => setFormOpen(false)} />

      <ConfirmDialog
        open={toDelete !== null}
        title="Удалить цель?"
        // Текст описывает РЕАЛЬНОЕ поведение бэкенда: связь Goals → Tasks
        // настроена на SetNull, задачи переживают удаление цели. Умолчать об
        // этом — значит заставить пользователя думать, что он потеряет задачи.
        message={`Цель «${toDelete?.title ?? ''}» будет удалена. Привязанные задачи сохранятся, но потеряют связь с целью.`}
        loading={deleteGoal.isPending}
        onConfirm={() => void confirmDelete()}
        onCancel={() => setToDelete(null)}
      />
    </PageShell>
  );
}
