/**
 * Зеркало DTO бэкенда `LifeOS.Application/DTO/**`.
 *
 * Типы написаны вручную, а не сгенерированы из OpenAPI. Обоснование:
 * генератор выдаёт тысячи строк с именами вида `PagedResponseOfGoalResponse`,
 * тянет за собой рантайм-клиент и требует запущенного бэкенда на каждой сборке.
 * Здесь же контракт читается глазами, комментируется и служит документацией
 * границы между сервисами. Цена — ручная синхронизация при изменении DTO;
 * это осознанный обмен, а не забывчивость.
 *
 * Соглашения, действующие для ВСЕХ типов ниже:
 *  - имена полей в camelCase — политика System.Text.Json по умолчанию (Web defaults);
 *  - enum'ы приходят строками (JsonStringEnumConverter в Program.cs);
 *  - C# `Guid`     → string (UUID);
 *  - C# `DateTime` → IsoDateTime (UTC, ISO 8601);
 *  - C# `DateOnly` → IsoDate («2026-08-17»), БЕЗ времени и часового пояса;
 *  - C# `decimal`  → number. Внимание: double теряет точность на очень больших
 *    суммах, но верхняя граница транзакции на бэкенде — 999 999 999,
 *    что укладывается в безопасный диапазон Number;
 *  - C# `T?`       → `T | null` (сервер присылает null, а не отсутствие ключа).
 */

import type {
  GoalStatus,
  ModuleType,
  MoodLevel,
  PriorityLevel,
  TransactionType,
  UserRole,
} from './enums';

/** Дата без времени: «2026-08-17». Соответствует C# DateOnly. */
export type IsoDate = string;

/** Момент времени в ISO 8601: «2026-08-17T09:30:00Z». Соответствует C# DateTime. */
export type IsoDateTime = string;

/** UUID. Соответствует C# Guid. */
export type Uuid = string;

// =========================================================================
// Общие обёртки
// =========================================================================

/** `LifeOS.Application.DTO.Common.PagedResponse<T>` */
export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/** Параметры пагинации. Бэкенд ограничивает pageSize сотней (PaginationParams). */
export interface PaginationParams {
  pageNumber?: number;
  pageSize?: number;
}

/** `LifeOS.Application.DTO.Ai.AiContributionResponse` */
export interface AiContribution {
  feature: string;
  value: number;
  impact: number;
}

/**
 * `LifeOS.Application.DTO.Ai.AiResultResponse<T>` — единая обёртка ВСЕХ ответов AI.
 *
 * `confidence` и `isConfident` проходят насквозь от FastAPI до этого места.
 * Требование MASTER_GUIDE «если AI не уверен — он сообщает об этом» реализуется
 * тем, что интерфейс обязан отрисовать `isConfident === false` иначе, чем
 * уверенный результат, а не тем, что бэкенд что-то скрывает.
 */
export interface AiResult<T> {
  result: T;
  confidence: number;
  isConfident: boolean;
  explanation: string;
  contributions: AiContribution[];
  modelVersion: string;
}

// =========================================================================
// Auth / Users
// =========================================================================

/** `DTO.Auth.UserResponse` */
export interface User {
  id: Uuid;
  name: string;
  surname: string;
  email: string;
  avatarUrl: string | null;
  role: UserRole;
  createdAt: IsoDateTime;
}

/** `DTO.Auth.AuthResponse` */
export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: IsoDateTime;
  user: User;
}

/** `DTO.Auth.RegisterRequest` */
export interface RegisterRequest {
  name: string;
  surname: string;
  email: string;
  password: string;
}

/** `DTO.Auth.LoginRequest` */
export interface LoginRequest {
  email: string;
  password: string;
}

/** `DTO.Auth.RefreshRequest` */
export interface RefreshRequest {
  refreshToken: string;
}

/** `DTO.Users.UpdateProfileRequest` */
export interface UpdateProfileRequest {
  name: string;
  surname: string;
}

/** `DTO.Users.ChangePasswordRequest` */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

/** Ответ `PUT /api/users/avatar` — анонимный объект `new { avatarUrl = url }`. */
export interface AvatarUploadResponse {
  avatarUrl: string;
}

// =========================================================================
// Goals
// =========================================================================

/** `DTO.Goals.GoalResponse` */
export interface Goal {
  id: Uuid;
  title: string;
  description: string | null;
  status: GoalStatus;
  priority: PriorityLevel;
  deadline: IsoDateTime | null;
  totalTasks: number;
  completedTasks: number;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

/** `DTO.Goals.CreateGoalRequest` и `UpdateGoalRequest` — совпадают по составу полей. */
export interface GoalPayload {
  title: string;
  description: string | null;
  status: GoalStatus;
  priority: PriorityLevel;
  deadline: IsoDateTime | null;
}

/** `DTO.Goals.GoalQueryParams` */
export interface GoalQuery extends PaginationParams {
  status?: GoalStatus;
  priority?: PriorityLevel;
  search?: string;
}

// =========================================================================
// Tasks
// =========================================================================

/** `DTO.Tasks.TaskResponse` */
export interface TaskItem {
  id: Uuid;
  goalId: Uuid | null;
  goalTitle: string | null;
  title: string;
  completed: boolean;
  deadline: IsoDateTime | null;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

/** `DTO.Tasks.CreateTaskRequest` */
export interface CreateTaskPayload {
  title: string;
  goalId: Uuid | null;
  deadline: IsoDateTime | null;
}

/** `DTO.Tasks.UpdateTaskRequest` — в отличие от создания содержит `completed`. */
export interface UpdateTaskPayload extends CreateTaskPayload {
  completed: boolean;
}

/** `DTO.Tasks.TaskQueryParams` */
export interface TaskQuery extends PaginationParams {
  completed?: boolean;
  goalId?: Uuid;
  dueBefore?: IsoDateTime;
  search?: string;
}

// =========================================================================
// Finance
// =========================================================================

/** `DTO.Finance.TransactionResponse` */
export interface Transaction {
  id: Uuid;
  type: TransactionType;
  category: string;
  amount: number;
  currency: string;
  date: IsoDate;
  description: string | null;
  createdAt: IsoDateTime;
}

/** `DTO.Finance.CreateTransactionRequest` и `UpdateTransactionRequest`. */
export interface TransactionPayload {
  type: TransactionType;
  category: string;
  amount: number;
  currency: string;
  date: IsoDate;
  description: string | null;
}

/** `DTO.Finance.TransactionQueryParams` */
export interface TransactionQuery extends PaginationParams {
  type?: TransactionType;
  category?: string;
  from?: IsoDate;
  to?: IsoDate;
}

/** `DTO.Finance.CategoryBreakdown` */
export interface CategoryBreakdown {
  type: TransactionType;
  category: string;
  amount: number;
  percentage: number;
}

/** `DTO.Finance.FinanceSummaryResponse` */
export interface FinanceSummary {
  from: IsoDate;
  to: IsoDate;
  currency: string;
  totalIncome: number;
  totalExpense: number;
  balance: number;
  transactionCount: number;
  byCategory: CategoryBreakdown[];
}

/** `DTO.Ai.FinanceForecastResponse` — тело `AiResult<T>` для `/finance/analysis`. */
export interface FinanceForecast {
  predictedExpense: number;
  predictedBalance: number;
  trend: string;
  topCategory: string | null;
  savingsRate: number;
  currency: string;
  monthsAnalyzed: number;
}

// =========================================================================
// Health
// =========================================================================

/** `DTO.Health.HealthLogResponse` */
export interface HealthLog {
  id: Uuid;
  date: IsoDate;
  weight: number | null;
  sleepHours: number | null;
  mood: MoodLevel;
  waterMl: number;
  steps: number;
  createdAt: IsoDateTime;
}

/** `DTO.Health.CreateHealthLogRequest` */
export interface CreateHealthLogPayload {
  date: IsoDate;
  weight: number | null;
  sleepHours: number | null;
  mood: MoodLevel;
  waterMl: number;
  steps: number;
}

/**
 * `DTO.Health.UpdateHealthLogRequest`.
 *
 * Поля `date` здесь НЕТ намеренно: дата входит в уникальный индекс
 * (UserId, Date) и на бэкенде не редактируется (ADR 38).
 */
export type UpdateHealthLogPayload = Omit<CreateHealthLogPayload, 'date'>;

/** `DTO.Health.HealthLogQueryParams` */
export interface HealthLogQuery extends PaginationParams {
  from?: IsoDate;
  to?: IsoDate;
}

/** `DTO.Ai.HealthAssessmentResponse` — тело `AiResult<T>` для `/health/analysis`. */
export interface HealthAssessment {
  wellbeingScore: number;
  /** Прогноз настроения числом 1..5 — шкала MoodLevel. */
  predictedMood: number;
  riskFactors: string[];
  recommendations: string[];
  daysAnalyzed: number;
}

// =========================================================================
// Files
// =========================================================================

/** `DTO.Files.FileResponse` */
export interface StoredFile {
  id: Uuid;
  fileName: string;
  url: string;
  contentType: string;
  sizeBytes: number;
  module: ModuleType;
  createdAt: IsoDateTime;
}

/** `DTO.Files.FileQueryParams` */
export interface FileQuery extends PaginationParams {
  module?: ModuleType;
}

// =========================================================================
// Study
// =========================================================================

/** `DTO.Study.StudyMaterialResponse` */
export interface StudyMaterial {
  id: Uuid;
  fileId: Uuid;
  title: string;
  summary: string | null;
  fileName: string;
  fileUrl: string;
  notesCount: number;
  quizzesCount: number;
  createdAt: IsoDateTime;
}

/** `DTO.Study.CreateStudyMaterialRequest` */
export interface CreateStudyMaterialPayload {
  fileId: Uuid;
  title: string;
}

/** `Interfaces.Services.StudySummaryResult` — тело `AiResult<T>` для конспекта. */
export interface StudySummary {
  summary: string;
  keyPoints: string[];
  /** «llm» | «extractive» | «unavailable» — какой алгоритм отработал. */
  source: string;
}

/** `DTO.Study.StudyNoteResponse` */
export interface StudyNote {
  id: Uuid;
  studyMaterialId: Uuid;
  content: string;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

/** `DTO.Study.CreateStudyNoteRequest` */
export interface CreateStudyNotePayload {
  studyMaterialId: Uuid;
  content: string;
}

/**
 * `DTO.Study.QuizQuestionResponse`.
 *
 * Поля `correctIndex` здесь НЕТ, и это не упущение: правильные ответы не
 * покидают сервер, иначе тест решался бы через DevTools (ADR 72).
 * Правильный вариант становится известен только из `QuizGrade.results`
 * после отправки ответов.
 */
export interface QuizQuestion {
  question: string;
  options: string[];
  explanation: string;
}

/** `DTO.Study.QuizResponse` */
export interface Quiz {
  id: Uuid;
  studyMaterialId: Uuid;
  questions: QuizQuestion[];
  totalQuestions: number;
  score: number | null;
  createdAt: IsoDateTime;
}

/** `DTO.Study.GenerateQuizRequest` */
export interface GenerateQuizPayload {
  studyMaterialId: Uuid;
  /** Бэкенд принимает 1..15 (GenerateQuizRequestValidator). */
  questionCount: number;
}

/** `DTO.Study.QuizAnswerResult` */
export interface QuizAnswerResult {
  questionIndex: number;
  submittedIndex: number;
  correctIndex: number;
  isCorrect: boolean;
  explanation: string;
}

/** `DTO.Study.QuizGradeResponse` */
export interface QuizGrade {
  quizId: Uuid;
  score: number;
  totalQuestions: number;
  results: QuizAnswerResult[];
}

// =========================================================================
// Career
// =========================================================================

/** `DTO.Career.CareerProfileResponse` */
export interface CareerProfile {
  id: Uuid;
  resumeFileId: Uuid | null;
  resumeFileName: string | null;
  skills: string | null;
  desiredPosition: string | null;
  aiReview: string | null;
  updatedAt: IsoDateTime;
}

/** `DTO.Career.UpdateCareerProfileRequest` */
export interface UpdateCareerProfilePayload {
  skills: string | null;
  desiredPosition: string | null;
  resumeFileId: Uuid | null;
}

/** `DTO.Career.ResumeAnalysisResponse` — тело `AiResult<T>` для разбора резюме. */
export interface ResumeAnalysis {
  overallScore: number;
  strengths: string[];
  weaknesses: string[];
  missingSkills: string[];
  suggestions: string[];
  source: string;
}

// =========================================================================
// Recommendations / AI history
// =========================================================================

/** `DTO.Ai.RecommendationResponse` */
export interface Recommendation {
  id: Uuid;
  module: ModuleType;
  content: string;
  confidence: number;
  createdAt: IsoDateTime;
}

/**
 * `DTO.Ai.AiHistoryResponse`.
 *
 * Payload запроса и ответа наружу не отдаётся — в нём могут быть фрагменты
 * личных документов (ADR 74). Поэтому в истории только эндпоинт и уверенность.
 */
export interface AiHistoryEntry {
  id: Uuid;
  endpoint: string;
  confidence: number | null;
  createdAt: IsoDateTime;
}

// =========================================================================
// Dashboard
// =========================================================================

/** `DTO.Dashboard.DashboardPeriod` */
export interface DashboardPeriod {
  from: IsoDate;
  to: IsoDate;
  days: number;
}

/** `DTO.Dashboard.GoalProgressItem` */
export interface GoalProgressItem {
  id: Uuid;
  title: string;
  status: GoalStatus;
  priority: PriorityLevel;
  deadline: IsoDateTime | null;
  totalTasks: number;
  completedTasks: number;
  progress: number;
  isOverdue: boolean;
}

/** `DTO.Dashboard.GoalsWidget` */
export interface GoalsWidget {
  total: number;
  notStarted: number;
  inProgress: number;
  completed: number;
  cancelled: number;
  /** Отменённые цели исключены из знаменателя — отмена не провал (ADR 82). */
  completionRate: number;
  overdueCount: number;
  upcoming: GoalProgressItem[];
}

/** `DTO.Dashboard.TaskItemBrief` */
export interface TaskItemBrief {
  id: Uuid;
  title: string;
  deadline: IsoDateTime | null;
  goalTitle: string | null;
  isOverdue: boolean;
}

/** `DTO.Dashboard.TasksWidget` */
export interface TasksWidget {
  total: number;
  completed: number;
  pending: number;
  overdueCount: number;
  dueTodayCount: number;
  dueThisWeekCount: number;
  completionRate: number;
  urgent: TaskItemBrief[];
}

/** `DTO.Dashboard.CategoryShare` */
export interface CategoryShare {
  category: string;
  amount: number;
  percentage: number;
}

/** `DTO.Dashboard.MonthlyPoint` */
export interface MonthlyPoint {
  /** Формат «2026-08» — приходит строкой, готовой к выводу на ось графика. */
  month: string;
  income: number;
  expense: number;
}

/** `DTO.Dashboard.FinanceWidget` */
export interface FinanceWidget {
  /** Самая частая валюта пользователя: смешивание валют исключено (ADR 83). */
  currency: string;
  totalIncome: number;
  totalExpense: number;
  balance: number;
  savingsRate: number;
  transactionCount: number;
  topExpenseCategories: CategoryShare[];
  /** Тренд всегда за 6 месяцев, независимо от параметра days (ADR 84). */
  monthlyTrend: MonthlyPoint[];
}

/** `DTO.Dashboard.HealthPoint` */
export interface HealthPoint {
  date: IsoDate;
  sleepHours: number | null;
  steps: number;
  waterMl: number;
  /** Настроение числом 1..5. */
  mood: number;
}

/** `DTO.Dashboard.HealthWidget` */
export interface HealthWidget {
  entriesCount: number;
  averageSleepHours: number | null;
  averageSteps: number;
  averageWaterMl: number;
  latestWeight: number | null;
  weightChange: number | null;
  averageMood: number;
  trend: HealthPoint[];
}

/** `DTO.Dashboard.StudyWidget` */
export interface StudyWidget {
  materialsCount: number;
  summarizedCount: number;
  notesCount: number;
  quizzesCount: number;
  completedQuizzesCount: number;
  averageQuizScore: number | null;
}

/** `DTO.Dashboard.CareerWidget` */
export interface CareerWidget {
  hasResume: boolean;
  desiredPosition: string | null;
  hasAiReview: boolean;
}

/** `DTO.Dashboard.RecentFileItem` */
export interface RecentFileItem {
  id: Uuid;
  fileName: string;
  url: string;
  module: ModuleType;
  sizeBytes: number;
  createdAt: IsoDateTime;
}

/**
 * `DTO.Dashboard.DashboardResponse` — один запрос на весь главный экран.
 *
 * Восемь виджетов приходят одним ответом вместо восьми запросов: восемь
 * запросов означали бы восемь TLS-рукопожатий и восемь проверок JWT (ADR 79).
 * Вызовов AI здесь нет — экран обязан открываться мгновенно (ADR 81).
 */
export interface DashboardData {
  period: DashboardPeriod;
  goals: GoalsWidget;
  tasks: TasksWidget;
  finance: FinanceWidget;
  health: HealthWidget;
  study: StudyWidget;
  career: CareerWidget;
  recommendations: Recommendation[];
  recentFiles: RecentFileItem[];
  generatedAt: IsoDateTime;
}
