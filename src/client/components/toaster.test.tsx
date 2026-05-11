import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Toaster, toast, _resetToasts } from './toaster';

afterEach(() => {
  // Real timers FIRST so the act-wrapped emit() that resetToasts fires
  // doesn't get queued behind a fake timer flush.
  vi.useRealTimers();
  act(() => _resetToasts());
});

describe('Toaster', () => {
  it('renders nothing when the queue is empty', () => {
    const { container } = render(<Toaster />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders a success toast as role=status', () => {
    render(<Toaster />);
    act(() => {
      toast.success('Saved.');
    });

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Saved.');
  });

  it('renders an error toast as role=alert', () => {
    render(<Toaster />);
    act(() => {
      toast.error('Boom.');
    });

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Boom.');
  });

  it('auto-dismisses a success toast after its TTL', () => {
    vi.useFakeTimers();
    render(<Toaster />);
    act(() => {
      toast.success('Saved.');
    });
    expect(screen.getByRole('status')).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('keeps an error toast visible past the auto-dismiss window', () => {
    vi.useFakeTimers();
    render(<Toaster />);
    act(() => {
      toast.error('Boom.');
    });
    expect(screen.getByRole('alert')).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(10_000);
    });

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('dismisses on the close button click', async () => {
    render(<Toaster />);
    act(() => {
      toast.error('Boom.');
    });
    expect(screen.getByRole('alert')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /dismiss/i }));

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('caps the visible stack to the four most-recent toasts', () => {
    render(<Toaster />);
    act(() => {
      for (let i = 0; i < 6; i++) toast.error(`E ${i}`);
    });

    const alerts = screen.getAllByRole('alert');
    expect(alerts).toHaveLength(4);
    // FIFO eviction: oldest two dropped, newest four remain in order.
    expect(alerts[0]).toHaveTextContent('E 2');
    expect(alerts[alerts.length - 1]).toHaveTextContent('E 5');
  });
});
