// MONTA o motion design das aulas gravadas (out/2026). Uso:
//   node renderizar.js <pasta-da-gravacao>
// Para cada <chave>.linha.json (do gravar.js): o Remotion compõe a tela real (<chave>.bruto.mp4)
// com abertura, câmera, destaque, cursor, painel e fechamento (motion/Aula.jsx), e sai
// <chave>.mp4 (H.264 1920×1080) mais a entrada do catálogo do app, com UM capítulo por função.
const fs = require('fs');
const os = require('os');
const path = require('path');
const crypto = require('crypto');
const { bundle } = require('@remotion/bundler');
const { renderMedia, selectComposition } = require('@remotion/renderer');
const tempos = require('./motion/tempos.json');

(async () => {
  const pasta = path.resolve(process.argv[2]);
  const linhas = fs.readdirSync(pasta).filter(f => f.endsWith('.linha.json'));
  const catalogo = [], resumo = [];
  if (linhas.length) {
    // A marca entra na pasta pública junto das gravações.
    fs.copyFileSync(path.join(__dirname, '../../../docs/mockups/marca/logo-branca-limpa.png'), path.join(pasta, 'marca-branca.png'));
    const serveUrl = await bundle({ entryPoint: path.join(__dirname, 'motion/index.jsx'), publicDir: pasta,
      webpackOverride: c => ({ ...c, resolve: { ...c.resolve, modules: [path.join(__dirname, 'node_modules'), 'node_modules'] } }) });
    for (const arquivo of linhas) {
      const aula = JSON.parse(fs.readFileSync(path.join(pasta, arquivo), 'utf8'));
      const saida = path.join(pasta, aula.chave + '.mp4');
      try {
        const composicao = await selectComposition({ serveUrl, id: 'Aula', inputProps: aula });
        const inicio = Date.now();
        await renderMedia({ serveUrl, composition: composicao, inputProps: aula, codec: 'h264', crf: 20, outputLocation: saida,
          imageFormat: 'jpeg', jpegQuality: 92, concurrency: Math.max(1, os.cpus().length), chromiumOptions: { gl: 'swangle' } });
        const sha = crypto.createHash('sha256').update(fs.readFileSync(saida)).digest('hex');
        const duracao = composicao.durationInFrames / tempos.fps;
        let n = 0;
        catalogo.push({
          Id: aula.chave, Titulo: aula.titulo, Categoria: aula.categoria, Descricao: aula.descricao,
          Telas: [aula.chave], Funcoes: aula.funcoes,
          Capitulos: aula.passos.filter(p => p.titulo).map(p => ({ Inicio: Math.round((tempos.abertura + p.inicio) * 100) / 100, Titulo: `${++n}. ${p.titulo}`, Texto: p.texto || '' })),
          Duracao: Math.round(duracao * 100) / 100, Sha256: sha,
        });
        resumo.push({ chave: aula.chave, duracao, funcoes: aula.funcoes.length, montagem: Math.round((Date.now() - inicio) / 1000) });
        console.log('MONTADA', aula.chave.padEnd(36), duracao.toFixed(1) + 's', `em ${Math.round((Date.now() - inicio) / 1000)}s`);
        fs.rmSync(path.join(pasta, aula.video), { force: true });
      } catch (e) {
        resumo.push({ chave: aula.chave, erro: 'montagem: ' + String(e.message || e) });
        console.log('ERRO   ', aula.chave, e.message || e);
      }
    }
  }
  const gravacao = path.join(pasta, 'resumo-gravacao.json');
  if (fs.existsSync(gravacao)) for (const r of JSON.parse(fs.readFileSync(gravacao, 'utf8'))) if (r.erro) resumo.push(r);
  fs.writeFileSync(path.join(pasta, 'catalogo.json'), JSON.stringify(catalogo, null, 2));
  fs.writeFileSync(path.join(pasta, 'resumo.json'), JSON.stringify(resumo, null, 2));
  const falhas = resumo.filter(r => r.erro);
  console.log(`${catalogo.length} aula(s) montada(s), ${falhas.length} com erro.`);
  if (falhas.length) process.exitCode = 1;
})().catch(e => { console.error(e); process.exit(1); });
