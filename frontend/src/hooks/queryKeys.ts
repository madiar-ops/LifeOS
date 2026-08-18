import type {
  FileQuery,
  GoalQuery,
  HealthLogQuery,
  PaginationParams,
  TaskQuery,
  TransactionQuery,
  Uuid,
} from '@/types/api';
import type { ModuleType } from '@/types/enums';

/**
 * Ключи кэша, собранные в одном месте.
 *
 * Иначе инвалидация превращается в поиск строковых литералов по проекту:
 * запрос заведён с ключом `['goals', query]`, а мутация сбрасывает
 * `['goal']` — кэш не обновляется, и баг проявляется только через минуту,
 * когда данные сами устареют. Здесь опечатка — это ошибка компиляции.
 *
 * Иерархия ключей важна: `queryClient.invalidateQueries({ queryKey: ['goals'] })`
 * сбрасывает все списки целей с любыми фильтрами, потому что все они начинаются
 * с одного корня.
 */
export const queryKeys = {
  currentUser: ['auth', 'me'] as const,

  goals: {
    all: ['goals'] as const,
    list: (query: GoalQuery) => ['goals', 'list', query] as const,
    detail: (id: Uuid) => ['goals', 'detail', id] as const,
  },

  tasks: {
    all: ['tasks'] as const,
    list: (query: TaskQuery) => ['tasks', 'list', query] as const,
    detail: (id: Uuid) => ['tasks', 'detail', id] as const,
  },

  finance: {
    all: ['finance'] as const,
    transactions: (query: TransactionQuery) => ['finance', 'transactions', query] as const,
    summary: (params: { from?: string; to?: string; currency?: string }) =>
      ['finance', 'summary', params] as const,
    analysis: (params: { monthsBack?: number; currency?: string }) =>
      ['finance', 'analysis', params] as const,
  },

  health: {
    all: ['health'] as const,
    logs: (query: HealthLogQuery) => ['health', 'logs', query] as const,
    analysis: (params: { daysBack?: number }) => ['health', 'analysis', params] as const,
  },

  files: {
    all: ['files'] as const,
    list: (query: FileQuery) => ['files', 'list', query] as const,
  },

  study: {
    all: ['study'] as const,
    materials: (query: PaginationParams) => ['study', 'materials', query] as const,
    material: (id: Uuid) => ['study', 'material', id] as const,
    notes: (materialId: Uuid) => ['study', 'notes', materialId] as const,
    quiz: (id: Uuid) => ['study', 'quiz', id] as const,
  },

  career: {
    all: ['career'] as const,
    profile: ['career', 'profile'] as const,
  },

  ai: {
    all: ['ai'] as const,
    recommendations: (query: PaginationParams & { module?: ModuleType }) =>
      ['ai', 'recommendations', query] as const,
    history: (query: PaginationParams) => ['ai', 'history', query] as const,
  },

  dashboard: {
    all: ['dashboard'] as const,
    byDays: (days: number) => ['dashboard', days] as const,
  },

  ping: ['ping'] as const,
};
