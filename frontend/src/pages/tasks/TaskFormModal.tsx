import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';

import { Button, Checkbox, Field, Input, Modal, Select } from '@/components/ui';
import { useGoals } from '@/hooks/useGoals';
import { useCreateTask, useUpdateTask } from '@/hooks/useTasks';
import { applyServerErrors } from '@/lib/formErrors';
import { datetimeLocalToIso, isoToDatetimeLocal } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { taskSchema, type TaskFormValues } from '@/schemas/task';
import type { TaskItem } from '@/types/api';

interface TaskFormModalProps {
  open: boolean;
  task: TaskItem | null;
  /** Предвыбранная цель — когда задача создаётся из карточки цели. */
  defaultGoalId?: string;
  onClose: () => void;
}

const EMPTY: TaskFormValues = { title: '', goalId: '', deadline: '', completed: false };

export function TaskFormModal({ open, task, defaultGoalId, onClose }: TaskFormModalProps) {
  const createTask = useCreateTask();
  const updateTask = useUpdateTask();
  const editing = task !== null;

  /*
   * Список целей для привязки.
   *
   * Загружается «широкой» страницей на 100 записей, а не постранично: это
   * выпадающий список, и подгружать его частями было бы неудобно. Верхняя
   * граница pageSize на бэкенде тоже 100 (PaginationParams.MaxPageSize),
   * так что запрос заведомо корректен. Для проекта личного планирования
   * сотня активных целей — потолок с большим запасом.
   */
  const goalsQuery = useGoals({ pageNumber: 1, pageSize: 100 });

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<TaskFormValues>({
    resolver: zodResolver(taskSchema),
    defaultValues: EMPTY,
  });

  useEffect(() => {
    if (!open) return;
    reset(
      task === null
        ? { ...EMPTY, goalId: defaultGoalId ?? '' }
        : {
            title: task.title,
            goalId: task.goalId ?? '',
            deadline: isoToDatetimeLocal(task.deadline),
            completed: task.completed,
          },
    );
  }, [open, task, defaultGoalId, reset]);

  const onSubmit = handleSubmit(async (values) => {
    const goalId = values.goalId === '' ? null : values.goalId;
    const deadline = datetimeLocalToIso(values.deadline);
    const title = values.title.trim();

    try {
      if (editing) {
        await updateTask.mutateAsync({
          id: task.id,
          payload: { title, goalId, deadline, completed: values.completed },
        });
        toast.success('Задача обновлена');
      } else {
        // При создании поле completed не отправляется: CreateTaskRequest его
        // не содержит, новая задача по определению не выполнена.
        await createTask.mutateAsync({ title, goalId, deadline });
        toast.success('Задача создана');
      }
      onClose();
    } catch (error) {
      applyServerErrors<TaskFormValues>(error, setError, ['title', 'goalId', 'deadline']);
    }
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={editing ? 'Изменить задачу' : 'Новая задача'}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Отмена
          </Button>
          <Button variant="primary" onClick={() => void onSubmit()} loading={isSubmitting}>
            {editing ? 'Сохранить' : 'Создать'}
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
        <Field label="Название" error={errors.title?.message} required>
          {(field) => (
            <Input {...field} {...register('title')} placeholder="Написать раздел о архитектуре" />
          )}
        </Field>

        <Field
          label="Цель"
          error={errors.goalId?.message}
          hint="Необязательно: задача может существовать сама по себе."
        >
          {(field) => (
            <Select {...field} {...register('goalId')} disabled={goalsQuery.isPending}>
              <option value="">Без цели</option>
              {goalsQuery.data?.items.map((goal) => (
                <option key={goal.id} value={goal.id}>
                  {goal.title}
                </option>
              ))}
            </Select>
          )}
        </Field>

        <Field label="Срок" error={errors.deadline?.message}>
          {(field) => <Input {...field} {...register('deadline')} type="datetime-local" />}
        </Field>

        {editing && (
          <Checkbox {...register('completed')} label="Задача выполнена" />
        )}
      </form>
    </Modal>
  );
}
