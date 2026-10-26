import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Dev: proxy /api và /health sang backend ASP.NET Core.
// Build: xuất vào wwwroot của API để chạy chung một ứng dụng.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 8090,
    proxy: {
      '/api': 'http://localhost:5080',
      '/health': 'http://localhost:5080',
    },
  },
  build: {
    outDir: '../CryptoBot.Api/wwwroot',
    emptyOutDir: true,
  },
});
