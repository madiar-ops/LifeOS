import { QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';

import { Toaster } from '@/components/ui';
import { AuthProvider } from '@/contexts/AuthContext';
import { ThemeProvider } from '@/contexts/ThemeContext';
import { queryClient } from '@/lib/queryClient';
import { AppRouter } from '@/router/AppRouter';

/**
 * Корень приложения.
 *
 * Порядок провайдеров не произволен:
 *  1. QueryClientProvider — самый внешний, потому что AuthProvider хранит
 *     данные пользователя в кэше запросов и без клиента работать не может;
 *  2. ThemeProvider — независим от остальных;
 *  3. AuthProvider — внутри роутера НЕ размещён специально: контекст
 *     аутентификации нужен самому маршрутизатору для выбора ветки маршрутов;
 *  4. BrowserRouter — снаружи AuthProvider, поскольку провайдеру может
 *     потребоваться навигация, а компонентам внутри — оба контекста.
 */
export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <BrowserRouter>
          <AuthProvider>
            <AppRouter />
            <Toaster />
          </AuthProvider>
        </BrowserRouter>
      </ThemeProvider>
    </QueryClientProvider>
  );
}
