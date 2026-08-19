import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import DetailView from './DetailView';
import type { Album, Game, MusicFormat } from '../services/types';

const album: Album = {
  title: 'Kind of Blue',
  artistName: 'Miles Davis',
  format: 'Cd',
  status: 'Owned',
  listenCount: 0,
};

const game: Game = {
  title: 'Journey',
  platform: 'Ps5',
  isDigital: true,
  digitalStore: 'Psn',
  status: 'Owned',
  completionStatus: 'Beaten',
};

function renderDetail(item: Album | Game, type: 'music' | 'games') {
  render(
    <MemoryRouter>
      <DetailView item={item} type={type} onEdit={vi.fn()} />
    </MemoryRouter>,
  );
}

describe('DetailView enum labels', () => {
  it('renders the music format label and preserves unknown runtime values', () => {
    renderDetail(album, 'music');
    expect(screen.getByText('CD')).toBeInTheDocument();

    renderDetail({ ...album, title: 'Tape', format: 'Cassette' as MusicFormat }, 'music');
    expect(screen.getByText('Cassette')).toBeInTheDocument();
  });

  it('renders the digital store display label', () => {
    renderDetail(game, 'games');
    expect(screen.getByText('PlayStation Network', { exact: false })).toBeInTheDocument();
    expect(screen.queryByText('Psn', { exact: false })).not.toBeInTheDocument();
  });
});
