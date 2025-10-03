import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Forward API requests to your backend
      '/api': {
        target: 'http://localhost:5000', // <-- change if your C# backend runs on a different port
        changeOrigin: true,
        secure: false,
      },
    },
  },
})