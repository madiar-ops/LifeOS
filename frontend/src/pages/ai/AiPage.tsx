import { History, Sparkles } from 'lucide-react';
import { useState } from 'react';

import { RecommendationList } from '@/components/ai/RecommendationList';
import { PageShell } from '@/components/layout/PageShell';
import {
  Badge,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  ErrorState,
  Pagination,
  Select,
  SkeletonRows,
} from '@/components/ui';
import { useAiHistory, useDismissRecommendation, useRecommendations } from '@/hooks/useAi';
import { formatDateTime, formatPercent } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import { MODULE_LABELS, MODULE_VALUES, type ModuleType } from '@/types/enums';

export default function AiPage() {
  const [recPage, setRecPage] = useState(1);
  const [historyPage, setHistoryPage] = useState(1);
  const [module, setModule] = useState<ModuleType | ''>('');
  const [dismissingId, setDismissingId] = useState<string | null>(null);

  const recommendations = useRecommendations({
    pageNumber: recPage,
    pageSize: 10,
    ...(module !== '' ? { module } : {}),
  });
  const history = useAiHistory({ pageNumber: historyPage, pageSize: 15 });
  const dismiss = useDismissRecommendation();

  const onDismiss = async (id: string) => {
    setDismissingId(id);
    try {
      await dismiss.mutateAsync(id);
      toast.success('Рекомендация скрыта');
    } catch {
      /* уведомление показал глобальный обработчик */
    } finally {
      setDismissingId(null);
    }
  };

  return (
    <PageShell
      title="AI-ассистент"
      description="Лента рекомендаций и аудит обращений к модели"
    >
      {/* ---- Рекомендации ------------------------------------------------ */}
      <Card>
        <CardHeader
          icon={<Sparkles size={15} />}
          title="Рекомендации"
          description="В ленту попадают только выводы, прошедшие порог уверенности"
          actions={
            <Select
              value={module}
              onChange={(event) => {
                setModule(event.target.value as ModuleType | '');
                setRecPage(1);
              }}
              className="w-40"
              aria-label="Фильтр по модулю"
            >
              <option value="">Все модули</option>
              {MODULE_VALUES.filter((value) => value !== 'Avatar').map((value) => (
                <option key={value} value={value}>
                  {MODULE_LABELS[value]}
                </option>
              ))}
            </Select>
          }
        />
        <CardBody>
          {recommendations.isPending ? (
            <SkeletonRows rows={4} />
          ) : recommendations.isError ? (
            <ErrorState
              error={recommendations.error}
              onRetry={() => void recommendations.refetch()}
            />
          ) : (
            <RecommendationList
              items={recommendations.data.items}
              onDismiss={(id) => void onDismiss(id)}
              dismissingId={dismissingId}
            />
          )}
        </CardBody>
        {recommendations.data !== undefined && (
          <Pagination page={recommendations.data} onChange={setRecPage} />
        )}
      </Card>

      {/* ---- История ----------------------------------------------------- */}
      <Card>
        <CardHeader
          icon={<History size={15} />}
          title="История обращений к AI"
          // Прямое объяснение, почему в таблице нет содержимого запросов.
          description="Тексты запросов и ответов не хранятся: в них попадают фрагменты личных документов"
        />

        {history.isPending ? (
          <div className="p-5">
            <SkeletonRows rows={5} />
          </div>
        ) : history.isError ? (
          <ErrorState error={history.error} onRetry={() => void history.refetch()} />
        ) : history.data.items.length === 0 ? (
          <EmptyState
            icon={<History size={20} />}
            title="Обращений к AI ещё не было"
            description="Запусти анализ финансов, здоровья, конспект материала или разбор резюме."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-[13px]">
              <thead className="border-b border-line text-[11.5px] tracking-wide text-fg-subtle uppercase">
                <tr>
                  <th scope="col" className="px-5 py-2.5 font-medium">Эндпоинт</th>
                  <th scope="col" className="px-5 py-2.5 font-medium">Уверенность</th>
                  <th scope="col" className="px-5 py-2.5 font-medium">Время</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {history.data.items.map((entry) => (
                  <tr key={entry.id} className="transition-colors hover:bg-surface-2">
                    <td className="px-5 py-2.5">
                      <code className="rounded bg-surface-3 px-1.5 py-0.5 font-mono text-[12px] text-fg">
                        {entry.endpoint}
                      </code>
                    </td>
                    <td className="px-5 py-2.5">
                      {entry.confidence === null ? (
                        <span className="text-fg-subtle">—</span>
                      ) : (
                        <Badge tone={entry.confidence >= 0.6 ? 'success' : 'warning'}>
                          {formatPercent(entry.confidence, false)}
                        </Badge>
                      )}
                    </td>
                    <td className="tabular px-5 py-2.5 whitespace-nowrap text-fg-muted">
                      {formatDateTime(entry.createdAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {history.data !== undefined && (
          <Pagination page={history.data} onChange={setHistoryPage} />
        )}
      </Card>
    </PageShell>
  );
}
