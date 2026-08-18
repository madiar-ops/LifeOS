import { lazy, Suspense } from 'react';
import { Route, Routes } from 'react-router-dom';

import { FullPageSpinner } from '@/components/ui';
import { AuthLayout } from '@/layouts/AuthLayout';
import { DashboardLayout } from '@/layouts/DashboardLayout';

import { GuestRoute, ProtectedRoute } from './ProtectedRoute';
import { ROUTES } from './routes';

/**
 * Маршруты приложения.
 *
 * Экраны загружаются через `lazy`: recharts со всеми графиками весит больше,
 * чем всё остальное приложение, и тянуть его на страницу входа бессмысленно.
 * Разделение по маршрутам даёт быстрый первый экран — прямое требование
 * «быстрый интерфейс» из DESIGN PHILOSOPHY.
 */
const LoginPage = lazy(() => import('@/pages/auth/LoginPage'));
const RegisterPage = lazy(() => import('@/pages/auth/RegisterPage'));
const DashboardPage = lazy(() => import('@/pages/dashboard/DashboardPage'));
const GoalsPage = lazy(() => import('@/pages/goals/GoalsPage'));
const TasksPage = lazy(() => import('@/pages/tasks/TasksPage'));
const FinancePage = lazy(() => import('@/pages/finance/FinancePage'));
const HealthPage = lazy(() => import('@/pages/health/HealthPage'));
const StudyPage = lazy(() => import('@/pages/study/StudyPage'));
const StudyMaterialPage = lazy(() => import('@/pages/study/StudyMaterialPage'));
const CareerPage = lazy(() => import('@/pages/career/CareerPage'));
const AiPage = lazy(() => import('@/pages/ai/AiPage'));
const ProfilePage = lazy(() => import('@/pages/profile/ProfilePage'));
const SettingsPage = lazy(() => import('@/pages/settings/SettingsPage'));
const NotFoundPage = lazy(() => import('@/pages/NotFoundPage'));

export function AppRouter() {
  return (
    <Suspense fallback={<FullPageSpinner label="Загружаем экран" />}>
      <Routes>
        {/* Публичная зона: вошедшего пользователя разворачиваем на дашборд. */}
        <Route element={<GuestRoute />}>
          <Route element={<AuthLayout />}>
            <Route path={ROUTES.login} element={<LoginPage />} />
            <Route path={ROUTES.register} element={<RegisterPage />} />
          </Route>
        </Route>

        {/* Приватная зона. */}
        <Route element={<ProtectedRoute />}>
          <Route element={<DashboardLayout />}>
            <Route index element={<DashboardPage />} />
            <Route path={ROUTES.goals} element={<GoalsPage />} />
            <Route path={ROUTES.tasks} element={<TasksPage />} />
            <Route path={ROUTES.finance} element={<FinancePage />} />
            <Route path={ROUTES.health} element={<HealthPage />} />
            <Route path={ROUTES.study} element={<StudyPage />} />
            <Route path={ROUTES.studyMaterial()} element={<StudyMaterialPage />} />
            <Route path={ROUTES.career} element={<CareerPage />} />
            <Route path={ROUTES.ai} element={<AiPage />} />
            <Route path={ROUTES.profile} element={<ProfilePage />} />
            <Route path={ROUTES.settings} element={<SettingsPage />} />
          </Route>
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  );
}
