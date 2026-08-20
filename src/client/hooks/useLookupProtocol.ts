import { useCallback, useRef, useState } from 'react';
import type { MediaType } from '../services/types';
import type { LookupByIdOutcome } from '../services/lookup';

export interface LookupProtocolConfig<TMedia extends MediaType, TItem, TResult> {
  getDraft: () => TItem;
  patchDraft: (p: Partial<TItem>) => void;
  importFields: (draft: TItem, r: TResult) => Partial<TItem>;
  providerNames: readonly string[];
  linkageKey: (d: TItem) => string | null;
  setLinkageKey: (d: TItem, v: string) => TItem;
  enrich?: {
    keyOf: (d: TItem) => string | null;
    run: (id: string) => Promise<LookupByIdOutcome<TResult>>;
    fill: (d: TItem, r: TResult) => TItem;
    shouldRun: (r: TResult) => boolean;
    loadingLabel: string;
    successLabel: string;
    notConfiguredLabel: string;
  };
  byId: { label: string; lookup: (id: string) => Promise<LookupByIdOutcome<TResult>> };
}

export function useLookupProtocol<TMedia extends MediaType, TItem, TResult>(cfg: LookupProtocolConfig<TMedia, TItem, TResult>) {
  const cfgRef = useRef(cfg);
  cfgRef.current = cfg;
  const draftRef = useRef(cfg.getDraft());
  draftRef.current = cfg.getDraft();
  const activeKeyRef = useRef(cfg.linkageKey(cfg.getDraft()));
  activeKeyRef.current = cfg.linkageKey(cfg.getDraft());
  const [fetchState, setFetchState] = useState<{ status: 'idle' | 'loading'; message?: string }>({ status: 'idle' });

  const importLookup = useCallback((result: TResult) => {
    const cfg = cfgRef.current;
    const raw = result as { provider?: string; providerKey?: string };
    const current = draftRef.current;
    const linked = raw.provider && cfg.providerNames.includes(raw.provider) && raw.providerKey
      ? cfg.setLinkageKey(current, raw.providerKey) : current;
    const patch = { ...(linked as object), ...cfg.importFields(linked, result) } as Partial<TItem>;
    draftRef.current = Object.assign({}, linked, patch);
    activeKeyRef.current = cfg.linkageKey(draftRef.current);
    cfg.patchDraft(patch);
    if (!cfg.enrich || !raw.providerKey || !cfg.enrich.shouldRun(result)) return;
    const key = raw.providerKey;
    setFetchState({ status: 'loading', message: cfg.enrich.loadingLabel });
    void cfg.enrich.run(key).then((outcome) => {
      if (outcome.kind !== 'found') {
        setFetchState({ status: 'idle', message: outcome.kind === 'not-configured' ? cfg.enrich?.notConfiguredLabel : undefined });
        return;
      }
      if (activeKeyRef.current !== key) return;
      const latest = draftRef.current;
      const filled = cfg.enrich!.fill(latest, outcome.result);
      cfg.patchDraft(filled);
      setFetchState({ status: 'idle', message: cfg.enrich!.successLabel });
    }).catch(() => setFetchState({ status: 'idle' }));
  }, []);

  const runById = useCallback(async (id: string) => {
    const cfg = cfgRef.current;
    const trimmed = id.trim();
    if (!trimmed) { setFetchState({ status: 'idle', message: `Enter a ${cfg.byId.label} first.` }); return; }
    setFetchState({ status: 'loading' });
    try {
      const outcome = await cfg.byId.lookup(trimmed);
      if (outcome.kind === 'found') { importLookup(outcome.result); setFetchState({ status: 'idle', message: 'Populated.' }); }
      else if (outcome.kind === 'not-configured') {
        const provider = cfg.byId.label.includes('MusicBrainz') ? 'MusicBrainz lookup not configured. Set the User-Agent.' : cfg.byId.label.includes('IGDB') ? 'IGDB lookup not configured. Set the Twitch client id and secret.' : 'TMDB lookup not configured. Set the provider key.';
        setFetchState({ status: 'idle', message: provider });
      } else {
        const noun = cfg.byId.label.includes('MusicBrainz') ? 'release' : cfg.byId.label.includes('IGDB') ? 'game' : 'movie';
        setFetchState({ status: 'idle', message: `No ${noun} with ${cfg.byId.label} ${trimmed}.` });
      }
    } catch (error) { setFetchState({ status: 'idle', message: (error as Error).message ?? 'Lookup failed.' }); }
  }, [importLookup]);

  const prefillEffect = useCallback((prefill?: TResult, barcode?: string) => {
    const cfg = cfgRef.current;
    if (prefill) importLookup(prefill);
    else if (barcode) {
      const patch: Partial<TItem> = {};
      Reflect.set(patch, 'barcode', barcode);
      cfg.patchDraft(patch);
    }
  }, [importLookup]);

  return { importLookup, runById, prefillEffect, fetchState };
}
