import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig, loadEnv } from 'vite'
import { apiBase, proxyTarget } from './config/environment.mjs'

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, process.cwd(), '')
  apiBase(environment.VITE_API_URL)
  return {
    plugins: [react(), tailwindcss()],
    server: {
      port: 5173,
      proxy: { '/api': { target: proxyTarget(environment.API_PROXY_TARGET), changeOrigin: true } },
    },
  }
})
