import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PhotoLookup from './PhotoLookup';
import type { MovieLookupResult, LookupResponse } from '../services/lookup';

// ---------- lookup mock ----------

const mockLookupByImage = vi.fn<(type: string, file: Blob) => Promise<LookupResponse<MovieLookupResult>>>();
vi.mock('../services/lookup', () => ({
  lookupByImage: (type: string, file: Blob) => mockLookupByImage(type, file),
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

// Minimal getUserMedia mock
const mockTrack = { stop: vi.fn() };
const mockStream = {
  getTracks: () => [mockTrack],
};

// ---------- Canvas mock (jsdom doesn't implement getContext) ----------
const originalCreateElement = document.createElement.bind(document);

function mockCanvas() {
  document.createElement = (tagName: string) => {
    const el = originalCreateElement(tagName);
    if (tagName.toLowerCase() === 'canvas') {
      const ctx = { canvas: el, drawImage: vi.fn() };
      (el as any).getContext = vi.fn().mockReturnValue(ctx);
      (el as any).toDataURL = vi.fn().mockReturnValue('data:image/jpeg;base64,fake');
      (el as any).toBlob = vi.fn((cb) => {
        cb(new Blob(['fake'], { type: 'image/jpeg' }));
      });
    }
    return el;
  };
}

function restoreCanvas() {
  document.createElement = originalCreateElement;
}

// ---------- Image mock (jsdom doesn't fire onload) ----------
const OriginalImage = globalThis.Image;
function mockImage() {
  class MockImage {
    onload: (() => void) | null = null;
    src = '';
    constructor() {
      const self = this;
      Object.defineProperty(this, 'src', {
        set(val: string) {
          (self as any)._src = val;
          // Simulate image load on next microtask
          Promise.resolve().then(() => self.onload?.());
        },
        get() {
          return (this as any)._src;
        },
        configurable: true,
      });
    }
  }
  globalThis.Image = MockImage as any;
}

function restoreImage() {
  globalThis.Image = OriginalImage;
}

beforeEach(() => {
  Object.defineProperty(globalThis.navigator, 'mediaDevices', {
    value: {
      getUserMedia: vi.fn().mockResolvedValue(mockStream),
    },
    configurable: true,
  });
  mockLookupByImage.mockReset();
  mockCanvas();
  mockImage();
});

afterEach(() => {
  restoreCanvas();
  restoreImage();
  vi.restoreAllMocks();
});

/**
 * Helper: patch video dimensions and click Snap.
 */
async function patchVideoAndSnap() {
  await waitFor(() => {
    const video = document.querySelector('video');
    expect(video).not.toBeNull();
    // Patch dimensions so the canvas draw works
    Object.defineProperty(video!, 'videoWidth', { value: 640, configurable: true });
    Object.defineProperty(video!, 'videoHeight', { value: 480, configurable: true });
  });
  await userEvent.click(screen.getByRole('button', { name: /Snap/i }));
}

describe('PhotoLookup', () => {
  it('renders "Snap cover photo" button in idle state', () => {
    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    expect(screen.getByRole('button', { name: /Snap cover photo/i })).toBeInTheDocument();
  });

  it('opens camera modal on click', async () => {
    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));
    expect(screen.getByRole('button', { name: 'Close' })).toBeInTheDocument();
    expect(document.querySelector('video')).toBeInTheDocument();
  });

  it('shows confirm step after snap', async () => {
    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));
    await patchVideoAndSnap();

    expect(screen.getByRole('button', { name: /Retake/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Search/i })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: /Photo preview/i })).toBeInTheDocument();
  });

  it('retake closes the modal', async () => {
    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));
    await patchVideoAndSnap();

    await userEvent.click(screen.getByRole('button', { name: /Retake/i }));
    expect(screen.getByRole('button', { name: /Snap cover photo/i })).toBeInTheDocument();
  });

  it('search uploads to correct endpoint', async () => {
    mockLookupByImage.mockResolvedValue({
      provider: 'tmdb',
      configured: true,
      results: [seededMovie],
    });

    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));
    await patchVideoAndSnap();
    await userEvent.click(screen.getByRole('button', { name: /Search/i }));

    await waitFor(() =>
      expect(mockLookupByImage).toHaveBeenCalledWith('movies', expect.any(Blob)),
    );
    expect(await screen.findByRole('button', { name: /Inception/i })).toBeInTheDocument();
  });

  it('renders candidate list on success and fires onPick', async () => {
    mockLookupByImage.mockResolvedValue({
      provider: 'tmdb',
      configured: true,
      results: [seededMovie],
    });

    const onPick = vi.fn();
    render(<PhotoLookup type="movies" onPick={onPick} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));
    await patchVideoAndSnap();
    await userEvent.click(screen.getByRole('button', { name: /Search/i }));

    const candidate = await screen.findByRole('button', { name: /Inception/i });
    await userEvent.click(candidate);

    expect(onPick).toHaveBeenCalledWith(seededMovie);
    expect(screen.queryByRole('button', { name: /Inception/i })).not.toBeInTheDocument();
  });

  it('shows hint message when server returns hint', async () => {
    mockLookupByImage.mockResolvedValue({
      provider: 'tmdb',
      configured: true,
      results: [],
      hint: 'No match found from this photo. Try retaking with better lighting.',
    });

    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));
    await patchVideoAndSnap();
    await userEvent.click(screen.getByRole('button', { name: /Search/i }));

    expect(
      await screen.findByText(/No match found from this photo/i),
    ).toBeInTheDocument();
  });

  it('shows not configured hint when configured=false', async () => {
    mockLookupByImage.mockResolvedValue({
      provider: 'tmdb',
      configured: false,
      results: [],
    });

    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));
    await patchVideoAndSnap();
    await userEvent.click(screen.getByRole('button', { name: /Search/i }));

    expect(
      await screen.findByText(/Cloud Vision API key/i),
    ).toBeInTheDocument();
  });

  it('shows HTTPS hint when getUserMedia unavailable', async () => {
    Object.defineProperty(globalThis.navigator, 'mediaDevices', {
      value: undefined,
      configurable: true,
    });

    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));

    expect(
      await screen.findByText(/secure context/i),
    ).toBeInTheDocument();
  });

  it('closes modal on Escape key', async () => {
    render(<PhotoLookup type="movies" onPick={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /Snap cover photo/i }));

    expect(screen.getByRole('button', { name: 'Close' })).toBeInTheDocument();

    fireEvent.keyDown(document, { key: 'Escape' });

    expect(
      screen.getByRole('button', { name: /Snap cover photo/i }),
    ).toBeInTheDocument();
  });
});
