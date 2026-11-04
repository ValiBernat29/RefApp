import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

// Builds into ASP.NET's wwwroot/dist with stable file names, so Razor views can
// reference them directly (cache-busting is handled by asp-append-version).
export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: '../wwwroot/dist',
    emptyOutDir: true,
    cssCodeSplit: false,
    rollupOptions: {
      // One entry per server-rendered page that hosts a Vue app.
      input: {
        'referee-map': 'src/main.js',
        assign: 'src/assign-main.js',
      },
      output: {
        entryFileNames: '[name].js',
        chunkFileNames: 'chunks/[name]-[hash].js',
        // All pages share one stylesheet (cssCodeSplit is off).
        assetFileNames: (info) => {
          const name = info.names?.[0] ?? info.name ?? '';
          return name.endsWith('.css') ? 'app[extname]' : 'assets/[name]-[hash][extname]';
        },
      },
    },
  },
});
