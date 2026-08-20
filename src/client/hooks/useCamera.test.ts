import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useCamera } from './useCamera';

const track = { stop: vi.fn() };
const stream = { getTracks: () => [track] } as unknown as MediaStream;

describe('useCamera', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('reports streaming when getUserMedia resolves', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: vi.fn().mockResolvedValue(stream) } });
    const { result } = renderHook(() => useCamera(true));
    await waitFor(() => expect(result.current.status).toBe('streaming'));
    act(() => result.current.stop());
  });

  it.each(['NotAllowedError', 'SecurityError'])('maps %s to denied', async (name) => {
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: vi.fn().mockRejectedValue({ name }) } });
    const { result } = renderHook(() => useCamera(true));
    await waitFor(() => expect(result.current.status).toBe('denied'));
  });

  it('maps NotFoundError to no-camera', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: vi.fn().mockRejectedValue({ name: 'NotFoundError' }) } });
    const { result } = renderHook(() => useCamera(true));
    await waitFor(() => expect(result.current.status).toBe('no-camera'));
  });

  it('reports no-https when getUserMedia is absent', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: undefined });
    const { result } = renderHook(() => useCamera(true));
    await waitFor(() => expect(result.current.status).toBe('no-https'));
  });
});
