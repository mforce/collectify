import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BarcodeLookup from './BarcodeLookup';
import type { MovieLookupResult, LookupResponse } from '../services/lookup';

// ---------- @zxing/browser mock ----------
//
// jsdom has no MediaStream / getUserMedia, so the real ZXing reader can't run.
// We expose a module-level `fireDetection` so tests can simulate a positive
// scan by calling the callback ZXing would have invoked.

let fireDetection: ((code: string) => void) | null = null;
const stop = vi.fn();

vi.mock('@zxing/browser', () => ({
  BrowserMultiFormatReader: class {
    decodeFromStream(
      _stream: MediaStream,
      _video: HTMLVideoElement,
      callback: (
        result: { getText: () => string } | null,
        err: unknown,
        controls: { stop: () => void },
      ) => void,
    ) {
      fireDetection = (code: string) =>
        callback({ getText: () => code }, null, { stop });
      return Promise.resolve({ stop });
    }
  },
}));

// ---------- lookup mock ----------

const mockLookupByBarcode = vi.fn<(type: string, code: string) => Promise<LookupResponse<MovieLookupResult>>>();
vi.mock('../services/lookup', () => ({
  lookupByBarcode: (type: string, code: string) => mockLookupByBarcode(type, code),
}));

const seededMovie: MovieLookupResult = {
  provider: 'tmdb',
  providerKey: '27205',
  title: 'Inception',
  originalTitle: 'Inception',
  year: 2010,
  director: null,
  runtimeMinutes: null,
  description: null,
  imageUrl: null,
  genres: null,
};

beforeEach(() => {
  // Pretend getUserMedia exists so the scanner takes the happy path.
  Object.defineProperty(globalThis.navigator, 'mediaDevices', {
    value: { getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [{ stop }] }) },
    configurable: true,
  });
  fireDetection = null;
  stop.mockClear();
  mockLookupByBarcode.mockReset();
});

afterEach(() => {
  // Some tests close the scanner; others rely on cleanup.
});

describe('BarcodeLookup', () => {
  it('opens the scanner when the trigger is clicked', async () => {
    render(<BarcodeLookup type="movies" onPick={vi.fn()} />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /Scan barcode/i }));

    // Scanner is lazy-loaded behind Suspense, so the dialog appears
    // asynchronously after the dynamic import resolves.
    expect(await screen.findByRole('dialog', { name: /Scan barcode/i })).toBeInTheDocument();
  });

  it('on detection, calls lookupByBarcode with the scanned code and renders candidates', async () => {
    mockLookupByBarcode.mockResolvedValue({
      provider: 'tmdb',
      configured: true,
      results: [seededMovie],
    });

    render(<BarcodeLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Scan barcode/i }));

    // Wait for ZXing's decodeFromVideoDevice promise to resolve so the
    // callback is registered.
    await waitFor(() => expect(fireDetection).not.toBeNull());
    fireDetection!('0883929473076');

    await waitFor(() =>
      expect(mockLookupByBarcode).toHaveBeenCalledWith('movies', '0883929473076'),
    );

    expect(await screen.findByRole('button', { name: /Inception/i })).toBeInTheDocument();
    // Scanner closed once detection fired.
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('clicking a candidate fires onPick with the result and clears the list', async () => {
    mockLookupByBarcode.mockResolvedValue({
      provider: 'tmdb',
      configured: true,
      results: [seededMovie],
    });

    const onPick = vi.fn();
    render(<BarcodeLookup type="movies" onPick={onPick} />);
    await userEvent.click(screen.getByRole('button', { name: /Scan barcode/i }));
    await waitFor(() => expect(fireDetection).not.toBeNull());
    fireDetection!('0883929473076');

    const candidate = await screen.findByRole('button', { name: /Inception/i });
    await userEvent.click(candidate);

    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick).toHaveBeenCalledWith(seededMovie);
    expect(screen.queryByRole('button', { name: /Inception/i })).not.toBeInTheDocument();
  });

  it('shows the "not configured" hint when the server reports configured=false', async () => {
    mockLookupByBarcode.mockResolvedValue({
      provider: 'tmdb',
      configured: false,
      results: [],
    });

    render(<BarcodeLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Scan barcode/i }));
    await waitFor(() => expect(fireDetection).not.toBeNull());
    fireDetection!('0883929473076');

    expect(await screen.findByText(/not configured/i)).toBeInTheDocument();
  });

  it('shows a "no match" hint when the provider returns zero results', async () => {
    mockLookupByBarcode.mockResolvedValue({
      provider: 'tmdb',
      configured: true,
      results: [],
    });

    render(<BarcodeLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Scan barcode/i }));
    await waitFor(() => expect(fireDetection).not.toBeNull());
    fireDetection!('0000000000000');

    expect(await screen.findByText(/No match for 0000000000000/i)).toBeInTheDocument();
  });

  it('exposes a soft-fallback button on miss when onBarcodeFallback is wired', async () => {
    mockLookupByBarcode.mockResolvedValue({
      provider: 'tmdb',
      configured: true,
      results: [],
    });
    const onBarcodeFallback = vi.fn();

    render(
      <BarcodeLookup
        type="movies"
        onPick={vi.fn()}
        onBarcodeFallback={onBarcodeFallback}
        fallbackLabel="Add with this barcode"
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: /Scan barcode/i }));
    await waitFor(() => expect(fireDetection).not.toBeNull());
    fireDetection!('0000000000000');

    const fallback = await screen.findByRole('button', { name: /Add with this barcode/i });
    await userEvent.click(fallback);

    expect(onBarcodeFallback).toHaveBeenCalledTimes(1);
    expect(onBarcodeFallback).toHaveBeenCalledWith('0000000000000');
    // The miss UI is dismissed once the fallback fires.
    expect(screen.queryByText(/No match for/i)).not.toBeInTheDocument();
  });

  it('does not render the fallback button when onBarcodeFallback is omitted', async () => {
    mockLookupByBarcode.mockResolvedValue({
      provider: 'tmdb',
      configured: true,
      results: [],
    });

    render(<BarcodeLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Scan barcode/i }));
    await waitFor(() => expect(fireDetection).not.toBeNull());
    fireDetection!('0000000000000');

    expect(await screen.findByText(/No match for/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Add with this barcode/i })).not.toBeInTheDocument();
  });

  it('renders an HTTPS hint when getUserMedia is unavailable', async () => {
    // Strip mediaDevices to simulate plain HTTP / non-secure context.
    Object.defineProperty(globalThis.navigator, 'mediaDevices', {
      value: undefined,
      configurable: true,
    });

    render(<BarcodeLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Scan barcode/i }));

    expect(await screen.findByText(/secure context/i)).toBeInTheDocument();
  });
});
