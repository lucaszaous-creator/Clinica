#!/usr/bin/env node
/* Abre CADA tela num Chromium, confere o que só a tela montada mostra e salva um PNG.
   O checklist do design-system/novas-telas.md §8 manda conferir visualmente, não pelo
   código: ícone que não carrega não quebra nada — ele simplesmente some. Aqui isso vira
   erro, com o nome da tela.
   Uso: npm run capturar  (precisa do `npm run preview` no ar, ou passe a URL como argumento) */
import { chromium } from 'playwright';
import { existsSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const BASE = process.argv[2] || 'http://localhost:4173';
const RAIZ = join(fileURLToPath(new URL('.', import.meta.url)), '..');
const SAIDA = join(RAIZ, 'tools', 'capturas');
mkdirSync(SAIDA, { recursive: true });

const TELAS = [
  ['recepcao','painel','Recepção — Painel do dia'],
  ['recepcao','agenda','Recepção — Agenda'],
  ['recepcao','pacientes','Recepção — Pacientes'],
  ['recepcao','confirmacoes','Recepção — Confirmações'],
  ['clinico','painel','Clínico — Painel clínico'],
  ['clinico','prontuarios','Clínico — Prontuários'],
  ['clinico','exames','Clínico — Exames'],
  ['clinico','prescricoes','Clínico — Prescrições'],
  ['gerente','painel','Gerente — Visão geral'],
  ['gerente','profissionais','Gerente — Profissionais'],
  ['gerente','convenios','Gerente — Convênios'],
  ['gerente','receber','Gerente — Contas a receber'],
  ['gerente','faturamento','Gerente — Painel de faturamento'],
  ['faturamento','pendencias','Faturamento — Pendências de baixa'],
  ['faturamento','lotes','Faturamento — Guias e lotes TISS'],
  ['faturamento','glosas','Faturamento — Glosas'],
  ['gerente','config','Comum — Configurações da clínica'],
  ['gerente','ajuda','Comum — Ajuda & suporte'],
];

/* Os três modais clínicos: tela + o botão que os abre. */
const MODAIS = [
  ['clinico','prontuarios','Abrir','modal-prontuario','Modal — Prontuário'],
  ['clinico','exames','Ver resultado','modal-exame','Modal — Resultado de exame'],
  ['clinico','prescricoes','Ver folha','modal-prescricao','Modal — Folha de prescrição'],
];

/* Regras duras do handoff (README §1 e novas-telas.md §1): o que NÃO pode faltar em
   nenhuma tela. Cada uma devolve null quando passa, ou a frase do problema. */
const conferir = pagina => pagina.evaluate(() => {
  const problemas = [];
  const svgs = el => el ? el.querySelectorAll('svg').length : 0;

  const topbar = document.querySelector('header');
  if (!topbar) problemas.push('sem Topbar');
  else {
    const alto = Math.round(topbar.getBoundingClientRect().height);
    if (alto !== 56) problemas.push(`Topbar com ${alto}px (esperado 56)`);
    const campo = topbar.querySelector('input');
    if (!campo) problemas.push('Topbar sem a busca');
    else {
      const pilula = campo.parentElement;
      const raio = parseFloat(getComputedStyle(pilula).borderRadius);
      if (raio < 100) problemas.push(`busca não está em pílula (raio ${raio}px)`);
      if (svgs(pilula) === 0) problemas.push('busca SEM a lupa (nenhum svg no campo)');
      if (!campo.placeholder) problemas.push('busca sem placeholder');
    }
    const sino = topbar.querySelector('button[aria-label="Notificações"]');
    if (!sino) problemas.push('Topbar sem o sino de notificações');
    else if (svgs(sino) === 0) problemas.push('sino sem ícone');
    // avatar = último filho da topbar, redondo, com as iniciais do usuário
    const avatar = topbar.lastElementChild;
    const redondo = avatar && getComputedStyle(avatar).borderRadius === '50%';
    if (!redondo) problemas.push('Topbar sem o avatar do usuário');
  }

  const nav = document.querySelector('nav');
  if (!nav) problemas.push('sem Sidebar');
  else {
    const larg = Math.round(nav.getBoundingClientRect().width);
    if (larg !== 240) problemas.push(`Sidebar com ${larg}px (esperado 240)`);
    const itens = [...nav.querySelectorAll('button')].filter(b => !b.getAttribute('aria-label'));
    if (!itens.length) problemas.push('Sidebar sem itens');
    const semIcone = itens.filter(b => svgs(b) === 0).map(b => b.innerText.trim() || '(sem rótulo)');
    if (semIcone.length) problemas.push('item(ns) de sidebar SEM ícone: ' + semIcone.join(', '));
  }

  // Icon.jsx desenha um quadrado tracejado quando o nome não existe no lucide.
  const desconhecidos = [...document.querySelectorAll('[title^="ícone desconhecido"]')]
    .map(e => e.getAttribute('title'));
  if (desconhecidos.length) problemas.push('ícone(s) inexistente(s): ' + [...new Set(desconhecidos)].join(', '));

  // svg de tamanho zero = ícone que ocupa lugar e não desenha nada
  const zerados = [...document.querySelectorAll('svg')].filter(s => {
    const r = s.getBoundingClientRect();
    return r.width === 0 || r.height === 0;
  }).length;
  if (zerados) problemas.push(`${zerados} ícone(s) renderizados com tamanho zero`);

  return { problemas, svgs: document.querySelectorAll('svg').length };
});

/* O ambiente pode trazer o Chromium fora da pasta que esta versão do Playwright procura
   (PLAYWRIGHT_BROWSERS_PATH); quando existir, aponte para ele em vez de baixar outro. */
const CHROMIUM = '/opt/pw-browsers/chromium';
const navegador = await chromium.launch(existsSync(CHROMIUM) ? { executablePath: CHROMIUM } : {});
const contexto = await navegador.newContext({ viewport: { width: 1440, height: 1024 }, deviceScaleFactor: 1 });
const pagina = await contexto.newPage();
const errosConsole = [];
pagina.on('console', m => { if (m.type() === 'error') errosConsole.push(m.text()); });
pagina.on('pageerror', e => errosConsole.push(String(e)));

let reprovadas = 0;
const linha = (rotulo, r) => {
  const ok = r.problemas.length === 0;
  if (!ok) reprovadas++;
  console.log(`${ok ? 'OK  ' : 'FALHA'} ${rotulo.padEnd(38)} ${r.svgs} ícones${ok ? '' : '\n      → ' + r.problemas.join('\n      → ')}`);
};

for (const [modulo, tela, rotulo] of TELAS) {
  await pagina.goto(`${BASE}/?modulo=${modulo}&tela=${tela}`, { waitUntil: 'networkidle' });
  await pagina.waitForSelector('nav svg');
  const r = await conferir(pagina);
  linha(rotulo, r);
  await pagina.screenshot({ path: join(SAIDA, `${modulo}-${tela}.png`), fullPage: true });
}

for (const [modulo, tela, botao, arquivo, rotulo] of MODAIS) {
  await pagina.goto(`${BASE}/?modulo=${modulo}&tela=${tela}`, { waitUntil: 'networkidle' });
  await pagina.getByRole('button', { name: botao, exact: true }).first().click();
  await pagina.waitForTimeout(150);
  const r = await conferir(pagina);
  linha(rotulo, r);
  await pagina.screenshot({ path: join(SAIDA, `${arquivo}.png`) });
}

await navegador.close();

/* Recurso externo que não baixa (a fonte Inter do Google, quando a máquina está sem
   rede) é AVISO: a pilha de fontes cai para Segoe UI/system-ui e a tela continua certa.
   Erro vindo do nosso código — Icon com nome inexistente, exceção de render — reprova. */
const unicos = [...new Set(errosConsole)];
const avisos = unicos.filter(e => e.includes('Failed to load resource'));
const graves = unicos.filter(e => !avisos.includes(e));
if (avisos.length) { console.log('\nAvisos (recurso externo indisponível):'); avisos.forEach(e => console.log('  ' + e)); }
if (graves.length) { console.log('\nErros no console do navegador:'); graves.forEach(e => console.log('  ' + e)); }
console.log(`\n${TELAS.length + MODAIS.length} telas conferidas · ${reprovadas} reprovada(s) · PNGs em tools/capturas/`);
process.exit((reprovadas || graves.length) ? 1 : 0);
