import { ArrowDownRight, ArrowUpRight, Pencil, Plus, Sparkles, Trash2, Wallet } from 'lucide-react';
import { useState } from 'react';

import { AiResultCard } from '@/components/ai/AiResultCard';
import { AiStateNotice } from '@/components/ai/AiStateNotice';
import { CategoryDonutChart } from '@/components/charts/CategoryDonutChart';
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
  Input,
  Pagination,
  SegmentedControl,
  Select,
  Skeleton,
  SkeletonRows,
  StatTile,
} from '@/components/ui';
import {
  useDeleteTransaction,
  useFinanceAnalysis,
  useFinanceSummary,
  useTransactions,
} from '@/hooks/useFinance';
import { cn } from '@/lib/cn';
import { firstDayOfMonthIso, formatDate, formatMoney, formatNumber, formatPercent, todayIsoDate } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import type { Transaction, TransactionQuery } from '@/types/api';
import { TRANSACTION_TYPE_LABELS, TRANSACTION_TYPE_VALUES, type TransactionType } from '@/types/enums';

import { TransactionFormModal } from './TransactionFormModal';

const MONTHS_OPTIONS = [
  { value: 3, label: '3 мес' },
  { value: 6, label: '6 мес' },
  { value: 12, label: '12 мес' },
] as const;

/** Человеческая формулировка для поля `trend`, которое AI-сервис отдаёт строкой. */
const TREND_LABELS: Record<string, string> = {
  up: 'расходы растут',
  down: 'расходы снижаются',
  stable: 'расходы стабильны',
  increasing: 'расходы растут',
  decreasing: 'расходы снижаются',
};

export default function FinancePage() {
  const [pageNumber, setPageNumber] = useState(1);
  const [type, setType] = useState<TransactionType | ''>('');
  const [from, setFrom] = useState(firstDayOfMonthIso());
  const [to, setTo] = useState(todayIsoDate());

  const [editing, setEditing] = useState<Transaction | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [toDelete, setToDelete] = useState<Transaction | null>(null);

  // Анализ включается по кнопке: каждый вызов идёт в FastAPI, пишется в
  // AIHistory и может создать рекомендацию.
  const [analysisEnabled, setAnalysisEnabled] = useState(false);
  const [monthsBack, setMonthsBack] = useState<number>(6);

  const query: TransactionQuery = {
    pageNumber,
    pageSize: 20,
    from,
    to,
    ...(type !== '' ? { type } : {}),
  };

  const transactions = useTransactions(query);
  const summary = useFinanceSummary({ from, to });
  const analysis = useFinanceAnalysis({ monthsBack }, analysisEnabled);
  const deleteTransaction = useDeleteTransaction();

  const currency = summary.data?.currency ?? 'KZT';
  const expenseCategories =
    summary.data?.byCategory.filter((item) => item.type === 'Expense') ?? [];

  const confirmDelete = async () => {
    if (toDelete === null) return;
    try {
      await deleteTransaction.mutateAsync(toDelete.id);
      toast.success('Операция удалена');
      setToDelete(null);
    } catch {
      /* уведомление показал глобальный обработчик */
    }
  };

  return (
    <PageShell
      title="Финансы"
      description="Доходы и расходы в одной таблице, прогноз — по запросу"
      actions={
        <Button
          variant="primary"
          leftIcon={<Plus size={15} />}
          onClick={() => {
            setEditing(null);
            setFormOpen(true);
          }}
        >
          Операция
        </Button>
      }
    >
      {/* ---- Сводка за период ------------------------------------------- */}
      {summary.isPending ? (
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-28 rounded-card" />
          ))}
        </div>
      ) : summary.isError ? (
        <ErrorState error={summary.error} onRetry={() => void summary.refetch()} />
      ) : (
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatTile
            label="Доходы"
            value={formatMoney(summary.data.totalIncome, currency)}
            icon={<ArrowUpRight size={15} />}
          />
          <StatTile
            label="Расходы"
            value={formatMoney(summary.data.totalExpense, currency)}
            icon={<ArrowDownRight size={15} />}
          />
          <StatTile
            label="Баланс"
            value={formatMoney(summary.data.balance, currency)}
            hint={`${formatNumber(summary.data.transactionCount)} операций`}
            icon={<Wallet size={15} />}
          />
          <StatTile
            label="Норма сбережений"
            value={
              summary.data.totalIncome === 0
                ? '—'
                : formatPercent((summary.data.balance / summary.data.totalIncome) * 100)
            }
            hint={`${formatDate(summary.data.from)} — ${formatDate(summary.data.to)}`}
          />
        </div>
      )}

      {/* ---- Структура расходов ----------------------------------------- */}
      {expenseCategories.length > 0 && (
        <Card>
          <CardHeader
            icon={<Wallet size={15} />}
            title="Структура расходов"
            description="Доли считаются в рамках одной валюты — конвертации курсов в MVP нет"
          />
          <CardBody>
            <CategoryDonutChart data={expenseCategories} currency={currency} />
          </CardBody>
        </Card>
      )}

      {/* ---- AI-прогноз -------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<Sparkles size={15} />}
          title="Прогноз расходов"
          description="В AI-сервис уходят только помесячные итоги, отдельных операций он не видит"
          actions={
            <div className="flex items-center gap-2">
              <SegmentedControl
                options={MONTHS_OPTIONS}
                value={monthsBack}
                onChange={setMonthsBack}
                ariaLabel="Глубина истории для прогноза"
              />
              <Button
                variant="primary"
                size="sm"
                loading={analysis.isFetching}
                onClick={() => {
                  setAnalysisEnabled(true);
                  if (analysisEnabled) void analysis.refetch();
                }}
              >
                {analysisEnabled ? 'Пересчитать' : 'Построить прогноз'}
              </Button>
            </div>
          }
        />
        <CardBody>
          {!analysisEnabled ? (
            <p className="text-[13px] leading-relaxed text-fg-muted">
              Прогноз не строится автоматически: каждый расчёт обращается к модели, попадает
              в историю AI и может создать рекомендацию. Нажми «Построить прогноз», когда он
              нужен.
            </p>
          ) : analysis.isPending ? (
            <SkeletonRows rows={3} />
          ) : analysis.isError ? (
            <AiStateNotice error={analysis.error} onRetry={() => void analysis.refetch()} />
          ) : (
            <AiResultCard title="Прогноз на следующий месяц" envelope={analysis.data}>
              <div className="grid gap-4 sm:grid-cols-3">
                <div>
                  <p className="text-[12px] text-fg-muted">Ожидаемые расходы</p>
                  <p className="tabular mt-0.5 text-xl font-semibold text-danger">
                    {formatMoney(analysis.data.result.predictedExpense, analysis.data.result.currency)}
                  </p>
                </div>
                <div>
                  <p className="text-[12px] text-fg-muted">Ожидаемый баланс</p>
                  <p
                    className={cn(
                      'tabular mt-0.5 text-xl font-semibold',
                      analysis.data.result.predictedBalance < 0 ? 'text-danger' : 'text-success',
                    )}
                  >
                    {formatMoney(analysis.data.result.predictedBalance, analysis.data.result.currency)}
                  </p>
                </div>
                <div>
                  <p className="text-[12px] text-fg-muted">Норма сбережений</p>
                  <p className="tabular mt-0.5 text-xl font-semibold">
                    {formatPercent(analysis.data.result.savingsRate, false)}
                  </p>
                </div>
              </div>

              <div className="flex flex-wrap gap-1.5">
                <Badge tone="info">
                  {TREND_LABELS[analysis.data.result.trend.toLowerCase()] ??
                    `тренд: ${analysis.data.result.trend}`}
                </Badge>
                {analysis.data.result.topCategory !== null && (
                  <Badge tone="warning">
                    крупнейшая категория: {analysis.data.result.topCategory}
                  </Badge>
                )}
                <Badge tone="neutral">
                  проанализировано месяцев: {analysis.data.result.monthsAnalyzed}
                </Badge>
              </div>
            </AiResultCard>
          )}
        </CardBody>
      </Card>

      {/* ---- Таблица операций ------------------------------------------- */}
      <Card>
        <div className="flex flex-wrap items-end gap-2 border-b border-line p-4">
          <label className="text-[12.5px] text-fg-muted">
            С
            <Input
              type="date"
              value={from}
              onChange={(event) => {
                setFrom(event.target.value);
                setPageNumber(1);
              }}
              className="mt-1 w-full sm:w-40"
            />
          </label>
          <label className="text-[12.5px] text-fg-muted">
            По
            <Input
              type="date"
              value={to}
              onChange={(event) => {
                setTo(event.target.value);
                setPageNumber(1);
              }}
              className="mt-1 w-full sm:w-40"
            />
          </label>
          <Select
            value={type}
            onChange={(event) => {
              setType(event.target.value as TransactionType | '');
              setPageNumber(1);
            }}
            className="w-full sm:w-40"
            aria-label="Фильтр по типу операции"
          >
            <option value="">Все операции</option>
            {TRANSACTION_TYPE_VALUES.map((value) => (
              <option key={value} value={value}>
                {TRANSACTION_TYPE_LABELS[value]}
              </option>
            ))}
          </Select>
        </div>

        {transactions.isPending ? (
          <div className="p-4">
            <SkeletonRows rows={7} />
          </div>
        ) : transactions.isError ? (
          <ErrorState error={transactions.error} onRetry={() => void transactions.refetch()} />
        ) : transactions.data.items.length === 0 ? (
          <EmptyState
            icon={<Wallet size={20} />}
            title="Операций за период нет"
            description="Измени диапазон дат или добавь доход либо расход."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-[13px]">
              <thead className="border-b border-line text-[11.5px] tracking-wide text-fg-subtle uppercase">
                <tr>
                  <th scope="col" className="px-4 py-2.5 font-medium">Дата</th>
                  <th scope="col" className="px-4 py-2.5 font-medium">Категория</th>
                  <th scope="col" className="px-4 py-2.5 font-medium">Описание</th>
                  <th scope="col" className="px-4 py-2.5 text-right font-medium">Сумма</th>
                  <th scope="col" className="px-4 py-2.5">
                    <span className="sr-only">Действия</span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {transactions.data.items.map((item) => (
                  <tr key={item.id} className="transition-colors hover:bg-surface-2">
                    <td className="tabular px-4 py-2.5 whitespace-nowrap text-fg-muted">
                      {formatDate(item.date)}
                    </td>
                    <td className="px-4 py-2.5">
                      <Badge tone={item.type === 'Income' ? 'success' : 'danger'} dot>
                        {item.category}
                      </Badge>
                    </td>
                    <td className="max-w-[16rem] truncate px-4 py-2.5 text-fg-muted">
                      {item.description ?? '—'}
                    </td>
                    <td
                      className={cn(
                        'tabular px-4 py-2.5 text-right font-medium whitespace-nowrap',
                        item.type === 'Income' ? 'text-success' : 'text-danger',
                      )}
                    >
                      {item.type === 'Income' ? '+' : '−'}
                      {formatMoney(item.amount, item.currency)}
                    </td>
                    <td className="px-4 py-2.5">
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label="Изменить операцию"
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
                          aria-label="Удалить операцию"
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

        {transactions.data !== undefined && (
          <Pagination page={transactions.data} onChange={setPageNumber} />
        )}
      </Card>

      <TransactionFormModal
        open={formOpen}
        transaction={editing}
        onClose={() => setFormOpen(false)}
      />

      <ConfirmDialog
        open={toDelete !== null}
        title="Удалить операцию?"
        message={
          toDelete === null
            ? ''
            : `${TRANSACTION_TYPE_LABELS[toDelete.type]} «${toDelete.category}» на ${formatMoney(toDelete.amount, toDelete.currency)} будет удалён безвозвратно.`
        }
        loading={deleteTransaction.isPending}
        onConfirm={() => void confirmDelete()}
        onCancel={() => setToDelete(null)}
      />
    </PageShell>
  );
}
