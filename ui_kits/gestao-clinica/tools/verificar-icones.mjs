#!/usr/bin/env node
/* Confere se TODO nome de ícone escrito no kit existe no lucide-react.
   Por que existe: nome de ícone é resolvido por STRING em tempo de execução — o build
   não pega, e a referência HTML nem erro dá (o ícone só some). Aqui um nome errado
   falha o comando, meses antes de alguém abrir a tela e não achar a lupa.
   Uso: npm run verificar-icones */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as Lucide from 'lucide-react';

const RAIZ = join(fileURLToPath(new URL('.', import.meta.url)), '..');
const FONTE = join(RAIZ, 'src');

const arquivos = (dir) => readdirSync(dir).flatMap(n => {
  const p = join(dir, n);
  return statSync(p).isDirectory() ? arquivos(p) : (/\.jsx?$/.test(p) ? [p] : []);
});

// icon="x" · iconRight="x" · icon:'x' · <Icon name="x"> · Icon,{name:'x'}
const PADROES = [
  /\bicon(?:Right)?\s*[:=]\s*["']([a-z0-9-]+)["']/g,
  /<Icon\b[^>]*?\bname\s*=\s*["']([a-z0-9-]+)["']/g,
  /\bIcon\s*,\s*\{\s*name\s*:\s*["']([a-z0-9-]+)["']/g,
];

const paraPascal = n => n.split('-').filter(Boolean).map(p => p[0].toUpperCase() + p.slice(1)).join('');

let usados = 0;
const faltando = [];
for (const arq of arquivos(FONTE)) {
  const texto = readFileSync(arq, 'utf8');
  for (const padrao of PADROES) {
    for (const [, nome] of texto.matchAll(padrao)) {
      usados++;
      const chave = paraPascal(nome);
      if (!Lucide.icons[chave] && !Lucide[chave]) faltando.push(`${relative(RAIZ, arq)}: "${nome}"`);
    }
  }
}

if (faltando.length) {
  console.error(`ERRO: ${faltando.length} nome(s) de ícone não existem no lucide-react:`);
  faltando.forEach(f => console.error('  ' + f));
  console.error('Confira em https://lucide.dev/icons — o kebab-case é o mesmo do pacote.');
  process.exit(1);
}
console.log(`Ícones: ${usados} usos conferidos, todos existem no lucide-react (${Object.keys(Lucide.icons).length} disponíveis).`);
