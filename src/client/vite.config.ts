import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
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
