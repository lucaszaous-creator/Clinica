// As fotos da tela real do WhatsApp nativo (out/2026): 24 estados × 1366 e 1024 (os cinco da mídia, 50–54, de 24/10), quatro no celular (480), e quatro fluxos
// executados de verdade (enviar, convite, assumir e anotar, e o áudio gravado e enviado). Uso, com a prévia no relógio fixo de
// docs/telas-blazor.md §5 (sexta 23/10, 10:12 — senão as horas da semente caem de madrugada):
//   TZ=America/Sao_Paulo faketime -f '@2026-10-23 10:12:00' dotnet run --project tools/PreviaBlazor --urls http://127.0.0.1:5199
//   node tools/PreviaBlazor/fotos-whatsapp.js
// PREVIA_URL troca a prévia (padrão http://127.0.0.1:5199 — outra porta para quem roda duas ao mesmo
// tempo) e FOTOS escolhe quais tirar, por prefixo separado por vírgula (FOTOS=50,51,55 só a mídia).
// O microfone é o falso do Chromium (--use-fake-device-for-media-stream): a foto "gravando" e o
// fluxo 55 gravam um tom de verdade, que o CRM de mentira guarda.
// ⚠️ UMA rodada por prévia RECÉM-INICIADA: os três fluxos gravam no CRM de mentira (que vive na
// memória da prévia), e a segunda rodada fotografaria os estados já com a mensagem enviada, o
// convite feito e a conversa assumida — e os fluxos 24 e 25 não achariam mais o que clicar.
// Reprova (lista em "falhas") o que nenhuma rede vê: página que rola para o lado, filho que vaza
// da borda do grupo — foi assim que apareceu o "Retenção 15" por baixo do "+" — e selo cortado na lista.
const { chromium } = require('playwright');
const path = require('path');
const DIR = path.join(__dirname, '../../docs/mockups/whatsapp-nativo/tela-real');
const URL = (process.env.PREVIA_URL || 'http://127.0.0.1:5199').replace(/\/$/, '') + '/tela/ConversasDoWhatsApp';
const ESCOLHA = (process.env.FOTOS || '').split(',').map(x => x.trim()).filter(Boolean);
const quer = nome => !ESCOLHA.length || ESCOLHA.some(x => nome.startsWith(x));
const erros = [], feitas = [], falhas = [];
const pausa = ms => new Promise(r => setTimeout(r, ms));

async function abrir(b, w, h) {
  const p = await b.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1, locale: 'pt-BR', permissions: ['microphone'] });
  p.on('console', m => { if (m.type() === 'error') erros.push(m.text()); });
  p.on('pageerror', e => erros.push(e.message));
  await p.goto(URL); await p.waitForSelector('.wa-linha', { timeout: 90000 }); await pausa(1500);
  return p;
}
const botao = (p, nome) => p.getByRole('button', { name: nome }).first();
async function clicar(p, nome) { await botao(p, nome).click({ timeout: 8000 }); await pausa(900); }
async function linha(p, texto) { await p.locator('.wa-linha', { hasText: texto }).first().click({ timeout: 8000 }); await pausa(1500); }
async function filtro(p, nome) { await p.locator('.wa-filtros button', { hasText: nome }).first().click({ timeout: 8000 }); await pausa(900); }
async function modo(p, nome) { await p.locator('.wa-modo button', { hasText: nome }).first().click({ timeout: 8000 }); await pausa(1200); }

const ROTEIRO = [
  ['01-lista', async p => {}],
  ['02-conversa-maria', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); }],
  ['04-modelos', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Modelos'); }],
  ['05-modelo-preenchido', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Modelos');
      await p.locator('.wa-popover .wa-opcao', { hasText: 'Horário remarcado' }).first().click({ timeout: 8000 }).catch(async () => { await p.locator('.wa-popover .wa-opcao').first().click(); }); await pausa(900); }],
  ['06-respostas-rapidas', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Respostas rápidas'); }],
  ['07-janela-fechada', async p => { await filtro(p, 'Minhas'); await linha(p, 'Carlos Eduardo'); }],
  ['08-conferir-ficha', async p => { await linha(p, 'Terezinha'); await clicar(p, /Conferir a ficha/); }],
  ['09-sem-ficha', async p => { await linha(p, '99712-0045'); }],
  ['10-transferir', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Transferir'); }],
  ['11-encerrar', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Encerrar o atendimento'); }],
  // Em ≤ 1180 px a ficha é um painel por cima: abre-se antes (clicar no botão escondido rolava a página para o lado).
  ['12-cancelar-horario', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças');
      if (p.viewportSize().width <= 1180) await clicar(p, 'Abrir a ficha do paciente');
      await p.locator('.wa-horario button', { hasText: 'Cancelar' }).first().click(); await pausa(900); }],
  ['13-nova-conversa', async p => { await clicar(p, 'Nova conversa'); }],
  ['14-retencao', async p => { await modo(p, 'Retenção'); }],
  ['15-retencao-convite', async p => { await modo(p, 'Retenção'); await linha(p, 'Sebastião'); }],
  ['16-retencao-convite-modelo', async p => { await modo(p, 'Retenção'); await linha(p, 'Sebastião'); await clicar(p, /Preparar convite/); }],
  ['17-retencao-respondeu', async p => { await modo(p, 'Retenção'); await linha(p, 'Alzira'); }],
  ['18-retencao-sem-autorizacao', async p => { await modo(p, 'Retenção'); await p.locator('.wa-linha', { hasText: 'Sem autorização' }).first().click(); await pausa(1500); }],
  ['19-como-foi-a-ligacao', async p => { await modo(p, 'Retenção'); await p.locator('.wa-linha', { hasText: 'Sem autorização' }).first().click(); await pausa(1500); await clicar(p, /Já liguei/); }],
  ['21-busca-na-conversa', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Buscar nesta conversa');
      await p.locator('.wa-conversa input[type=search], .wa-conversa input').first().fill('terça'); await pausa(900); }],
  // A mídia (24/10): a foto e a nota de voz do Roberto, o PDF que a Ana mandou, a foto ampliada, o
  // pedido médico e o vídeo do João (que o provedor não entregou), e a gravação de áudio.
  ['50-midia-conversa', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await mostrar(p, '.wa-midia-foto'); }],
  ['51-midia-foto-ampliada', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await p.locator('.wa-foto').first().click({ timeout: 8000 }); await pausa(900); }],
  ['52-midia-documento-e-video', async p => { await filtro(p, 'Minhas'); await linha(p, 'João Batista'); await mostrar(p, '.wa-midia-documento'); }],
  ['53-midia-video-falhou', async p => { await filtro(p, 'Minhas'); await linha(p, 'João Batista'); await mostrar(p, '.wa-midia-video');
      await p.locator('.wa-video-capa').first().click({ timeout: 8000 }); await pausa(1200); }],
  // A figurinha solta (sem balão nem ampliar) e o retrato da ficha nos avatares (25/10).
  ['56-figurinha', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await mostrar(p, '.wa-figurinha'); }],
  ['57-retratos-nos-avatares', async p => { await filtro(p, 'Minhas'); await linha(p, 'João Batista'); await p.locator('.wa-linha .avatar.com-foto').first().waitFor({ timeout: 8000 }); }],
  ['54-midia-gravando', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await clicar(p, 'Gravar áudio'); await pausa(2600); }],
  // A conversa resolve (25/10, §16): marcar horário pela coluna do paciente (com e sem horário à frente), guardar na ficha a foto
  // recebida, perguntar ao profissional (a ação ao lado do balão e o formulário) e a faixa e o formulário do "Passar o turno".
  ['70-marcar-horario', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await fichaAoLado(p); await p.locator('.wa-marcar button').first().click({ timeout: 8000 }); await pausa(1500); }],
  ['71-marcar-sem-horario', async p => { await filtro(p, 'Todas'); await linha(p, 'Fernanda'); await fichaAoLado(p); await mostrar(p, '.wa-marcar'); }],
  ['72-guardar-na-ficha', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await mostrar(p, '.wa-midia-foto');
      await p.locator('.wa-midia-guardar button').first().click({ timeout: 8000 }); await pausa(900);
      await p.locator('.wa-guardar-titulos label', { hasText: 'Pedido médico' }).click(); await pausa(400); }],
  ['73-perguntar-acao', async p => { await filtro(p, 'Todas'); await linha(p, 'Lúcia'); await p.locator('.wa-m.recebida', { hasText: 'braço' }).last().hover(); await pausa(600); }],
  ['74-perguntar-ao-profissional', async p => { await filtro(p, 'Todas'); await linha(p, 'Lúcia'); await p.locator('.wa-m.recebida', { hasText: 'braço' }).last().hover();
      await p.locator('.wa-m.recebida', { hasText: 'braço' }).last().locator('.wa-m-acoes button').click({ timeout: 8000 }); await pausa(1200); }],
  ['75-passar-o-turno-faixa', async p => { await filtro(p, 'Minhas'); }],
  ['76-passar-o-turno-form', async p => { await filtro(p, 'Minhas'); await p.locator('.wa-turno button').click({ timeout: 8000 }); await pausa(900);
      await p.locator('.modal textarea').fill('Fim do turno da manhã. A Maria espera a confirmação do horário de terça.'); await pausa(400); }],
  // A tela diz a verdade (25/10, §14): transferência com aceite, "Sem resposta" numa palavra só, "Para resolver",
  // o nome no WhatsApp antes de conferir, a Retenção sem telefone e com o mesmo responsável dos dois lados. O 62 ENVIA o
  // pedido (muda o Roberto no CRM de mentira): fica por último.
  ['60-transferencia-para-voce', async p => { await filtro(p, 'Todas'); await linha(p, 'Lúcia Andrade'); }],
  ['61-transferir-pessoa-ou-setor', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await clicar(p, 'Transferir'); }],
  ['63-sem-resposta-uma-palavra', async p => { await filtro(p, 'Minhas'); await linha(p, 'Carlos Eduardo'); }],
  ['64-para-resolver', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await mostrar(p, '.wa-resolver'); await pausa(900); }],
  ['65-nome-no-whatsapp', async p => { await linha(p, 'Terezinha'); }],
  ['66-retencao-sem-telefone', async p => { await modo(p, 'Retenção'); await linha(p, 'Helena Martins'); }],
  ['67-retencao-responsavel', async p => { await modo(p, 'Retenção'); await linha(p, 'Alzira'); }],
  ['62-aguardando-aceite', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima');
    // A largura anterior já enviou o pedido (o CRM de mentira guarda): cancela e pede de novo.
    if (await p.locator('.wa-acao button', { hasText: 'Cancelar o pedido' }).count()) await clicar(p, 'Cancelar o pedido');
    await clicar(p, 'Transferir');
    await p.locator('.modal .wa-escolha', { hasText: 'Bruna Reis' }).first().click({ timeout: 8000 }); await pausa(400);
    await clicar(p, 'Enviar o pedido'); await pausa(1500); }],
];
// Em 1024 a coluna da ficha é a gaveta do botão "Abrir a ficha do paciente": abre antes de mexer nela (sem isso o
// scrollIntoView rolava a página de lado até a gaveta fechada).
async function fichaAoLado(p) { const b = p.getByRole('button', { name: 'Abrir a ficha do paciente' }); if (await b.isVisible()) { await b.click(); await pausa(700); } }
// Contêiner com overflow:hidden também ROLA (o foco num campo fora de vista o rola): a página não passa da
// janela, mas a tela fica deslocada para o lado — o scrollWidth não vê (foto 31 em 480, 25/10/2026).
const deslocados = p => p.evaluate(() => [...document.querySelectorAll('body *')].filter(e => e.scrollLeft > 0)
  .map(e => (typeof e.className === 'string' && e.className.trim() ? e.className.trim().split(/\s+/).slice(0, 2).join('.') : e.tagName)));
async function mostrar(p, seletor) { await p.locator(seletor).first().scrollIntoViewIfNeeded({ timeout: 8000 }); await pausa(600); }

(async () => {
  const b = await chromium.launch({ args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'] });
  for (const [nome, passos] of ROTEIRO) {
    if (!quer(nome)) continue;
    for (const [w, h] of [[1366, 900], [1024, 768]]) {
      const p = await abrir(b, w, h);
      try {
        await passos(p);
        const larg = await p.evaluate(() => document.documentElement.scrollWidth);
        if (larg > w) falhas.push(`${nome}-${w}: rola para o lado (${larg})`);
        if ((await deslocados(p)).length) falhas.push(`${nome}-${w}: rolado para o lado por dentro: ${(await deslocados(p)).join(', ')}`);
        // Filho que passa da borda do grupo (o "Retenção 15" passava por baixo do "+").
        const vazam = await p.evaluate(() => [...document.querySelectorAll('.segmentado, .wa-cab-acoes, .wa-faixa, .wa-filtros, .wa-lista-topo, .wa-escrever-rodape, .wa-doc-acoes, .wa-midia-linha, .wa-gravando, .wa-audio, .wa-turno, .wa-marcar, .wa-guardar-arquivo')]
          .flatMap(g => { const r = g.getBoundingClientRect(); if (!r.width) return [];
            return [...g.children].filter(c => { const x = c.getBoundingClientRect(); return x.width && (x.right > r.right + 1 || x.left < r.left - 1); })
              .map(c => `${g.className.split(' ').pop()} > ${(c.textContent || c.className).trim().slice(0, 24)}`); }));
        if (vazam.length) falhas.push(`${nome}-${w}: vaza do grupo: ${[...new Set(vazam)].join(' | ')}`);
        // O selo da linha é a informação: cortado ("Novo contato · …") reprova (25/10/2026, item 8 da revisão).
        const cortados = await p.evaluate(() => [...document.querySelectorAll('.wa-linha-rodape .selo')].filter(s => s.scrollWidth > s.clientWidth + 1).map(s => s.textContent.trim()));
        if (cortados.length) falhas.push(`${nome}-${w}: selo cortado na lista: ${[...new Set(cortados)].join(' | ')}`);
        await p.screenshot({ path: `${DIR}/${nome}-${w}.png` }); feitas.push(`${nome}-${w}`);
      } catch (e) { falhas.push(`${nome}-${w}: ${e.message.split('\n')[0]}`); }
      await p.close();
    }
  }
  // Celular (480): a lista, a conversa por cima dela e a ficha — as cinco ações do cabeçalho alcançáveis.
  for (const [nome, passos] of [
    ['30-celular-lista', async p => {}],
    ['31-celular-conversa', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); }],
    ['32-celular-ficha', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Abrir a ficha do paciente'); }],
    ['33-celular-midia', async p => { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await mostrar(p, '.wa-midia-foto'); }],
  ]) {
    if (!quer(nome)) continue;
    const p = await abrir(b, 480, 860);
    try {
      await passos(p);
      if (await p.evaluate(() => document.documentElement.scrollWidth) > 480) falhas.push(`${nome}: rola para o lado`);
      if ((await deslocados(p)).length) falhas.push(`${nome}: rolado para o lado por dentro: ${(await deslocados(p)).join(', ')}`);
      const escondidas = await p.evaluate(() => document.querySelector('.wa-conversa.aberta')
        ? [...document.querySelectorAll('.wa-cab-acoes > button')].filter(x => !x.getBoundingClientRect().width).map(x => x.getAttribute('aria-label') || x.textContent.trim()) : []);
      if (escondidas.length) falhas.push(`${nome}: ações escondidas no celular: ${escondidas.join(', ')}`);
      await p.screenshot({ path: `${DIR}/${nome}-480.png` }); feitas.push(nome);
    } catch (e) { falhas.push(`${nome}: ${e.message.split('\n')[0]}`); }
    await p.close();
  }
  // Os fluxos executados de verdade (1366): enviar, convite enviado, assumir e anotar, e o áudio gravado e enviado.
  let p;
  if (quer('23')) { p = await abrir(b, 1366, 900);
  try { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças');
    await p.locator('.wa-escrever textarea').first().fill('Combinado, Maria! Fica para quarta às 14h com a Dra. Helena.');
    await clicar(p, 'Enviar'); await pausa(1500); await p.screenshot({ path: `${DIR}/23-enviou-1366.png` }); feitas.push('23'); }
  catch (e) { falhas.push('23: ' + e.message.split('\n')[0]); } await p.close(); }
  if (quer('24')) { p = await abrir(b, 1366, 900);
  try { await modo(p, 'Retenção'); await linha(p, 'Sebastião'); await clicar(p, /Preparar convite/);
    await p.locator('.wa-popover button.primario, .wa-modelo-form button.primario').last().click({ timeout: 8000 }); await pausa(2500);
    await p.screenshot({ path: `${DIR}/24-convite-enviado-1366.png` }); feitas.push('24'); }
  catch (e) { falhas.push('24: ' + e.message.split('\n')[0]); } await p.close(); }
  if (quer('25')) { p = await abrir(b, 1366, 900);
  try { await linha(p, 'Terezinha'); await clicar(p, /^Assumir/); await clicar(p, 'Nota interna');
    await p.locator('.wa-escrever textarea').first().fill('Conferir com a Terezinha se o Pedro vem com ela ou com o pai.');
    await clicar(p, /Guardar nota|Enviar/); await pausa(1500); await p.screenshot({ path: `${DIR}/25-assumiu-e-nota-1366.png` }); feitas.push('25'); }
  catch (e) { falhas.push('25: ' + e.message.split('\n')[0]); } await p.close(); }
  if (quer('55')) { p = await abrir(b, 1366, 900);
  try { await filtro(p, 'Minhas'); await linha(p, 'Roberto Lima'); await clicar(p, 'Gravar áudio'); await pausa(3200);
    await clicar(p, 'Enviar áudio'); await pausa(3500);
    if (!await p.locator(".wa-m.enviada .wa-audio audio").last().isVisible()) falhas.push('55: o áudio enviado não apareceu no balão');
    await p.screenshot({ path: `${DIR}/55-audio-enviado-1366.png` }); feitas.push('55'); }
  catch (e) { falhas.push('55: ' + e.message.split('\n')[0]); } await p.close(); }
  // 77 e 79 executam de verdade (cancelar e passar o turno gravam no CRM de mentira); 78 é a moldura da aula com ?espera=true
  // (a prévia precisa de CLINICA_NAVEGACAO apontando para um navegacao.json com o app "recepcao" e a chave "whatsapp-crm").
  if (quer('77')) { p = await abrir(b, 1366, 900);
  try { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await p.locator('.wa-horario button', { hasText: 'Cancelar' }).first().click({ timeout: 8000 }); await pausa(900);
    await p.locator('.modal select').selectOption('Profissional ausente'); await p.locator('.modal-rodape button.perigo').click(); await pausa(2500);
    const rascunho = await p.locator('.wa-escrever textarea').first().inputValue();
    if (!rascunho.includes('cancelei o seu horário')) falhas.push('77: o rascunho do cancelamento não ficou no campo');
    await p.screenshot({ path: `${DIR}/77-cancelou-rascunho-1366.png` }); feitas.push('77'); }
  catch (e) { falhas.push('77: ' + e.message.split('\n')[0]); } await p.close(); }
  if (quer('78')) {
    for (const [w, h] of [[1366, 900], [1024, 768]]) {
      const pa = await b.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1, locale: 'pt-BR' });
      pa.on('console', m => { if (m.type() === 'error') erros.push(m.text()); }); pa.on('pageerror', e => erros.push(e.message));
      try {
        await pa.goto(URL.replace('/tela/ConversasDoWhatsApp', '/aula/recepcao/agenda-recepcao?espera=true'));
        await pa.waitForSelector('.moldura-nav .moldura-contador', { timeout: 90000 }); await pausa(1200);
        await pa.screenshot({ path: `${DIR}/78-contador-no-menu-${w}.png` });
        await pa.locator('.moldura-sino button').first().click(); await pausa(900);
        if (!await pa.locator('.sino-lista section', { hasText: 'Esperando resposta no WhatsApp' }).isVisible()) falhas.push(`78-${w}: a seção do sino não apareceu`);
        if (await pa.evaluate(() => document.documentElement.scrollWidth) > w) falhas.push(`78-${w}: rola para o lado`);
        await pa.screenshot({ path: `${DIR}/78-sino-esperando-${w}.png` }); feitas.push(`78-${w}`);
      } catch (e) { falhas.push(`78-${w}: ${e.message.split('\n')[0]}`); }
      await pa.close();
    }
  }
  if (quer('79')) { p = await abrir(b, 1366, 900);
  try { await filtro(p, 'Minhas'); await p.locator('.wa-turno button').click({ timeout: 8000 }); await pausa(900);
    await p.locator('.modal textarea').fill('Fim do turno da manhã. A Maria espera a confirmação do horário de terça.');
    await p.locator('.modal-rodape button.primario').click(); await pausa(3500);
    if (await p.locator('.wa-turno').count()) falhas.push('79: as conversas não voltaram à fila');
    await p.screenshot({ path: `${DIR}/79-turno-passado-1366.png` }); feitas.push('79'); }
  catch (e) { falhas.push('79: ' + e.message.split('\n')[0]); } await p.close(); }
  await b.close();
  console.log('feitas:', feitas.length, '\nfalhas:', falhas.length ? falhas : 'nenhuma', '\nerros de console:', erros.length ? [...new Set(erros)].slice(0, 5) : 'nenhum');
})();
