import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';

import { Button, Field, Input, Modal, Select, Textarea } from '@/components/ui';
import { useCreateTransaction, useUpdateTransaction } from '@/hooks/useFinance';
import { applyServerErrors } from '@/lib/formErrors';
import { todayIsoDate } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { transactionSchema, type TransactionFormValues } from '@/schemas/transaction';
import type { Transaction, TransactionPayload } from '@/types/api';
import { TRANSACTION_TYPE_LABELS, TRANSACTION_TYPE_VALUES } from '@/types/enums';

/** Валюта по умолчанию совпадает с FinanceService.DefaultCurrency на бэкенде. */
const DEFAULT_CURRENCY = 'KZT';

/** Подсказки категорий. Не справочник: бэкенд принимает любую строку до 100 знаков. */
const CATEGORY_SUGGESTIONS = [
  'Еда',
  'Транспорт',
  'Жильё',
  'Учёба',
  'Здоровье',
  'Развлечения',
  'Стипендия',
  'Зарплата',
  'Подработка',
];

interface TransactionFormModalProps {
  open: boolean;
  transaction: Transaction | null;
  onClose: () => void;
}

export function TransactionFormModal({ open, transaction, onClose }: TransactionFormModalProps) {
  const createTransaction = useCreateTransaction();
  const updateTransaction = useUpdateTransaction();
  const editing = transaction !== null;

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<TransactionFormValues>({
    resolver: zodResolver(transactionSchema),
    defaultValues: {
      type: 'Expense',
      category: '',
      amount: 0,
      currency: DEFAULT_CURRENCY,
      date: todayIsoDate(),
      description: '',
    },
  });

  useEffect(() => {
    if (!open) return;
    reset(
      transaction === null
        ? {
            type: 'Expense',
            category: '',
            amount: 0,
            currency: DEFAULT_CURRENCY,
            date: todayIsoDate(),
            description: '',
          }
        : {
            type: transaction.type,
            category: transaction.category,
            amount: transaction.amount,
            currency: transaction.currency,
            date: transaction.date,
            description: transaction.description ?? '',
          },
    );
  }, [open, transaction, reset]);

  const onSubmit = handleSubmit(async (values) => {
    const payload: TransactionPayload = {
      type: values.type,
      category: values.category.trim(),
      /*
       * Сумма всегда положительна, знак несёт `type` (ADR 35). Бэкенд
       * применяет Math.Abs, но отправлять отрицательное значение всё равно не
       * следует: валидатор требует Amount > 0 и вернёт 400 раньше.
       */
      amount: Math.abs(values.amount),
      currency: values.currency.toUpperCase(),
      date: values.date,
      description: values.description.trim() === '' ? null : values.description.trim(),
    };

    try {
      if (editing) {
        await updateTransaction.mutateAsync({ id: transaction.id, payload });
        toast.success('Операция обновлена');
      } else {
        await createTransaction.mutateAsync(payload);
        toast.success('Операция добавлена');
      }
      onClose();
    } catch (error) {
      applyServerErrors<TransactionFormValues>(error, setError, [
        'type',
        'category',
        'amount',
        'currency',
        'date',
        'description',
      ]);
    }
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={editing ? 'Изменить операцию' : 'Новая операция'}
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
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Тип" error={errors.type?.message}>
            {(field) => (
              <Select {...field} {...register('type')}>
                {TRANSACTION_TYPE_VALUES.map((value) => (
                  <option key={value} value={value}>
                    {TRANSACTION_TYPE_LABELS[value]}
                  </option>
                ))}
              </Select>
            )}
          </Field>

          <Field label="Дата" error={errors.date?.message} required>
            {(field) => <Input {...field} {...register('date')} type="date" />}
          </Field>
        </div>

        <Field
          label="Категория"
          error={errors.category?.message}
          hint="Свободный текст. Категории группируются в сводке автоматически."
          required
        >
          {(field) => (
            <>
              <Input
                {...field}
                {...register('category')}
                list="category-suggestions"
                placeholder="Еда"
              />
              {/* datalist подсказывает, но не ограничивает: список категорий
                  у каждого свой, и запирать его в фиксированный набор значило бы
                  навязать чужую систему учёта. */}
              <datalist id="category-suggestions">
                {CATEGORY_SUGGESTIONS.map((category) => (
                  <option key={category} value={category} />
                ))}
              </datalist>
            </>
          )}
        </Field>

        <div className="grid gap-4 sm:grid-cols-[1fr_120px]">
          <Field label="Сумма" error={errors.amount?.message} required>
            {(field) => (
              <Input
                {...field}
                // valueAsNumber — иначе в схему приедет строка и z.number()
                // отклонит любое значение.
                {...register('amount', { valueAsNumber: true })}
                type="number"
                min={0}
                step="0.01"
                inputMode="decimal"
                placeholder="35000"
              />
            )}
          </Field>

          <Field label="Валюта" error={errors.currency?.message} required>
            {(field) => (
              <Input
                {...field}
                {...register('currency')}
                maxLength={3}
                placeholder="KZT"
                className="uppercase"
              />
            )}
          </Field>
        </div>

        <Field label="Описание" error={errors.description?.message}>
          {(field) => (
            <Textarea {...field} {...register('description')} rows={3} placeholder="Комментарий" />
          )}
        </Field>
      </form>
    </Modal>
  );
}
