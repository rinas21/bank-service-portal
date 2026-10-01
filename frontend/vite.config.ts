import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // In development the SPA talks to the API through Vite so the browser
      // stays same-origin, mirroring the nginx proxy used in Docker.
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8081',
        changeOrigin: true,
      },
      '/health': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8081',
        changeOrigin: true,
      },
    },
  },
})
