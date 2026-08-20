import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { Button } from './Button';
import { EmptyState } from './EmptyState';

/**
 * Тесты пустого состояния.
 *
 * У нового пользователя данных нет по определению, поэтому пустое состояние —
 * первое, что он видит в каждом модуле. Проверяется, что необязательные части
 * (описание, иконка, действие) именно ОТСУТСТВУЮТ, а не отрисовываются
 * пустыми: пустой блок оставляет дыру в вёрстке и выглядит как сбой загрузки.
 */

describe('EmptyState', () => {
  it('показывает заголовок, описание и кнопку действия', () => {
    render(
      <EmptyState
        title="Пока нет целей"
        description="Создай первую цель, чтобы начать отслеживать прогресс."
        action={<Button variant="primary">Новая цель</Button>}
      />,
    );

    expect(screen.getByText('Пока нет целей')).toBeInTheDocument();
    expect(
      screen.getByText('Создай первую цель, чтобы начать отслеживать прогресс.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Новая цель' })).toBeInTheDocument();
  });

  it('обходится одним заголовком, когда остального нет', () => {
    const { container } = render(<EmptyState title="Ничего не найдено" />);

    expect(screen.getByText('Ничего не найдено')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    // Единственный текстовый узел — заголовок: пустого абзаца описания нет.
    expect(container.querySelectorAll('p')).toHaveLength(1);
  });

  it('передаёт нажатие действию, а не перехватывает его', async () => {
    const onClick = vi.fn();
    const user = userEvent.setup();
    render(
      <EmptyState title="Пока нет задач" action={<Button onClick={onClick}>Добавить</Button>} />,
    );

    await user.click(screen.getByRole('button', { name: 'Добавить' }));

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('отрисовывает переданную иконку рядом с заголовком', () => {
    render(<EmptyState title="Нет файлов" icon={<svg data-testid="icon" />} />);

    expect(screen.getByTestId('icon')).toBeInTheDocument();
    expect(screen.getByText('Нет файлов')).toBeInTheDocument();
  });
});
