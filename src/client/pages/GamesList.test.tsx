import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import type { Game } from '../services/types';
import GamesList from './GamesList';

vi.mock('../components/CollectionList', () => ({
  default: ({ renderItem }: { renderItem: (game: Game) => { tertiary?: string } }) => {
    const rendered = renderItem({
      title: 'Journey',
      platform: 'Ps5',
      isDigital: true,
      digitalStore: 'Psn',
      status: 'Owned',
      completionStatus: 'Beaten',
    });
    return <div>{rendered.tertiary}</div>;
  },
}));

describe('GamesList enum labels', () => {
  it('renders the digital store display label in list rows', () => {
    render(
      <MemoryRouter>
        <GamesList />
      </MemoryRouter>,
    );

    expect(screen.getByText('Digital · PlayStation Network')).toBeInTheDocument();
    expect(screen.queryByText('Digital · Psn')).not.toBeInTheDocument();
  });
});
