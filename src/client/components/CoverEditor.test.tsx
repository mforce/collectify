import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CoverEditor from './CoverEditor';
import { _resetToasts } from './toaster';

const originalFetch = globalThis.fetch;

beforeEach(() => {
  _resetToasts();
});

afterEach(() => {
  globalThis.fetch = originalFetch;
  vi.restoreAllMocks();
});

function mockFetch(impl: typeof fetch) {
  globalThis.fetch = vi.fn(impl) as unknown as typeof fetch;
}

describe('CoverEditor', () => {
  it('applies a pasted URL via onChange', async () => {
    const onChange = vi.fn();
    render(<CoverEditor value={null} onChange={onChange} />);

    await userEvent.type(screen.getByPlaceholderText(/cover.jpg/i), 'https://example.com/x.jpg');
    await userEvent.click(screen.getByRole('button', { name: /apply url/i }));

    expect(onChange).toHaveBeenCalledWith('https://example.com/x.jpg');
  });

  it('uploads a file and patches imagePath with the returned /covers path', async () => {
    mockFetch(async () =>
      new Response(JSON.stringify({ imagePath: '/covers/abc1234567890def' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    const onChange = vi.fn();
    render(<CoverEditor value={null} onChange={onChange} />);

    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF])], 'cover.jpg', { type: 'image/jpeg' });
    await userEvent.upload(screen.getByLabelText(/upload a file/i), file);

    // userEvent.upload kicks off the async POST; wait for state to settle.
    await vi.waitFor(() => expect(onChange).toHaveBeenCalledWith('/covers/abc1234567890def'));
    expect(globalThis.fetch).toHaveBeenCalledOnce();
  });

  it('rejects files larger than 5 MB without hitting the network', async () => {
    const fetchSpy = vi.fn();
    mockFetch(fetchSpy as unknown as typeof fetch);

    const onChange = vi.fn();
    render(<CoverEditor value={null} onChange={onChange} />);

    // 6 MB JPEG (size only; content is zero-filled).
    const big = new File([new Uint8Array(6 * 1024 * 1024)], 'huge.jpg', { type: 'image/jpeg' });
    await userEvent.upload(screen.getByLabelText(/upload a file/i), big);

    expect(await screen.findByRole('alert')).toHaveTextContent(/too large/i);
    expect(fetchSpy).not.toHaveBeenCalled();
    expect(onChange).not.toHaveBeenCalled();
  });

  it('rejects unsupported MIME types without hitting the network', async () => {
    const fetchSpy = vi.fn();
    mockFetch(fetchSpy as unknown as typeof fetch);

    const onChange = vi.fn();
    render(<CoverEditor value={null} onChange={onChange} />);

    const notImg = new File([new Uint8Array([0x68, 0x69])], 'note.txt', { type: 'text/plain' });
    // applyAccept: false so the test exercises *our* MIME guard rather
    // than user-event's own accept-attribute filter (which would
    // silently drop the file before onChange runs).
    await userEvent.upload(screen.getByLabelText(/upload a file/i), notImg, { applyAccept: false });

    expect(await screen.findByRole('alert')).toHaveTextContent(/unsupported/i);
    expect(fetchSpy).not.toHaveBeenCalled();
    expect(onChange).not.toHaveBeenCalled();
  });

  it('surfaces a 415 response from the server as an inline error', async () => {
    mockFetch(async () => new Response('{}', { status: 415 }));

    const onChange = vi.fn();
    render(<CoverEditor value={null} onChange={onChange} />);

    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF])], 'cover.jpg', { type: 'image/jpeg' });
    await userEvent.upload(screen.getByLabelText(/upload a file/i), file);

    expect(await screen.findByRole('alert')).toHaveTextContent(/unsupported/i);
    expect(onChange).not.toHaveBeenCalled();
  });

  it('clears imagePath on "Remove cover"', async () => {
    const onChange = vi.fn();
    // value is set -> editor renders the collapsed disclosure with
    // "Change cover" and "Remove cover" links.
    render(<CoverEditor value="/covers/abc1234567890def" onChange={onChange} />);

    await userEvent.click(screen.getByRole('button', { name: /remove cover/i }));

    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('starts collapsed when a cover already exists and renders compact buttons', () => {
    render(<CoverEditor value="/covers/abc1234567890def" onChange={vi.fn()} />);

    // The compact actions should be visible; the URL input shouldn't.
    expect(screen.getByTestId('cover-collapsed-actions')).toHaveClass('flex-col');
    expect(screen.getByRole('button', { name: /change cover/i })).toHaveClass('rounded-md');
    expect(screen.getByRole('button', { name: /remove cover/i })).toHaveClass('rounded-md');
    expect(screen.queryByPlaceholderText(/cover.jpg/i)).not.toBeInTheDocument();
  });

  it('uses wrapping, min-width-safe controls when expanded', async () => {
    render(<CoverEditor value="/covers/abc1234567890def" onChange={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: /change cover/i }));

    expect(screen.getByTestId('cover-editor-card')).toHaveClass('w-full', 'min-w-0');
    expect(screen.getByTestId('cover-url-row')).toHaveClass('flex-wrap');
    expect(screen.getByTestId('cover-url-field')).toHaveClass('w-full', 'min-w-0', 'sm:basis-64');
    expect(screen.getByPlaceholderText(/cover.jpg/i)).toHaveClass('min-w-0');
    expect(screen.getByLabelText(/upload a file/i)).toHaveClass('max-w-full');
    expect(screen.getByTestId('cover-editor-actions')).toHaveClass('flex-wrap');
  });
});
