import {
  BookOpen,
  Briefcase,
  HeartPulse,
  LayoutDashboard,
  ListChecks,
  Settings,
  Sparkles,
  Target,
  User,
  Wallet,
  type LucideIcon,
} from 'lucide-react';

/**
 * Маршруты приложения.
 *
 * Пути собраны в один объект, а не разбросаны строками по `<Link to="...">`:
 * опечатка в строке даёт неработающую ссылку без единой ошибки компиляции.
 * Здесь любое переименование маршрута ломает сборку в местах использования.
 */
export const ROUTES = {
  login: '/login',
  register: '/register',
  dashboard: '/',
  goals: '/goals',
  tasks: '/tasks',
  finance: '/finance',
  health: '/health',
  study: '/study',
  studyMaterial: (id = ':id') => `/study/${id}`,
  career: '/career',
  ai: '/ai',
  profile: '/profile',
  settings: '/settings',
} as const;

export interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Группа в боковом меню. */
  group: 'main' | 'modules' | 'account';
}

/**
 * Пункты навигации в порядке §13 архитектуры: сначала обзор, затем модули по
 * зависимостям (цели → задачи → финансы → здоровье → учёба → карьера → AI),
 * в конце личные настройки.
 *
 * Раздела «Администрирование» здесь нет сознательно: на бэкенде
 * `AdminController` не реализован. Пункт меню, ведущий на несуществующий API,
 * выглядел бы как готовая функция и провалился бы на первом нажатии.
 */
export const NAV_ITEMS: readonly NavItem[] = [
  { to: ROUTES.dashboard, label: 'Обзор', icon: LayoutDashboard, group: 'main' },
  { to: ROUTES.goals, label: 'Цели', icon: Target, group: 'modules' },
  { to: ROUTES.tasks, label: 'Задачи', icon: ListChecks, group: 'modules' },
  { to: ROUTES.finance, label: 'Финансы', icon: Wallet, group: 'modules' },
  { to: ROUTES.health, label: 'Здоровье', icon: HeartPulse, group: 'modules' },
  // Иконки не повторяются: одинаковый значок у «Учёбы» и «AI-ассистента»
  // заставлял бы читать подписи вместо того, чтобы узнавать пункт по виду.
  { to: ROUTES.study, label: 'Учёба', icon: BookOpen, group: 'modules' },
  { to: ROUTES.career, label: 'Карьера', icon: Briefcase, group: 'modules' },
  { to: ROUTES.ai, label: 'AI-ассистент', icon: Sparkles, group: 'modules' },
  { to: ROUTES.profile, label: 'Профиль', icon: User, group: 'account' },
  { to: ROUTES.settings, label: 'Настройки', icon: Settings, group: 'account' },
];
