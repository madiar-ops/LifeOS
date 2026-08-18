import { Outlet } from 'react-router-dom';

import { ThemeToggle } from '@/components/layout/ThemeToggle';

const HIGHLIGHTS = [
  {
    title: 'Один агрегирующий запрос',
    text: 'Главный экран собирается одним обращением к API — восемь виджетов приходят вместе.',
  },
  {
    title: 'AI сообщает, когда не уверен',
    text: 'Каждый вывод модели несёт оценку уверенности и объяснение, а не только результат.',
  },
  {
    title: 'Refresh-токены с ротацией',
    text: 'Повторное использование токена трактуется как кража и отзывает всю цепочку.',
  },
];

/**
 * Оболочка страниц входа и регистрации.
 *
 * Две колонки: форма и краткое описание системы. Правая колонка скрывается на
 * узких экранах — на телефоне важна форма, а не презентация.
 */
export function AuthLayout() {
  return (
    <div className="grid min-h-screen bg-bg lg:grid-cols-2">
      <div className="relative flex flex-col justify-center px-5 py-10 sm:px-10">
        <div className="absolute top-4 right-4">
          <ThemeToggle />
        </div>
        <div className="mx-auto w-full max-w-sm">
          <Outlet />
        </div>
      </div>

      <aside className="relative hidden overflow-hidden border-l border-line bg-surface lg:block">
        {/* Декоративный градиент. aria-hidden — для скринридера это шум. */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 bg-[radial-gradient(80%_60%_at_70%_15%,var(--accent-soft),transparent_70%)]"
        />
        <div className="relative flex h-full flex-col justify-center gap-8 px-12">
          <div>
            <span className="inline-flex items-center gap-2">
              <span className="flex size-8 items-center justify-center rounded-lg bg-accent text-sm font-bold text-accent-fg">
                L
              </span>
              <span className="text-lg font-semibold tracking-tight">LifeOS</span>
            </span>
            <h2 className="mt-6 max-w-md text-2xl leading-snug font-semibold tracking-tight">
              Персональное пространство: цели, финансы, здоровье, учёба и карьера в одном месте.
            </h2>
          </div>

          <ul className="max-w-md space-y-5">
            {HIGHLIGHTS.map((item) => (
              <li key={item.title} className="border-l-2 border-accent/40 pl-4">
                <p className="text-[13.5px] font-medium text-fg">{item.title}</p>
                <p className="mt-1 text-[13px] leading-relaxed text-fg-muted">{item.text}</p>
              </li>
            ))}
          </ul>
        </div>
      </aside>
    </div>
  );
}
