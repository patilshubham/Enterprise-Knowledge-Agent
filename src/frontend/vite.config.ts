/// <reference types="vitest/config" />
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// Dev-time API origin. Matches src/api/Properties/launchSettings.json's
// "http" profile (`applicationUrl`). Overridable so contributors running the
// API on a different port don't have to edit this file.
const apiDevProxyTarget = process.env.VITE_DEV_API_PROXY_TARGET ?? 'http://localhost:5207';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    // Same-origin dev proxy (TASK-FND-06): keeps the SPA and API on one
    // origin so the auth cookie + CSRF + SameSite (D7) work without CORS.
    // In compose, nginx.conf performs the equivalent proxying for /api and
    // /hubs. `ws: true` on /hubs allows the SignalR WebSocket upgrade to
    // pass through, and the auth cookie rides along with every proxied
    // request since the browser sees a single origin.
    proxy: {
      '/api': {
        target: apiDevProxyTarget,
        changeOrigin: true,
      },
      '/hubs': {
        target: apiDevProxyTarget,
        changeOrigin: true,
        ws: true,
      },
      '/health': {
        target: apiDevProxyTarget,
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
  },
});
