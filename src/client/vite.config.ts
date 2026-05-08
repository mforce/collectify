import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import basicSsl from '@vitejs/plugin-basic-ssl';

// vite.config runs in Node; declare the bits we read so we don't need
// to add @types/node just for one env-var lookup.
declare const process: { env: Record<string, string | undefined> };

// HTTPS in dev is on by default. The barcode scanner calls
// navigator.mediaDevices.getUserMedia, which browsers refuse to expose on
// plain HTTP outside localhost -- testing from a phone over the LAN
// (e.g. https://192.168.x.x:5173) needs a TLS endpoint. basicSsl mints a
// self-signed cert per process; the phone shows a one-time "accept risk"
// warning before the camera works. Set VITE_HTTPS=0 to opt out.
const useHttps = process.env.VITE_HTTPS !== '0';

export default defineConfig({
  plugins: [react(), ...(useHttps ? [basicSsl()] : [])],
  server: {
    // host: true binds 0.0.0.0 so the phone can reach the dev server over
    // the LAN. Without it Vite only listens on 127.0.0.1.
    host: true,
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5041',
      // Cover images also live on the API. In production the SPA is served
      // by the same Kestrel process so this is the same origin; in dev we
      // need an explicit proxy entry or `<img src="/covers/...">` falls
      // through to the SPA fallback and renders index.html as HTML.
      '/covers': 'http://localhost:5041',
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./test/setup.ts'],
    include: ['**/*.{test,spec}.{ts,tsx}'],
  },
});
