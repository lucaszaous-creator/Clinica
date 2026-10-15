// Fotografa uma tela da prévia (out/2026). Uso:
//   node tools/PreviaBlazor/foto.js <url> <saida.png> <largura> <altura> [clique ...]
// Cada passo, em ordem, antes da foto: um seletor CSS (clica), "texto=Rótulo do botão"
// (clica no botão com esse nome) ou "digitar=seletor|texto" (preenche o campo). É assim que
// se fotografa um FORMULÁRIO aberto e preenchido, e não só a tela de trás.
// Imprime os erros do console: tela que fotografa bonita e loga erro não está pronta.
const { chromium } = require('playwright');
(async () => {
  const [,, url, saida, largura, altura, ...cliques] = process.argv;
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: +largura, height: +altura }, deviceScaleFactor: 1, locale: 'pt-BR' });
  const erros = [];
  p.on('console', m => { if (m.type() === 'error') erros.push(m.text()); });
  p.on('pageerror', e => erros.push(e.message));
  await p.goto(url);
  await p.waitForSelector('.pagina', { timeout: 30000 });
  await p.waitForTimeout(1500);
  for (const c of cliques) {
    if (c.startsWith('digitar=')) {
      const [sel, ...texto] = c.slice(8).split('|');
      await p.locator(sel).first().fill(texto.join('|'));
      await p.waitForTimeout(700);
      continue;
    }
    const alvo = c.startsWith('texto=') ? p.getByRole('button', { name: c.slice(6) }).first() : p.locator(c).first();
    await alvo.click({ timeout: 10000 });
    await p.waitForTimeout(700);
  }
  await p.screenshot({ path: saida, fullPage: !cliques.length });
  console.log('erros:', erros.length ? erros : 'nenhum');
  await b.close();
})();
