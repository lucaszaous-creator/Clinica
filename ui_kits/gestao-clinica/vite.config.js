import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  // caminhos relativos: o dist abre por file:// e por servidor estático
  base: './',
  plugins: [react()],
  server: {
    port: 5273,
    // styles.css e tokens/ vivem na RAIZ do repositório — o kit lê os tokens de lá em vez
    // de manter uma segunda cópia, que divergiria na primeira correção de cor.
    fs: { allow: ['../..'] },
  },
});
