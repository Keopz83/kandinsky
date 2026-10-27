import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    // Not 5173: other local apps register service workers on that origin.
    port: 5180,
    strictPort: true,
    proxy: {
      '/api': 'http://localhost:5080',
      '/openapi': 'http://localhost:5080',
    },
  },
})
