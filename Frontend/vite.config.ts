import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // Forward /api/* to the .NET API so the SPA can use same-origin requests
    // in dev. In production the SPA calls VITE_API_URL directly (docs 06).
    proxy: {
      '/api': {
        target: 'http://localhost:5131',
        changeOrigin: true,
      },
    },
  },
})
