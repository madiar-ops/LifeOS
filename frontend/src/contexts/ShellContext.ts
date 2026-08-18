import { createContext, useContext } from 'react';

interface ShellContextValue {
  openMenu: () => void;
}

/**
 * Связь страницы с оболочкой приложения.
 *
 * Состояние мобильного меню принадлежит `DashboardLayout`, а кнопка «гамбургер»
 * живёт в шапке страницы. Контекст передаёт только функцию открытия — иначе
 * пришлось бы прокидывать её пропсами через каждый экран, а страницы не должны
 * ничего знать о боковом меню.
 */
export const ShellContext = createContext<ShellContextValue>({
  openMenu: () => {
    /* вне оболочки открывать нечего */
  },
});

export function useShell(): ShellContextValue {
  return useContext(ShellContext);
}
