import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RatingInput } from './ui';

describe('RatingInput', () => {
  it('renders 10 buttons numbered 1..10', () => {
    render(<RatingInput value={null} onChange={() => {}} />);
    for (let n = 1; n <= 10; n++) {
      expect(screen.getByRole('radio', { name: `${n} of 10` })).toBeInTheDocument();
    }
  });

  it('marks the selected value as aria-checked', () => {
    render(<RatingInput value={7} onChange={() => {}} />);
    expect(screen.getByRole('radio', { name: '7 of 10' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('radio', { name: '8 of 10' })).toHaveAttribute('aria-checked', 'false');
  });

  it('reports the clicked value', async () => {
    const onChange = vi.fn();
    render(<RatingInput value={null} onChange={onChange} />);

    await userEvent.click(screen.getByRole('radio', { name: '8 of 10' }));

    expect(onChange).toHaveBeenCalledWith(8);
  });

  it('clears the rating when the selected value is clicked again', async () => {
    const onChange = vi.fn();
    render(<RatingInput value={5} onChange={onChange} />);

    await userEvent.click(screen.getByRole('radio', { name: '5 of 10' }));

    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('clear button resets the rating', async () => {
    const onChange = vi.fn();
    render(<RatingInput value={3} onChange={onChange} />);

    await userEvent.click(screen.getByRole('button', { name: 'Clear rating' }));

    expect(onChange).toHaveBeenCalledWith(null);
  });
});
