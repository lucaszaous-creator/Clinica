import './style.css';
import { createElement, House, Landmark, Wallet, ChartNoAxesCombined, ArrowDownLeft, ArrowUpRight, Plus, Search, ChevronDown, ChevronLeft, ChevronRight, Menu, Download, RefreshCw, X, Receipt, Check, CalendarDays, ArrowLeftRight, Package, Settings2, Rows3, CircleHelp, CreditCard, Expand, ArrowUp, ArrowDown, History, QrCode, Eye, EyeOff, ListChecks, Users, ChartColumn, type IconNode } from 'lucide';

type Linha = { id: string; data: string; descricao: string; categoria: string; situacao: string; valor: string; podeRealizar: boolean; podeCancelar: boolean; ehEntrada: boolean };
type Estado = {
  tipo: 'estado'; mes: string; entradas: string; saidas: string; saldo: string; previsto: string;
  liquido: string; deducoes: string; ultimoMovimento: string; detalheUltimoMovimento: string;
  graficoEntradas: string; graficoSaidas: string; serieDisponivel: boolean; situacaoSerie: string;
  linhas: Linha[]; usuario: string; carregando: boolean; erro: string | null;
  rotas: { chave: string; rotulo: string }[]; podeEditar?: boolean; truncado?: boolean; resumoFiltro?: string; filtroTexto?: string;
  aviso?: { texto: string; tipo: 'info' | 'sucesso' | 'erro' } | null;
};
type Mensagem = { acao: string; valor?: string; id?: string };
type Ponte = { postMessage: (m: Mensagem) => void; addEventListener: (nome: 'message', cb: (e: MessageEvent) => void) => void };
declare global { interface Window { chrome?: { webview?: Ponte } } }

const ponte = window.chrome?.webview;
const demo = !ponte && new URLSearchParams(location.search).get('demo') === '1';
const app = document.querySelector<HTMLDivElement>('#app')!;
const icones: Record<string, IconNode> = { casa: House, banco: Landmark, carteira: Wallet, grafico: ChartNoAxesCombined, entrada: ArrowDownLeft, saida: ArrowUpRight, mais: Plus, buscar: Search, baixo: ChevronDown, anterior: ChevronLeft, proximo: ChevronRight, menu: Menu, baixar: Download, atualizar: RefreshCw, fechar: X, recibo: Receipt, confirmar: Check, calendario: CalendarDays, troca: ArrowLeftRight, estoque: Package, ajustes: Settings2, linhas: Rows3, ajuda: CircleHelp, cartao: CreditCard, expandir: Expand, subir: ArrowUp, descer: ArrowDown, historico: History, pix: QrCode, olho: Eye, oculto: EyeOff, contas: ListChecks, profissionais: Users, producao: ChartColumn };
const svg = (nome: string, classe = '') => {
  const el = createElement(icones[nome] ?? Rows3, { width: 20, height: 20, 'stroke-width': 1.65, 'aria-hidden': 'true', focusable: 'false', class: classe });
  return el.outerHTML;
};
const h = (valor: unknown) => String(valor ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
const moeda = (valor: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor);
const hoje = new Date();
const mesInicial = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}`;
let estado: Estado = { tipo: 'estado', mes: mesInicial, entradas: '—', saidas: '—', saldo: '—', previsto: '—', liquido: '—', deducoes: '—', ultimoMovimento: 'Nenhum movimento disponível', detalheUltimoMovimento: '', graficoEntradas: '', graficoSaidas: '', serieDisponivel: false, situacaoSerie: 'Aguardando os dados do sistema.', linhas: [], usuario: '', carregando: !!ponte, erro: null, rotas: [], podeEditar: false };
let valoresOcultos = false;
const valorVisivel = (valor: string) => valoresOcultos ? '••••' : h(valor);
const textoPrivado = (texto: string) => h(valoresOcultos ? texto.replace(/R\$\s*[\d.,]+/g, 'R$ ••••') : texto);
let menuAberto = false;
let usuarioAberto = false;
let buscaMenu = '';
let termo = '';
let avisoDemo = '';
let filtroTimer: ReturnType<typeof setTimeout>;
let avisoTimer: ReturnType<typeof setTimeout>;

function nomeMes(mes: string) {
  if (!/^\d{4}-\d{2}$/.test(mes)) return 'Selecionar mês';
  return new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' }).format(new Date(`${mes}-15T12:00:00`));
}
function nomeIcone(rotulo: string) {
  if (/estoque|validade/i.test(rotulo)) return 'estoque';
  if (/repass/i.test(rotulo)) return 'profissionais';
  if (/tax|plano|categor/i.test(rotulo)) return 'ajustes';
  if (/produ/i.test(rotulo)) return 'producao';
  if (/fluxo|resultado/i.test(rotulo)) return 'grafico';
  if (/cart|receb/i.test(rotulo)) return 'cartao';
  if (/conta|inadimpl/i.test(rotulo)) return 'contas';
  if (/extrato|concili/i.test(rotulo)) return 'banco';
  return 'linhas';
}
function enviar(acao: string, valor?: string, id?: string) {
  const mensagem: Mensagem = { acao, ...(valor === undefined ? {} : { valor }), ...(id === undefined ? {} : { id }) };
  if (ponte) ponte.postMessage(mensagem);
  else if (demo) acaoDemo(mensagem);
}
function botao(acao: string, texto: string, icone?: string, classe = '', desabilitado = false) {
  return `<button class="botao ${classe}" data-action="${acao}" ${desabilitado ? 'disabled' : ''}>${icone ? svg(icone) : ''}<span>${h(texto)}</span></button>`;
}
function grafico(caminho: string, classe: string, descricao: string) {
  // O caminho vem do cálculo da série no host. Não se cria curva se não houver série.
  if (!estado.serieDisponivel || !caminho || !/^[MLCQSTHVZAmlcqsthvza\d.,\s+eE-]+$/.test(caminho))
    return `<div class="grafico-vazio">${h(estado.situacaoSerie || 'Sem movimentos para desenhar a série.')}</div>`;
  return `<svg class="grafico ${classe}" viewBox="0 0 280 58" role="img" aria-label="${h(descricao)}" preserveAspectRatio="none"><path class="linha-base" d="M0 54H280"/><path class="curva" d="${h(caminho)}"/></svg>`;
}
function linhas() {
  if (estado.carregando && !estado.linhas.length) return '<tr><td colspan="5" class="vazio">Carregando os movimentos do mês…</td></tr>';
  if (!estado.linhas.length) return `<tr><td colspan="5" class="vazio">${svg('linhas')}<strong>${termo ? 'Nenhum movimento encontrado' : 'O mês ainda não tem movimentos'}</strong><span>${termo ? 'Ajuste a busca para encontrar outro lançamento.' : 'Os lançamentos registrados aparecerão aqui.'}</span></td></tr>`;
  return estado.linhas.map(l => `<tr>
    <td><div class="descricao"><span class="tipo-movimento ${l.ehEntrada ? 'entrada' : 'saida'}">${svg(l.ehEntrada ? 'entrada' : 'saida')}</span><div><strong>${h(l.descricao)}</strong><small>${h(l.categoria || 'Sem categoria')}</small></div></div></td>
    <td class="data">${h(l.data)}</td>
    <td><span class="situacao ${/realizado|pago|recebido/i.test(l.situacao) ? 'realizado' : /cancelado/i.test(l.situacao) ? 'cancelado' : 'previsto'}"><i></i>${h(l.situacao)}</span></td>
    <td class="valor ${l.ehEntrada ? 'valor-entrada' : ''}">${l.ehEntrada ? '+' : '−'} ${valorVisivel(l.valor.replace(/^[+−-]\s*/, ""))}</td>
    <td class="acoes-linha">
      ${l.podeRealizar && estado.podeEditar ? `<button class="acao-linha" data-action="realizar" data-id="${h(l.id)}" title="Realizar lançamento" aria-label="Realizar ${h(l.descricao)}">${svg('confirmar')}</button>` : ''}
      ${l.ehEntrada && /realizado|pago|recebido/i.test(l.situacao) ? `<button class="acao-linha" data-action="recibo" data-id="${h(l.id)}" title="Emitir recibo" aria-label="Emitir recibo de ${h(l.descricao)}">${svg('recibo')}</button>` : ''}
      ${l.podeCancelar && estado.podeEditar ? `<button class="acao-linha perigo" data-action="cancelar" data-id="${h(l.id)}" title="Cancelar lançamento" aria-label="Cancelar ${h(l.descricao)}">${svg('fechar')}</button>` : ''}
    </td></tr>`).join('');
}
function render() {
  const foco = document.activeElement as HTMLInputElement | null;
  const focoId = foco?.id;
  const selecao = foco?.selectionStart;
  const mainAnterior = document.querySelector<HTMLElement>('.conteudo');
  const scrollAnterior = mainAnterior?.scrollTop ?? 0;
  const iniciais = estado.usuario.trim().split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase() || 'CL';
  const bloqueado = !ponte && !demo;
  const rotasDisponiveis = estado.rotas.filter(r => r.chave !== 'caixa');
  const rotas = rotasDisponiveis.filter(r => r.rotulo.toLocaleLowerCase('pt-BR').includes(buscaMenu.toLocaleLowerCase('pt-BR')));
  const atalhos = ['contas', 'recebiveis', 'conciliacao', 'fluxo-caixa', 'estoque', 'repasses']
    .flatMap(chave => rotasDisponiveis.filter(r => r.chave === chave));
  app.innerHTML = `
    <header class="topbar">
      <a class="marca" href="#resumo" aria-label="Clínica SemDor — início"><img src="./logo-clinica.png" alt="Clínica SemDor" /></a>
      <span class="contexto-app">Financeiro<span class="separador"></span>Visão geral</span>
      <button class="busca-global" data-local="menu" aria-label="Pesquisar seção do financeiro">${svg('buscar')}<span>Pesquisar no financeiro</span><kbd>Ctrl K</kbd></button>
      <div class="usuario-area"><button class="usuario" data-local="usuario" aria-expanded="${usuarioAberto}" aria-label="Menu do usuário"><span class="avatar">${h(iniciais)}</span><span class="nome-usuario">${h(estado.usuario || 'Clínica SemDor')}</span>${svg('baixo')}</button>
      ${usuarioAberto ? `<div class="menu-usuario">${botao('sistema', 'Abrir sistema completo', 'expandir', '', bloqueado)}<p>Atalhos, notificações e troca de usuário.</p></div>` : ''}</div>
    </header>
    <aside class="trilho-financeiro" aria-label="Navegação financeira">
      <div class="atalhos-financeiros">
      <button class="icone-botao ativo" data-local="inicio" title="Resumo financeiro" aria-label="Resumo financeiro">${svg('carteira')}</button>
      ${atalhos.map(r => `<button class="icone-botao" data-action="navegar" data-value="${h(r.chave)}" title="${h(r.rotulo)} — abrir tela do sistema" aria-label="${h(r.rotulo)}">${svg(r.chave === 'contas' ? 'contas' : r.chave === 'conciliacao' ? 'troca' : nomeIcone(r.rotulo))}</button>`).join('')}
      </div>
      <div class="trilho-rodape"><button class="icone-botao" data-local="menu" title="Expandir menu financeiro" aria-label="Expandir menu financeiro" aria-expanded="${menuAberto}">${svg('menu')}</button></div>
    </aside>
    <main class="conteudo" data-testid="resumo-financeiro" id="resumo" tabindex="-1" aria-busy="${estado.carregando}">
      ${demo ? '<div class="faixa-demo">Demonstração visual · dados fictícios · nenhuma operação é gravada</div>' : ''}
      ${bloqueado ? '<div class="aviso-conexao" role="status">Abra esta tela pelo aplicativo da clínica para carregar seus dados.</div>' : ''}
      <section class="cabecalho-pagina"><div class="titulo-periodo"><h1>Resumo financeiro</h1><div class="periodo"><button class="seta-periodo" data-local="anterior" title="Mês anterior" aria-label="Mês anterior" ${bloqueado ? 'disabled' : ''}>${svg('anterior')}</button><label class="seletor-mes">${svg('calendario')}<span>${h(nomeMes(estado.mes))}</span>${svg('baixo')}<input id="mes" data-testid="mes" type="month" aria-label="Mês do resumo financeiro" value="${h(estado.mes)}" ${bloqueado ? 'disabled' : ''}/></label><button class="seta-periodo" data-local="proximo" title="Próximo mês" aria-label="Próximo mês" ${bloqueado ? 'disabled' : ''}>${svg('proximo')}</button></div></div><div class="acoes-cabecalho">${botao('exportar', 'Exportar', 'baixar', '', bloqueado || !estado.linhas.length)}</div></section>
      ${estado.erro ? `<div class="erro" role="alert">${h(estado.erro)} ${botao('atualizar', 'Tentar novamente', 'atualizar')}</div>` : ''}
      <section class="painel-principal" aria-label="Indicadores do mês">
        <article class="cartao-resultado"><div class="rotulo-resultado"><span class="icone-circulo">${svg('carteira')}</span><span>Resultado líquido do mês</span><button class="acao-linha privacidade" data-local="privacidade" data-testid="alternar-privacidade" aria-pressed="${valoresOcultos}" aria-label="${valoresOcultos ? 'Mostrar valores' : 'Ocultar valores'}" title="${valoresOcultos ? 'Mostrar valores' : 'Ocultar valores'}">${svg(valoresOcultos ? 'oculto' : 'olho')}</button><button class="acao-linha" data-action="atualizar" title="Atualizar dados" aria-label="Atualizar dados" ${bloqueado || estado.carregando ? 'disabled' : ''}>${svg('atualizar', estado.carregando ? 'girando' : '')}</button></div>
          <div data-testid="valor-resultado" class="numero-principal ${estado.saldo.includes('-') ? 'negativo' : ''}">${valorVisivel(estado.saldo)}</div><p class="contexto-resultado">Receita líquida menos saídas realizadas</p>
          <div class="ultimo-movimento"><span class="marcador-movimento">${svg('troca')}</span><div><span>Último movimento</span><strong>${textoPrivado(estado.ultimoMovimento || 'Sem movimentos no período')}</strong><small>${textoPrivado(estado.detalheUltimoMovimento)}</small></div></div>
          <div class="acoes-resultado">${botao('novo', 'Novo lançamento', 'mais', 'primario', bloqueado || !estado.podeEditar)}${botao('pix', 'Cobrar com Pix', 'pix', 'secundario', bloqueado || !estado.podeEditar)}</div>
          <div class="nota-saldo">O resultado do mês não representa o saldo bancário.</div>
        </article>
        <div class="painel-movimentos">
          <article class="cartao-serie"><div class="serie-cabecalho"><div><span class="rotulo-serie"><span class="ponto entrada"></span>Entradas realizadas</span><div class="numero-serie">${valorVisivel(estado.entradas)}</div></div><span class="icone-serie entrada">${svg('entrada')}</span></div>${grafico(estado.graficoEntradas, 'entrada', 'Evolução das entradas realizadas no mês')}<div class="serie-legenda"><span>Movimentos do período</span><span>${h(nomeMes(estado.mes))}</span></div></article>
          <article class="cartao-serie"><div class="serie-cabecalho"><div><span class="rotulo-serie"><span class="ponto saida"></span>Saídas realizadas</span><div class="numero-serie">${valorVisivel(estado.saidas)}</div></div><span class="icone-serie saida">${svg('saida')}</span></div>${grafico(estado.graficoSaidas, 'saida', 'Evolução das saídas realizadas no mês')}<div class="serie-legenda"><span>Movimentos do período</span><span>${h(nomeMes(estado.mes))}</span></div></article>
        </div>
      </section>
      <section class="resumo-complementar" aria-label="Composição financeira">
        <div><span>Resultado bruto projetado</span><strong>${valorVisivel(estado.previsto)}</strong><small>Realizados + previstos, antes das deduções</small></div>
        <div><span>Receita líquida</span><strong>${valorVisivel(estado.liquido)}</strong><small>Receita após deduções</small></div>
        <div><span>Deduções</span><strong>${valorVisivel(estado.deducoes)}</strong><small>Taxas e impostos vinculados</small></div>
        <button class="atalho-historico" data-action="historico" ${bloqueado ? 'disabled' : ''}>${svg('historico')}<span>Conferir histórico</span>${svg('proximo')}</button>
      </section>
      <section class="movimentos"><div class="cabecalho-movimentos"><div><h2>Movimentações</h2><p>${h(estado.resumoFiltro || 'Entradas e saídas deste mês, em um só lugar.')}</p></div><div class="acoes-movimentos">${botao('exportar', 'Exportar', 'baixar', '', bloqueado || !estado.linhas.length)}${botao('novo', 'Novo lançamento', 'mais', 'primario', bloqueado || !estado.podeEditar)}</div></div>
        <div class="filtros"><label class="busca-movimento">${svg('buscar')}<input type="search" id="busca" data-testid="filtro-lancamentos" placeholder="Buscar descrição ou categoria" aria-label="Buscar movimentos" value="${h(termo)}" ${bloqueado ? 'disabled' : ''}/>${termo ? `<button data-local="limpar" aria-label="Limpar busca">${svg('fechar')}</button>` : ''}</label><span class="contagem">${estado.linhas.length} ${estado.linhas.length === 1 ? 'movimento' : 'movimentos'}</span></div>
        ${estado.truncado ? '<div class="aviso-lista">Há mais movimentos no período. Refine a busca para localizar o lançamento.</div>' : ''}
        <div class="tabela-scroll" tabindex="0" aria-label="Tabela de movimentações"><table data-testid="tabela-lancamentos"><thead><tr><th>Descrição</th><th>Data</th><th>Situação</th><th class="valor">Valor</th><th class="acoes-linha">Ações</th></tr></thead><tbody>${linhas()}</tbody></table></div>
      </section>
      <footer class="rodape-pagina"><span>Clínica SemDor</span><span>${estado.carregando ? 'Atualizando dados…' : demo ? 'Ambiente de demonstração' : ponte ? 'Dados do sistema da clínica' : 'Sem conexão com o aplicativo'}</span></footer>
    </main>
    <div class="atalhos-rolagem" aria-label="Rolagem da página"><button data-local="subir" title="Rolar para cima" aria-label="Rolar para cima">${svg('subir')}</button><button data-local="descer" title="Rolar para baixo" aria-label="Rolar para baixo">${svg('descer')}</button></div>
    ${menuAberto ? `<div class="fundo-menu" data-local="fechar-menu"></div><nav class="menu-expandido" aria-label="Todos os recursos financeiros"><div class="menu-cabecalho"><div><small>CLÍNICA SEMDOR</small><h2>Financeiro</h2></div><button class="icone-botao" data-local="fechar-menu" aria-label="Fechar menu">${svg('fechar')}</button></div><label class="busca-menu">${svg('buscar')}<input id="busca-menu" value="${h(buscaMenu)}" placeholder="Encontrar uma seção" aria-label="Pesquisar seção" /></label><div class="rotas"><button class="rota ativo" data-local="inicio">${svg('carteira')}<span>Resumo financeiro</span></button>${rotas.map(r => `<button class="rota" data-action="navegar" data-value="${h(r.chave)}">${svg(nomeIcone(r.rotulo))}<span>${h(r.rotulo)}</span>${svg('proximo')}</button>`).join('')}${!rotas.length ? '<p class="nenhuma-rota">Nenhuma seção disponível para esta busca.</p>' : ''}</div><div class="nota-menu">As demais seções abrem nas telas do sistema da clínica.</div></nav>` : ''}
    ${estado.aviso?.texto ? `<div class="toast ${estado.aviso.tipo === 'erro' ? 'toast-erro' : ''}" role="${estado.aviso.tipo === 'erro' ? 'alert' : 'status'}">${h(estado.aviso.texto)}</div>` : ''}
    ${avisoDemo ? `<div class="toast" role="status">${h(avisoDemo)}</div>` : ''}`;
  const conteudo = document.querySelector<HTMLElement>('.conteudo')!;
  conteudo.scrollTop = scrollAnterior;
  conteudo.addEventListener('scroll', atualizarRolagem, { passive: true });
  requestAnimationFrame(atualizarRolagem);
  if (focoId) {
    const alvo = document.getElementById(focoId) as HTMLInputElement | null;
    alvo?.focus({ preventScroll: true });
    if (alvo?.type === 'search' || alvo?.type === 'text') alvo.setSelectionRange(selecao ?? 0, selecao ?? 0);
  }
}

function atualizarRolagem() {
  const conteudo = document.querySelector<HTMLElement>('.conteudo');
  const atalhos = document.querySelector<HTMLElement>('.atalhos-rolagem');
  if (!conteudo || !atalhos) return;
  atalhos.hidden = conteudo.scrollHeight <= conteudo.clientHeight + 2;
  const acima = atalhos.querySelector<HTMLButtonElement>('[data-local="subir"]')!;
  const abaixo = atalhos.querySelector<HTMLButtonElement>('[data-local="descer"]')!;
  acima.disabled = conteudo.scrollTop <= 1;
  abaixo.disabled = conteudo.scrollTop + conteudo.clientHeight >= conteudo.scrollHeight - 2;
}
window.addEventListener('resize', atualizarRolagem);

app.addEventListener('click', e => {
  const b = (e.target as Element).closest<HTMLButtonElement>('button, [data-local]');
  if (!b || b.disabled) return;
  if (b.dataset.action) { enviar(b.dataset.action, b.dataset.value, b.dataset.id); return; }
  switch (b.dataset.local) {
    case 'menu': menuAberto = !menuAberto; render(); if (menuAberto) document.getElementById('busca-menu')?.focus(); break;
    case 'fechar-menu': menuAberto = false; render(); break;
    case 'privacidade': valoresOcultos = !valoresOcultos; render(); break;
    case 'usuario': usuarioAberto = !usuarioAberto; render(); break;
    case 'inicio': menuAberto = false; render(); document.querySelector('.conteudo')?.scrollTo({ top: 0, behavior: 'smooth' }); break;
    case 'limpar': termo = ''; enviar('filtrar', ''); render(); document.getElementById('busca')?.focus(); break;
    case 'anterior': case 'proximo': {
      const data = new Date(`${estado.mes}-15T12:00:00`); data.setMonth(data.getMonth() + (b.dataset.local === 'anterior' ? -1 : 1));
      enviar('mes', `${data.getFullYear()}-${String(data.getMonth() + 1).padStart(2, '0')}`); break;
    }
    case 'subir': case 'descer': document.querySelector('.conteudo')?.scrollBy({ top: b.dataset.local === 'subir' ? -420 : 420, behavior: 'smooth' }); break;
  }
});
app.addEventListener('input', e => {
  const input = e.target as HTMLInputElement;
  if (input.id === 'busca') { termo = input.value; clearTimeout(filtroTimer); filtroTimer = setTimeout(() => enviar('filtrar', termo), 250); }
  if (input.id === 'busca-menu') { buscaMenu = input.value; render(); }
});
app.addEventListener('change', e => { const input = e.target as HTMLInputElement; if (input.id === 'mes' && /^\d{4}-\d{2}$/.test(input.value)) enviar('mes', input.value); });
document.addEventListener('keydown', e => {
  if (e.key === 'Escape' && (menuAberto || usuarioAberto)) { menuAberto = false; usuarioAberto = false; render(); }
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); menuAberto = true; render(); document.getElementById('busca-menu')?.focus(); }
});

function receber(e: MessageEvent) {
  let dados: unknown = e.data;
  if (typeof dados === 'string') { try { dados = JSON.parse(dados); } catch { return; } }
  if (!dados || typeof dados !== 'object' || (dados as Estado).tipo !== 'estado') return;
  const recebido = dados as Estado;
  if (!Array.isArray(recebido.linhas) || !Array.isArray(recebido.rotas)) return;
  estado = { ...estado, ...recebido };
  if (typeof recebido.filtroTexto === 'string' && document.activeElement?.id !== 'busca') termo = recebido.filtroTexto;
  render();
}
ponte?.addEventListener('message', receber);

// A demonstração nunca é fallback para uma falha no host e nunca grava registros.
const linhasDemo: Linha[] = [
  { id: 'demo-1', data: '08/10/2026', descricao: 'Consultas particulares', categoria: 'Atendimentos', situacao: 'Realizado', valor: moeda(1480), podeRealizar: false, podeCancelar: true, ehEntrada: true },
  { id: 'demo-2', data: '08/10/2026', descricao: 'Materiais e insumos', categoria: 'Estrutura da clínica', situacao: 'Realizado', valor: moeda(7215), podeRealizar: false, podeCancelar: true, ehEntrada: false },
  { id: 'demo-3', data: '07/10/2026', descricao: 'Sessões de fisioterapia', categoria: 'Atendimentos', situacao: 'Realizado', valor: moeda(9600), podeRealizar: false, podeCancelar: true, ehEntrada: true },
  { id: 'demo-4', data: '07/10/2026', descricao: 'Serviços administrativos', categoria: 'Serviços', situacao: 'Previsto', valor: moeda(580), podeRealizar: true, podeCancelar: true, ehEntrada: false },
  { id: 'demo-5', data: '06/10/2026', descricao: 'Consultas de acompanhamento', categoria: 'Atendimentos', situacao: 'Realizado', valor: moeda(7560), podeRealizar: false, podeCancelar: true, ehEntrada: true },
];
function carregarDemo(mes = '2026-10') {
  const atual = mes === '2026-10';
  estado = { ...estado, mes, entradas: moeda(atual ? 18640 : 0), saidas: moeda(atual ? 7215 : 0), saldo: moeda(atual ? 10865 : 0), previsto: moeda(atual ? 10845 : 0), liquido: moeda(atual ? 18080 : 0), deducoes: moeda(atual ? 560 : 0), ultimoMovimento: atual ? 'Consultas particulares' : 'Sem movimentos no período', detalheUltimoMovimento: atual ? '08/10 · Realizado · R$ 1.480,00' : '', graficoEntradas: atual ? 'M0 48 L20 44 L40 45 L60 36 L80 40 L100 26 L120 30 L140 20 L160 24 L180 14 L200 19 L220 7 L240 12 L260 5 L280 9' : '', graficoSaidas: atual ? 'M0 50 L20 47 L40 49 L60 40 L80 44 L100 38 L120 42 L140 31 L160 36 L180 28 L200 34 L220 26 L240 30 L260 20 L280 24' : '', serieDisponivel: atual, situacaoSerie: atual ? '' : 'Sem movimentos neste mês de demonstração.', usuario: 'Ana Gomes', carregando: false, erro: null, podeEditar: true, linhas: atual ? linhasDemo.filter(l => `${l.descricao} ${l.categoria}`.toLowerCase().includes(termo.toLowerCase())) : [], rotas: [{ chave: 'contas', rotulo: 'Contas a pagar e receber' }, { chave: 'conciliacao', rotulo: 'Conciliação' }, { chave: 'fluxo-caixa', rotulo: 'Fluxo de caixa' }, { chave: 'repasses', rotulo: 'Repasses' }, { chave: 'estoque', rotulo: 'Estoque' }, { chave: 'resultado', rotulo: 'Resultado do mês' }, { chave: 'producao', rotulo: 'Produção' }, { chave: 'taxas', rotulo: 'Taxas e impostos' }] };
  render();
}
function acaoDemo(m: Mensagem) {
  if (m.acao === 'mes' && m.valor) { carregarDemo(m.valor); return; }
  if (m.acao === 'filtrar' || m.acao === 'atualizar') { carregarDemo(estado.mes); return; }
  if (m.acao === 'pronto') return;
  avisoDemo = 'Demonstração: esta ação abre o fluxo real dentro do aplicativo da clínica.';
  clearTimeout(avisoTimer); render(); avisoTimer = setTimeout(() => { avisoDemo = ''; render(); }, 4200);
}
if (demo) carregarDemo(); else render();
enviar('pronto');
