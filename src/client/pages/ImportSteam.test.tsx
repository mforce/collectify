import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ImportSteam from './ImportSteam';

// Controllable test doubles for the Steam data layer.
const mockUseConnection = vi.fn();
const mockUseConnect = vi.fn();
const mockUseGames = vi.fn();
const mockUseImport = vi.fn();
const mockUseDisconnect = vi.fn();
const mockConnectMutate = vi.fn();

vi.mock('../services/steam', () => ({
  useSteamConnection: () => mockUseConnection(),
  useSteamConnect: () => mockUseConnect(),
  useSteamGames: (enabled: boolean) => mockUseGames(enabled),
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
    expect(mockUseGames).toHaveBeenCalledWith(false);
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

  it('filters the owned-games list by title', async () => {
    const user = userEvent.setup();
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
      error: null,
    });

    renderPage();

    expect(screen.getByText('Hades')).toBeInTheDocument();
    await user.type(screen.getByLabelText(/filter owned games/i), 'celeste');
    expect(screen.queryByText('Hades')).not.toBeInTheDocument();
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
});
