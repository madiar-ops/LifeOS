import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './App';
import './index.css';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('Элемент #root не найден в index.html.');
}

createRoot(container).render(
  // StrictMode оставлен включённым сознательно: он дважды вызывает эффекты в
  // разработке и тем самым вскрывает подписки без отписки — например,
  // забытый возврат функции очистки в onSessionExpired.
  <StrictMode>
    <App />
  </StrictMode>,
);
