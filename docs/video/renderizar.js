/**
 * renderizar.js — renderiza um dos HTMLs de cena para MP4, sem gravar tela.
 * ---------------------------------------------------------------------------
 *   node docs/video/renderizar.js <arquivo.html> <largura> <altura> <segundos> <saida.mp4>
 *
 * Ex.:
 *   node docs/video/renderizar.js docs/video/vertical/vertical-41s.html 1080 1920 41 vertical.mp4
 *   node docs/video/renderizar.js docs/video/cenas-animadas.html      1920 1080 90 entrega.mp4
 *
 * POR QUE TEMPO VIRTUAL
 * As animações são transições CSS, que correm no relógio de PAREDE. Tirar um
 * print, esperar, tirar outro produziria quadros com espaçamento irregular — o
 * vídeo sai trêmulo e nenhum ajuste de fps conserta. `Emulation.setVirtualTime
 * Policy` congela o tempo do navegador e o avança em passos exatos de 1/fps:
 * o relógio do JS, o requestAnimationFrame e as transições CSS andam juntos, e
 * o quadro N é sempre o mesmo quadro N.
 *
 * Requer: puppeteer-core + o Chromium do ambiente + ffmpeg.
 */
const puppeteer = require('puppeteer-core');
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');

const CHROME = process.env.CHROME_BIN || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const FPS = Number(process.env.FPS || 30);

async function main() {
  const [arquivo, larguraS, alturaS, segundosS, saida] = process.argv.slice(2);
  if (!arquivo || !saida) {
    console.error('uso: node renderizar.js <html> <largura> <altura> <segundos> <saida.mp4>');
    process.exit(1);
  }
  const L = Number(larguraS), A = Number(alturaS), SEG = Number(segundosS);
  const TOTAL = Math.round(SEG * FPS);
  // Carrega PAUSADO (sem play=1). O play é dado depois que o tempo virtual já
  // está congelado — senão a animação corre durante o carregamento e o quadro
  // 0 já sai adiantado.
  const url = 'file://' + path.resolve(arquivo) + '?c=1';

  const tmp = fs.mkdtempSync('/tmp/quadros-');
  console.log(`render ${L}x${A} @${FPS}fps · ${SEG}s · ${TOTAL} quadros`);

  const browser = await puppeteer.launch({
    executablePath: CHROME,
    headless: 'new',
    args: [
      '--no-sandbox', '--disable-gpu', '--hide-scrollbars',
      '--force-device-scale-factor=1',
      // Sem isto o Chrome adia trabalho de páginas em segundo plano e alguns
      // quadros saem sem a animação aplicada.
      '--disable-background-timer-throttling',
      '--disable-renderer-backgrounding',
      '--disable-backgrounding-occluded-windows',
      // Determinismo do compositor: garante que cada quadro seja desenhado por
      // inteiro antes de ser capturado.
      '--run-all-compositor-stages-before-draw',
      '--disable-new-content-rendering-timeout',
      `--window-size=${L},${A}`
    ],
    protocolTimeout: 60000
  });

  const page = await browser.newPage();
  await page.setViewport({ width: L, height: A, deviceScaleFactor: 1 });
  const cdp = await page.target().createCDPSession();

  // A ordem importa e não é intuitiva: congelar o tempo ANTES de navegar trava
  // o próprio carregamento (o load nunca chega e a navegação estoura). Carrega
  // primeiro, com a peça pausada em t=0; só então congela.
  await page.goto(url, { waitUntil: 'load' });
  await cdp.send('Emulation.setVirtualTimePolicy', { policy: 'pause' });

  // Zera e dá play já sob tempo congelado. `ultimo` recebe o relógio virtual
  // atual para o primeiro delta ser exatamente um quadro, e não o tempo que o
  // carregamento levou.
  //
  // O MARCADOR DE 1 PIXEL não é firula. Com o tempo congelado, o compositor só
  // entrega um quadro novo quando alguma coisa MUDA — e nos trechos parados
  // (antes da primeira animação, entre batidas) nada muda, então a captura
  // fica pendurada até estourar, sem erro que explique a causa. O marcador
  // alterna a opacidade de um único pixel a cada requestAnimationFrame, o que
  // garante um quadro novo sempre. A diferença é de 1/255 em 1 px: invisível
  // no vídeo, e some de vez na compressão.
  // (A API própria para isto, HeadlessExperimental.beginFrame, foi removida
  //  das versões novas do Chrome.)
  await page.evaluate(() => {
    // irPara(0) + tocar() é o CONTRATO com as peças. Antes isto mexia direto
    // na variável de relógio, e cada HTML chama a dele com um nome diferente
    // (`ultimo` no vertical, `ultimoFrame` no 16:9): no arquivo errado a
    // atribuição criava uma global solta e o vídeo começava adiantado, sem
    // erro nenhum. Peça nova só precisa expor estas duas funções.
    if (typeof irPara !== 'function' || typeof tocar !== 'function') {
      throw new Error('a peça precisa expor irPara(t) e tocar()');
    }
    irPara(0);
    tocar();

    var m = document.createElement('div');
    m.style.cssText = 'position:fixed;left:0;top:0;width:1px;height:1px;' +
                      'z-index:99999;pointer-events:none';
    document.body.appendChild(m);
    var f = 0;
    (function tick(){
      m.style.background = (f++ % 2) ? 'rgba(0,0,0,0.004)' : 'rgba(0,0,0,0)';
      requestAnimationFrame(tick);
    })();
  });

  const passo = 1000 / FPS;
  const t0 = Date.now();

  for (let i = 0; i < TOTAL; i++) {
    // Avança exatamente um quadro de tempo virtual e espera o orçamento acabar.
    const expirou = new Promise(res => cdp.once('Emulation.virtualTimeBudgetExpired', res));
    await cdp.send('Emulation.setVirtualTimePolicy', { policy: 'advance', budget: passo });
    await expirou;

    // fromSurface:false captura pelo RENDERIZADOR e não pela superfície do
    // navegador. É obrigatório aqui: com o tempo virtual congelado o
    // compositor não entrega quadro novo, e page.screenshot() fica pendurado
    // até estourar — sem erro que explique por quê.
    const { data } = await cdp.send('Page.captureScreenshot', {
      format: 'png', fromSurface: false
    });
    fs.writeFileSync(path.join(tmp, String(i).padStart(5, '0') + '.png'),
                     Buffer.from(data, 'base64'));

    if (i % 60 === 0 || i === TOTAL - 1) {
      const pct = Math.round((i + 1) / TOTAL * 100);
      const seg = Math.round((Date.now() - t0) / 1000);
      process.stdout.write(`\r  quadro ${i + 1}/${TOTAL}  ${pct}%  ${seg}s`);
    }
  }
  process.stdout.write('\n');
  await browser.close();

  console.log('codificando…');
  await new Promise((res, rej) => {
    const ff = spawn('ffmpeg', [
      '-y', '-framerate', String(FPS),
      '-i', path.join(tmp, '%05d.png'),
      '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
      // yuv420p é o que faz o arquivo abrir em celular, WhatsApp e navegador;
      // sem isso o x264 escolhe 4:4:4 e metade dos players recusa.
      '-pix_fmt', 'yuv420p',
      '-movflags', '+faststart',
      saida
    ], { stdio: ['ignore', 'ignore', 'inherit'] });
    ff.on('close', c => c === 0 ? res() : rej(new Error('ffmpeg saiu com ' + c)));
  });

  fs.rmSync(tmp, { recursive: true, force: true });
  const mb = (fs.statSync(saida).size / 1048576).toFixed(1);
  console.log(`pronto: ${saida} (${mb} MB)`);
}

main().catch(e => { console.error(e); process.exit(1); });
