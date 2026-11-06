// Confere, SEM ROLAR NADA, se cada coisa da folha do médico está à vista: dentro da janela e não
// recortada por nenhum contêiner com rolagem — e se a caixa de texto tem altura para escrever.
// Com a prévia no ar (/atendimento-medico): node tools/PreviaBlazor/conferir-folha.js <larg> <alt> [estresse]
// O notebook da clínica (1280×720) deixa ~570 px para a tela no app: confira 1280 570 e 1280 570 estresse.
// "estresse" acrescenta à coluna do EMITIR o que aparece em dia cheio: o botão do termo pendente,
// os botões de BSV e três papéis em "Já saiu hoje" — é o caso que mais cresce a coluna.
const { chromium } = require('playwright');
const [,, larg, alt, modo] = process.argv;
const alvos = [
  ['Receita', 'button.at-papel', 'Receita'],
  ['Atestado', 'button.at-papel', 'Atestado'],
  ['Laudo médico', 'button.at-papel', 'Laudo médico'],
  ['Pedido de exame', 'button.at-papel', 'Pedido de exame'],
  ['Comparecimento', 'button.at-papel', 'Comparecimento'],
  ['Prescrição de infusão', 'button.at-papel', 'Prescrição de infusão'],
  ['Documentos emitidos', 'button', 'Documentos emitidos'],
  ['Termo pendente (estresse)', 'button.at-termo', ''],
  ['Já saiu hoje (estresse)', '.at-saiu-hoje summary', ''],
  ['Consultar histórico', 'button', 'Consultar histórico'],
  ['Data da consulta', '.at-consulta input', ''],
  ['EVA antes', 'select[aria-label="EVA antes"]', ''],
  ['EVA depois', 'select[aria-label="EVA depois"]', ''],
  ['Copiar última evolução', 'button', 'Copiar última evolução'],
  ['Usar modelo', 'button', 'Usar modelo'],
  ['Mapa corporal', 'button', 'Mapa corporal'],
  ['Caixa de texto', '.at-editor .editor-texto', ''],
  ['Campos complementares', 'button.at-detalhe', ''],
  ['Imprimir', 'button', 'Imprimir'],
  ['Salvar e concluir sessão', 'button', 'Salvar e concluir sessão'],
  ['Trocar paciente', 'button', 'Trocar paciente'],
];
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: +larg, height: +alt }, locale: 'pt-BR' });
  await p.goto('http://127.0.0.1:5199/atendimento-medico');
  await p.waitForSelector('.at-registro', { timeout: 30000 });
  await p.waitForTimeout(1500);
  if (modo === 'estresse') {
    await p.evaluate(() => {
      const e = document.querySelector('.at-emitir');
      const acoes = e.querySelector('.at-emitir-acoes');
      const bsv = document.createElement('button'); bsv.className = 'botao mini'; bsv.textContent = 'Novo paciente de BSV';
      acoes.appendChild(bsv);
      const avisos = document.querySelector('.at-avisos');
      avisos.insertAdjacentHTML('afterbegin', '<div class="at-aviso grave"><button class="botao mini contorno-perigo at-termo">Colher termo do procedimento</button></div>');
      document.querySelector('.at-rodape .botoes-linha').insertAdjacentHTML('afterbegin', '<details class="at-saiu-hoje"><summary class="botao fantasma">Já saiu hoje (3)</summary><ul class="at-saiu"><li>Receita 2026/0101</li></ul></details>');
    });
    await p.waitForTimeout(300);
  }
  const resultado = await p.evaluate((alvos) => {
    const vis = el => {
      const r = el.getBoundingClientRect();
      if (r.width === 0 || r.height === 0) return 'sem tamanho';
      if (r.top < 0 || r.bottom > innerHeight + 0.5 || r.left < 0 || r.right > innerWidth + 0.5) return 'fora da janela';
      for (let a = el.parentElement; a; a = a.parentElement) {
        const s = getComputedStyle(a);
        if (/(auto|scroll|hidden)/.test(s.overflowY + s.overflowX)) {
          const ra = a.getBoundingClientRect();
          if (r.top < ra.top - 0.5 || r.bottom > ra.bottom + 0.5) return 'escondido pela rolagem de ' + (a.className || a.tagName);
        }
      }
      return 'ok';
    };
    return alvos.map(([nome, sel, texto]) => {
      const el = [...document.querySelectorAll(sel)].find(x => !texto || x.textContent.includes(texto));
      return [nome, el ? vis(el) : 'não existe'];
    });
  }, alvos);
  const alturaTexto = await p.evaluate(() => Math.round(document.querySelector('.at-editor .editor-texto').getBoundingClientRect().height));
  if (alturaTexto < 180) resultado.push(['Caixa de texto com altura útil', `só ${alturaTexto}px`]);
  const rolaPagina = await p.evaluate(() => [...document.querySelectorAll('.palco-tela')].map(x => x.scrollHeight > x.clientHeight + 1));
  console.log(`${larg}x${alt}${modo ? ' ' + modo : ''} — a página rola: ${rolaPagina[0] ? 'SIM' : 'não'}`);
  for (const [n, r] of resultado) if (!(r === 'não existe' && n.includes('estresse'))) console.log((r === 'ok' ? '  ok   ' : '  FALHA ') + n + (r === 'ok' ? '' : ' — ' + r));
  await b.close();
})();
