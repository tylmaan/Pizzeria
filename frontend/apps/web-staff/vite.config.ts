/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      manifest: {
        name: 'Pizzeria — panel lokalu',
        short_name: 'Pizzeria',
        lang: 'pl',
        display: 'standalone',
        start_url: '/',
        theme_color: '#ea580c',
        background_color: '#f5f5f5',
      },
    }),
  ],
  server: {
    port: 5173,
    // Keep the browser's Host (the shop is recognised by its subdomain) and add X-Forwarded-* headers.
    proxy: { '/api': { target: 'http://localhost:5080', changeOrigin: false, xfwd: true } },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
});
