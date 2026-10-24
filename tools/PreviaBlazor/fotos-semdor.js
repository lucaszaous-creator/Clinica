// As fotos do @SemDor na tela de WhatsApp (24/10/2026, docs/whatsapp-nativo-implementacao.md §10), em 1366 e 1024.
// Com a prévia no relógio fixo (docs/telas-blazor.md §5):
//   TZ=America/Sao_Paulo faketime -f '@2026-10-23 10:12:00' dotnet run --project tools/PreviaBlazor --urls http://127.0.0.1:5199
//   node tools/PreviaBlazor/fotos-semdor.js              → 50–53 (ligado: etiquetas, sugestão, revisão com ↶, resumo)
// e, numa prévia com CLINICA_PREVIA_CRM=semdor-desligado (ou semdor-falhando):
//   node tools/PreviaBlazor/fotos-semdor.js desligado    → 56–57 (nada do @SemDor na tela)
//   node tools/PreviaBlazor/fotos-semdor.js falhando     → 58 (o aviso da gestão e a frase do CRM no clique)
// PREVIA_URL troca o endereço (padrão http://127.0.0.1:5199). Nada aqui grava no CRM de mentira: o ✨ e o resumo
// só leem, e o rascunho vive na página — o roteiro pode rodar mais de uma vez na mesma prévia.
// Reprova, como o fotos-whatsapp.js, página que rola para o lado e filho que vaza do grupo.
const { chromium } = require('playwright');
const path = require('path');
const DIR = path.join(__dirname, '../../docs/mockups/whatsapp-nativo/tela-real');
const URL = (process.env.PREVIA_URL || 'http://127.0.0.1:5199') + '/tela/ConversasDoWhatsApp';
const MODO = process.argv[2] || 'ligado';
const erros = [], feitas = [], falhas = [];
const pausa = ms => new Promise(r => setTimeout(r, ms));

async function abrir(b, w, h) {
  const p = await b.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1, locale: 'pt-BR' });
  p.on('console', m => { if (m.type() === 'error') erros.push(m.text()); });
  p.on('pageerror', e => erros.push(e.message));
  await p.goto(URL); await p.waitForSelector('.wa-linha', { timeout: 90000 }); await pausa(1500);
  return p;
}
async function filtro(p, nome) { await p.locator('.wa-filtros button', { hasText: nome }).first().click({ timeout: 8000 }); await pausa(900); }
async function linha(p, texto) { await p.locator('.wa-linha', { hasText: texto }).first().click({ timeout: 8000 }); await pausa(1500); }
// Depois do ✨, espera a notificação passar: ela cobre o rodapé do campo, que é o que a foto quer mostrar (o ↶).
async function brilho(p) { await p.locator('.wa-semdor-sugerir').first().click({ timeout: 8000 }); await pausa(1200);
  await p.locator('.notificacoes .notificacao').first().waitFor({ state: 'detached', timeout: 12000 }).catch(() => {}); await pausa(600); }
async function escrever(p, texto) { await p.locator('.wa-escrever textarea').fill(texto); await pausa(500); }

const ROTEIROS = {
  ligado: [
    ['50-semdor-lista', async p => { await filtro(p, 'Todas'); }],
    ['51-semdor-sugestao', async p => { await filtro(p, 'Todas'); await linha(p, 'Maria das Graças'); await brilho(p); }],
    ['52-semdor-revisao', async p => { await filtro(p, 'Todas'); await linha(p, 'Maria das Graças');
        await escrever(p, 'oi maria, vc pode vir amanha as 14h? confirma pra mim'); await brilho(p); }],
    ['53-semdor-resumo', async p => { await filtro(p, 'Todas'); await linha(p, 'Maria das Graças');
        await p.locator('[data-acao=resumo]').click({ timeout: 8000 }); await pausa(1200); }],
  ],
  desligado: [
    ['56-semdor-desligado-lista', async p => { await filtro(p, 'Todas'); }],
    ['57-semdor-desligado-conversa', async p => { await filtro(p, 'Todas'); await linha(p, 'Maria das Graças'); }],
  ],
  falhando: [
    ['58-semdor-falhando', async p => { await filtro(p, 'Todas'); await linha(p, 'Maria das Graças'); await escrever(p, 'oi maria'); await brilho(p); }],
  ],
};

(async () => {
  const roteiro = ROTEIROS[MODO];
  if (!roteiro) { console.error(`Modo desconhecido: ${MODO} (ligado | desligado | falhando)`); process.exit(2); }
  const b = await chromium.launch();
  for (const [nome, passos] of roteiro) {
    for (const [w, h] of [[1366, 900], [1024, 768]]) {
      const p = await abrir(b, w, h);
      try {
        await passos(p);
        const larg = await p.evaluate(() => document.documentElement.scrollWidth);
        if (larg > w) falhas.push(`${nome}-${w}: rola para o lado (${larg})`);
        const vazam = await p.evaluate(() => [...document.querySelectorAll('.segmentado, .wa-cab-acoes, .wa-faixa, .wa-filtros, .wa-lista-topo, .wa-escrever-rodape, .wa-linha-rodape')]
          .flatMap(g => { const r = g.getBoundingClientRect(); if (!r.width) return [];
            return [...g.children].filter(c => { const x = c.getBoundingClientRect(); return x.width && (x.right > r.right + 1 || x.left < r.left - 1); })
              .map(c => `${g.className.split(' ').pop()} > ${(c.textContent || c.className).trim().slice(0, 24)}`); }));
        if (vazam.length) falhas.push(`${nome}-${w}: vaza do grupo: ${[...new Set(vazam)].join(' | ')}`);
        await p.screenshot({ path: `${DIR}/${nome}-${w}.png` }); feitas.push(`${nome}-${w}`);
      } catch (e) { falhas.push(`${nome}-${w}: ${e.message.split('\n')[0]}`); }
      await p.close();
    }
  }
  await b.close();
  console.log(`feitas (${feitas.length}): ${feitas.join(', ')}`);
  if (erros.length) console.log('erros do console:\n  ' + [...new Set(erros)].join('\n  '));
  if (falhas.length) { console.log('FALHAS:\n  ' + falhas.join('\n  ')); process.exit(1); }
})();
