import { Link } from 'react-router-dom';

import { ROUTES } from '@/router/routes';

export default function NotFoundPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-bg px-6 text-center">
      <p className="text-6xl font-semibold tracking-tight text-fg-subtle">404</p>
      <div>
        <h1 className="text-lg font-semibold">Страница не найдена</h1>
        <p className="mt-1 text-[13.5px] text-fg-muted">
          Такого адреса в LifeOS нет. Возможно, ссылка устарела.
        </p>
      </div>
      {/*
        Ссылка, оформленная как кнопка, а не <Button> с вложенным <Link>:
        переход должен оставаться настоящей ссылкой — работать по Ctrl+клик,
        показывать адрес в статусной строке и быть доступным для скринридера
        как ссылка, а не как кнопка.
      */}
      <Link
        to={ROUTES.dashboard}
        className="inline-flex h-9.5 items-center rounded-lg bg-accent px-4 text-sm font-medium text-accent-fg transition-colors hover:bg-accent-hover"
      >
        На главную
      </Link>
    </div>
  );
}
