#!/usr/bin/env node
/*
 * GERADOR DOS MANUAIS EM PDF.
 *
 * Lê os HTML de `docs/manuais/` e imprime um PDF por manual em `docs/manuais/pdf/`.
 * O rodapé (nome do manual + "página X de Y") é desenhado aqui, e não em CSS, porque
 * o Chromium não implementa as caixas de margem do @page — sem isto, um manual de
 * quarenta páginas sairia sem numeração e o sumário não teria como apontar nada.
 *
 * Uso:  node tools/gerar-manuais.js
 * Exige o Playwright e o Chromium já instalados no ambiente.
 */
const path = require('path');
const fs = require('fs');

const RAIZ = path.resolve(__dirname, '..');
const ENTRADA = path.join(RAIZ, 'docs', 'manuais');
const SAIDA = path.join(ENTRADA, 'pdf');

function carregarPlaywright() {
  const candidatos = [
    'playwright',
    '/opt/node22/lib/node_modules/playwright',
    '/usr/lib/node_modules/playwright'
  ];
  for (const c of candidatos) {
    try { return require(c); } catch { /* tenta o próximo */ }
  }
  throw new Error('Playwright não encontrado. Instale com: npm i -g playwright');
}

const rodape = (titulo) => `
  <div style="font-family:Arial,sans-serif;font-size:7.5pt;color:#6B7280;width:100%;
              padding:0 13mm;display:flex;justify-content:space-between;align-items:center;">
    <span>${titulo}</span>
    <span>página <span class="pageNumber"></span> de <span class="totalPages"></span></span>
  </div>`;

(async () => {
  const { chromium } = carregarPlaywright();
  fs.mkdirSync(SAIDA, { recursive: true });

  const manuais = fs.readdirSync(ENTRADA)
    .filter(f => f.startsWith('manual-') && f.endsWith('.html'))
    .sort();

  if (manuais.length === 0) {
    console.error('Nenhum manual-*.html em docs/manuais/');
    process.exit(1);
  }

  const navegador = await chromium.launch();
  for (const arquivo of manuais) {
    const pagina = await navegador.newPage();
    await pagina.goto('file://' + path.join(ENTRADA, arquivo), { waitUntil: 'networkidle' });
    const titulo = await pagina.title();
    const destino = path.join(SAIDA, arquivo.replace(/\.html$/, '.pdf'));
    await pagina.pdf({
      path: destino,
      format: 'A4',
      printBackground: true,
      displayHeaderFooter: true,
      headerTemplate: '<div></div>',
      footerTemplate: rodape(titulo),
      margin: { top: '16mm', bottom: '18mm', left: '13mm', right: '13mm' }
    });
    await pagina.close();
    const kb = Math.round(fs.statSync(destino).size / 1024);
    console.log(`  ${arquivo}  →  pdf/${path.basename(destino)}  (${kb} KB)`);
  }
  await navegador.close();
  console.log(`\n${manuais.length} manual(is) gerado(s) em docs/manuais/pdf/`);
})().catch(e => { console.error(e); process.exit(1); });
