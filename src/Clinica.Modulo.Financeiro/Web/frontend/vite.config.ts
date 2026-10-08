import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  resolve: { dedupe: ['react', 'react-dom', '@mantine/core', '@mantine/hooks', 'motion'] },
  build: { outDir: '../wwwroot', emptyOutDir: true, sourcemap: false },
});
