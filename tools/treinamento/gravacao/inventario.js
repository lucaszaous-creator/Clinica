// INVENTÁRIO das telas para escrever os roteiros das videoaulas (out/2026). Uso:
//   node inventario.js <url-da-previa> <navegacao.json> <pasta-saida>
// Para cada tela folha do Treinamento: abre a aula na prévia (moldura real + tela), espera a
// tela assentar e grava <chave>.png e <chave>.json com o que dá para apontar e clicar —
// botões, abas, campos, títulos — pelo NOME acessível, que é como o roteiro os acha. Não
// clica em nada: é a fotografia do que existe, para o roteiro não inventar rótulo.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const { lerNavegacao, aulasDaNavegacao, urlDaAula } = require('./comum');
const { executar } = require('./gravar');

// Tela que só mostra o conteúdo com um paciente escolhido: o roteiro diz como chegar lá
// (url e passos "antes"), e o inventário fotografa a tela JÁ nesse estado.
const PASTA_ROTEIROS = path.join(__dirname, 'roteiros');
const roteiros = Object.fromEntries((fs.existsSync(PASTA_ROTEIROS) ? fs.readdirSync(PASTA_ROTEIROS) : [])
  .filter(f => f.endsWith('.json')).map(f => JSON.parse(fs.readFileSync(path.join(PASTA_ROTEIROS, f), 'utf8'))).map(r => [r.chave, r]));

(async () => {
  const [, , base, navegacao, saida] = process.argv;
  fs.mkdirSync(saida, { recursive: true });
  const aulas = aulasDaNavegacao(lerNavegacao(navegacao));
  const b = await chromium.launch();
  const indice = [];
  for (const aula of aulas) {
    const p = await b.newPage({ viewport: { width: 1440, height: 810 }, deviceScaleFactor: 1, locale: 'pt-BR' });
    const erros = [];
    p.on('console', m => { if (m.type() === 'error') erros.push(m.text()); });
    p.on('pageerror', e => erros.push(e.message));
    const roteiro = roteiros[aula.chave] || {};
    const doApp = roteiro.app ? { ...aula, app: roteiro.app } : aula;
    let info = { ...doApp, url: urlDaAula(base, doApp, roteiro.url || {}) };
    try {
      await p.goto(info.url);
      await p.waitForSelector('.moldura', { timeout: 30000 });
      await p.waitForTimeout(3000);
      // "inventario": passos só do inventário (ex.: escolher o paciente que o vídeo escolhe
      // na frente da pessoa), para fotografar o que a tela mostra DEPOIS deles.
      const preparo = [...(roteiro.antes || []), ...(roteiro.inventario || [])];
      for (const passo of preparo) await executar(p, passo, null, true);
      if (preparo.length) await p.waitForTimeout(1500);
      const dados = await p.evaluate(() => {
        const visivel = el => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden'; };
        const nome = el => (el.getAttribute('aria-label') || el.innerText || el.getAttribute('placeholder') || el.getAttribute('title') || el.value || '').replace(/\s+/g, ' ').trim().slice(0, 120);
        const palco = document.querySelector('.palco-tela:not([hidden])') || document.body;
        const rect = el => { const r = el.getBoundingClientRect(); return [Math.round(r.x), Math.round(r.y), Math.round(r.width), Math.round(r.height)]; };
        // O roteiro acha o elemento pelo que a pessoa lê: rótulo acessível (aria-label, que o
        // "campo=" usa), placeholder ("placeholder=") ou o texto do botão ("botao="). Os três
        // ficam separados para o roteiro escolher o localizador certo.
        const controles = [...palco.querySelectorAll('button, a[href], input, select, textarea, [role=tab], [role=button], [role=textbox], [role=option], [role=checkbox], summary')]
          .filter(visivel).map(el => ({ tag: el.tagName.toLowerCase(), tipo: el.getAttribute('type'), role: el.getAttribute('role'), nome: nome(el),
            aria: el.getAttribute('aria-label') || null, placeholder: el.getAttribute('placeholder') || null,
            classe: el.className && el.className.baseVal === undefined ? el.className : '', desabilitado: !!el.disabled, rect: rect(el) }));
        const titulos = [...palco.querySelectorAll('h1,h2,h3,h4,legend,th,.rotulo,label')].filter(visivel).map(el => el.innerText.replace(/\s+/g, ' ').trim()).filter(Boolean).slice(0, 120);
        const abasMoldura = [...document.querySelectorAll('.moldura-abas [role=tab]')].map(el => el.innerText.trim());
        const falha = document.querySelector('.moldura')?.innerText.includes('não tem componente na prévia');
        return { controles, titulos, abasMoldura, falha, texto: palco.innerText.replace(/\n{2,}/g, '\n').slice(0, 4000), altura: palco.scrollHeight };
      });
      info = { ...info, ...dados, erros };
      await p.screenshot({ path: path.join(saida, aula.chave + '.png') });
    } catch (e) {
      info.erro = String(e);
      try { await p.screenshot({ path: path.join(saida, aula.chave + '.png') }); } catch { }
    }
    fs.writeFileSync(path.join(saida, aula.chave + '.json'), JSON.stringify(info, null, 2));
    indice.push({ chave: aula.chave, app: aula.app, componente: aula.componente, controles: info.controles?.length ?? 0, erros: (info.erros || []).length, erro: info.erro || null, falha: info.falha || false });
    console.log(aula.app.padEnd(24), aula.chave.padEnd(36), info.erro ? 'ERRO' : `${info.controles.length} controles`);
    await p.close();
  }
  fs.writeFileSync(path.join(saida, 'indice.json'), JSON.stringify(indice, null, 2));
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
