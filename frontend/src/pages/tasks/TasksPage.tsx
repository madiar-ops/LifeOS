import { ListChecks, Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { useState } from 'react';

import { PageShell } from '@/components/layout/PageShell';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Input,
  Pagination,
  SegmentedControl,
  Select,
  SkeletonRows,
} from '@/components/ui';
import { useDebounce } from '@/hooks/useDebounce';
import { useGoals } from '@/hooks/useGoals';
import { useDeleteTask, useTasks, useToggleTask } from '@/hooks/useTasks';
import { cn } from '@/lib/cn';
import { deadlineState, formatDate } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import type { TaskItem, TaskQuery } from '@/types/api';

import { TaskFormModal } from './TaskFormModal';

type CompletionFilter = 'all' | 'active' | 'done';

const COMPLETION_OPTIONS = [
  { value: 'all', label: 'Все' },
  { value: 'active', label: 'В работе' },
  { value: 'done', label: 'Выполненные' },
] as const;

export default function TasksPage() {
  const [pageNumber, setPageNumber] = useState(1);
  const [completion, setCompletion] = useState<CompletionFilter>('all');
  const [goalId, setGoalId] = useState('');
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search);

  const [editing, setEditing] = useState<TaskItem | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [toDelete, setToDelete] = useState<TaskItem | null>(null);

  const query: TaskQuery = {
    pageNumber,
    pageSize: 20,
    ...(completion !== 'all' ? { completed: completion === 'done' } : {}),
    ...(goalId !== '' ? { goalId } : {}),
    ...(debouncedSearch.trim() !== '' ? { search: debouncedSearch.trim() } : {}),
  };

  const { data, isPending, isError, error, refetch } = useTasks(query);
  const goalsQuery = useGoals({ pageNumber: 1, pageSize: 100 });
  const toggleTask = useToggleTask();
  const deleteTask = useDeleteTask();

  const confirmDelete = async () => {
    if (toDelete === null) return;
    try {
      await deleteTask.mutateAsync(toDelete.id);
      toast.success('Задача удалена');
      setToDelete(null);
    } catch {
      /* уведомление показал глобальный обработчик */
    }
  };

  return (
    <PageShell
      title="Задачи"
      description="Конкретные шаги — со целью или без неё"
      actions={
        <Button
          variant="primary"
          leftIcon={<Plus size={15} />}
          onClick={() => {
            setEditing(null);
            setFormOpen(true);
          }}
        >
          Новая задача
        </Button>
      }
    >
      <Card>
        <div className="flex flex-wrap items-center gap-2 border-b border-line p-4">
          <SegmentedControl
            options={COMPLETION_OPTIONS}
            value={completion}
            onChange={(value) => {
              setCompletion(value);
              setPageNumber(1);
            }}
            ariaLabel="Фильтр по выполнению"
          />

          <Input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPageNumber(1);
            }}
            placeholder="Поиск по названию"
            icon={<Search size={15} />}
            className="w-full sm:w-56"
            aria-label="Поиск по названию задачи"
          />

          <Select
            value={goalId}
            onChange={(event) => {
              setGoalId(event.target.value);
              setPageNumber(1);
            }}
            className="w-full sm:w-52"
            aria-label="Фильтр по цели"
          >
            <option value="">Любая цель</option>
            {goalsQuery.data?.items.map((goal) => (
              <option key={goal.id} value={goal.id}>
                {goal.title}
              </option>
            ))}
          </Select>
        </div>

        {isPending ? (
          <div className="p-4">
            <SkeletonRows rows={7} />
          </div>
        ) : isError ? (
          <ErrorState error={error} onRetry={() => void refetch()} />
        ) : data.items.length === 0 ? (
          <EmptyState
            icon={<ListChecks size={20} />}
            title="Задач нет"
            description="Разбей цель на шаги или добавь отдельную задачу без цели."
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
                Создать задачу
              </Button>
            }
          />
        ) : (
          <ul className="divide-y divide-line">
            {data.items.map((task) => {
              const state = deadlineState(task.deadline);
              const overdue = state === 'overdue' && !task.completed;

              return (
                <li
                  key={task.id}
                  className="flex items-center gap-3 px-4 py-3 transition-colors hover:bg-surface-2"
                >
                  <Checkbox
                    checked={task.completed}
                    // Отдельный PATCH-эндпоинт вместо PUT со всем объектом:
                    // клиенту не нужно знать остальные поля, чтобы поставить
                    // галочку.
                    onChange={() => void toggleTask.mutateAsync(task.id).catch(() => undefined)}
                    aria-label={
                      task.completed
                        ? `Отметить «${task.title}» как невыполненную`
                        : `Отметить «${task.title}» как выполненную`
                    }
                  />

                  <div className="min-w-0 flex-1">
                    <p
                      className={cn(
                        'truncate text-[13.5px]',
                        task.completed ? 'text-fg-subtle line-through' : 'text-fg',
                      )}
                    >
                      {task.title}
                    </p>
                    <div className="mt-0.5 flex flex-wrap items-center gap-2 text-[11.5px] text-fg-subtle">
                      {task.goalTitle !== null ? (
                        <Badge tone="accent">{task.goalTitle}</Badge>
                      ) : (
                        <span>без цели</span>
                      )}
                      {task.deadline !== null && (
                        <span className={cn('tabular', overdue && 'font-medium text-danger')}>
                          срок {formatDate(task.deadline)}
                        </span>
                      )}
                    </div>
                  </div>

                  <div className="flex shrink-0 gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Изменить задачу «${task.title}»`}
                      onClick={() => {
                        setEditing(task);
                        setFormOpen(true);
                      }}
                    >
                      <Pencil size={15} />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Удалить задачу «${task.title}»`}
                      onClick={() => setToDelete(task)}
                    >
                      <Trash2 size={15} />
                    </Button>
                  </div>
                </li>
              );
            })}
          </ul>
        )}

        {data !== undefined && <Pagination page={data} onChange={setPageNumber} />}
      </Card>

      <TaskFormModal open={formOpen} task={editing} onClose={() => setFormOpen(false)} />

      <ConfirmDialog
        open={toDelete !== null}
        title="Удалить задачу?"
        message={`Задача «${toDelete?.title ?? ''}» будет удалена безвозвратно.`}
        loading={deleteTask.isPending}
        onConfirm={() => void confirmDelete()}
        onCancel={() => setToDelete(null)}
      />
    </PageShell>
  );
}
