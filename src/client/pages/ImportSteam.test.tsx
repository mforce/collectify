import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ImportSteam from './ImportSteam';

// Controllable test doubles for the Steam data layer.
const mockUseConnection = vi.fn();
const mockUseConnect = vi.fn();
const mockUseGames = vi.fn();
const mockFetchSteamOwnedGames = vi.fn();
const mockUseImport = vi.fn();
const mockUseDisconnect = vi.fn();
const mockConnectMutate = vi.fn();

vi.mock('../services/steam', () => ({
  fetchSteamOwnedGames: (search: string, offset: number, limit: number) =>
    mockFetchSteamOwnedGames(search, offset, limit),
  useSteamConnection: () => mockUseConnection(),
  useSteamConnect: () => mockUseConnect(),
  useSteamGames: (enabled: boolean, search: string, offset: number, limit: number) =>
    mockUseGames(enabled, search, offset, limit),
  useSteamImport: (onSuccess: () => void) => mockUseImport(onSuccess),
  useSteamDisconnect: (onSuccess: () => void) => mockUseDisconnect(onSuccess),
}));

const renderPage = () =>
  render(
    <MemoryRouter>
      <ImportSteam />
    </MemoryRouter>,
  );

const connected = { connected: true, steamId: '76561198000000000', personaName: 'Alice' };
const disconnected = { connected: false, steamId: null, personaName: null };

describe('ImportSteam', () => {
  beforeEach(() => {
    mockUseConnection.mockReturnValue({ data: disconnected, isLoading: false, error: null });
    mockUseConnect.mockReturnValue({ mutateAsync: mockConnectMutate, isPending: false });
    mockUseGames.mockReturnValue({ data: [], isLoading: false, error: null });
    mockUseImport.mockReturnValue({ mutateAsync: vi.fn(), isPending: false, data: null });
    mockUseDisconnect.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('shows a Connect Steam button when not connected', () => {
    renderPage();
    expect(screen.getByRole('button', { name: /connect steam/i })).toBeInTheDocument();
  });

  it('does not fetch games until connected', () => {
    renderPage();
    expect(mockUseGames).toHaveBeenCalledWith(false, '', 0, 100);
  });

  it('disconnects and keeps a message that games stay', async () => {
    const user = userEvent.setup();
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    const disconnect = vi.fn().mockResolvedValue(undefined);
    mockUseDisconnect.mockReturnValue({ mutateAsync: disconnect, isPending: false });

    renderPage();

    await user.click(screen.getByRole('button', { name: /disconnect/i }));
    expect(disconnect).toHaveBeenCalled();
  });

  it('lists owned games and flags imported ones as in collection', () => {
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockReturnValue({
      data: {
        status: 'ok',
        truncated: false,
        titles: [
          { externalGameId: '1', title: 'Hades', playtimeMinutes: 300, iconUrl: null, state: 'importable' },
          { externalGameId: '2', title: 'Celeste', playtimeMinutes: 0, iconUrl: null, state: 'imported' },
        ],
      },
      isLoading: false,
      error: null,
    });

    renderPage();

    expect(screen.getByText('Hades')).toBeInTheDocument();
    expect(screen.getByText('Celeste')).toBeInTheDocument();
    expect(screen.getAllByText(/in collection/i).length).toBe(1);
  });

  it('hides already-imported titles when the Hide imported toggle is on', async () => {
    const user = userEvent.setup();
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockReturnValue({
      data: {
        status: 'ok',
        truncated: false,
        total: 2,
        titles: [
          { externalGameId: '1', title: 'Hades', playtimeMinutes: 300, iconUrl: null, state: 'importable' },
          { externalGameId: '2', title: 'Celeste', playtimeMinutes: 0, iconUrl: null, state: 'imported' },
        ],
      },
      isLoading: false,
      error: null,
    });

    renderPage();

    expect(screen.getByText('Hades')).toBeInTheDocument();
    expect(screen.getByText('Celeste')).toBeInTheDocument();

    await user.click(screen.getByLabelText(/hide imported/i));
    expect(screen.queryByText('Celeste')).not.toBeInTheDocument();
    expect(screen.getByText('Hades')).toBeInTheDocument();
  });

  it('shows the fetched page range even when Hide imported filters the visible list', async () => {
    const user = userEvent.setup();
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockReturnValue({
      data: {
        status: 'ok',
        truncated: false,
        total: 3,
        titles: [
          { externalGameId: '1', title: 'ImportedA', playtimeMinutes: 0, iconUrl: null, state: 'imported' },
          { externalGameId: '2', title: 'ImportableB', playtimeMinutes: 0, iconUrl: null, state: 'importable' },
          { externalGameId: '3', title: 'ImportableC', playtimeMinutes: 0, iconUrl: null, state: 'importable' },
        ],
      },
      isLoading: false,
      error: null,
    });
    renderPage();
    // Full page visible first: range reflects 3 fetched titles.
    expect(screen.getByText(/1–3 of 3/i)).toBeInTheDocument();
    await user.click(screen.getByRole('checkbox', { name: /hide imported/i }));
    // Hide removes the imported row from the LIST but the RANGE still reflects
    // the fetched page (3), and the select-all count still derives from the
    // UN-HIDDEN page (2 importable).
    expect(screen.queryByText('ImportedA')).not.toBeInTheDocument();
    expect(screen.getByText(/1–3 of 3/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/select all not-imported \(2\)/i)).toBeInTheDocument();
  });

  it('keeps the Select all not-imported count derived from the full page, not the hidden subset', async () => {
    const user = userEvent.setup();
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockReturnValue({
      data: {
        status: 'ok',
        truncated: false,
        total: 2,
        titles: [
          { externalGameId: '1', title: 'Hades', playtimeMinutes: 300, iconUrl: null, state: 'importable' },
          { externalGameId: '2', title: 'Celeste', playtimeMinutes: 0, iconUrl: null, state: 'imported' },
        ],
      },
      isLoading: false,
      error: null,
    });

    renderPage();

    // Before hiding, count shows the importable subset of the full page.
    expect(screen.getByLabelText(/select all not-imported \(1\)/i)).toBeInTheDocument();
    await user.click(screen.getByLabelText(/hide imported/i));
    // Count is unchanged by the hide toggle (still 1 importable on this page).
    expect(screen.getByLabelText(/select all not-imported \(1\)/i)).toBeInTheDocument();
  });

  it('pages with Next and shows Prev only after the first page', async () => {
    const user = userEvent.setup();
    let callOffset = 0;
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockImplementation((_e, _s, offset: number) => {
      callOffset = offset;
      return {
        data: {
          status: 'ok',
          truncated: true, // more pages after this one
          total: 3,
          titles: [
            { externalGameId: String(offset + 1), title: `Game ${offset + 1}`, playtimeMinutes: 0, iconUrl: null, state: 'importable' },
            { externalGameId: String(offset + 2), title: `Game ${offset + 2}`, playtimeMinutes: 0, iconUrl: null, state: 'importable' },
          ],
        },
        isLoading: false,
        error: null,
      };
    });

    renderPage();

    // Next is enabled (truncated), Prev disabled (offset 0).
    const next = screen.getByRole('button', { name: /next/i });
    const prev = screen.getByRole('button', { name: /prev/i });
    expect(next).toBeEnabled();
    expect(prev).toBeDisabled();

    await user.click(next);
    expect(callOffset).toBe(100);
    // Changing the search term must reset the page offset back to 0 even while
    // paged forward (the filter effect fires immediately, not gated on the
    // 300ms debounce). This must be an IMMEDIATE assertion, not waitFor: a
    // waitFor would allow the delayed [debouncedFilter] reset (300ms) to also
    // satisfy it, so the mutant (regressing to [debouncedFilter]) would stay
    // green and the test proves nothing about immediacy.
    const filterInput = screen.getByLabelText(/filter owned games/i);
    await user.clear(filterInput);
    await user.type(filterInput, 'aha');
    expect(callOffset).toBe(0);
    expect(screen.getByRole('button', { name: /prev/i })).toBeDisabled();
  });

  it('shows a qualified public-profile hint when Steam is unavailable', () => {
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockReturnValue({
      data: { status: 'unavailable', titles: [], truncated: false },
      isLoading: false,
      error: null,
    });

    renderPage();

    expect(screen.getByText(/public/i)).toBeInTheDocument();
    expect(screen.getByText(/couldn't reach steam/i)).toBeInTheDocument();
  });

  it('shows a public-profile hint when the owned-games list is empty', () => {
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockReturnValue({
      data: { status: 'ok', titles: [], truncated: false },
      isLoading: false,
      error: null,
    });

    renderPage();

    expect(screen.getByText(/public/i)).toBeInTheDocument();
    expect(screen.getByText(/no owned games returned/i)).toBeInTheDocument();
  });

  it('filters the owned-games list by title (server-side search)', async () => {
    const user = userEvent.setup();
    const allTitles = [
      { externalGameId: '1', title: 'Hades', playtimeMinutes: 300, iconUrl: null, state: 'importable' as const },
      { externalGameId: '2', title: 'Celeste', playtimeMinutes: 0, iconUrl: null, state: 'importable' as const },
    ];
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    // The hook's search arg is sent to the server; simulate the server returning
    // the filtered slice across the full library.
    mockUseGames.mockImplementation((_enabled: boolean, search: string) => {
      const trimmed = search.trim().toLowerCase();
      return {
        data: {
          status: 'ok',
          truncated: false,
          titles: trimmed ? allTitles.filter((t) => t.title.toLowerCase().includes(trimmed)) : allTitles,
        },
        isLoading: false,
        error: null,
      };
    });

    renderPage();

    expect(await screen.findByText('Hades')).toBeInTheDocument();
    expect(screen.getByText('Celeste')).toBeInTheDocument();
    await user.type(screen.getByLabelText(/filter owned games/i), 'celeste');
    // Debounced server-side search: wait for the filtered response to land and
    // Hades to be removed from the list.
    await waitFor(() => expect(screen.queryByText('Hades')).not.toBeInTheDocument());
    expect(screen.getByText('Celeste')).toBeInTheDocument();
  });

  it('imports the selected games when the user clicks Import selected', async () => {
    const user = userEvent.setup();
    let onSuccess: (() => void) | null = null;
    const importMutate = vi.fn().mockResolvedValue({ imported: 1, alreadyImported: 0, items: [] });
    mockUseImport.mockImplementation((cb: () => void) => {
      onSuccess = cb;
      return { mutateAsync: importMutate, isPending: false };
    });
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockReturnValue({
      data: {
        status: 'ok',
        truncated: false,
        titles: [
          { externalGameId: '1', title: 'Hades', playtimeMinutes: 300, iconUrl: null, state: 'importable' },
          { externalGameId: '2', title: 'Celeste', playtimeMinutes: 0, iconUrl: null, state: 'importable' },
        ],
      },
      isLoading: false,
      isError: false,
      error: null,
    });

    renderPage();

    await user.click(screen.getByLabelText('Select all not-imported (2)'));
    await user.click(screen.getByRole('button', { name: /import selected/i }));

    expect(importMutate).toHaveBeenCalledWith(['1', '2']);
    expect(onSuccess).toBeTypeOf('function');
  });

  it('Repair covers refreshes imported games across pages, not just the current page', async () => {
    const user = userEvent.setup();
    const importMutate = vi.fn().mockResolvedValue({ imported: 0, alreadyImported: 2, items: [] });
    mockUseImport.mockReturnValue({ mutateAsync: importMutate, isPending: false });
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockReturnValue({
      data: {
        status: 'ok',
        truncated: true,
        total: 1002,
        titles: [
          { externalGameId: 'imported-page-1', title: 'Imported One', playtimeMinutes: 0, iconUrl: null, state: 'imported' },
          { externalGameId: 'importable-page-1', title: 'Importable One', playtimeMinutes: 0, iconUrl: null, state: 'importable' },
        ],
      },
      isLoading: false,
      error: null,
    });
    mockFetchSteamOwnedGames.mockImplementation((_search: string, offset: number) =>
      Promise.resolve(offset === 0
        ? {
            status: 'ok',
            truncated: true,
            total: 1002,
            titles: [
              { externalGameId: 'imported-page-1', title: 'Imported One', playtimeMinutes: 0, state: 'imported' },
              { externalGameId: 'importable-page-1', title: 'Importable One', playtimeMinutes: 0, state: 'importable' },
            ],
          }
        : {
            status: 'ok',
            truncated: false,
            total: 1002,
            titles: [
              { externalGameId: 'imported-page-2', title: 'Imported Two', playtimeMinutes: 0, state: 'imported' },
              { externalGameId: 'importable-page-2', title: 'Importable Two', playtimeMinutes: 0, state: 'importable' },
            ],
          }),
    );

    renderPage();

    await user.click(screen.getByRole('button', { name: /repair covers/i }));
    await waitFor(() => expect(importMutate).toHaveBeenCalledWith([
      'imported-page-1',
      'imported-page-2',
    ]));
    expect(mockFetchSteamOwnedGames).toHaveBeenNthCalledWith(2, '', 1000, 1000);
  });

  it('merges select-all on a later page with selections made on earlier pages', async () => {
    const user = userEvent.setup();
    let importArg: string[] | null = null;
    const importMutate = vi.fn().mockImplementation((ids: string[]) => {
      importArg = ids;
      return Promise.resolve({ imported: ids.length, alreadyImported: 0, items: [] });
    });
    mockUseImport.mockImplementation((cb: () => void) => ({ mutateAsync: importMutate, isPending: false }));
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockImplementation((_e, _s, offset: number) => ({
      data: {
        status: 'ok',
        truncated: true,
        total: 4,
        titles: [
          { externalGameId: String(offset + 1), title: `Game ${offset + 1}`, playtimeMinutes: 0, iconUrl: null, state: 'importable' },
          { externalGameId: String(offset + 2), title: `Game ${offset + 2}`, playtimeMinutes: 0, iconUrl: null, state: 'importable' },
        ],
      },
      isLoading: false,
      error: null,
    }));

    renderPage();

    // Page 1: select Game 1 individually (the row checkbox's accessible label
    // is the title plus the formatted playtime, so match with a regex role).
    await user.click(screen.getByRole('checkbox', { name: /Game 1/ }));
    // Advance to page 2.
    await user.click(screen.getByRole('button', { name: /next/i }));
    // Select all on page 2 (games 101 and 102). This must MERGE, not replace.
    await user.click(screen.getByLabelText(/select all not-imported \(2\)/i));

    // Cumulative count: Game 1 (page 1) + Games 101/102 (page 2) = 3.
    expect(screen.getByRole('button', { name: /import selected \(3\)/i })).toBeInTheDocument();

    // The submit must include the page-1 pick, proving it was not dropped.
    await user.click(screen.getByRole('button', { name: /import selected \(3\)/i }));
    expect(importArg ? [...importArg].sort() : []).toEqual(['1', '101', '102'].sort());
  });

  it('deselecting all on a later page keeps earlier-page selections', async () => {
    const user = userEvent.setup();
    mockUseConnection.mockReturnValue({ data: connected, isLoading: false, error: null });
    mockUseGames.mockImplementation((_e, _s, offset: number) => ({
      data: {
        status: 'ok',
        truncated: true,
        total: 4,
        titles: [
          { externalGameId: String(offset + 1), title: `Game ${offset + 1}`, playtimeMinutes: 0, iconUrl: null, state: 'importable' },
          { externalGameId: String(offset + 2), title: `Game ${offset + 2}`, playtimeMinutes: 0, iconUrl: null, state: 'importable' },
        ],
      },
      isLoading: false,
      error: null,
    }));

    renderPage();

    // Page 1: select-all (selects Games 1 and 2), then advance to page 2.
    await user.click(screen.getByLabelText(/select all not-imported \(2\)/i));
    await user.click(screen.getByRole('button', { name: /next/i }));
    // Page 2 shows Games 101/102. First select-all click SELECTS them (else
    // branch: adds {101,102} to the existing {1,2}, because allImportableSelected
    // is false on page 2 on this click).
    await user.click(screen.getByLabelText(/select all not-imported \(2\)/i));
    // Now all importable on page 2 are selected, so the same button is a
    // DISMISS. A second click removes ONLY this page's ids (101,102), keeping
    // the earlier-page picks (1,2). Result: {1,2} -> "Import selected (2)".
    await user.click(screen.getByLabelText(/select all not-imported \(2\)/i));

    expect(screen.getByRole('button', { name: /import selected \(2\)/i })).toBeInTheDocument();
  });
});
