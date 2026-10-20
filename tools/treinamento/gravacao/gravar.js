// GRAVA as videoaulas (out/2026). Uso:
//   node gravar.js <url-da-previa> <navegacao.json> <pasta-saida>
// Variável AULAS (opcional): chaves separadas por vírgula — grava só essas.
//
// Para cada roteiro em roteiros/<chave>.json: abre a aula na prévia (moldura real, tela real,
// dados fictícios das sementes), injeta a cena (cena.js) e percorre os passos — o cursor vai
// até o elemento, o destaque o cerca, a legenda explica e, quando o passo manda, o clique
// acontece DE VERDADE na tela. Os quadros vêm do screencast do Chromium (só quando a tela
// muda, com a hora de cada um) e o ffmpeg os monta num MP4 H.264 1920×1080 a 30 qps.
//
// Sai, por aula: <chave>.mp4, <chave>-passos/ (uma foto por passo, para conferir sem assistir)
// e a entrada do catálogo. No fim: catalogo.json (o formato do Clinica.Desktop.Shell) e
// resumo.json. Roteiro cujo alvo não existe REPROVA a aula, com o motivo — aula que aponta
// para um botão que a tela não tem ensina a procurar o que não existe.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { execFileSync } = require('child_process');
const { lerNavegacao, aulasDaNavegacao, urlDaAula } = require('./comum');

const LARGURA = 1440, ALTURA = 810, ESCALA = 4 / 3; // 1920×1080 no vídeo
const PASTA_ROTEIROS = path.join(__dirname, 'roteiros');
const espera = ms => new Promise(r => setTimeout(r, ms));

/** Quanto tempo a legenda fica: o de ler (≈ 16 caracteres por segundo) mais o de achar o lugar. */
function tempoDeLeitura(passo) {
  if (passo.ms) return passo.ms;
  const n = (passo.titulo || '').length + (passo.texto || '').length;
  return Math.max(3800, Math.min(14000, 1400 + n * 62));
}

/** O elemento do passo, pelo nome que a pessoa lê na tela — nunca por classe solta. */
function localizar(pagina, alvo) {
  const palco = pagina.locator('.palco-tela:not([hidden])');
  const [tipo, ...resto] = alvo.split('=');
  const v = resto.join('=');
  switch (tipo) {
    case 'botao': return palco.getByRole('button', { name: v }).first();
    case 'botao-exato': return palco.getByRole('button', { name: v, exact: true }).first();
    case 'aba': return palco.getByRole('tab', { name: v }).first();
    case 'link': return palco.getByRole('link', { name: v }).first();
    case 'campo': return palco.getByLabel(v).first();
    case 'placeholder': return palco.getByPlaceholder(v).first();
    case 'titulo': return palco.getByRole('heading', { name: v }).first();
    case 'texto': return palco.getByText(v).first();
    case 'texto-exato': return palco.getByText(v, { exact: true }).first();
    case 'linha': return palco.locator('tr, .linha, li').filter({ hasText: v }).first();
    case 'css': return palco.locator(v).first();
    case 'menu': return pagina.locator('.moldura-nav').getByRole('menuitem', { name: v }).first();
    case 'aba-app': return pagina.locator('.moldura-abas').getByRole('tab', { name: v }).first();
    case 'janela': return pagina.locator('.modal, [role=dialog]').last().getByRole('button', { name: v }).first();
    case 'janela-campo': return pagina.locator('.modal, [role=dialog]').last().getByLabel(v).first();
    case 'janela-texto': return pagina.locator('.modal, [role=dialog]').last().getByText(v).first();
    case 'pagina': return pagina.locator(v).first();
    default: throw new Error(`alvo desconhecido: ${alvo}`);
  }
}

async function caixa(loc) {
  await loc.waitFor({ state: 'visible', timeout: 8000 });
  await loc.scrollIntoViewIfNeeded({ timeout: 4000 });
  await espera(250);
  const r = await loc.boundingBox();
  if (!r) throw new Error('sem caixa');
  return r;
}

async function gravarAula(navegador, base, aula, roteiro, saida) {
  const pagina = await navegador.newPage({ viewport: { width: LARGURA, height: ALTURA }, deviceScaleFactor: ESCALA, locale: 'pt-BR' });
  const erros = [];
  pagina.on('pageerror', e => erros.push(e.message));
  const pastaQuadros = path.join(saida, '.quadros', aula.chave);
  const pastaPassos = path.join(saida, aula.chave + '-passos');
  fs.rmSync(pastaQuadros, { recursive: true, force: true });
  fs.mkdirSync(pastaQuadros, { recursive: true });
  fs.mkdirSync(pastaPassos, { recursive: true });

  await pagina.goto(urlDaAula(base, aula, roteiro.url || {}));
  await pagina.waitForSelector('.moldura', { timeout: 30000 });
  await pagina.waitForSelector('.palco-tela:not([hidden])', { timeout: 30000 });
  await espera(2500);
  if (await pagina.getByText('não tem componente na prévia').count()) throw new Error('a tela não abriu na prévia');
  for (const pre of roteiro.antes || []) await executar(pagina, pre, null, true);
  await pagina.addScriptTag({ path: path.join(__dirname, 'cena.js') });

  // Screencast: um quadro por mudança, com a hora em que a tela o desenhou.
  const cdp = await pagina.context().newCDPSession(pagina);
  const quadros = [];
  let n = 0;
  cdp.on('Page.screencastFrame', async f => {
    const arquivo = String(++n).padStart(6, '0') + '.jpg';
    fs.writeFileSync(path.join(pastaQuadros, arquivo), Buffer.from(f.data, 'base64'));
    quadros.push({ t: f.metadata.timestamp, arquivo });
    try { await cdp.send('Page.screencastFrameAck', { sessionId: f.sessionId }); } catch { }
  });
  const agora = () => pagina.evaluate(() => Date.now() / 1000);
  const titulo = roteiro.titulo || aula.rotulo;
  const categoria = roteiro.categoria || aula.nomeApp;
  const passos = roteiro.passos.filter(p => p.titulo);

  await pagina.evaluate(([m, t, d, l]) => window.cenaDaAula.cartela(m, t, d, l, 'Treinamento · dados fictícios para demonstração'),
    [categoria, titulo, roteiro.descricao, passos.map(p => p.titulo)]);
  await cdp.send('Page.startScreencast', { format: 'jpeg', quality: 92, maxWidth: 1920, maxHeight: 1080, everyNthFrame: 2 });
  await espera(400);
  // O tempo ZERO do vídeo é o primeiro quadro: a cartela parada não gera quadro novo, então
  // contar de depois dela perderia a abertura inteira.
  const inicioBruto = await agora();
  await espera(Math.max(4500, 2200 + passos.length * 450));
  await pagina.evaluate(() => window.cenaDaAula.semCartela());
  await espera(900);

  const capitulos = [];
  let indice = 0;
  for (const passo of roteiro.passos) {
    if (passo.titulo) {
      indice++;
      capitulos.push({ Inicio: await agora(), Titulo: `${indice}. ${passo.titulo}`, Texto: passo.texto || '' });
    }
    await executar(pagina, passo, passo.titulo ? `Passo ${indice} de ${passos.length}` : null, false);
    if (passo.titulo) await pagina.screenshot({ path: path.join(pastaPassos, String(indice).padStart(2, '0') + '.jpg'), type: 'jpeg', quality: 80 });
  }
  await pagina.evaluate(() => { window.cenaDaAula.semLegenda(); window.cenaDaAula.destacar(null); });
  await espera(500);
  await pagina.evaluate(([t]) => window.cenaDaAula.cartela('Fim da aula', t, 'Os capítulos ao lado do vídeo levam direto a cada passo.', [], 'Clínica SemDor · Treinamento'), [titulo]);
  await espera(3200);
  const fim = await agora();
  await cdp.send('Page.stopScreencast');
  await espera(300);
  await pagina.close();

  if (!quadros.length) throw new Error('nenhum quadro capturado');
  const inicio = Math.min(inicioBruto, ...quadros.map(q => q.t));
  for (const c of capitulos) c.Inicio = Math.round((c.Inicio - inicio) * 100) / 100;
  const arquivo = path.join(saida, aula.chave + '.mp4');
  montarVideo(quadros, inicio, fim, pastaQuadros, arquivo);
  fs.rmSync(pastaQuadros, { recursive: true, force: true });
  const sha = crypto.createHash('sha256').update(fs.readFileSync(arquivo)).digest('hex');
  return {
    Id: aula.chave, Titulo: titulo, Categoria: categoria, Descricao: roteiro.descricao,
    Telas: [aula.chave], Funcoes: passos.map(p => p.titulo), Capitulos: capitulos,
    Duracao: Math.round((fim - inicio) * 100) / 100, Sha256: sha, erros,
  };
}

/** Um passo: leva o cursor, destaca, mostra a legenda, faz o gesto e espera o tempo de ler. */
async function executar(pagina, passo, rotuloPasso, silencioso) {
  const acao = passo.acao || (passo.alvo ? 'mostrar' : 'legenda');
  if (acao === 'esperar') { await espera(passo.ms || 1000); return; }
  if (acao === 'tecla') { await pagina.keyboard.press(passo.valor); await espera(passo.ms || 600); return; }
  let r = null, loc = null;
  if (passo.alvo) {
    loc = localizar(pagina, passo.alvo);
    try { r = await caixa(loc); }
    catch { throw new Error(`alvo não encontrado: ${passo.alvo}`); }
  }
  const ms = tempoDeLeitura(passo);
  if (!silencioso) {
    const emCima = r ? (r.y + r.height / 2) > ALTURA * 0.52 : false;
    if (r) {
      await pagina.evaluate(([x, y]) => window.cenaDaAula.cursorEm(x, y), [r.x + Math.min(r.width / 2, 60), r.y + r.height / 2]);
      await espera(900);
      await pagina.evaluate(rr => window.cenaDaAula.destacar(rr), r);
    } else {
      await pagina.evaluate(() => window.cenaDaAula.destacar(null));
    }
    if (rotuloPasso) await pagina.evaluate(([p, t, x, c, m]) => window.cenaDaAula.legenda(p, t, x, c, m), [rotuloPasso, passo.titulo, passo.texto, emCima, ms]);
  }
  const lerAntes = acao === 'mostrar' || acao === 'legenda' || acao === 'rolar';
  if (!silencioso) await espera(lerAntes ? ms : Math.min(ms, Math.max(2200, ms * 0.55)));
  if (acao === 'clicar' || acao === 'digitar' || acao === 'passar' || acao === 'marcar' || acao === 'limpar') {
    if (!silencioso && r) {
      await pagina.evaluate(([x, y]) => window.cenaDaAula.clicar(x, y), [r.x + Math.min(r.width / 2, 60), r.y + r.height / 2]);
      await espera(250);
    }
    if (!silencioso) await pagina.evaluate(() => window.cenaDaAula.destacar(null));
    if (acao === 'clicar') await loc.click({ timeout: 5000 });
    if (acao === 'marcar') await loc.check({ timeout: 5000 });
    if (acao === 'passar') await loc.hover({ timeout: 5000 });
    if (acao === 'limpar') await loc.fill('');
    if (acao === 'digitar') {
      await loc.click({ timeout: 5000 });
      await loc.fill('');
      await loc.pressSequentially(passo.valor, { delay: silencioso ? 0 : 85 });
    }
    await espera(passo.depois ?? (silencioso ? 800 : 1600));
    if (!silencioso && !lerAntes) await espera(Math.max(1200, ms * 0.45));
  }
  if (acao === 'selecionar') {
    await loc.selectOption(passo.valor);
    await espera(passo.depois ?? 1200);
  }
}

/** Os quadros, cada um com a duração até o seguinte, viram um MP4 de taxa constante. */
function montarVideo(quadros, inicio, fim, pasta, arquivo) {
  const uteis = quadros.filter(q => q.t >= inicio - 0.05 && q.t <= fim + 0.05);
  if (uteis.length < 5) throw new Error(`só ${uteis.length} quadros capturados`);
  const linhas = [];
  for (let i = 0; i < uteis.length; i++) {
    const proximo = i + 1 < uteis.length ? uteis[i + 1].t : fim;
    const dur = Math.max(0.001, proximo - uteis[i].t);
    linhas.push(`file '${uteis[i].arquivo}'`, `duration ${dur.toFixed(4)}`);
  }
  linhas.push(`file '${uteis[uteis.length - 1].arquivo}'`);
  fs.writeFileSync(path.join(pasta, 'lista.txt'), linhas.join('\n'));
  execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', 'lista.txt',
    '-vf', 'scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0xF8FAFC,fps=30,format=yuv420p',
    '-c:v', 'libx264', '-preset', 'slow', '-crf', '21', '-movflags', '+faststart', path.resolve(arquivo)], { cwd: pasta, stdio: 'inherit' });
}

(async () => {
  const [, , base, navegacao, saida] = process.argv;
  fs.mkdirSync(saida, { recursive: true });
  const aulas = aulasDaNavegacao(lerNavegacao(navegacao));
  const filtro = (process.env.AULAS || '').split(',').map(s => s.trim()).filter(Boolean);
  // A matriz do workflow grava um app por máquina: APPS diz quais (o prefixo basta —
  // "consultorio" leva junto as aulas gravadas no Consultório da enfermagem).
  const apps = (process.env.APPS || '').split(',').map(s => s.trim()).filter(Boolean);
  const roteiros = fs.readdirSync(PASTA_ROTEIROS).filter(f => f.endsWith('.json'))
    .map(f => JSON.parse(fs.readFileSync(path.join(PASTA_ROTEIROS, f), 'utf8')));
  const navegador = await chromium.launch();
  const catalogo = [], resumo = [];
  for (const roteiro of roteiros) {
    if (filtro.length && !filtro.includes(roteiro.chave)) continue;
    const aula = aulas.find(a => a.chave === roteiro.chave);
    if (!aula) { resumo.push({ chave: roteiro.chave, erro: 'a chave não está no Treinamento de nenhum app' }); continue; }
    const doApp = roteiro.app ? { ...aula, app: roteiro.app } : aula;
    if (apps.length && !apps.some(a => doApp.app.startsWith(a))) continue;
    try {
      const entrada = await gravarAula(navegador, base, doApp, roteiro, saida);
      const { erros, ...semErros } = entrada;
      catalogo.push(semErros);
      resumo.push({ chave: roteiro.chave, duracao: entrada.Duracao, passos: entrada.Capitulos.length, errosDaPagina: erros });
      console.log('OK  ', roteiro.chave.padEnd(36), entrada.Duracao.toFixed(1) + 's');
    } catch (e) {
      resumo.push({ chave: roteiro.chave, erro: String(e.message || e) });
      console.log('ERRO', roteiro.chave.padEnd(36), e.message || e);
    }
  }
  await navegador.close();
  fs.rmSync(path.join(saida, '.quadros'), { recursive: true, force: true });
  catalogo.sort((a, b) => a.Categoria.localeCompare(b.Categoria) || a.Titulo.localeCompare(b.Titulo));
  fs.writeFileSync(path.join(saida, 'catalogo.json'), JSON.stringify(catalogo, null, 2));
  fs.writeFileSync(path.join(saida, 'resumo.json'), JSON.stringify(resumo, null, 2));
  const falhas = resumo.filter(r => r.erro);
  console.log(`${catalogo.length} aula(s) gravada(s), ${falhas.length} com erro.`);
  if (falhas.length) process.exitCode = 1;
})().catch(e => { console.error(e); process.exit(1); });
