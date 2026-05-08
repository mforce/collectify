import { useEffect, useRef, useState } from 'react';
import { BrowserMultiFormatReader } from '@zxing/browser';

interface BarcodeScannerProps {
  open: boolean;
  onDetected: (code: string) => void;
  onClose: () => void;
}

type Status = 'requesting' | 'streaming' | 'denied' | 'no-camera' | 'no-https';

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
  const videoRef = useRef<HTMLVideoElement>(null);
  const [status, setStatus] = useState<Status>('requesting');

  useEffect(() => {
    if (!open) return;

    if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
      setStatus('no-https');
      return;
    }

    setStatus('requesting');

    let cancelled = false;
    let myStream: MediaStream | null = null;
    const reader = new BrowserMultiFormatReader();

    // ZXing's IScannerControls.stop() pauses the video element and
    // detaches its srcObject -- which is unsafe under React 18
    // StrictMode in dev: the synthetic mount → unmount → mount cycle
    // means the first cleanup runs while the second mount is already
    // attaching its own stream to the same video element. Calling the
    // first controls.stop() then nukes the second mount's srcObject and
    // we end up with a black viewfinder.
    //
    // Workaround: track the MediaStream ZXing attached for *this* mount
    // and tear down only its tracks on cleanup. The video element's
    // srcObject is left alone -- whoever owns the active stream still
    // owns the playback. (`facingMode: environment` prefers the rear
    // camera on phones, falling back to the front when no rear camera
    // is available.)
    const startPromise = reader.decodeFromConstraints(
      { video: { facingMode: { ideal: 'environment' } } },
      videoRef.current!,
      (result, _err, ctl) => {
        if (cancelled || !result) return;
        // First positive read wins; ctl.stop() here is fine because
        // by that point this mount is the active one.
        ctl.stop();
        onDetected(result.getText());
      },
    );

    startPromise
      .then(() => {
        myStream = (videoRef.current?.srcObject as MediaStream) ?? null;
        if (cancelled) {
          myStream?.getTracks().forEach((t) => t.stop());
          myStream = null;
          return;
        }
        setStatus('streaming');
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        const name = (err as { name?: string })?.name;
        if (name === 'NotAllowedError' || name === 'SecurityError') setStatus('denied');
        else setStatus('no-camera');
      });

    return () => {
      cancelled = true;
      // Stop just our tracks; don't touch the video element's srcObject
      // (a sibling mount may already own it under StrictMode).
      if (myStream) {
        myStream.getTracks().forEach((t) => t.stop());
        myStream = null;
      } else {
        // Startup hadn't resolved yet; once it does, the .then sees
        // cancelled=true and stops our tracks.
      }
    };
  }, [open, onDetected]);

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
      className="fixed inset-0 z-50 bg-black/95 flex items-center justify-center p-4 flex-col gap-4"
    >
      <video
        ref={videoRef}
        className="max-w-full max-h-[60vh] w-full sm:w-auto rounded-md bg-slate-900"
        muted
        playsInline
      />
      {status === 'requesting' && <p className="text-slate-300 text-sm">Requesting camera…</p>}
      {status === 'streaming' && (
        <p className="text-slate-300 text-sm">Point the camera at a UPC / EAN barcode.</p>
      )}
      {status === 'denied' && (
        <p className="text-rose-300 text-sm max-w-md text-center">
          Camera permission denied. Allow camera access in your browser settings to scan.
        </p>
      )}
      {status === 'no-camera' && (
        <p className="text-rose-300 text-sm max-w-md text-center">
          No camera available on this device.
        </p>
      )}
      {status === 'no-https' && (
        <p className="text-rose-300 text-sm max-w-md text-center">
          Camera access requires a secure context (HTTPS or localhost).
        </p>
      )}
      <button
        type="button"
        onClick={onClose}
        className="px-4 py-2 rounded-md bg-slate-700 hover:bg-slate-600 text-white"
      >
        Cancel
      </button>
    </div>
  );
}
