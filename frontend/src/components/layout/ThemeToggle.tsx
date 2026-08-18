import { Moon, Sun } from 'lucide-react';

import { Button } from '@/components/ui';
import { useTheme } from '@/hooks/useTheme';

export function ThemeToggle() {
  const { theme, toggle } = useTheme();
  const nextTheme = theme === 'dark' ? 'светлую' : 'тёмную';

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={toggle}
      aria-label={`Переключить на ${nextTheme} тему`}
      title={`Переключить на ${nextTheme} тему`}
    >
      {theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />}
    </Button>
  );
}
