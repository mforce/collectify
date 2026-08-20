import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { Filters } from '../services/filters';
import type { MediaType } from '../services/types';
import FiltersPanel from './FiltersPanel';

function renderPanel<T extends MediaType>(type: T, value: Filters<T>, onChange = vi.fn()) {
  render(<FiltersPanel type={type} value={value} onChange={onChange} onClear={vi.fn()} />);
  return onChange;
}

describe('FiltersPanel', () => {
  it.each(['movies', 'music', 'games'] as const)('exposes a Tags field for %s', async (type) => {
    renderPanel(type, {});
    await userEvent.click(screen.getByRole('button', { name: /Filters/ }));
    expect(screen.getByText('Tags')).toBeInTheDocument();
    expect(screen.getByLabelText('Add tag')).toBeInTheDocument();
  });

  it('updates tags while preserving unrelated filters', async () => {
    const onChange = renderPanel('movies', { director: 'Nolan', tag: ['imax'] });
    await userEvent.click(screen.getByRole('button', { name: /Filters/ }));
    await userEvent.type(screen.getByLabelText('Add tag'), 'Sci-Fi{Enter}');

    expect(onChange).toHaveBeenCalledWith({ director: 'Nolan', tag: ['imax', 'sci-fi'] });
  });

  it('counts one rendered year-range chip as one active filter', () => {
    renderPanel('movies', { yearFrom: 2000, yearTo: 2020 });
    expect(screen.getByRole('button', { name: /Filters \(1\)/ })).toBeInTheDocument();
    expect(screen.getByText('2000–2020')).toBeInTheDocument();
  });

  it('clears both year bounds when removing the Year chip', async () => {
    const onChange = renderPanel('movies', { yearFrom: 2000, yearTo: 2020, director: 'Nolan' });
    await userEvent.click(screen.getByRole('button', { name: 'Remove Year filter' }));

    expect(onChange).toHaveBeenCalledWith({
      yearFrom: undefined,
      yearTo: undefined,
      director: 'Nolan',
    });
  });

  it('renders the digital-store display label in the active-filter chip', () => {
    renderPanel('games', { digitalStore: 'Psn' });

    expect(screen.getByText('PlayStation Network')).toBeInTheDocument();
    expect(screen.queryByText('Psn')).not.toBeInTheDocument();
  });

  it('toggles digital-store checkboxes into a comma-joined filter', async () => {
    // The real app drives the panel from URL-synced filter state, so toggles
    // accumulate; a plain vi.fn() would stay on the initial value and every
    // toggle would start from scratch. Mirror that statefulness here.
    let current: Filters<'games'> = {};
    const onToggle = (next: Filters<'games'>) => { current = next; };
    const { rerender } = render(
      <FiltersPanel type="games" value={current} onChange={onToggle} onClear={vi.fn()} />,
    );
    // Expand the filter section first (the chevron is part of the accessible name).
    await userEvent.click(screen.getByRole('button', { name: /Filters/ }));
    const click = async (name: string) => {
      await userEvent.click(screen.getByRole('button', { name }));
      rerender(<FiltersPanel type="games" value={current} onChange={onToggle} onClear={vi.fn()} />);
    };

    await click('Steam');
    await click('Epic');
    expect(current).toEqual({ digitalStore: 'Steam,Epic' });

    // Toggling Epic back off leaves only Steam.
    await click('Epic');
    expect(current).toEqual({ digitalStore: 'Steam' });
  });

  it('decodes a numeric-mask store filter and toggles into name-only keys', async () => {
    // A server-valid numeric URL (?digitalStore=5 = Steam|Epic) must render
    // Steam + Epic pressed, and toggling must serialize canonical names (not
    // "5,Steam", which the server 400s).
    let current: Filters<'games'> = { digitalStore: '5' };
    const onToggle = (next: Filters<'games'>) => { current = next; };
    const { rerender } = render(
      <FiltersPanel type="games" value={current} onChange={onToggle} onClear={vi.fn()} />,
    );
    await userEvent.click(screen.getByRole('button', { name: /Filters/ }));
    const press = (name: string) =>
      screen.getByRole('button', { name }).getAttribute('aria-pressed');

    expect(press('Steam')).toBe('true');
    expect(press('Epic')).toBe('true');
    expect(press('GOG')).toBe('false');

    await userEvent.click(screen.getByRole('button', { name: 'GOG' }));
    rerender(<FiltersPanel type="games" value={current} onChange={onToggle} onClear={vi.fn()} />);
    // Steam|Epic|Gog canonical keys (serialized as the flag `key`), not a
    // mixed "5,Gog".
    expect(current).toEqual({ digitalStore: 'Steam,Epic,Gog' });
  });
});
