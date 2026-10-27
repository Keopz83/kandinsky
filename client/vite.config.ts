import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const apiUrl = loadEnv(mode, '.', '').KANDINSKY_API_URL || 'http://localhost:5080'
  return {
    plugins: [react()],
    server: {
      // Not 5173: other local apps register service workers on that origin.
      port: 5180,
      strictPort: true,
      proxy: {
        '/api': apiUrl,
        '/openapi': apiUrl,
      },
    },
  }
})
