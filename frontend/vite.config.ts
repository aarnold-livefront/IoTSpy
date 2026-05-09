import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      onwarn(warning, warn) {
        // Suppress pure annotation warnings from SignalR library
        if (
          warning.loc?.file?.includes('@microsoft/signalr') &&
          warning.message?.includes('/*#__PURE__*/')
        ) {
          return
        }
        warn(warning)
      },
    },
  },
  server: {
    port: 3000,
    host: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        configure: (proxy) => {
          proxy.removeAllListeners('error')
          proxy.on('error', (err) => {
            if ((err as NodeJS.ErrnoException).code !== 'ECONNRESET') {
              console.error('[vite] api proxy error:', err)
            }
          })
        },
      },
      '/hubs': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
        configure: (proxy) => {
          // Remove Vite's default error listener so it doesn't log before our handler runs.
          // ECONNRESET is expected when the backend restarts or SignalR closes a connection
          // — the frontend SignalR client handles reconnection automatically.
          proxy.removeAllListeners('error')
          proxy.on('error', (err) => {
            if ((err as NodeJS.ErrnoException).code !== 'ECONNRESET') {
              console.error('[vite] ws proxy error:', err)
            }
          })
        },
      },
    },
  },
})
