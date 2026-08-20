import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useLookupProtocol } from './useLookupProtocol';

type Draft = { title: string; key: string | null; barcode?: string };
type Result = { title: string; provider: string; providerKey: string };

function setup(lookup = vi.fn()) {
  let draft: Draft = { title: '', key: null };
  const config = {
    getDraft: () => draft,
    patchDraft: (p: Partial<Draft>) => { draft = { ...draft, ...p }; },
    importFields: (_d: Draft, r: Result) => ({ title: r.title }),
    providerNames: ['provider'],
    linkageKey: (d: Draft) => d.key,
    setLinkageKey: (d: Draft, key: string) => ({ ...d, key }),
    byId: { label: 'item', lookup },
  } as const;
  const hook = renderHook(() => useLookupProtocol<'movies', Draft, Result>(config));
  return { ...hook, draft: () => draft };
}

describe.each(['movies', 'music', 'games'])('useLookupProtocol %s', () => {
  it('found fills fields', async () => {
    const s = setup(vi.fn().mockResolvedValue({ kind: 'found', result: { title: 'Found', provider: 'provider', providerKey: '1' } }));
    await act(() => s.result.current.runById('1'));
    expect(s.draft()).toMatchObject({ title: 'Found', key: '1' });
  });
  it.each([['not-configured', 'not configured'], ['not-found', 'no ']] as const)('%s exposes outcome', async (kind, message) => {
    const s = setup(vi.fn().mockResolvedValue({ kind }));
    await act(() => s.result.current.runById('1'));
    expect(s.result.current.fetchState.message?.toLowerCase()).toContain(message);
    expect(s.draft().title).toBe('');
  });
  it('surfaces errors', async () => {
    const s = setup(vi.fn().mockRejectedValue(new Error('boom')));
    await act(() => s.result.current.runById('1'));
    expect(s.result.current.fetchState.message).toBe('boom');
  });
});

it('guards enrichment against a newer linkage key', async () => {
  let resolve!: (v: unknown) => void;
  const pending = new Promise((r) => { resolve = r; });
  let draft: Draft = { title: '', key: null };
  const { result } = renderHook(() => useLookupProtocol<'movies', Draft, Result>({
    getDraft: () => draft,
    patchDraft: (p) => { draft = { ...draft, ...p }; },
    importFields: (_d, r) => ({ title: r.title }), providerNames: ['provider'],
    linkageKey: (d) => d.key, setLinkageKey: (d, key) => ({ ...d, key }),
    enrich: { keyOf: (d) => d.key, run: (id) => id === 'old' ? pending as never : new Promise(() => undefined), fill: (d) => ({ ...d, title: 'old enriched' }), shouldRun: () => true, loadingLabel: 'loading', successLabel: 'done', notConfiguredLabel: 'no' },
    byId: { label: 'item', lookup: vi.fn() },
  }));
  act(() => { result.current.importLookup({ title: 'old', provider: 'provider', providerKey: 'old' }); result.current.importLookup({ title: 'new', provider: 'provider', providerKey: 'new' }); });
  resolve({ kind: 'found', result: { title: 'old enriched', provider: 'provider', providerKey: 'old' } });
  await waitFor(() => expect(draft.key).toBe('new'));
  expect(draft.title).toBe('new');
});

it('prefill owns the draft and barcode-only seeds barcode otherwise', () => {
  const full = setup();
  act(() => full.result.current.prefillEffect({ title: 'Prefill', provider: 'provider', providerKey: 'p' }, '111'));
  expect(full.draft()).toMatchObject({ title: 'Prefill', key: 'p' });
  expect(full.draft().barcode).toBeUndefined();
  const barcode = setup();
  act(() => barcode.result.current.prefillEffect(undefined, '111'));
  expect(barcode.draft().barcode).toBe('111');
});
