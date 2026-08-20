import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { Pagination } from './Pagination';

/**
 * Тесты пагинации.
 *
 * Компонент подчиняется флагам `hasPrevious` / `hasNext` С СЕРВЕРА, а не
 * собственному сравнению `pageNumber < totalPages`. Проверяется именно это:
 * если кнопки начнут вычислять доступность сами, интерфейс однажды разрешит
 * переход на страницу, которой уже нет, и пользователь увидит пустой список.
 */

function pageMeta(overrides: Partial<Parameters<typeof Pagination>[0]['page']> = {}) {
  return {
    pageNumber: 2,
    pageSize: 20,
    totalCount: 57,
    totalPages: 3,
    hasPrevious: true,
    hasNext: true,
    ...overrides,
  };
}

describe('Pagination', () => {
  it('показывает диапазон записей на текущей странице', () => {
    render(<Pagination page={pageMeta()} onChange={vi.fn()} />);

    // Вторая страница по 20 записей из 57 — это 21–40.
    expect(screen.getByText(/21–40 из 57/u)).toBeInTheDocument();
    expect(screen.getByText('2 / 3')).toBeInTheDocument();
  });

  it('обрезает верхнюю границу диапазона по общему количеству', () => {
    render(<Pagination page={pageMeta({ pageNumber: 3, hasNext: false })} onChange={vi.fn()} />);

    // На последней странице записей меньше, чем pageSize: «41–60 из 57» было бы
    // очевидной ошибкой в глазах пользователя.
    expect(screen.getByText(/41–57 из 57/u)).toBeInTheDocument();
  });

  it('сообщает выбранный номер страницы обработчику', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<Pagination page={pageMeta()} onChange={onChange} />);

    await user.click(screen.getByRole('button', { name: /Вперёд/u }));
    expect(onChange).toHaveBeenCalledWith(3);

    await user.click(screen.getByRole('button', { name: /Назад/u }));
    expect(onChange).toHaveBeenLastCalledWith(1);
  });

  it('блокирует кнопки по флагам сервера, а не по номеру страницы', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    // Номер страницы допускает переход вперёд, но сервер сказал «дальше нет».
    render(
      <Pagination
        page={pageMeta({ pageNumber: 1, hasPrevious: false, hasNext: false, totalPages: 3 })}
        onChange={onChange}
      />,
    );

    const next = screen.getByRole('button', { name: /Вперёд/u });
    expect(next).toBeDisabled();
    expect(screen.getByRole('button', { name: /Назад/u })).toBeDisabled();

    await user.click(next);
    expect(onChange).not.toHaveBeenCalled();
  });

  it('полностью скрывается, когда записей нет', () => {
    // Пустой список сопровождается собственным пустым состоянием; полоса
    // «0–0 из 0» рядом с ним выглядит как остаток сломанной вёрстки.
    const { container } = render(
      <Pagination
        page={pageMeta({ totalCount: 0, totalPages: 0, hasPrevious: false, hasNext: false })}
        onChange={vi.fn()}
      />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it('никогда не показывает «1 / 0» при нулевом числе страниц', () => {
    render(
      <Pagination
        page={pageMeta({ pageNumber: 1, totalCount: 5, totalPages: 0, hasPrevious: false, hasNext: false })}
        onChange={vi.fn()}
      />,
    );

    expect(screen.getByText('1 / 1')).toBeInTheDocument();
  });
});
