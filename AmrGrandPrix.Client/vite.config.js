import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: true, // Allow external connections (required for Docker)
    port: 5173,
    proxy: {
      '/api': {
        // Use host.docker.internal to reach host machine from container
        target: 'http://host.docker.internal:5000',
        changeOrigin: true,
        secure: false,
      },
      '/weatherforecast': {
        // Use host.docker.internal to reach host machine from container
        target: 'http://host.docker.internal:5000',
        changeOrigin: true,
        secure: false,
      }
    }
  },
  preview: {
    host: true,
    port: 4173
  }
})
