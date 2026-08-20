import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ProgressBar } from './ProgressBar';

/**
 * Тесты полосы прогресса.
 *
 * Значение приходит из расчётов бэкенда (`completionRate`, доля выполненных
 * задач) и в теории может выйти за 0..100 — например при делении на ноль.
 * Без обрезки полоса вылезла бы за пределы карточки, а скринридер прочитал бы
 * «прогресс 320 процентов». Проверяются именно границы.
 */

function bar(): HTMLElement {
  return screen.getByRole('progressbar');
}

describe('ProgressBar', () => {
  it('сообщает значение скринридеру, а не только рисует полоску', () => {
    render(<ProgressBar value={42} label="Прогресс цели" />);

    expect(bar()).toHaveAttribute('aria-valuenow', '42');
    expect(bar()).toHaveAttribute('aria-valuemin', '0');
    expect(bar()).toHaveAttribute('aria-valuemax', '100');
    expect(bar()).toHaveAccessibleName('Прогресс цели');
  });

  it('обрезает значение сверху и снизу', () => {
    const { rerender } = render(<ProgressBar value={320} />);
    expect(bar()).toHaveAttribute('aria-valuenow', '100');

    rerender(<ProgressBar value={-15} />);
    expect(bar()).toHaveAttribute('aria-valuenow', '0');
  });

  it('задаёт ширину заполнения в процентах от обрезанного значения', () => {
    const { container } = render(<ProgressBar value={137.4} />);

    const fill = container.querySelector('[role="progressbar"] > div');
    expect(fill).not.toBeNull();
    expect((fill as HTMLElement).style.width).toBe('100%');
  });

  it('округляет дробное значение для aria-valuenow', () => {
    render(<ProgressBar value={33.6} />);
    expect(bar()).toHaveAttribute('aria-valuenow', '34');
  });

  it('подставляет нейтральную подпись, если она не задана', () => {
    render(<ProgressBar value={10} />);
    expect(bar()).toHaveAccessibleName('Прогресс');
  });
});
