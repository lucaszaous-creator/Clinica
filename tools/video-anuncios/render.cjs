// uso: node anuncios-render.cjs estaticos <saida>  |  node anuncios-render.cjs quadros <saida> V1 9x16 0,1500,...  |  node anuncios-render.cjs video <saida.mp4> V1 9x16
const { chromium } = require('playwright-core'); const { spawn } = require('child_process'); const fs = require('fs');
const CH = process.env.CHROMIUM || undefined; // Chromium do Playwright (vazio = o padrão dele)
const FF = process.env.FFMPEG || 'ffmpeg';
const U = 'file://' + require('path').resolve(__dirname, 'cenas.html');
const H = { '4x5': 1350, '9x16': 1920 };
const [modo, saida, peca, fmt, lista] = process.argv.slice(2);
(async () => {
  const b = await chromium.launch({ executablePath: CH, args: ['--no-sandbox', '--allow-file-access-from-files'] });
  async function abrir(p, f, t) { const pg = await b.newPage({ viewport: { width: 1080, height: H[f] }, deviceScaleFactor: 1 });
    const erros = []; pg.on('pageerror', e => erros.push(e.message));
    await pg.goto(`${U}?peca=${p}&fmt=${f}${t !== undefined ? '&t=' + t : ''}`); await pg.waitForFunction(() => window.pronto === true); await pg.waitForTimeout(150);
    if (erros.length) console.log('ERRO', p, f, erros.join(' | ')); return pg; }
  if (modo === 'estaticos') {
    fs.mkdirSync(saida, { recursive: true });
    for (const [p,fs] of [['R1',['1x1','4x5','9x16']],['R2',['1x1','4x5','9x16']],['R3',['1x1','4x5','9x16']],['R4',['1x1','4x5','9x16']],['R5',['1x1','4x5','9x16']],['C1',['4x5']],['C2',['4x5']],['C3',['4x5']],['C4',['4x5']],['C5',['4x5']]]) for (const f of fs) {
      const pg = await abrir(p, f); await pg.screenshot({ path: `${saida}/${p}-${f}.png` }); await pg.close(); console.log('ok', p, f); }
  } else if (modo === 'quadros') {
    fs.mkdirSync(saida, { recursive: true }); const pg = await abrir(peca, fmt, 0);
    for (const t of lista.split(',').map(Number)) { await pg.evaluate(t => window.desenhar(t), t); await pg.waitForTimeout(60);
      await pg.screenshot({ path: `${saida}/${peca}-${fmt}-${String(t).padStart(5, '0')}ms.png` }); }
    console.log('ok quadros', peca, fmt);
  } else if (modo === 'video') {
    const pg = await abrir(peca, fmt, 0); const dur = await pg.evaluate(() => window.duracao); const fps = 30, n = Math.round(dur / 1000 * fps);
    const ff = spawn(FF, ['-y', '-loglevel', 'error', '-f', 'image2pipe', '-framerate', String(fps), '-i', '-', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-preset', 'medium', '-crf', '18', '-movflags', '+faststart', saida]);
    ff.stderr.on('data', d => process.stderr.write(d));
    for (let i = 0; i < n; i++) { await pg.evaluate(t => window.desenhar(t), i * 1000 / fps);
      const buf = await pg.screenshot({ type: 'png' }); if (!ff.stdin.write(buf)) await new Promise(r => ff.stdin.once('drain', r)); }
    ff.stdin.end(); await new Promise(r => ff.on('close', r)); console.log('ok video', peca, fmt, n, 'quadros');
  }
  await b.close();
})();
