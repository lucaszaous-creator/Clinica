#!/usr/bin/env node
/*
 * GERADOR DOS MANUAIS EM PDF.
 *
 * Lê os HTML de `docs/manuais/` e imprime um PDF por manual em `docs/manuais/pdf/`.
 *
 * O que ele faz além de imprimir:
 *
 * 1. TROCA A ILUSTRAÇÃO PELA CAPTURA REAL. Cada <figure data-captura="nome"> é um
 *    ENCAIXE: se existir `docs/manuais/capturas/nome.png`, a figura sai com a foto da
 *    tela; se não existir, sai o desenho com um selo laranja dizendo que é ilustração.
 *    ⚠️ O selo é obrigatório enquanto não há foto — reprodução que se passa por captura
 *    é a garantia aparente do projeto vestida de manual, e quem descobre é o cliente.
 *    Roteiro do que capturar: `docs/manuais/capturas/ROTEIRO.md`.
 *
 * 2. PDF INTERATIVO. `outline:true` gera os marcadores laterais do leitor a partir dos
 *    títulos; `tagged:true` gera a árvore de estrutura (leitor de tela e reflow); os
 *    href="#cap-N" do sumário e das remissões viram links clicáveis dentro do PDF.
 *
 * 3. RODAPÉ com "página X de Y" — desenhado aqui e não em CSS, porque o Chromium não
 *    implementa as caixas de margem do @page.
 *
 * Uso:  node tools/gerar-manuais.js
 */
const path = require('path');
const fs = require('fs');

const RAIZ = path.resolve(__dirname, '..');
const ENTRADA = path.join(RAIZ, 'docs', 'manuais');
const CAPTURAS = path.join(ENTRADA, 'capturas');
const SAIDA = path.join(ENTRADA, 'pdf');

function carregarPlaywright() {
  const candidatos = ['playwright', '/opt/node22/lib/node_modules/playwright',
                      '/usr/lib/node_modules/playwright'];
  for (const c of candidatos) {
    try { return require(c); } catch { /* tenta o próximo */ }
  }
  throw new Error('Playwright não encontrado. Instale com: npm i -g playwright');
}

/** Extensões aceitas para a captura, na ordem de preferência. */
const EXTENSOES = ['.png', '.jpg', '.jpeg'];

function acharCaptura(nome) {
  for (const ext of EXTENSOES) {
    const arquivo = path.join(CAPTURAS, nome + ext);
    if (fs.existsSync(arquivo)) return 'capturas/' + nome + ext;
  }
  return null;
}

/**
 * Troca o conteúdo de cada <figure data-captura="..."> pela imagem, quando ela existe.
 * O que fica antes do <figcaption> é o desenho; é ele que sai ou entra.
 */
function aplicarCapturas(html, contagem) {
  return html.replace(
    /<figure data-captura="([^"]+)">([\s\S]*?)(<figcaption)/g,
    (todo, nome, desenho, fim) => {
      const src = acharCaptura(nome);
      if (src) {
        contagem.reais++;
        return `<figure data-captura="${nome}">\n<img class="captura" src="${src}" alt="Tela do sistema: ${nome}">\n${fim}`;
      }
      contagem.desenhos++;
      const selo = '<div class="selo-ilustracao">ILUSTRAÇÃO — não é uma captura da tela real</div>\n';
      return `<figure data-captura="${nome}">\n${selo}${desenho}${fim}`;
    });
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
  fs.mkdirSync(CAPTURAS, { recursive: true });

  const manuais = fs.readdirSync(ENTRADA)
    .filter(f => f.startsWith('manual-') && f.endsWith('.html'))
    .sort();

  if (manuais.length === 0) {
    console.error('Nenhum manual-*.html em docs/manuais/');
    process.exit(1);
  }

  const navegador = await chromium.launch();
  const temporarios = [];
  let totalReais = 0, totalDesenhos = 0;

  for (const arquivo of manuais) {
    const origem = path.join(ENTRADA, arquivo);
    const contagem = { reais: 0, desenhos: 0 };
    const html = aplicarCapturas(fs.readFileSync(origem, 'utf8'), contagem);
    totalReais += contagem.reais;
    totalDesenhos += contagem.desenhos;

    // O temporário fica NA MESMA PASTA: o CSS e as capturas são caminhos relativos.
    const temporario = path.join(ENTRADA, '.gerando-' + arquivo);
    fs.writeFileSync(temporario, html);
    temporarios.push(temporario);

    const pagina = await navegador.newPage();
    await pagina.goto('file://' + temporario, { waitUntil: 'networkidle' });
    const titulo = await pagina.title();
    const destino = path.join(SAIDA, arquivo.replace(/\.html$/, '.pdf'));
    await pagina.pdf({
      path: destino,
      format: 'A4',
      printBackground: true,
      tagged: true,     // árvore de estrutura (acessibilidade, reflow)
      outline: true,    // marcadores laterais do leitor, a partir dos títulos
      displayHeaderFooter: true,
      headerTemplate: '<div></div>',
      footerTemplate: rodape(titulo),
      margin: { top: '16mm', bottom: '18mm', left: '13mm', right: '13mm' }
    });
    await pagina.close();

    const kb = Math.round(fs.statSync(destino).size / 1024);
    const figuras = contagem.reais + contagem.desenhos;
    const nota = figuras === 0 ? '' :
      `  ·  ${contagem.reais}/${figuras} figura(s) com captura real`;
    console.log(`  ${arquivo}  →  pdf/${path.basename(destino)}  (${kb} KB)${nota}`);
  }

  await navegador.close();
  for (const t of temporarios) fs.unlinkSync(t);

  console.log(`\n${manuais.length} manual(is) gerado(s) em docs/manuais/pdf/`);
  if (totalDesenhos > 0) {
    console.log(
      `\n⚠️  ${totalDesenhos} de ${totalReais + totalDesenhos} figuras ainda são ILUSTRAÇÃO.\n` +
      `   Para trocar por telas reais, ponha os PNG em docs/manuais/capturas/ com os nomes\n` +
      `   do roteiro (docs/manuais/capturas/ROTEIRO.md) e rode este comando de novo.`);
  }
})().catch(e => { console.error(e); process.exit(1); });
