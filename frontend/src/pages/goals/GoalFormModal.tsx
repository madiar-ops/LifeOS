import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';

import { Button, Field, Input, Modal, Select, Textarea } from '@/components/ui';
import { useCreateGoal, useUpdateGoal } from '@/hooks/useGoals';
import { applyServerErrors } from '@/lib/formErrors';
import { datetimeLocalToIso, isoToDatetimeLocal } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { goalSchema, type GoalFormValues } from '@/schemas/goal';
import type { Goal, GoalPayload } from '@/types/api';
import { GOAL_STATUS_LABELS, GOAL_STATUS_VALUES, PRIORITY_LABELS, PRIORITY_VALUES } from '@/types/enums';

interface GoalFormModalProps {
  open: boolean;
  /** null — создание, объект — правка. */
  goal: Goal | null;
  onClose: () => void;
}

const EMPTY: GoalFormValues = {
  title: '',
  description: '',
  status: 'NotStarted',
  priority: 'Medium',
  deadline: '',
};

/**
 * Форма цели: создание и правка в одном компоненте.
 *
 * Разделять их на два почти идентичных файла нет причины — набор полей и
 * правила валидации совпадают, различается только вызываемая мутация.
 */
export function GoalFormModal({ open, goal, onClose }: GoalFormModalProps) {
  const createGoal = useCreateGoal();
  const updateGoal = useUpdateGoal();
  const editing = goal !== null;

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<GoalFormValues>({
    resolver: zodResolver(goalSchema),
    defaultValues: EMPTY,
  });

  /*
   * Значения формы перезаполняются при каждом открытии.
   *
   * Без этого effect'а форма сохраняла бы данные предыдущей цели: React
   * не размонтирует содержимое диалога, а `defaultValues` читаются только при
   * первом рендере. Пользователь открыл бы «Создать» после правки и увидел
   * чужие данные.
   */
  useEffect(() => {
    if (!open) return;
    reset(
      goal === null
        ? EMPTY
        : {
            title: goal.title,
            description: goal.description ?? '',
            status: goal.status,
            priority: goal.priority,
            deadline: isoToDatetimeLocal(goal.deadline),
          },
    );
  }, [open, goal, reset]);

  const onSubmit = handleSubmit(async (values) => {
    const payload: GoalPayload = {
      title: values.title.trim(),
      // Пустое описание отправляем как null, а не как пустую строку: в базе
      // это поле nullable, и хранить там '' означает две формы «пусто».
      description: values.description.trim() === '' ? null : values.description.trim(),
      status: values.status,
      priority: values.priority,
      deadline: datetimeLocalToIso(values.deadline),
    };

    try {
      if (editing) {
        await updateGoal.mutateAsync({ id: goal.id, payload });
        toast.success('Цель обновлена');
      } else {
        await createGoal.mutateAsync(payload);
        toast.success('Цель создана');
      }
      onClose();
    } catch (error) {
      applyServerErrors<GoalFormValues>(error, setError, [
        'title',
        'description',
        'status',
        'priority',
        'deadline',
      ]);
    }
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={editing ? 'Изменить цель' : 'Новая цель'}
      description={editing ? undefined : 'Задачи можно привязать к цели позже.'}
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
            <Input {...field} {...register('title')} placeholder="Сдать дипломный проект" />
          )}
        </Field>

        <Field label="Описание" error={errors.description?.message}>
          {(field) => (
            <Textarea
              {...field}
              {...register('description')}
              placeholder="Что именно нужно сделать и зачем"
            />
          )}
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Статус" error={errors.status?.message}>
            {(field) => (
              <Select {...field} {...register('status')}>
                {GOAL_STATUS_VALUES.map((status) => (
                  <option key={status} value={status}>
                    {GOAL_STATUS_LABELS[status]}
                  </option>
                ))}
              </Select>
            )}
          </Field>

          <Field label="Приоритет" error={errors.priority?.message}>
            {(field) => (
              <Select {...field} {...register('priority')}>
                {PRIORITY_VALUES.map((priority) => (
                  <option key={priority} value={priority}>
                    {PRIORITY_LABELS[priority]}
                  </option>
                ))}
              </Select>
            )}
          </Field>
        </div>

        <Field
          label="Срок"
          error={errors.deadline?.message}
          hint="Необязательно. Указывается в местном времени, на сервер уходит UTC."
        >
          {(field) => <Input {...field} {...register('deadline')} type="datetime-local" />}
        </Field>
      </form>
    </Modal>
  );
}
