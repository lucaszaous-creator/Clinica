// Rolagem para o lado no celular — a checagem do CI (nov/2026, pedido do dono: "responsividade profissional").
//
// Abre, na PRÉVIA (tools/PreviaBlazor, dados fictícios), cada tela Blazor solta (/tela/X) e cada tela da gestão web
// dentro da moldura (/aula/gestao/chave, com CLINICA_NAVEGACAO apontando para o catalogo-gestao.json), em 360, 390 e
// 430 px, e mede:
//   1. a PÁGINA não rola para o lado (scrollWidth <= largura);
//   2. nenhum elemento passa da borda direita, a não ser dentro de uma faixa que rola sozinha (abas, chips, tabela);
//   3. as JANELAS da tela (cada botão que abre um .modal): o corpo não rola para o lado e o rodapé cabe.
// Reprova (exit 1) listando tela, largura e o elemento que estourou. Fotos de cada falha em DIR.
//
// Uso: node tools/responsivo/varredura.mjs BASE [DIR] — env MOTOR=webkit|chromium (padrão webkit, o motor do iPhone),
//      LARGURAS=360,390,430, JANELAS=0 para pular as janelas, SO=tela1,tela2 para restringir.
import { webkit, chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';

const BASE = (process.argv[2] ?? 'http://127.0.0.1:5199').replace(/\/$/, '');
const DIR = process.argv[3] ?? 'artifacts/responsivo';
const MOTOR = process.env.MOTOR ?? 'webkit';
const LARGURAS = (process.env.LARGURAS ?? '360,390,430').split(',').map(Number);
const JANELAS = process.env.JANELAS !== '0';
const SO = process.env.SO ? new Set(process.env.SO.split(',')) : null;
const pausa = ms => new Promise(r => setTimeout(r, ms));
await mkdir(DIR, { recursive: true });

const navegador = await (MOTOR === 'chromium' ? chromium : webkit).launch();
const indice = await navegador.newPage();
await indice.goto(BASE + '/', { waitUntil: 'networkidle' });
let alvos = (await indice.$$eval('a[href^="tela/"]', as => as.map(a => a.getAttribute('href')))).map(h => ({ nome: h.slice(5), url: '/' + h }));
await indice.close();
// A moldura da gestão: as chaves do catálogo (quando a prévia tem CLINICA_NAVEGACAO).
if (process.env.CATALOGO) {
  const { readFile } = await import('node:fs/promises');
  for (const app of JSON.parse(await readFile(process.env.CATALOGO, 'utf8')))
    for (const b of app.barra) for (const k of [...(b.itens ?? []), ...(b.direto ? [b.direto] : [])])
      alvos.push({ nome: `${app.app}/${k}`, url: `/aula/${app.app}/${k}` });
}
if (SO) alvos = alvos.filter(a => SO.has(a.nome));

// Quem passa da borda — fora de faixa que rola sozinha (overflow-x auto/scroll/hidden/clip) e fora de position:fixed.
const medir = () => {
  const W = document.documentElement.clientWidth;
  const contido = e => { for (let a = e.parentElement; a && a !== document.body; a = a.parentElement) { const o = getComputedStyle(a).overflowX; if (o !== 'visible') return true; } return false; };
  const fora = [...document.querySelectorAll('body *')].filter(e => {
    const r = e.getBoundingClientRect(); const cs = getComputedStyle(e);
    return r.width > 0 && r.height > 0 && r.right > W + 1 && cs.position !== 'fixed' && cs.visibility !== 'hidden' && !contido(e);
  });
  const nome = e => (e.tagName.toLowerCase() + (typeof e.className === 'string' && e.className.trim() ? '.' + e.className.trim().split(/\s+/).slice(0, 3).join('.') : '')).slice(0, 60);
  return { lado: document.documentElement.scrollWidth - W, fora: [...new Set(fora.filter(e => !fora.includes(e.parentElement)).map(e => nome(e) + ` (+${Math.round(e.getBoundingClientRect().right - W)}px)`))].slice(0, 4) };
};
const medirJanela = () => {
  const m = document.querySelector('.modal'); if (!m) return null;
  const W = document.documentElement.clientWidth, c = m.querySelector('.modal-corpo') ?? m;
  const rodape = [...m.querySelectorAll('.modal-rodape > *')].filter(e => e.getBoundingClientRect().right > W + 1 && getComputedStyle(e).display !== 'none').length;
  return { corpo: c.scrollWidth - c.clientWidth, janela: Math.round(m.getBoundingClientRect().right - W), rodape, titulo: m.querySelector('h2')?.innerText?.slice(0, 40) };
};

const falhas = [];
const registrar = f => { falhas.push(f); console.log('  ✗', JSON.stringify(f)); };
const foto = async (p, nome) => p.screenshot({ path: path.join(DIR, nome.replace(/[^\w.-]+/g, '_') + '.png') }).catch(() => {});
for (const w of LARGURAS) {
  const ctx = await navegador.newContext({ viewport: { width: w, height: 800 }, isMobile: MOTOR !== 'firefox', hasTouch: true, locale: 'pt-BR' });
  for (const alvo of alvos) {
    let p;
    try {
      p = await ctx.newPage();
      await p.goto(BASE + alvo.url, { waitUntil: 'networkidle', timeout: 30000 }); await pausa(1200);
      const m = await p.evaluate(medir);
      if (m.lado > 0 || m.fora.length) { registrar({ tela: alvo.nome, largura: w, ...m }); await foto(p, `${alvo.nome}-${w}`); }
      if (JANELAS) {
        const rotulos = (await p.$$eval('button:not([disabled])', bs => bs.filter(b => b.offsetParent && !b.closest('.moldura-nav,.moldura-topo,.moldura-lateral,.chat-flutuante') && b.innerText.trim().length > 2
          && !/excluir|cancelar|apagar|sair|remover|desfazer|estornar|encerrar/i.test(b.innerText)).map(b => b.innerText.trim().split('\n')[0])))
          .filter((x, i, a) => a.indexOf(x) === i).slice(0, 8);
        for (const r of rotulos) {
          try {
            await p.getByRole('button', { name: r, exact: true }).first().click({ timeout: 2000 }); await pausa(900);
            const j = await p.evaluate(medirJanela);
            if (j && (j.corpo > 0 || j.janela > 0 || j.rodape > 0)) { registrar({ tela: alvo.nome, largura: w, janela: r, ...j }); await foto(p, `${alvo.nome}-janela-${r.slice(0, 24)}-${w}`); }
            if (j) {
              await p.keyboard.press('Escape'); await pausa(300);
              if (await p.$('.modal')) await p.locator('.modal .fechar[aria-label="Fechar"]').first().click({ timeout: 1500 }).catch(() => {});
              await pausa(300);
              if (await p.$('.modal')) { await p.goto(BASE + alvo.url, { waitUntil: 'networkidle' }); await pausa(900); }
            }
          } catch { /* botão que sumiu depois de um clique: segue */ }
        }
      }
    } catch (e) { registrar({ tela: alvo.nome, largura: w, erro: e.message.split('\n')[0] }); }
    await p?.close().catch(() => {});
  }
  await ctx.close().catch(() => {});
}
await navegador.close();
console.log(`${MOTOR}: ${alvos.length} telas × ${LARGURAS.join('/')} px${JANELAS ? ' (com as janelas)' : ''} — ${falhas.length} falha(s)`);
process.exitCode = falhas.length ? 1 : 0;
