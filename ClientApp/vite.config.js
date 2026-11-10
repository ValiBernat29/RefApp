import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

// Builds into ASP.NET's wwwroot/dist with stable file names, so Razor views can
// reference them directly (cache-busting is handled by asp-append-version).
export default defineConfig({
  plugins: [vue()],
  // The built files are served from /dist, so lazy-loaded page chunks must be requested from there.
  base: '/dist/',
  build: {
    outDir: '../wwwroot/dist',
    emptyOutDir: true,
    // Written to dist/manifest.json. ViteAssets (C#) reads it to find the hashed entry file names.
    manifest: 'manifest.json',
    rollupOptions: {
      // A single entry: the Vue single-page app that SpaController serves at /app.
      input: { app: 'src/main.js' },
      output: {
        // Content-hashed names: a file's URL changes whenever its content does, so browsers never serve stale code.
        // The entry's URL must be exactly the one the page loads, or the browser would run the app twice.
        entryFileNames: '[name]-[hash].js',
        chunkFileNames: 'chunks/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash][extname]',
      },
    },
  },
});
