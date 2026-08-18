/**
 * Шина уведомлений.
 *
 * Всплывающие сообщения нужны не только компонентам, но и слоям, которые о
 * React ничего не знают, — например, глобальному обработчику ошибок мутаций в
 * queryClient. Если бы `toast` был доступен только через хук, такой обработчик
 * пришлось бы поднимать в дерево компонентов и передавать вниз пропсами.
 *
 * Здесь toast — обычная функция, а провайдер лишь подписывается на события и
 * отвечает за отрисовку.
 */

export type ToastKind = 'success' | 'error' | 'info';

export interface ToastMessage {
  id: string;
  kind: ToastKind;
  title: string;
  description?: string | undefined;
}

type Listener = (message: ToastMessage) => void;

const listeners = new Set<Listener>();

export function subscribeToToasts(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function emit(kind: ToastKind, title: string, description?: string): void {
  const message: ToastMessage = {
    id: `${String(Date.now())}-${Math.random().toString(36).slice(2, 8)}`,
    kind,
    title,
    description,
  };
  for (const listener of listeners) listener(message);
}

export const toast = {
  success: (title: string, description?: string) => emit('success', title, description),
  error: (title: string, description?: string) => emit('error', title, description),
  info: (title: string, description?: string) => emit('info', title, description),
};
