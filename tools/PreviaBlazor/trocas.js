// TROCAS REPETIDAS NO NAVEGADOR (out/2026). Uso:
//   node tools/PreviaBlazor/trocas.js <url-base> <pasta-saida> [trocas=300]
// Abre /palco (o MESMO PalcoDeTelas do WPF), abre as 14 telas e troca entre elas — e com a
// "tela WPF" no meio — em intervalos de 0 a 400 ms. Mede QUADRO A QUADRO, por dois caminhos:
//  1. uma sonda em requestAnimationFrame lê o DOM a cada quadro pintado: quantas telas à
//     vista, se a da vez tem conteúdo, se aparece esqueleto (carregando) numa tela que VOLTOU;
//  2. o screencast do Chromium grava os quadros que a tela de fato pintou, e o analisar.py
//     procura quadro escuro (o flash) e quadro em branco.
// ⚠️ Isto é o Chromium do Linux: ele NÃO tem a janela nativa do WebView2, onde nascia o
// preto. Mede o que o palco faz DENTRO do navegador; o preto da janela é o --blazor do
// ValidarLayoutWindows, no runner Windows.
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const [,, base, saida, totalTxt] = process.argv;
  const total = +(totalTxt || 300);
  fs.mkdirSync(saida + '/quadros', { recursive: true });
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1366, height: 768 }, deviceScaleFactor: 1, locale: 'pt-BR' });
  const erros = [];
  p.on('console', m => { if (m.type() === 'error') erros.push(m.text()); });
  p.on('pageerror', e => erros.push(e.message));
  await p.addInitScript(() => {
    window.__quadros = [];
    window.__marca = null;
    const passo = () => {
      const telas = [...document.querySelectorAll('.palco-tela')];
      const vis = telas.filter(t => !t.hidden);
      const v = vis[0];
      window.__quadros.push({
        t: performance.now(), marca: window.__marca, total: telas.length, visiveis: vis.length,
        indice: v ? telas.indexOf(v) : -1,
        conteudo: v ? (v.querySelector('.pagina')?.offsetHeight || 0) : 0,
        esqueleto: v ? !!v.querySelector('.esqueleto') : false,
        texto: v ? v.innerText.length : 0,
        erroTela: !!document.body.innerText.includes('Algo deu errado nesta tela'),
      });
      requestAnimationFrame(passo);
    };
    requestAnimationFrame(passo);
  });
  await p.goto(base + '/palco');
  await p.waitForSelector('#barra-palco button', { timeout: 30000 });
  // AUTOTESTE: com FLASH_DE_TESTE=1 a tela à vista nasce PRETA por 200 ms a cada troca — o
  // defeito que o analisar.py existe para pegar. Rodar assim e ver reprovar é o que prova
  // que "nenhum quadro escuro" é medida, e não uma câmera desligada.
  if (process.env.FLASH_DE_TESTE === '1')
    await p.addStyleTag({ content: '@keyframes flash-teste{from{background:#000}to{background:#000}} .palco-tela:not([hidden]){animation:flash-teste .2s}' });
  await p.waitForTimeout(1500);
  const nomes = await p.$$eval('#barra-palco button', bs => bs.map(x => x.dataset.tela).filter(n => n !== 'WPF'));

  const cdp = await p.context().newCDPSession(p);
  const quadros = [];
  cdp.on('Page.screencastFrame', async f => {
    quadros.push({ t: f.metadata.timestamp, data: f.data });
    try { await cdp.send('Page.screencastFrameAck', { sessionId: f.sessionId }); } catch {}
  });
  await cdp.send('Page.startScreencast', { format: 'png', everyNthFrame: 1 });

  const trocas = [];
  const trocar = async (nome, fase) => {
    const t0 = await p.evaluate(n => { window.__marca = n; return performance.now(); }, nome);
    await p.click(`#barra-palco button[data-tela="${nome}"]`);
    trocas.push({ nome, fase, t0, wall: Date.now() / 1000 });
  };
  // Primeira abertura de cada uma, esperando assentar.
  for (const n of nomes) { await trocar(n, 'abrir'); await p.waitForTimeout(900); }
  // As trocas: aleatórias, com a tela WPF no meio uma vez a cada cinco.
  let semente = 20261016;
  const sorteio = m => { semente = (semente * 1103515245 + 12345) % 2147483648; return semente % m; };
  for (let i = 0; i < total; i++) {
    const nome = sorteio(5) === 0 ? 'WPF' : nomes[sorteio(nomes.length)];
    await trocar(nome, 'trocar');
    await p.waitForTimeout(sorteio(5) === 0 ? 0 : sorteio(400));
  }
  await p.waitForTimeout(1500);
  await cdp.send('Page.stopScreencast');
  const sonda = await p.evaluate(() => window.__quadros);
  quadros.forEach((q, i) => fs.writeFileSync(`${saida}/quadros/${String(i).padStart(5, '0')}_${q.t.toFixed(3)}.png`, Buffer.from(q.data, 'base64')));
  fs.writeFileSync(saida + '/sonda.json', JSON.stringify({ nomes, trocas, sonda, erros }));
  console.log(`telas: ${nomes.length} · trocas: ${trocas.length} · quadros sondados: ${sonda.length} · quadros gravados: ${quadros.length}`);
  console.log('erros do console:', erros.length ? erros : 'nenhum');
  await b.close();
})();
