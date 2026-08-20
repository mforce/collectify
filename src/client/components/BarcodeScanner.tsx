import { useEffect, useState, type FormEvent } from 'react';
import { BrowserMultiFormatReader } from '@zxing/browser';
import { useCamera } from '../hooks/useCamera';

interface BarcodeScannerProps {
  open: boolean;
  onDetected: (code: string) => void;
  onClose: () => void;
}

/**
 * Fullscreen camera viewfinder backed by @zxing/browser. While `open`, the
 * component requests camera access, streams frames into a hidden ZXing
 * decoder, and fires `onDetected(code)` the first time a UPC/EAN/QR
 * barcode is recognised; the camera stream is stopped automatically.
 *
 * Closing happens via Escape, the Cancel button, or `open=false`. The
 * useEffect cleanup tears down the stream so navigating away or losing
 * focus mid-scan doesn't leak a `MediaStream` (the browser's "in use"
 * indicator otherwise stays on for ages).
 *
 * `getUserMedia` is gated on a secure context. When served over plain
 * HTTP (other than localhost) the API isn't available; we surface a
 * dedicated message instead of the generic "no camera" copy so the
 * setup story (mkcert / reverse proxy) is obvious.
 */
export default function BarcodeScanner({ open, onDetected, onClose }: BarcodeScannerProps) {
  const { videoRef, status, stream, stop } = useCamera(open);
  const [manualCode, setManualCode] = useState('');

  const submitManual = (e: FormEvent) => {
    e.preventDefault();
    const code = manualCode.trim();
    if (!code) return;
    setManualCode('');
    onDetected(code);
  };

  useEffect(() => {
    if (!open) return;

    let cancelled = false;
    if (status !== 'streaming' || !stream) return;

    // Defer ZXing setup by one tick so React 18 StrictMode's synthetic
    // mount → unmount → mount cycle in dev never spins up two
    // overlapping streams on the same video element. The first mount's
    // cleanup runs synchronously and clears the timeout before any
    // getUserMedia / srcObject / play() ever fires, so mount 2 is the
    // only one that actually attaches a stream. (Without this, mount
    // 1's pending video.play() gets aborted the instant mount 2 swaps
    // srcObject, and the warning shows up as the DOMException the user
    // saw.)
    const handle = setTimeout(() => {
      if (cancelled) return;

      const reader = new BrowserMultiFormatReader();
      reader
        .decodeFromStream(
          stream,
          videoRef.current!,
          (result, _err, ctl) => {
            if (cancelled || !result) return;
            ctl.stop();
            onDetected(result.getText());
          },
        )
        .catch(() => undefined);
    }, 0);

    return () => {
      cancelled = true;
      clearTimeout(handle);
      stop();
    };
  }, [open, onDetected, status, stop, stream, videoRef]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: globalThis.KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Scan barcode"
      // h-dvh / w-dvw use the *visible* viewport. Mobile browsers
      // (Samsung Internet in particular) compute 100vh against the
      // maximum viewport, including the area behind the address bar
      // -- with `inset-0` alone the modal centred its content below
      // the visible fold, so the video looked off-centre. Tailwind
      // 3.4+ provides h-dvh / w-dvw for the visual viewport.
      className="fixed inset-0 z-50 flex flex-col items-center justify-center gap-4 bg-black/95 p-4 h-dvh w-dvw"
    >
      <video
        ref={videoRef}
        // Fixed-height + object-cover gives a predictable viewfinder
        // regardless of the camera's intrinsic aspect ratio (phones
        // commonly stream 16:9 even when held portrait). Without
        // object-cover the stream was stretched / mis-cropped, leaving
        // the slate-900 fallback bg visible alongside the live frame.
        className="h-[60dvh] w-full max-w-md rounded-2xl bg-imgPlaceholder object-cover"
        muted
        playsInline
      />
      {status === 'requesting' && <p className="text-text-primary text-sm">Requesting camera…</p>}
      {status === 'streaming' && (
        <p className="text-text-primary text-sm">Point the camera at a UPC / EAN barcode.</p>
      )}
      {status === 'denied' && (
        <p className="text-error text-sm max-w-md text-center">
          Camera permission denied. Allow camera access in your browser settings to scan.
        </p>
      )}
      {status === 'no-camera' && (
        <p className="text-error text-sm max-w-md text-center">
          No camera available on this device.
        </p>
      )}
      {status === 'no-https' && (
        <p className="text-error text-sm max-w-md text-center">
          Camera access requires a secure context (HTTPS or localhost).
        </p>
      )}

      <form onSubmit={submitManual} className="flex gap-2 items-stretch w-full max-w-sm">
        <input
          value={manualCode}
          onChange={(e) => setManualCode(e.target.value)}
          inputMode="numeric"
          autoComplete="off"
          placeholder="Or type a barcode"
          aria-label="Barcode"
          className="min-h-[44px] flex-1 rounded-xl border border-border bg-input-bg px-3 py-2 text-sm text-text-primary placeholder:text-text-tertiary focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/20"
        />
        <button
          type="submit"
          disabled={!manualCode.trim()}
          className="rounded-xl bg-brand px-3 py-2 text-sm font-bold text-white transition-colors hover:bg-brand-hover disabled:cursor-not-allowed disabled:opacity-50"
        >
          Look up
        </button>
      </form>

      <button
        type="button"
        onClick={onClose}
        className="rounded-xl bg-pill-bg px-4 py-2 font-semibold text-text-primary transition-colors hover:bg-card"
      >
        Cancel
      </button>
    </div>
  );
}
