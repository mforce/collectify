import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
        // Vite serves client modules from the project root, so an import like
        // `./api/auth` resolves to a request for /api/auth.ts which would
        // otherwise be proxied to the .NET API and 404. Skip proxying any
        // request whose path has a file extension; real REST endpoints don't.
        bypass: (req) => {
          if (req.url && /\.[a-z0-9]+(\?|$)/i.test(req.url)) {
            return req.url;
          }
        },
      },
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
