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
});
