import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 3000,
    host: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
        configure: (proxy) => {
          // ECONNRESET is expected when the backend restarts or SignalR closes a
          // connection — the frontend SignalR client handles reconnection automatically.
          proxy.on('error', (err) => {
            if ((err as NodeJS.ErrnoException).code !== 'ECONNRESET') throw err
          })
        },
      },
    },
  },
})
