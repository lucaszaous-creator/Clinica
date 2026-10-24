// As fotos da CONVERSA AO LADO (out/2026, fase 2 do WhatsApp nativo): a gaveta aberta sobre a Agenda do
// dia, a Ficha do balcão, as Confirmações e o Acompanhamento, em 1366 e 1024 (e a agenda em 480), mais o
// paciente sem conversa e o formulário "Nova conversa" que ele abre. Uso, com a prévia no relógio fixo de
// docs/telas-blazor.md §5 (sexta 23/10, 10:12 — senão as horas da semente caem de madrugada):
//   TZ=America/Sao_Paulo faketime -f '@2026-10-23 10:12:00' dotnet run --project tools/PreviaBlazor --urls http://127.0.0.1:5199
//   node tools/PreviaBlazor/fotos-conversa-ao-lado.js [http://127.0.0.1:5199]
// Reprova (lista em "falhas") página que rola para o lado, gaveta que não abriu ou abriu na conversa
// errada, e filho que vaza da borda da gaveta.
const { chromium } = require('playwright');
const path = require('path');
const BASE = process.argv[2] || 'http://127.0.0.1:5199';
const DIR = path.join(__dirname, '../../docs/mockups/whatsapp-nativo/tela-real');
const erros = [], feitas = [], falhas = [];
const pausa = ms => new Promise(r => setTimeout(r, ms));

async function abrir(b, tela, w, h) {
  const p = await b.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1, locale: 'pt-BR' });
  p.on('console', m => { if (m.type() === 'error') erros.push(`${tela}: ${m.text()}`); });
  p.on('pageerror', e => erros.push(`${tela}: ${e.message}`));
  await p.goto(`${BASE}/tela/${tela}`); await p.waitForSelector('.pagina', { timeout: 90000 }); await pausa(2500);
  return p;
}

// A gaveta abriu, com quem se esperava no cabeçalho da conversa (ou no estado "sem conversa").
async function conferir(p, nome, esperado) {
  await p.waitForSelector('.wa-lado', { timeout: 15000 });
  await pausa(2000);
  const texto = await p.locator('.wa-lado').innerText();
  if (esperado && !texto.includes(esperado)) falhas.push(`${nome}: a gaveta não mostra «${esperado}»`);
  const largura = await p.evaluate(() => ({ doc: document.documentElement.scrollWidth, janela: window.innerWidth }));
  if (largura.doc > largura.janela) falhas.push(`${nome}: a página rola para o lado (${largura.doc} > ${largura.janela})`);
  const vaza = await p.evaluate(() => {
    const g = document.querySelector('.wa-lado').getBoundingClientRect();
    return [...document.querySelectorAll('.wa-lado .wa-cab *, .wa-lado .wa-escrever *, .wa-lado .wa-lado-faixa *, .wa-lado .wa-acao *, .wa-lado .wa-faixa *')]
      .filter(e => e.offsetParent !== null && !e.closest('select'))
      .filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && (r.right > g.right + 1 || r.left < g.left - 1); })
      .map(e => (e.className?.baseVal ?? e.className) || e.tagName).slice(0, 4);
  });
  if (vaza.length) falhas.push(`${nome}: vaza da gaveta: ${vaza.join(', ')}`);
}

async function foto(p, nome) { await p.screenshot({ path: path.join(DIR, `${nome}.png`) }); feitas.push(nome); }

const linhaDe = (p, seletor, nome) => p.locator(seletor, { hasText: nome }).first();

const ROTEIRO = [
  // A Agenda do dia: o ícone do WhatsApp na linha da Maria (conversa ligada à ficha, com a resposta nova).
  ['agenda', 'AgendaDoDia', 'Maria das Graças', async p => {
    await linhaDe(p, 'tr.rd-linha', 'Maria das Graças').locator('.wa-lado-abrir').click({ timeout: 10000 }); }],
  // A Ficha do balcão: a ficha é a que a agenda pediu (o pedido da ficha é do processo da prévia).
  ['ficha', 'FichaDoBalcao', 'Carlos Eduardo Mota', async p => {}],
  // As Confirmações (a rodada de amanhã): o lembrete do Pedro saiu para o telefone que ele divide com a
  // mãe — a conversa é do TELEFONE, sem ficha ligada, e a gaveta pede para conferir quem escreve.
  ['confirmacoes', 'ConfirmacoesDeAgenda', 'Pelo telefone da ficha', async p => {
    await linhaDe(p, 'tr', 'Pedro Henrique').locator('.conf-whats').click({ timeout: 10000 }); }],
  // O Acompanhamento (toda a equipe): a Alzira respondeu ao convite (o "Como foi?" está na conversa).
  ['acompanhamento', 'AcompanhamentoDePacientes', 'Alzira', async p => {
    await p.getByRole('button', { name: 'Toda a equipe' }).click({ timeout: 10000 }); await pausa(1500);
    await linhaDe(p, 'tr.acomp-linha', 'Alzira').locator('.wa-lado-abrir, .acomp-whats').first().click({ timeout: 10000 }); }],
];

(async () => {
  const b = await chromium.launch();
  // A ficha do Carlos (conversa com a janela de 24 h fechada): a agenda pede (menu ⋯ → Abrir ficha); a navegação não existe fora da aula, mas o pedido fica.
  {
    const p = await abrir(b, 'AgendaDoDia', 1366, 900);
    await linhaDe(p, 'tr.rd-linha', 'Carlos Eduardo Mota').locator('.rd-menu > button').click({ timeout: 10000 });
    await p.locator('.rd-menu-lista button', { hasText: 'Ficha do paciente' }).first().click({ timeout: 10000 })
      .catch(() => falhas.push('ficha: o menu da agenda não abriu a ficha'));
    await pausa(800); await p.close();
  }
  for (const [nome, tela, esperado, passos] of ROTEIRO) {
    for (const [w, h] of [[1366, 900], [1024, 768]]) {
      const id = `50-painel-${nome}-${w}`;
      try {
        const p = await abrir(b, tela, w, h);
        if (nome === 'ficha') await p.locator('.fb-acoes .wa-lado-abrir').click({ timeout: 10000 });
        else await passos(p);
        await conferir(p, id, esperado);
        await foto(p, id);
        await p.close();
      } catch (e) { falhas.push(`${id}: ${e.message.split('\n')[0]}`); }
    }
  }
  // O celular: a gaveta é a tela toda.
  try {
    const p = await abrir(b, 'AgendaDoDia', 480, 860);
    await linhaDe(p, 'tr.rd-linha', 'Maria das Graças').locator('.wa-lado-abrir').click({ timeout: 10000 });
    await conferir(p, '50-painel-agenda-480', 'Maria das Graças');
    await foto(p, '50-painel-agenda-480');
    await p.close();
  } catch (e) { falhas.push(`50-painel-agenda-480: ${e.message.split('\n')[0]}`); }
  // Paciente sem conversa (e sem caso no Acompanhamento): o painel oferece começar por modelo.
  for (const [w, h] of [[1366, 900], [1024, 768]]) {
    const id = `51-painel-sem-conversa-${w}`;
    try {
      const p = await abrir(b, 'AgendaDoDia', w, h);
      const botoes = p.locator('tr.rd-linha .wa-lado-abrir:not([disabled])');
      const n = await botoes.count();
      let achou = false;
      for (let i = 0; i < n && !achou; i++) {
        await botoes.nth(i).click({ timeout: 10000 }); await pausa(2500);
        achou = await p.getByRole('button', { name: 'Começar com um modelo' }).count() > 0;
      }
      if (!achou) { falhas.push(`${id}: nenhum paciente da agenda sem conversa`); await p.close(); continue; }
      await conferir(p, id, 'Ainda não há conversa');
      await foto(p, id);
      await p.getByRole('button', { name: 'Começar com um modelo' }).click(); await pausa(1500);
      await foto(p, `52-painel-nova-conversa-${w}`);
      await p.close();
    } catch (e) { falhas.push(`${id}: ${e.message.split('\n')[0]}`); }
  }
  await b.close();
  console.log('fotos:', feitas.length, feitas.join(' '));
  console.log('erros do console:', erros.length ? erros : 'nenhum');
  console.log('falhas:', falhas.length ? falhas : 'nenhuma');
})();
