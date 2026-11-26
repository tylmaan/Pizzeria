/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5174,
    // Keep the browser's Host (the shop is recognised by its subdomain) and add X-Forwarded-* headers.
    proxy: { '/api': { target: 'http://localhost:5080', changeOrigin: false, xfwd: true } },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
});
