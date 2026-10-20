// GRAVA a tela real de cada aula (out/2026). Uso:
//   node gravar.js <url-da-previa> <navegacao.json> <pasta-saida>
// Variáveis: AULAS (chaves, por vírgula) e APPS (prefixos de app — a matriz do workflow).
//
// Esta é a metade "câmera" da produção: para cada roteiro em roteiros/<chave>.json, abre a
// aula na prévia (moldura real, tela real, dados fictícios das sementes) e EXECUTA cada
// função — clica, abre o formulário, digita, mostra o resultado —, gravando a tela LIMPA
// (sem nada desenhado por cima) e a LINHA DO TEMPO: quando cada função começa, onde está o
// elemento dela e quando foi o clique. A outra metade é o motion design (motion/), que o
// renderizar.js monta por cima com o Remotion: câmera, destaque, cursor, legenda e cartelas.
//
// Sai, por aula: <chave>.bruto.mp4 (a tela) e <chave>.linha.json (o roteiro com os tempos).
// Roteiro cujo alvo não existe REPROVA a aula, com o motivo — aula que aponta para um botão
// que a tela não tem ensina a procurar o que não existe.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const { execFileSync, spawn } = require('child_process');
const { lerNavegacao, aulasDaNavegacao, urlDaAula } = require('./comum');

const LARGURA = 1440, ALTURA = 810, ESCALA = 4 / 3; // a tela grava em 1920×1080
const PASTA_ROTEIROS = path.join(__dirname, 'roteiros');
const espera = ms => new Promise(r => setTimeout(r, ms));

/** Quanto tempo a função fica na tela: o de ler a legenda (≈ 17 caracteres/s) mais o de achar o lugar. */
function tempoDeLeitura(passo) {
  if (passo.ms) return passo.ms;
  const n = (passo.titulo || '').length + (passo.texto || '').length;
  return Math.max(3600, Math.min(13000, 1500 + n * 58));
}

/** O elemento do passo, pelo nome que a pessoa lê na tela — nunca por classe solta. */
function localizar(pagina, alvo) {
  const palco = pagina.locator('.palco-tela:not([hidden])');
  const janela = pagina.locator('.modal, [role=dialog]').last();
  const [tipo, ...resto] = alvo.split('=');
  const v = resto.join('=');
  switch (tipo) {
    case 'botao': return palco.getByRole('button', { name: v }).first();
    case 'botao-exato': return palco.getByRole('button', { name: v, exact: true }).first();
    case 'aba': return palco.getByRole('tab', { name: v }).first();
    case 'link': return palco.getByRole('link', { name: v }).first();
    case 'opcao': return palco.getByRole('option', { name: v }).first();
    case 'caixa': return palco.getByRole('checkbox', { name: v }).first();
    case 'campo': return palco.getByLabel(v).first();
    case 'placeholder': return palco.getByPlaceholder(v).first();
    // Campo pelo que a pessoa lê: o rótulo acessível OU o texto de dentro (placeholder).
    case 'entrada': return palco.getByLabel(v).or(palco.getByPlaceholder(v)).first();
    case 'titulo': return palco.getByRole('heading', { name: v }).first();
    case 'texto': return palco.getByText(v).first();
    case 'texto-exato': return palco.getByText(v, { exact: true }).first();
    case 'linha': return palco.locator('tr, .linha, li, [role=row]').filter({ hasText: v }).first();
    case 'css': return palco.locator(v).first();
    case 'item': return pagina.getByRole('menuitem', { name: v }).first();
    case 'menu': return pagina.locator('.moldura-nav').getByRole('menuitem', { name: v }).first();
    case 'aba-app': return pagina.locator('.moldura-abas').getByRole('tab', { name: v }).first();
    case 'janela': return janela.getByRole('button', { name: v }).first();
    case 'janela-exato': return janela.getByRole('button', { name: v, exact: true }).first();
    case 'janela-campo': return janela.getByLabel(v).or(janela.getByPlaceholder(v)).first();
    case 'janela-texto': return janela.getByText(v).first();
    case 'janela-opcao': return janela.getByRole('option', { name: v }).first();
    case 'janela-caixa': return janela.getByRole('checkbox', { name: v }).first();
    case 'janela-toda': return janela;
    case 'pagina': return pagina.locator(v).first();
    default: throw new Error(`alvo desconhecido: ${alvo}`);
  }
}

async function caixa(loc) {
  await loc.waitFor({ state: 'visible', timeout: 8000 });
  await loc.scrollIntoViewIfNeeded({ timeout: 4000 });
  await espera(300);
  const r = await loc.boundingBox();
  if (!r) throw new Error('sem caixa');
  return r;
}

/**
 * Um passo. Com a linha do tempo (`linha`), ele é FILMADO: registra onde está o elemento, dá
 * o tempo de ler e faz o gesto; sem ela (preparação, inventário), só faz o gesto, rápido.
 */
async function executar(pagina, passo, linha, silencioso) {
  const acao = passo.acao || (passo.alvo ? 'mostrar' : 'legenda');
  const agora = () => (Date.now() - linha.t0) / 1000;
  if (acao === 'esperar') { await espera(passo.ms || 1000); return; }
  if (acao === 'tecla') { await pagina.keyboard.press(passo.valor); await espera(passo.ms || (silencioso ? 500 : 900)); return; }
  if (acao === 'rolar') {
    const loc = localizar(pagina, passo.alvo);
    await loc.scrollIntoViewIfNeeded({ timeout: 4000 }).catch(() => { });
    await espera(passo.ms || 700);
    return;
  }
  let r = null, loc = null;
  if (passo.alvo) {
    loc = localizar(pagina, passo.alvo);
    try { r = await caixa(loc); }
    catch {
      // Passo sem legenda marcado "opcional" (um "Trocar" que só existe se a escolha pegou)
      // não derruba a aula inteira: some, e a aula segue com o próximo passo.
      if (passo.opcional) return;
      throw new Error(`alvo não encontrado: ${passo.alvo}`);
    }
  }
  const ms = silencioso ? 0 : tempoDeLeitura(passo);
  const registro = silencioso || !linha ? null : {
    inicio: agora(), titulo: passo.titulo || null, texto: passo.texto || null, acao,
    rect: r ? { x: r.x, y: r.y, w: r.width, h: r.height } : null, clique: null,
  };
  if (registro) {
    linha.passos.push(registro);
    if (r) await loc.hover({ timeout: 3000 }).catch(() => { }); // o "hover" real da tela
  }
  const gesto = ['clicar', 'digitar', 'marcar', 'limpar', 'selecionar', 'passar'].includes(acao);
  // Ler ANTES do gesto: a legenda diz o que vai acontecer, e o clique acontece com a pessoa
  // já sabendo por quê. O resultado do clique fica na tela o tempo de ser visto.
  if (!silencioso) await espera(gesto ? Math.max(2600, ms * 0.6) : ms);
  if (gesto) {
    if (registro) registro.clique = agora();
    if (acao === 'clicar') await loc.click({ timeout: 6000 });
    if (acao === 'marcar') await loc.check({ timeout: 6000 });
    if (acao === 'passar') await loc.hover({ timeout: 6000 });
    if (acao === 'limpar') await loc.fill('');
    if (acao === 'selecionar') await loc.selectOption({ label: passo.valor });
    if (acao === 'digitar') {
      await loc.click({ timeout: 6000 });
      await loc.fill('');
      await loc.pressSequentially(passo.valor, { delay: silencioso ? 0 : 75 });
    }
    await espera(passo.depois ?? (silencioso ? 900 : Math.max(2200, ms * 0.45)));
  }
  if (registro) registro.fim = agora();
}

async function gravarAula(navegador, base, aula, roteiro, saida) {
  const pagina = await navegador.newPage({ viewport: { width: LARGURA, height: ALTURA }, deviceScaleFactor: ESCALA, locale: 'pt-BR' });
  const erros = [];
  pagina.on('pageerror', e => erros.push(e.message));
  const pastaQuadros = path.join(saida, '.quadros', aula.chave);
  fs.rmSync(pastaQuadros, { recursive: true, force: true });
  fs.mkdirSync(pastaQuadros, { recursive: true });

  await pagina.goto(urlDaAula(base, aula, roteiro.url || {}));
  await pagina.waitForSelector('.moldura', { timeout: 30000 });
  await pagina.waitForSelector('.palco-tela:not([hidden])', { timeout: 30000 });
  await espera(2500);
  if (await pagina.getByText('não tem componente na prévia').count()) throw new Error('a tela não abriu na prévia');
  for (const pre of roteiro.antes || []) await executar(pagina, pre, null, true);
  // O cursor do sistema não aparece na gravação; o motion design desenha o dele.
  await pagina.mouse.move(LARGURA - 10, ALTURA - 10);
  await espera(800);

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
  const linha = { t0: Date.now(), passos: [] };
  await cdp.send('Page.startScreencast', { format: 'jpeg', quality: 95, maxWidth: 1920, maxHeight: 1080, everyNthFrame: 1 });
  linha.t0 = Date.now();
  await espera(1800); // a tela parada um instante antes da primeira função
  for (const [i, passo] of roteiro.passos.entries()) {
    try { await executar(pagina, passo, linha, false); }
    catch (e) {
      // A foto da tela no instante da falha sobe no artefato da fatia: é ela que diz se o
      // alvo mudou de nome, se a tela não carregou ou se um botão estava apagado.
      await pagina.screenshot({ path: path.join(saida, aula.chave + '.erro.png') }).catch(() => { });
      throw new Error(`passo ${i + 1}: ${e.message || e}`);
    }
  }
  await espera(1500);
  const fimRel = (Date.now() - linha.t0) / 1000;
  await cdp.send('Page.stopScreencast');
  await espera(300);
  await pagina.close();

  if (quadros.length < 3) throw new Error(`só ${quadros.length} quadros capturados`);
  // Os quadros e a linha do tempo usam o mesmo relógio de parede da máquina.
  const inicio = linha.t0 / 1000;
  const arquivo = path.join(saida, aula.chave + '.bruto.mp4');
  montarBruto(quadros, inicio, inicio + fimRel, pastaQuadros, arquivo);
  fs.rmSync(pastaQuadros, { recursive: true, force: true });

  const funcoes = linha.passos.filter(p => p.titulo);
  return {
    chave: aula.chave, app: aula.app, nomeApp: aula.nomeApp,
    titulo: roteiro.titulo || aula.rotulo, categoria: roteiro.categoria || aula.nomeApp,
    descricao: roteiro.descricao, video: aula.chave + '.bruto.mp4',
    tela: { largura: LARGURA, altura: ALTURA, escala: ESCALA },
    duracao: fimRel, passos: linha.passos, funcoes: funcoes.map(p => p.titulo), errosDaPagina: erros,
  };
}

/** Os quadros, cada um com a duração até o seguinte, viram um MP4 de alta qualidade (o "bruto"). */
function montarBruto(quadros, inicio, fim, pasta, arquivo) {
  // O último quadro antes do início vale como o primeiro (tela parada não gera quadro novo).
  const antes = quadros.filter(q => q.t <= inicio);
  const uteis = [...(antes.length ? [{ ...antes[antes.length - 1], t: inicio }] : []), ...quadros.filter(q => q.t > inicio && q.t <= fim)];
  if (!uteis.length) throw new Error('nenhum quadro no intervalo da aula');
  if (uteis[0].t > inicio) uteis[0] = { ...uteis[0], t: inicio };
  const linhas = [];
  for (let i = 0; i < uteis.length; i++) {
    const proximo = i + 1 < uteis.length ? uteis[i + 1].t : fim;
    linhas.push(`file '${uteis[i].arquivo}'`, `duration ${Math.max(0.001, proximo - uteis[i].t).toFixed(4)}`);
  }
  linhas.push(`file '${uteis[uteis.length - 1].arquivo}'`);
  fs.writeFileSync(path.join(pasta, 'lista.txt'), linhas.join('\n'));
  execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', 'lista.txt',
    '-vf', 'scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0xF8FAFC,fps=30,format=yuv420p',
    '-c:v', 'libx264', '-preset', 'fast', '-crf', '14', '-movflags', '+faststart', path.resolve(arquivo)], { cwd: pasta, stdio: 'inherit' });
}

/**
 * Uma prévia NOVA por aula (com PREVIA_DLL): as aulas clicam de verdade — registram chegada,
 * concluem sessão —, e a aula seguinte não pode encontrar os dados que a anterior mudou.
 * Sem a variável, usa a prévia que já está no ar (o inventário e a gravação local).
 */
async function subirPrevia(base) {
  const dll = process.env.PREVIA_DLL;
  if (!dll) return null;
  const proc = spawn('dotnet', [dll], { cwd: path.dirname(dll), env: { ...process.env, ASPNETCORE_URLS: base }, stdio: 'ignore' });
  for (let i = 0; i < 120; i++) {
    try { const r = await fetch(base + '/'); if (r.ok) return proc; } catch { }
    await espera(500);
  }
  proc.kill('SIGKILL');
  throw new Error('a prévia não subiu');
}
async function derrubarPrevia(proc) {
  if (!proc) return;
  const fim = new Promise(r => proc.once('exit', r));
  proc.kill('SIGTERM');
  await Promise.race([fim, espera(5000)]);
}

module.exports = { executar, localizar };
if (require.main !== module) return;

(async () => {
  const [, , base, navegacao, saida] = process.argv;
  fs.mkdirSync(saida, { recursive: true });
  const aulas = aulasDaNavegacao(lerNavegacao(navegacao));
  // AULAS vem do workflow_dispatch; no PR (onde não há dispatch) a lista sai de
  // regravar.txt — uma chave por linha. Vazio grava todas.
  const doArquivo = fs.existsSync(path.join(__dirname, 'regravar.txt'))
    ? fs.readFileSync(path.join(__dirname, 'regravar.txt'), 'utf8').split(/\r?\n/).map(s => s.replace(/#.*/, '').trim()).filter(Boolean) : [];
  const filtro = (process.env.AULAS || '').split(',').map(s => s.trim()).filter(Boolean);
  if (!filtro.length) filtro.push(...doArquivo);
  // A matriz do workflow grava um app por máquina: APPS diz quais (o prefixo basta —
  // "consultorio" leva junto as aulas gravadas no Consultório da enfermagem).
  const apps = (process.env.APPS || '').split(',').map(s => s.trim()).filter(Boolean);
  // FATIA=i/n: a matriz do workflow divide as aulas entre n máquinas (a i-ésima de cada n).
  const [fatia, fatias] = (process.env.FATIA || '1/1').split('/').map(Number);
  const roteiros = fs.readdirSync(PASTA_ROTEIROS).filter(f => f.endsWith('.json')).sort()
    // Com filtro, divide só as aulas pedidas — senão 19 máquinas sobem para gravar nada.
    .filter(f => !filtro.length || filtro.includes(f.replace(/\.json$/, '')))
    .filter((_, i) => i % fatias === fatia - 1)
    .map(f => JSON.parse(fs.readFileSync(path.join(PASTA_ROTEIROS, f), 'utf8')));
  const navegador = await chromium.launch();
  const resumo = [];
  for (const roteiro of roteiros) {
    if (filtro.length && !filtro.includes(roteiro.chave)) continue;
    const aula = aulas.find(a => a.chave === roteiro.chave);
    if (!aula) { resumo.push({ chave: roteiro.chave, erro: 'a chave não está no Treinamento de nenhum app' }); continue; }
    const doApp = roteiro.app ? { ...aula, app: roteiro.app } : aula;
    if (apps.length && !apps.some(a => doApp.app.startsWith(a))) continue;
    let previa = null;
    try {
      previa = await subirPrevia(base);
      const linha = await gravarAula(navegador, base, doApp, roteiro, saida);
      fs.writeFileSync(path.join(saida, roteiro.chave + '.linha.json'), JSON.stringify(linha, null, 2));
      resumo.push({ chave: roteiro.chave, duracao: linha.duracao, funcoes: linha.funcoes.length, errosDaPagina: linha.errosDaPagina });
      console.log('OK  ', roteiro.chave.padEnd(36), linha.duracao.toFixed(1) + 's', linha.funcoes.length + ' funções');
    } catch (e) {
      resumo.push({ chave: roteiro.chave, erro: String(e.message || e) });
      console.log('ERRO', roteiro.chave.padEnd(36), e.message || e);
    } finally {
      await derrubarPrevia(previa);
    }
  }
  await navegador.close();
  fs.rmSync(path.join(saida, '.quadros'), { recursive: true, force: true });
  fs.writeFileSync(path.join(saida, 'resumo-gravacao.json'), JSON.stringify(resumo, null, 2));
  const falhas = resumo.filter(r => r.erro);
  console.log(`${resumo.length - falhas.length} aula(s) gravada(s), ${falhas.length} com erro.`);
  if (falhas.length) process.exitCode = 1;
})().catch(e => { console.error(e); process.exit(1); });
