import { useCallback, useEffect, useRef, useState } from 'react';

export type CameraStatus = 'requesting' | 'streaming' | 'denied' | 'no-camera' | 'no-https';

/** getUserMedia with a discriminated failure taxonomy. */
export function useCamera(active: boolean, constraints: MediaStreamConstraints = { video: { facingMode: { ideal: 'environment' } } }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [status, setStatus] = useState<CameraStatus>('requesting');

  const stop = useCallback(() => {
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
  }, []);

  useEffect(() => {
    if (!active) { stop(); return; }
    if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
      setStatus('no-https');
      return;
    }
    let cancelled = false;
    setStatus('requesting');
    void navigator.mediaDevices.getUserMedia(constraints).then(async (stream) => {
      if (cancelled) { stream.getTracks().forEach((track) => track.stop()); return; }
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        try { await videoRef.current.play(); } catch { /* autoplay may be unavailable in tests */ }
      }
      if (!cancelled) setStatus('streaming');
    }).catch((error: unknown) => {
      if (cancelled) return;
      const name = (error as { name?: string }).name;
      setStatus(name === 'NotAllowedError' || name === 'SecurityError' ? 'denied' : 'no-camera');
    });
    return () => { cancelled = true; stop(); };
  }, [active, stop]);

  return { status, videoRef, stream: streamRef.current, stop };
}
