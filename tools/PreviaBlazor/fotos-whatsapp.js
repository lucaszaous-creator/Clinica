// As fotos da tela real do WhatsApp nativo (out/2026): 21 estados × 1366 e 1024, três no celular (480), e três fluxos
// executados de verdade (enviar, convite, assumir e anotar). Uso, com a prévia no relógio fixo de
// docs/telas-blazor.md §5 (sexta 23/10, 10:12 — senão as horas da semente caem de madrugada):
//   TZ=America/Sao_Paulo faketime -f '@2026-10-23 10:12:00' dotnet run --project tools/PreviaBlazor --urls http://127.0.0.1:5199
//   node tools/PreviaBlazor/fotos-whatsapp.js
// ⚠️ UMA rodada por prévia RECÉM-INICIADA: os três fluxos gravam no CRM de mentira (que vive na
// memória da prévia), e a segunda rodada fotografaria os estados já com a mensagem enviada, o
// convite feito e a conversa assumida — e os fluxos 24 e 25 não achariam mais o que clicar.
// Reprova (lista em "falhas") o que nenhuma rede vê: página que rola para o lado e filho que vaza
// da borda do grupo — foi assim que apareceu o "Retenção 15" por baixo do "+".
const { chromium } = require('playwright');
const path = require('path');
const DIR = path.join(__dirname, '../../docs/mockups/whatsapp-nativo/tela-real');
const URL = 'http://127.0.0.1:5199/tela/ConversasDoWhatsApp';
const erros = [], feitas = [], falhas = [];
const pausa = ms => new Promise(r => setTimeout(r, ms));

async function abrir(b, w, h) {
  const p = await b.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1, locale: 'pt-BR' });
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
      await p.locator('.wa-popover .wa-opcao', { hasText: 'semdor_horario_remarcado' }).first().click({ timeout: 8000 }).catch(async () => { await p.locator('.wa-popover .wa-opcao').first().click(); }); await pausa(900); }],
  ['06-respostas-rapidas', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Respostas rápidas'); }],
  ['07-janela-fechada', async p => { await filtro(p, 'Minhas'); await linha(p, 'Carlos Eduardo'); }],
  ['08-conferir-ficha', async p => { await linha(p, 'Terezinha'); await clicar(p, /Conferir a ficha/); }],
  ['09-sem-ficha', async p => { await linha(p, '99712-0045'); }],
  ['10-transferir', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Transferir'); }],
  ['11-encerrar', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Encerrar o atendimento'); }],
  ['12-cancelar-horario', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await p.locator('.wa-horario button', { hasText: 'Cancelar' }).first().click(); await pausa(900); }],
  ['13-nova-conversa', async p => { await clicar(p, 'Nova conversa'); }],
  ['14-retencao', async p => { await modo(p, 'Retenção'); }],
  ['15-retencao-convite', async p => { await modo(p, 'Retenção'); await linha(p, 'Sebastião'); }],
  ['16-retencao-convite-modelo', async p => { await modo(p, 'Retenção'); await linha(p, 'Sebastião'); await clicar(p, /Preparar convite/); }],
  ['17-retencao-respondeu', async p => { await modo(p, 'Retenção'); await linha(p, 'Alzira'); }],
  ['18-retencao-sem-autorizacao', async p => { await modo(p, 'Retenção'); await p.locator('.wa-linha', { hasText: 'Sem autorização' }).first().click(); await pausa(1500); }],
  ['19-como-foi-a-ligacao', async p => { await modo(p, 'Retenção'); await p.locator('.wa-linha', { hasText: 'Sem autorização' }).first().click(); await pausa(1500); await clicar(p, /Já liguei/); }],
  ['21-busca-na-conversa', async p => { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças'); await clicar(p, 'Buscar nesta conversa');
      await p.locator('.wa-conversa input[type=search], .wa-conversa input').first().fill('terça'); await pausa(900); }],
];

(async () => {
  const b = await chromium.launch();
  for (const [nome, passos] of ROTEIRO) {
    for (const [w, h] of [[1366, 900], [1024, 768]]) {
      const p = await abrir(b, w, h);
      try {
        await passos(p);
        const larg = await p.evaluate(() => document.documentElement.scrollWidth);
        if (larg > w) falhas.push(`${nome}-${w}: rola para o lado (${larg})`);
        // Filho que passa da borda do grupo (o "Retenção 15" passava por baixo do "+").
        const vazam = await p.evaluate(() => [...document.querySelectorAll('.segmentado, .wa-cab-acoes, .wa-faixa, .wa-filtros, .wa-lista-topo, .wa-escrever-rodape')]
          .flatMap(g => { const r = g.getBoundingClientRect(); if (!r.width) return [];
            return [...g.children].filter(c => { const x = c.getBoundingClientRect(); return x.width && (x.right > r.right + 1 || x.left < r.left - 1); })
              .map(c => `${g.className.split(' ').pop()} > ${(c.textContent || c.className).trim().slice(0, 24)}`); }));
        if (vazam.length) falhas.push(`${nome}-${w}: vaza do grupo: ${[...new Set(vazam)].join(' | ')}`);
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
  ]) {
    const p = await abrir(b, 480, 860);
    try {
      await passos(p);
      if (await p.evaluate(() => document.documentElement.scrollWidth) > 480) falhas.push(`${nome}: rola para o lado`);
      const escondidas = await p.evaluate(() => document.querySelector('.wa-conversa.aberta')
        ? [...document.querySelectorAll('.wa-cab-acoes > button')].filter(x => !x.getBoundingClientRect().width).map(x => x.getAttribute('aria-label') || x.textContent.trim()) : []);
      if (escondidas.length) falhas.push(`${nome}: ações escondidas no celular: ${escondidas.join(', ')}`);
      await p.screenshot({ path: `${DIR}/${nome}-480.png` }); feitas.push(nome);
    } catch (e) { falhas.push(`${nome}: ${e.message.split('\n')[0]}`); }
    await p.close();
  }
  // Os fluxos executados de verdade (1366): enviar, convite enviado, assumir e anotar.
  let p = await abrir(b, 1366, 900);
  try { await filtro(p, 'Minhas'); await linha(p, 'Maria das Graças');
    await p.locator('.wa-escrever textarea').first().fill('Combinado, Maria! Fica para quarta às 14h com a Dra. Helena. O lembrete chega na manhã do dia.');
    await clicar(p, 'Enviar'); await pausa(1500); await p.screenshot({ path: `${DIR}/23-enviou-1366.png` }); feitas.push('23'); }
  catch (e) { falhas.push('23: ' + e.message.split('\n')[0]); } await p.close();
  p = await abrir(b, 1366, 900);
  try { await modo(p, 'Retenção'); await linha(p, 'Sebastião'); await clicar(p, /Preparar convite/);
    await p.locator('.wa-popover button.primario, .wa-modelo-form button.primario').last().click({ timeout: 8000 }); await pausa(2500);
    await p.screenshot({ path: `${DIR}/24-convite-enviado-1366.png` }); feitas.push('24'); }
  catch (e) { falhas.push('24: ' + e.message.split('\n')[0]); } await p.close();
  p = await abrir(b, 1366, 900);
  try { await linha(p, 'Terezinha'); await clicar(p, /^Assumir/); await clicar(p, 'Nota interna');
    await p.locator('.wa-escrever textarea').first().fill('Conferir com a Terezinha se o Pedro vem com ela ou com o pai.');
    await clicar(p, /Guardar nota|Enviar/); await pausa(1500); await p.screenshot({ path: `${DIR}/25-assumiu-e-nota-1366.png` }); feitas.push('25'); }
  catch (e) { falhas.push('25: ' + e.message.split('\n')[0]); } await p.close();
  await b.close();
  console.log('feitas:', feitas.length, '\nfalhas:', falhas.length ? falhas : 'nenhuma', '\nerros de console:', erros.length ? [...new Set(erros)].slice(0, 5) : 'nenhum');
})();
