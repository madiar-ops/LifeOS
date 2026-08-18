import { ChevronLeft, ChevronRight } from 'lucide-react';

import { formatNumber } from '@/lib/format';
import type { PagedResponse } from '@/types/api';

import { Button } from './Button';

interface PaginationProps {
  /** Метаданные из `PagedResponse<T>` — сервер уже посчитал totalPages и флаги. */
  page: Pick<
    PagedResponse<unknown>,
    'pageNumber' | 'pageSize' | 'totalCount' | 'totalPages' | 'hasPrevious' | 'hasNext'
  >;
  onChange: (pageNumber: number) => void;
}

/**
 * Пагинация.
 *
 * Кнопки управляются флагами `hasPrevious` / `hasNext` С СЕРВЕРА, а не
 * вычислением `pageNumber < totalPages` на клиенте. Сервер — источник правды о
 * том, есть ли ещё страницы; собственный расчёт разошёлся бы с ним, если
 * данные изменились между запросами.
 */
export function Pagination({ page, onChange }: PaginationProps) {
  if (page.totalCount === 0) return null;

  const firstOnPage = (page.pageNumber - 1) * page.pageSize + 1;
  const lastOnPage = Math.min(page.pageNumber * page.pageSize, page.totalCount);

  return (
    <nav
      aria-label="Постраничная навигация"
      className="flex flex-wrap items-center justify-between gap-3 border-t border-line px-5 py-3"
    >
      <p className="tabular text-[12.5px] text-fg-muted">
        {formatNumber(firstOnPage)}–{formatNumber(lastOnPage)} из {formatNumber(page.totalCount)}
      </p>

      <div className="flex items-center gap-1.5">
        <Button
          variant="secondary"
          size="sm"
          disabled={!page.hasPrevious}
          onClick={() => onChange(page.pageNumber - 1)}
          leftIcon={<ChevronLeft size={14} />}
        >
          Назад
        </Button>
        <span className="tabular px-2 text-[12.5px] text-fg-muted">
          {page.pageNumber} / {Math.max(1, page.totalPages)}
        </span>
        <Button
          variant="secondary"
          size="sm"
          disabled={!page.hasNext}
          onClick={() => onChange(page.pageNumber + 1)}
          rightIcon={<ChevronRight size={14} />}
        >
          Вперёд
        </Button>
      </div>
    </nav>
  );
}
