import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';

import { Button, Field, Input, Modal, Select } from '@/components/ui';
import { useCreateHealthLog, useUpdateHealthLog } from '@/hooks/useHealth';
import { applyServerErrors } from '@/lib/formErrors';
import { todayIsoDate } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { healthLogSchema, type HealthLogFormValues } from '@/schemas/healthLog';
import type { HealthLog } from '@/types/api';
import { MOOD_EMOJI, MOOD_LABELS, MOOD_VALUES } from '@/types/enums';

interface HealthLogFormModalProps {
  open: boolean;
  log: HealthLog | null;
  onClose: () => void;
}

const EMPTY: HealthLogFormValues = {
  date: todayIsoDate(),
  weight: null,
  sleepHours: null,
  mood: 'Neutral',
  waterMl: 2000,
  steps: 0,
};

/**
 * Пустое поле → null, а не 0.
 *
 * Разница смысловая: «не взвешивался» и «весил 0 кг» — разные утверждения.
 * Ноль попал бы в датасет для health-модели как реальное измерение и испортил
 * бы её выводы.
 */
const nullableNumber = { setValueAs: (value: string) => (value === '' ? null : Number(value)) };

export function HealthLogFormModal({ open, log, onClose }: HealthLogFormModalProps) {
  const createLog = useCreateHealthLog();
  const updateLog = useUpdateHealthLog();
  const editing = log !== null;

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<HealthLogFormValues>({
    resolver: zodResolver(healthLogSchema),
    defaultValues: EMPTY,
  });

  useEffect(() => {
    if (!open) return;
    reset(
      log === null
        ? EMPTY
        : {
            date: log.date,
            weight: log.weight,
            sleepHours: log.sleepHours,
            mood: log.mood,
            waterMl: log.waterMl,
            steps: log.steps,
          },
    );
  }, [open, log, reset]);

  const onSubmit = handleSubmit(async (values) => {
    try {
      if (editing) {
        /*
         * Дата в запрос обновления НЕ входит.
         *
         * Она часть уникального индекса (UserId, Date) и на бэкенде не
         * редактируется (ADR 38): UpdateHealthLogRequest поля `Date` просто не
         * содержит. Чтобы «перенести» запись на другой день, её нужно удалить
         * и создать заново.
         */
        await updateLog.mutateAsync({
          id: log.id,
          payload: {
            weight: values.weight,
            sleepHours: values.sleepHours,
            mood: values.mood,
            waterMl: values.waterMl,
            steps: values.steps,
          },
        });
        toast.success('Запись обновлена');
      } else {
        await createLog.mutateAsync(values);
        toast.success('Запись добавлена');
      }
      onClose();
    } catch (error) {
      const matched = applyServerErrors<HealthLogFormValues>(error, setError, [
        'date',
        'weight',
        'sleepHours',
        'mood',
        'waterMl',
        'steps',
      ]);
      // 409 приходит без словаря полей, но причина всегда одна — дата занята.
      if (!matched && !editing) {
        setError('date', {
          type: 'server',
          message: 'На эту дату запись уже есть — в дневнике одна запись в день.',
        });
      }
    }
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={editing ? 'Изменить запись' : 'Новая запись'}
      description="Одна запись на дату: сон, вода, шаги, вес и настроение."
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Отмена
          </Button>
          <Button variant="primary" onClick={() => void onSubmit()} loading={isSubmitting}>
            {editing ? 'Сохранить' : 'Добавить'}
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
        <Field
          label="Дата"
          error={errors.date?.message}
          hint={
            editing
              ? 'Дата не редактируется: она часть уникального ключа записи.'
              : 'Не более одной записи на дату.'
          }
          required
        >
          {(field) => (
            <Input {...field} {...register('date')} type="date" disabled={editing} readOnly={editing} />
          )}
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Вес" error={errors.weight?.message} hint="20–400 кг, необязательно">
            {(field) => (
              <Input
                {...field}
                {...register('weight', nullableNumber)}
                type="number"
                step="0.1"
                min={20}
                max={400}
                inputMode="decimal"
                placeholder="72.5"
                suffix="кг"
              />
            )}
          </Field>

          <Field label="Сон" error={errors.sleepHours?.message} hint="0–24 ч, необязательно">
            {(field) => (
              <Input
                {...field}
                {...register('sleepHours', nullableNumber)}
                type="number"
                step="0.5"
                min={0}
                max={24}
                inputMode="decimal"
                placeholder="7.5"
                suffix="ч"
              />
            )}
          </Field>

          <Field label="Вода" error={errors.waterMl?.message} required>
            {(field) => (
              <Input
                {...field}
                {...register('waterMl', { valueAsNumber: true })}
                type="number"
                step="50"
                min={0}
                max={20000}
                inputMode="numeric"
                suffix="мл"
              />
            )}
          </Field>

          <Field label="Шаги" error={errors.steps?.message} required>
            {(field) => (
              <Input
                {...field}
                {...register('steps', { valueAsNumber: true })}
                type="number"
                step="100"
                min={0}
                max={200000}
                inputMode="numeric"
              />
            )}
          </Field>
        </div>

        <Field label="Настроение" error={errors.mood?.message}>
          {(field) => (
            <Select {...field} {...register('mood')}>
              {MOOD_VALUES.map((mood) => (
                <option key={mood} value={mood}>
                  {MOOD_EMOJI[mood]} {MOOD_LABELS[mood]}
                </option>
              ))}
            </Select>
          )}
        </Field>
      </form>
    </Modal>
  );
}
