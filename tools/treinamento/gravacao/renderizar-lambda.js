// MONTA o motion design das aulas na AWS Lambda (out/2026). Uso:
//   node renderizar-lambda.js <pasta-com-as-gravacoes>
// Credenciais: AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY (vindas dos segredos do GitHub) e
// REMOTION_REGIAO (padrão us-east-2).
//
// O mesmo motion/Aula.jsx do renderizar.js, montado em pedaços por várias funções ao mesmo
// tempo, em vez de quadro a quadro numa máquina de 4 núcleos. A tela gravada de cada aula
// (<chave>.bruto.mp4) sobe para o S3 e é lida pelo Lambda por um endereço assinado; o MP4 pronto
// volta para a pasta, com a entrada do catálogo — o mesmo resultado do renderizar.js.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { deployFunction, deploySite, getOrCreateBucket, renderMediaOnLambda, getRenderProgress } = require('@remotion/lambda');
const { S3Client, PutObjectCommand, GetObjectCommand } = require('@aws-sdk/client-s3');
const { getSignedUrl } = require('@aws-sdk/s3-request-presigner');
const { ServiceQuotasClient, GetServiceQuotaCommand } = require('@aws-sdk/client-service-quotas');
const tempos = require('./motion/tempos.json');

const regiao = process.env.REMOTION_REGIAO || 'us-east-2';
const espera = ms => new Promise(r => setTimeout(r, ms));

async function limiteDeExecucoes() {
  try {
    const r = await new ServiceQuotasClient({ region: regiao }).send(new GetServiceQuotaCommand({ ServiceCode: 'lambda', QuotaCode: 'L-B99A9384' }));
    return Math.floor(r.Quota?.Value ?? 10);
  } catch (e) { console.log('não li a cota do Lambda:', e.message); return 10; }
}

(async () => {
  const pasta = path.resolve(process.argv[2]);
  const linhas = fs.readdirSync(pasta).filter(f => f.endsWith('.linha.json'));
  const catalogo = [], resumo = [];
  if (linhas.length) {
    const limite = await limiteDeExecucoes();
    console.log(`Lambda em ${regiao}: até ${limite} execuções ao mesmo tempo; ${linhas.length} aula(s).`);
    const { functionName } = await deployFunction({ region: regiao, timeoutInSeconds: 900, memorySizeInMb: 3008, diskSizeInMb: 10240, createCloudWatchLogGroup: true });
    const { bucketName } = await getOrCreateBucket({ region: regiao });
    const publico = path.join(pasta, '.publico');
    fs.mkdirSync(publico, { recursive: true });
    fs.copyFileSync(path.join(__dirname, '../../../docs/mockups/marca/logo-branca-limpa.png'), path.join(publico, 'marca-branca.png'));
    const { serveUrl } = await deploySite({ entryPoint: path.join(__dirname, 'motion/index.jsx'), bucketName, region: regiao, siteName: 'clinica-videoaulas',
      options: { publicDir: publico, webpackOverride: c => ({ ...c, resolve: { ...c.resolve, modules: [path.join(__dirname, 'node_modules'), 'node_modules'] } }) } });
    console.log('função', functionName, '· site', serveUrl);
    const s3 = new S3Client({ region: regiao });

    // Cada aula pede ~frames/porLambda execuções; as aulas entram em fila para a soma caber no
    // limite da conta (conta nova: 10; com o aumento pedido, centenas).
    const porLambda = Math.max(120, Number(process.env.QUADROS_POR_LAMBDA || 240));
    const aulas = linhas.map(f => JSON.parse(fs.readFileSync(path.join(pasta, f), 'utf8')));
    const frames = a => Math.round((tempos.abertura + a.duracao + tempos.fechamento) * tempos.fps);
    let emUso = 0;
    const montar = async aula => {
      const pedacos = Math.ceil(frames(aula) / porLambda) + 1;
      while (emUso > 0 && emUso + pedacos > limite) await espera(3000);
      emUso += pedacos;
      const inicio = Date.now();
      try {
        const chaveBruto = `videoaulas/brutos/${aula.chave}-${Date.now()}.mp4`;
        await s3.send(new PutObjectCommand({ Bucket: bucketName, Key: chaveBruto, Body: fs.readFileSync(path.join(pasta, aula.video)), ContentType: 'video/mp4' }));
        const url = await getSignedUrl(s3, new GetObjectCommand({ Bucket: bucketName, Key: chaveBruto }), { expiresIn: 6 * 3600 });
        const { renderId } = await renderMediaOnLambda({ region: regiao, functionName, serveUrl, composition: 'Aula', inputProps: { ...aula, video: url },
          codec: 'h264', crf: 20, imageFormat: 'jpeg', jpegQuality: 90, framesPerLambda: porLambda, maxRetries: 2, privacy: 'private' });
        let ultimo = -1;
        for (;;) {
          const p = await getRenderProgress({ renderId, bucketName, functionName, region: regiao });
          if (p.fatalErrorEncountered) throw new Error(p.errors.map(e => e.message).join(' | '));
          const pct = Math.floor(p.overallProgress * 10);
          if (pct !== ultimo) { ultimo = pct; console.log(`   ${aula.chave}: ${pct * 10}%`); }
          if (p.done) {
            const r = await s3.send(new GetObjectCommand({ Bucket: p.outBucket, Key: p.outKey }));
            const saida = path.join(pasta, aula.chave + '.mp4');
            fs.writeFileSync(saida, Buffer.from(await r.Body.transformToByteArray()));
            const sha = crypto.createHash('sha256').update(fs.readFileSync(saida)).digest('hex');
            const duracao = frames(aula) / tempos.fps;
            let n = 0;
            catalogo.push({
              Id: aula.chave, Titulo: aula.titulo, Categoria: aula.categoria, Descricao: aula.descricao,
              Telas: [aula.chave], Funcoes: aula.funcoes,
              Capitulos: aula.passos.filter(x => x.titulo).map(x => ({ Inicio: Math.round((tempos.abertura + x.inicio) * 100) / 100, Titulo: `${++n}. ${x.titulo}`, Texto: x.texto || '' })),
              Duracao: Math.round(duracao * 100) / 100, Sha256: sha,
            });
            const seg = Math.round((Date.now() - inicio) / 1000);
            resumo.push({ chave: aula.chave, duracao, funcoes: aula.funcoes.length, montagem: seg, custo: p.costs?.displayCost });
            console.log('MONTADA', aula.chave.padEnd(36), duracao.toFixed(1) + 's', `em ${seg}s`, p.costs?.displayCost || '');
            fs.rmSync(path.join(pasta, aula.video), { force: true });
            break;
          }
          await espera(4000);
        }
      } catch (e) {
        resumo.push({ chave: aula.chave, erro: 'montagem (Lambda): ' + String(e.message || e) });
        console.log('ERRO   ', aula.chave, e.message || e);
      } finally {
        emUso -= pedacos;
      }
    };
    await Promise.all(aulas.map(montar));
  }
  const gravacao = path.join(pasta, 'resumo-gravacao.json');
  if (fs.existsSync(gravacao)) for (const r of JSON.parse(fs.readFileSync(gravacao, 'utf8'))) if (r.erro) resumo.push(r);
  fs.writeFileSync(path.join(pasta, 'catalogo.json'), JSON.stringify(catalogo, null, 2));
  fs.writeFileSync(path.join(pasta, 'resumo.json'), JSON.stringify(resumo, null, 2));
  const falhas = resumo.filter(r => r.erro);
  console.log(`${catalogo.length} aula(s) montada(s) na AWS, ${falhas.length} com erro.`);
  if (falhas.length) process.exitCode = 1;
})().catch(e => { console.error(e); process.exit(1); });
