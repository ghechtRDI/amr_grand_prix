import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    host: true, // Allow external connections (required for Docker)
    port: 5173,
    proxy: {
      '/api': {
        // Proxy to API running on port 8080
        // Use localhost for local dev, host.docker.internal in Docker
        target: process.env.DOCKER_ENV ? 'http://api:8080' : 'http://localhost:8080',
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
