import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import GameForm from './GameForm';
import type { Game } from '../services/types';
import type { GameLookupResult } from '../services/lookup';

// Mock the data/services layer. OnlineSearch / BarcodeLookup / PhotoLookup all
// consume useLookup; we feed them an empty result so they render their "no
// results" state without network. Tags and the lookup-by-id path are stubbed
// too.
const mockUseLookup = vi.fn().mockReturnValue({ data: undefined, isLoading: false, error: null });
const mockLookupGameByIgdbId = vi.fn();
vi.mock('../services/lookup', async (importOriginal) => {
  const original = await importOriginal<typeof import('../services/lookup')>();
  return {
    ...original,
    useLookup: () => mockUseLookup(),
    lookupGameByIgdbId: (id: string) => mockLookupGameByIgdbId(id),
  };
});
vi.mock('../services/tags', () => ({
  useTags: () => ({ data: [], isLoading: false, error: null }),
}));

const pcGame: Game = {
  title: 'Tomb Raider Game of the Year',
  platform: 'Pc',
  year: 2013,
  publisher: 'Square Enix',
  developer: null,
  description: 'My own description that must survive.',
  imagePath: '/covers/mine.jpg',
  digitalStores: 1, // Steam
  status: 'Owned',
  completionStatus: 'NotStarted',
  tags: [],
  igdbId: null,
};

// A search result that would previously have clobbered the record above:
// IGDB's first-listed platform is Ps3 even though the release is on PC.
const igdbResult: GameLookupResult = {
  provider: 'igdb',
  providerKey: '53818',
  title: 'Tomb Raider: Game of the Year Edition',
  platform: 'Ps3',
  platforms: ['Ps3', 'Mobile', 'Pc', 'Xbox360'],
  year: 2014,
  publisher: 'Square Enix, Feral Interactive',
  developer: 'Crystal Dynamics',
  description: 'IGDB summary that must NOT overwrite the user description.',
  imageUrl: 'https://images.igdb.com/igdb/image/upload/t_cover_big/co3h8v.jpg',
  genres: 'Shooter, Platform, Puzzle, Adventure',
};

afterEach(() => {
  vi.clearAllMocks();
  mockUseLookup.mockReturnValue({ data: undefined, isLoading: false, error: null });
});

function renderForm(onSubmit: (g: Game) => void, initial: Game, prefillLookup?: GameLookupResult) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <GameForm initial={initial} prefillLookup={prefillLookup} onSubmit={onSubmit} />
    </QueryClientProvider>,
  );
}

// The prefill effect runs importLookup on mount with the provided result, which
// is the exact same code path as picking a search result. We assert on the value
// the form SUBMITS — the authoritative record the user would save — which keeps
// this test independent of how the SearchableSelect/Field widgets render.
describe('GameForm — fill-only IGDB import (no clobber)', () => {
  it('keeps the user’s platform (Pc) instead of IGDB’s first-listed Ps3', () => {
    const onSubmit = vi.fn();
    renderForm(onSubmit, pcGame, igdbResult);
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSubmit.mock.calls[0][0]).toMatchObject({ platform: 'Pc' });
  });

  it('does not overwrite an existing title, publisher, or description', () => {
    const onSubmit = vi.fn();
    renderForm(onSubmit, pcGame, igdbResult);
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    const s = onSubmit.mock.calls[0][0] as Game;
    expect(s.title).toBe('Tomb Raider Game of the Year');
    expect(s.publisher).toBe('Square Enix');
    expect(s.description).toBe('My own description that must survive.');
  });

  it('fills a missing developer from IGDB', () => {
    const onSubmit = vi.fn();
    renderForm(onSubmit, pcGame, igdbResult);
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    const s = onSubmit.mock.calls[0][0] as Game;
    expect(s.developer).toBe('Crystal Dynamics');
  });

  it('always writes the IGDB id even when other fields are preserved', () => {
    const onSubmit = vi.fn();
    renderForm(onSubmit, pcGame, igdbResult);
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    const s = onSubmit.mock.calls[0][0] as Game;
    expect(s.igdbId).toBe('53818');
  });

  it('fills the title when the record has none', () => {
    const onSubmit = vi.fn();
    renderForm(onSubmit, { ...pcGame, title: '' }, igdbResult);
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    const s = onSubmit.mock.calls[0][0] as Game;
    expect(s.title).toBe('Tomb Raider: Game of the Year Edition');
  });
});
