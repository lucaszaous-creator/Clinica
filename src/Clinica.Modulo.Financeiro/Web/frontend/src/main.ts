import './style.css';
import './paginas.css';
import './navegacao.css';
import './ferramentas.css';
import '../../../../Clinica.Desktop.Shell/Web/frontend/src/identidade-visual.css';
import { treinamento, filtroTreinamento, prepararVideo, type Catalogo, type Aula } from '../../../../Clinica.Desktop.Shell/Web/frontend/src/treinamento';
import { GraduationCap, Bell, ClipboardCheck } from 'lucide';
import { agruparRotas } from './navegacao';
import { pagina as renderPagina, acoes as renderAcoes, campos as renderCampos, guardarRascunho, limparRascunhos, type Pagina, type Contexto } from './paginas';
import { createElement, House, Landmark, Wallet, ChartNoAxesCombined, ArrowDownLeft, ArrowUpRight, Plus, Search, ChevronDown, ChevronLeft, ChevronRight, Menu, Download, RefreshCw, X, Receipt, Check, CalendarDays, ArrowLeftRight, Package, Settings2, Rows3, CircleHelp, CreditCard, Expand, ArrowUp, ArrowDown, History, QrCode, Eye, EyeOff, ListChecks, Users, ChartColumn, type IconNode } from 'lucide';

type Linha = { id: string; data: string; descricao: string; categoria: string; situacao: string; valor: string; podeRealizar: boolean; podeCancelar: boolean; ehEntrada: boolean };
type Estado = {
  ferramentas?: { naoLidos: number; avisos: { mensagem: string; tipo: string; hora: string }[]; filaInfusaoDisponivel: boolean; resumoAssinaturasInfusao: string; treinamentoDisponivel: boolean };
  treinamento?: Catalogo; aula?: Aula; videoUrl?: string;
  pagina?: Pagina & { contexto: string }; dialogo?: { id: string; pagina: Pagina; ocupado: boolean; podeFechar: boolean } | null; ocupado?: boolean;
  tipo: 'estado'; mes: string; entradas: string; saidas: string; saldo: string; previsto: string;
  liquido: string; deducoes: string; ultimoMovimento: string; detalheUltimoMovimento: string;
  graficoEntradas: string; graficoSaidas: string; serieDisponivel: boolean; situacaoSerie: string;
  linhas: Linha[]; usuario: string; carregando: boolean; erro: string | null;
  rotas: { chave: string; rotulo: string }[]; podeEditar?: boolean; truncado?: boolean; resumoFiltro?: string; filtroTexto?: string;
  aviso?: { texto: string; tipo: 'info' | 'sucesso' | 'erro' } | null;
};
type Mensagem = { acao: string; valor?: unknown; id?: string; chave?: string; contexto?: string; tabela?: string; linha?: string };
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
Object.assign(icones, { treinamento: GraduationCap, avisos: Bell, infusao: ClipboardCheck });
const h = (valor: unknown) => String(valor ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
const moeda = (valor: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor);
const hoje = new Date();
const mesInicial = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}`;
let estado: Estado = { tipo: 'estado', mes: mesInicial, entradas: '—', saidas: '—', saldo: '—', previsto: '—', liquido: '—', deducoes: '—', ultimoMovimento: 'Nenhum movimento disponível', detalheUltimoMovimento: '', graficoEntradas: '', graficoSaidas: '', serieDisponivel: false, situacaoSerie: 'Aguardando os dados do sistema.', linhas: [], usuario: '', carregando: !!ponte, erro: null, rotas: [], podeEditar: false };
let valoresOcultos = false;
const valorVisivel = (valor: string) => valoresOcultos ? '••••' : h(valor);
const textoPrivado = (texto: string) => h(valoresOcultos ? texto.replace(/R\$\s*[\d.,]+/g, 'R$ ••••') : texto);
let menuAberto = false;
let grupoAberto: string | null = null;
let usuarioAberto = false;
let treinoAberto = false;
let avisosAbertos = false;
function sairTreinamento() { document.querySelector<HTMLVideoElement>('#video-aula')?.pause(); treinoAberto = false; }
let buscaMenu = '';
let termo = '';
let avisoDemo = '';
let filtroTimer: ReturnType<typeof setTimeout>;
let avisoTimer: ReturnType<typeof setTimeout>;
const alteracoesPendentes = new Map<string, Mensagem>();
let campoTimer: ReturnType<typeof setTimeout>;
let seletorRetorno = '[data-local="inicio"]';
function postar(m: Mensagem) { if (ponte) ponte.postMessage(m); else if (demo) acaoDemo(m); }
function descarregarCampos() {
  clearTimeout(campoTimer);
  for (const m of alteracoesPendentes.values()) postar(m);
  alteracoesPendentes.clear();
}
function alterarCampo(input: HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement) {
  if (!input.dataset.campo) return;
  const c: Contexto = { escopo: input.dataset.escopo === 'dialogo' ? 'dialogo' : 'pagina', id: input.dataset.contexto ?? '', tabela: input.dataset.tabela, linha: input.dataset.linha };
  const valor = input instanceof HTMLInputElement && input.type === 'checkbox' ? input.checked : input.value;
  guardarRascunho(c, input.dataset.campo, valor);
  alteracoesPendentes.set(input.id, { acao: c.escopo === 'dialogo' ? 'dlg-campo' : 'pagina-campo', id: c.escopo === 'dialogo' ? c.id : undefined, contexto: c.escopo === 'pagina' ? c.id : undefined, chave: input.dataset.campo, valor, tabela: c.tabela, linha: c.linha });
  clearTimeout(campoTimer);
  campoTimer = setTimeout(descarregarCampos, 250);
}

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
      <button class="acao-linha" data-action="historico" data-id="${h(l.id)}" title="Histórico" aria-label="Histórico de ${h(l.descricao)}">${svg('historico')}</button>
      ${l.ehEntrada && estado.podeEditar ? `<button class="acao-linha" data-action="pix" data-id="${h(l.id)}" title="Cobrar com Pix" aria-label="Cobrar ${h(l.descricao)} com Pix">${svg('pix')}</button>` : ''}
      ${l.podeRealizar && estado.podeEditar ? `<button class="acao-linha" data-action="realizar" data-id="${h(l.id)}" title="Realizar lançamento" aria-label="Realizar ${h(l.descricao)}">${svg('confirmar')}</button>` : ''}
      ${l.ehEntrada && /realizado|pago|recebido/i.test(l.situacao) ? `<button class="acao-linha" data-action="recibo" data-id="${h(l.id)}" title="Emitir recibo" aria-label="Emitir recibo de ${h(l.descricao)}">${svg('recibo')}</button>` : ''}
      ${l.podeCancelar && estado.podeEditar ? `<button class="acao-linha perigo" data-action="cancelar" data-id="${h(l.id)}" title="Cancelar lançamento" aria-label="Cancelar ${h(l.descricao)}">${svg('fechar')}</button>` : ''}
    </td></tr>`).join('');
}
function contextoPagina(): Contexto { return { escopo: 'pagina', id: estado.pagina?.contexto ?? '', ocupado: estado.ocupado || estado.pagina?.carregando, privado: valoresOcultos }; }
function resumoFinanceiro(bloqueado: boolean) { return `      <section class="cabecalho-pagina"><div class="titulo-periodo"><h1>Resumo financeiro</h1><div class="periodo"><button class="seta-periodo" data-local="anterior" title="Mês anterior" aria-label="Mês anterior" ${bloqueado ? 'disabled' : ''}>${svg('anterior')}</button><label class="seletor-mes">${svg('calendario')}<span>${h(nomeMes(estado.mes))}</span>${svg('baixo')}<input id="mes" data-testid="mes" type="month" aria-label="Mês do resumo financeiro" value="${h(estado.mes)}" ${bloqueado ? 'disabled' : ''}/></label><button class="seta-periodo" data-local="proximo" title="Próximo mês" aria-label="Próximo mês" ${bloqueado ? 'disabled' : ''}>${svg('proximo')}</button></div></div><div class="acoes-cabecalho">${botao('exportar', 'Exportar', 'baixar', '', bloqueado || !estado.linhas.length)}</div></section>
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
        <button class="atalho-historico" data-local="movimentos" ${bloqueado ? 'disabled' : ''}>${svg('historico')}<span>Conferir histórico</span>${svg('proximo')}</button>
      </section>
      <section class="movimentos"><div class="cabecalho-movimentos"><div><h2>Movimentações</h2><p>${h(estado.resumoFiltro || 'Entradas e saídas deste mês, em um só lugar.')}</p></div><div class="acoes-movimentos">${botao('exportar', 'Exportar', 'baixar', '', bloqueado || !estado.linhas.length)}${botao('novo', 'Novo lançamento', 'mais', 'primario', bloqueado || !estado.podeEditar)}</div></div>
        <div class="filtros"><label class="busca-movimento">${svg('buscar')}<input type="search" id="busca" data-testid="filtro-lancamentos" placeholder="Buscar descrição ou categoria" aria-label="Buscar movimentos" value="${h(termo)}" ${bloqueado ? 'disabled' : ''}/>${termo ? `<button data-local="limpar" aria-label="Limpar busca">${svg('fechar')}</button>` : ''}</label><span class="contagem">${estado.linhas.length} ${estado.linhas.length === 1 ? 'movimento' : 'movimentos'}</span></div>
        ${estado.pagina ? renderCampos(estado.pagina.campos.filter(c => c.chave === 'FiltroSituacao'), contextoPagina()) : ''}
        ${estado.truncado ? '<div class="aviso-lista">Há mais movimentos no período. Refine a busca para localizar o lançamento.</div>' : ''}
        <div class="tabela-scroll" tabindex="0" aria-label="Tabela de movimentações"><table data-testid="tabela-lancamentos"><thead><tr><th>Descrição</th><th>Data</th><th>Situação</th><th class="valor">Valor</th><th class="acoes-linha">Ações</th></tr></thead><tbody>${linhas()}</tbody></table></div>
      </section>
`; }
function renderDialogo() {
  const d = estado.dialogo;
  if (!d) return '';
  const c: Contexto = { escopo: 'dialogo', id: d.id, ocupado: d.ocupado, privado: valoresOcultos };
  return `<div class="fundo-dialogo"><section class="dialogo-web" role="dialog" aria-modal="true" aria-labelledby="titulo-dialogo" data-testid="dialogo-financeiro" data-dialogo="${h(d.id)}"><header class="dialogo-cabecalho"><h2 id="titulo-dialogo">${h(d.pagina.titulo)}</h2><button class="icone-botao" data-fechar-dialogo="${h(d.id)}" aria-label="Fechar formulário" ${!d.podeFechar || d.ocupado ? 'disabled' : ''}>${svg('fechar')}</button></header><div class="dialogo-corpo" tabindex="-1">${d.pagina.subtitulo ? `<p class="mensagem-web">${h(d.pagina.subtitulo)}</p>` : ''}${estado.erro ? `<div class="erro" role="alert">${h(estado.erro)}</div>` : ''}${renderPagina(d.pagina, c)}</div><footer class="dialogo-rodape"><div class="rolagem-dialogo"><button data-rolar-dialogo="subir" aria-label="Rolar formulário para cima">${svg('subir')}</button><button data-rolar-dialogo="descer" aria-label="Rolar formulário para baixo">${svg('descer')}</button></div><div class="acoes-web">${renderAcoes([...d.pagina.acoes.filter(a => a.chave === 'fechar'), ...d.pagina.acoes.filter(a => a.chave !== 'fechar')], c)}</div></footer></section></div>`;
}
function chaveRolagem(el: HTMLElement): string {
  const dialogo = el.closest<HTMLElement>('[data-dialogo]')?.dataset.dialogo;
  const pagina = el.closest<HTMLElement>('.conteudo');
  const contexto = dialogo ? `dialogo:${dialogo}` : `pagina:${pagina?.dataset.rota ?? ''}:${pagina?.dataset.contexto ?? ''}`;
  const tabela = el.closest<HTMLElement>('[data-tabela-container]')?.dataset.tabelaContainer
    ?? el.querySelector<HTMLElement>('table[data-testid]')?.dataset.testid;
  return `${contexto}:${tabela ? `tabela:${tabela}` : el.classList.contains('dialogo-corpo') ? 'corpo' : 'rotas'}`;
}
function render() {
  const videoAnterior = document.querySelector<HTMLVideoElement>('#video-aula');
  const videoRodando = videoAnterior && !videoAnterior.paused;
  if (videoAnterior) videoAnterior.dataset.movendo = 'true';
  const foco = document.activeElement as HTMLInputElement | null;
  const focoId = foco?.id;
  const selecao = foco?.selectionStart;
  const valorDigitado = foco && 'value' in foco ? foco.value : null;
  const posicoes = new Map([...document.querySelectorAll<HTMLElement>('.tabela-scroll,.dialogo-corpo,.rotas')]
    .map(el => [chaveRolagem(el), [el.scrollTop, el.scrollLeft] as const] as const));
  const mainAnterior = document.querySelector<HTMLElement>('.conteudo');
  const scrollAnterior = mainAnterior?.scrollTop ?? 0;
  const iniciais = estado.usuario.trim().split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase() || 'CL';
  const bloqueado = (!ponte && !demo) || !!estado.ocupado;
  const rotasDisponiveis = estado.rotas.filter(r => r.chave !== 'caixa');
  const rotas = rotasDisponiveis.filter(r => r.rotulo.toLocaleLowerCase('pt-BR').includes(buscaMenu.toLocaleLowerCase('pt-BR')));
  const grupos = agruparRotas(estado.rotas);
  if (!grupos.some(g => g.chave === grupoAberto)) grupoAberto = null;
  app.innerHTML = `
    <header class="topbar">
      <a class="marca" href="#resumo" data-local="inicio" aria-label="Clínica SemDor — início"><img src="./logo-clinica.png" alt="Clínica SemDor" /></a>
      <nav class="navegacao-topo" aria-label="Navegação financeira">
        <button class="nav-inicio ${!estado.pagina || estado.pagina.chave === 'caixa' ? 'ativo' : ''}" data-local="inicio" ${!estado.pagina || estado.pagina.chave === 'caixa' ? 'aria-current="page"' : ''}>Resumo</button>
        ${grupos.map(g => `<div class="grupo-topo" data-grupo="${h(g.chave)}"><button id="nav-${h(g.chave)}" class="nav-gatilho ${g.rotas.some(r => r.chave === estado.pagina?.chave) ? 'ativo' : ''}" data-local="grupo" data-grupo-chave="${h(g.chave)}" aria-expanded="${grupoAberto === g.chave}" aria-controls="submenu-${h(g.chave)}">${h(g.rotulo)}${svg('baixo')}</button><div class="submenu-topo" id="submenu-${h(g.chave)}" aria-labelledby="nav-${h(g.chave)}" ${grupoAberto === g.chave ? '' : 'hidden'}>${g.rotas.map(r => `<button class="rota ${estado.pagina?.chave === r.chave ? 'ativo' : ''}" data-action="navegar" data-value="${h(r.chave)}" ${estado.pagina?.chave === r.chave ? 'aria-current="page"' : ''}>${svg(nomeIcone(r.rotulo))}<span>${h(r.rotulo)}</span></button>`).join('')}</div></div>`).join('')}
      </nav>
      <div class="ferramentas-topo"><button class="ferramenta-topo" data-treinamento aria-label="Treinamento" title="Treinamento">${svg('treinamento')}</button>${estado.ferramentas?.filaInfusaoDisponivel ? `<button class="ferramenta-topo" data-fila-infusao aria-label="${h(estado.ferramentas.resumoAssinaturasInfusao)}">${svg('infusao')}</button>` : ''}<button class="ferramenta-topo" data-avisos aria-label="Avisos desta sessão" aria-expanded="${avisosAbertos}" title="Avisos">${svg('avisos')}${estado.ferramentas?.naoLidos ? `<small>${estado.ferramentas.naoLidos}</small>` : ''}</button><button class="busca-global" data-local="menu" aria-label="Pesquisar seção do financeiro">${svg('buscar')}<span>Pesquisar no financeiro</span><kbd>Ctrl K</kbd></button></div>
      <div class="usuario-area"><button class="usuario" data-local="usuario" aria-expanded="${usuarioAberto}" aria-label="Menu do usuário"><span class="avatar">${h(iniciais)}</span><span class="nome-usuario">${h(estado.usuario || 'Clínica SemDor')}</span>${svg('baixo')}</button>
      ${usuarioAberto ? `<div class="menu-usuario">${botao('trocar-senha', 'Trocar minha senha', 'ajustes', '', bloqueado || !!estado.dialogo)}${botao('trocar-usuario', 'Trocar usuário', 'profissionais', '', bloqueado || !!estado.dialogo)}<p>Financeiro · Clínica SemDor</p></div>` : ''}</div>
    </header>
    <main class="conteudo" data-testid="resumo-financeiro" data-rota="${h(estado.pagina?.chave ?? 'caixa')}" data-contexto="${h(estado.pagina?.contexto ?? '')}" id="resumo" tabindex="-1" aria-busy="${estado.pagina?.carregando ?? estado.carregando}" ${estado.dialogo ? 'inert' : ''}>
      ${demo ? '<div class="faixa-demo">Demonstração visual · dados fictícios · nenhuma operação é gravada</div>' : ''}
      ${bloqueado ? '<div class="aviso-conexao" role="status">Abra esta tela pelo aplicativo da clínica para carregar seus dados.</div>' : ''}
      ${(treinoAberto || estado.pagina?.chave !== 'caixa') && estado.erro && !estado.dialogo ? `<div class="erro" role="alert">${h(estado.erro)}</div>` : ''}
      ${treinoAberto ? treinamento(estado.treinamento, estado.aula, estado.videoUrl) : estado.pagina && estado.pagina.chave !== 'caixa' ? renderPagina(estado.pagina, contextoPagina()) : resumoFinanceiro(bloqueado)}
      <footer class="rodape-pagina"><span>Clínica SemDor</span><span>${estado.carregando ? 'Atualizando dados…' : demo ? 'Ambiente de demonstração' : ponte ? 'Dados do sistema da clínica' : 'Sem conexão com o aplicativo'}</span></footer>
    </main>
    <div class="atalhos-rolagem" aria-label="Rolagem da página"><button data-local="subir" title="Rolar para cima" aria-label="Rolar para cima">${svg('subir')}</button><button data-local="descer" title="Rolar para baixo" aria-label="Rolar para baixo">${svg('descer')}</button></div>
    ${menuAberto ? `<div class="fundo-menu" data-local="fechar-menu"></div><nav class="menu-expandido" aria-label="Todos os recursos financeiros"><div class="menu-cabecalho"><div><small>CLÍNICA SEMDOR</small><h2>Financeiro</h2></div><button class="icone-botao" data-local="fechar-menu" aria-label="Fechar menu">${svg('fechar')}</button></div><label class="busca-menu">${svg('buscar')}<input id="busca-menu" value="${h(buscaMenu)}" placeholder="Encontrar uma seção" aria-label="Pesquisar seção" /></label><div class="rotas"><button class="rota ${!estado.pagina || estado.pagina.chave === 'caixa' ? 'ativo' : ''}" data-local="inicio">${svg('carteira')}<span>Resumo financeiro</span></button>${rotas.map(r => `<button class="rota ${estado.pagina?.chave === r.chave ? 'ativo' : ''}" data-action="navegar" data-value="${h(r.chave)}">${svg(nomeIcone(r.rotulo))}<span>${h(r.rotulo)}</span>${svg('proximo')}</button>`).join('')}${!rotas.length ? '<p class="nenhuma-rota">Nenhuma seção disponível para esta busca.</p>' : ''}</div><div class="nota-menu">Todas as ferramentas financeiras, no mesmo lugar.</div></nav>` : ''}
    ${avisosAbertos ? `<aside class="painel-avisos" role="dialog" aria-label="Avisos desta sessão"><header><h2>Avisos</h2><button class="botao" data-avisos>Fechar</button></header>${estado.ferramentas?.avisos.length ? estado.ferramentas.avisos.map(a => `<article><time>${h(a.hora)}</time><p>${textoPrivado(a.mensagem)}</p></article>`).join('') : '<p>Nenhum aviso nesta sessão.</p>'}</aside>` : ''}
    ${estado.aviso?.texto ? `<div class="toast ${estado.aviso.tipo === 'erro' ? 'toast-erro' : ''}" role="${estado.aviso.tipo === 'erro' ? 'alert' : 'status'}">${textoPrivado(estado.aviso.texto)}</div>` : ''}
    ${avisoDemo ? `<div class="toast" role="status">${h(avisoDemo)}</div>` : ''}${renderDialogo()}`;
  const conteudo = document.querySelector<HTMLElement>('.conteudo')!;
  conteudo.scrollTop = scrollAnterior;
  [...document.querySelectorAll<HTMLElement>('.tabela-scroll,.dialogo-corpo,.rotas')].forEach(el => { const anterior = posicoes.get(chaveRolagem(el)); if (anterior) { el.scrollTop = anterior[0]; el.scrollLeft = anterior[1]; } });
  document.querySelectorAll<HTMLElement>('.topbar,.menu-expandido,.fundo-menu,.atalhos-rolagem,.painel-avisos').forEach(el => el.inert = !!estado.dialogo);
  conteudo.addEventListener('scroll', atualizarRolagem, { passive: true });
  requestAnimationFrame(atualizarRolagem);
  if (focoId) {
    const alvo = document.getElementById(focoId) as HTMLInputElement | null;
    if (alvo && valorDigitado !== null && ['search', 'text', 'textarea'].includes(alvo.type)) alvo.value = valorDigitado;
    alvo?.focus({ preventScroll: true });
    if (alvo?.type === 'search' || alvo?.type === 'text') alvo.setSelectionRange(selecao ?? 0, selecao ?? 0);
  }
  const videoNovo = document.querySelector<HTMLVideoElement>('#video-aula');
  if (videoAnterior && videoNovo && videoNovo.src === videoAnterior.src) { videoNovo.replaceWith(videoAnterior); if (videoRodando) void videoAnterior.play().catch(() => {}); queueMicrotask(() => delete videoAnterior.dataset.movendo); }
  prepararVideo((acao, chave, valor) => postar({ acao, chave, valor, contexto: estado.pagina?.contexto }));
}

function atualizarRolagem() {
  document.querySelectorAll<HTMLElement>('.tabela-web').forEach(t => {
    const area = t.querySelector<HTMLElement>('.tabela-scroll');
    const controle = t.querySelector<HTMLElement>('.controle-tabela');
    if (area && controle) controle.hidden = area.scrollWidth <= area.clientWidth + 2;
  });
  const corpo = document.querySelector<HTMLElement>('.dialogo-corpo');
  const controle = document.querySelector<HTMLElement>('.rolagem-dialogo');
  if (corpo && controle) controle.hidden = corpo.scrollHeight <= corpo.clientHeight + 2;
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

function abrirGrupo(chave: string | null) {
  grupoAberto = estado.dialogo || menuAberto ? null : chave;
  document.querySelectorAll<HTMLElement>('.grupo-topo').forEach(grupo => {
    const aberto = grupo.dataset.grupo === grupoAberto;
    grupo.querySelector('button')?.setAttribute('aria-expanded', String(aberto));
    const submenu = grupo.querySelector<HTMLElement>('.submenu-topo');
    if (submenu) submenu.hidden = !aberto;
  });
}
app.addEventListener('pointerover', e => {
  if (e.pointerType !== 'mouse' || estado.dialogo || menuAberto) return;
  const grupo = (e.target as Element).closest<HTMLElement>('.grupo-topo');
  if (grupo && !(e.relatedTarget instanceof Node && grupo.contains(e.relatedTarget))) abrirGrupo(grupo.dataset.grupo!);
});
app.addEventListener('pointerout', e => {
  if (e.pointerType !== 'mouse') return;
  const grupo = (e.target as Element).closest<HTMLElement>('.grupo-topo');
  if (grupo && !(e.relatedTarget instanceof Node && grupo.contains(e.relatedTarget)) && !grupo.contains(document.activeElement)) abrirGrupo(null);
});
document.addEventListener('focusin', e => {
  const grupo = (e.target as Element).closest<HTMLElement>('.grupo-topo');
  if (grupoAberto && grupo?.dataset.grupo !== grupoAberto) abrirGrupo(null);
});
document.addEventListener('click', e => {
  if (!(e.target as Element).closest('.grupo-topo')) abrirGrupo(null);
});

app.addEventListener('click', e => {
  const b = (e.target as Element).closest<HTMLButtonElement>('button, [data-local]');
  if (!b || b.disabled) return;
  if (b.hasAttribute('data-treinamento')) { descarregarCampos(); treinoAberto = true; avisosAbertos = false; menuAberto = false; abrirGrupo(null); postar({ acao: 'treinamento', contexto: estado.pagina?.contexto }); render(); return; }
  if (b.hasAttribute('data-sair-treinamento')) { sairTreinamento(); render(); return; }
  if (b.hasAttribute('data-avisos')) { avisosAbertos = !avisosAbertos; if (avisosAbertos) postar({ acao: 'avisos-lidos', contexto: estado.pagina?.contexto }); render(); return; }
  if (b.hasAttribute('data-fila-infusao')) { sairTreinamento(); postar({ acao: 'fila-infusao', contexto: estado.pagina?.contexto }); return; }
  if (b.dataset.aula) { document.querySelector<HTMLVideoElement>('#video-aula')?.pause(); postar({ acao: 'abrir-aula', chave: b.dataset.aula, contexto: estado.pagina?.contexto }); return; }
  if (b.dataset.capitulo !== undefined) { const v = document.querySelector<HTMLVideoElement>('#video-aula'); if (v) v.currentTime = Number(b.dataset.capitulo); return; }
  if (b.dataset.aulaReiniciar) { const v = document.querySelector<HTMLVideoElement>('#video-aula'); if (v) v.currentTime = 0; postar({ acao: 'reiniciar-aula', chave: b.dataset.aulaReiniciar, contexto: estado.pagina?.contexto }); return; }
  if (b.dataset.aulaConcluir) { postar({ acao: 'progresso-aula', chave: b.dataset.aulaConcluir, valor: { posicao: document.querySelector<HTMLVideoElement>('#video-aula')?.currentTime ?? 0, concluida: true }, contexto: estado.pagina?.contexto }); return; }
  if (b.dataset.rolarTabela) { const area = b.closest('.tabela-web')?.querySelector('.tabela-scroll'); area?.scrollBy({ left: b.dataset.rolarTabela === 'direita' ? 380 : -380, behavior: 'smooth' }); return; }
  if (b.dataset.rolarDialogo) { document.querySelector('.dialogo-corpo')?.scrollBy({ top: b.dataset.rolarDialogo === 'subir' ? -320 : 320, behavior: 'smooth' }); return; }
  if (b.dataset.irSecao) { document.getElementById('secao-' + b.dataset.irSecao)?.scrollIntoView({ block: 'start', behavior: 'smooth' }); return; }
  if (b.dataset.fecharDialogo) { alteracoesPendentes.clear(); clearTimeout(campoTimer); postar({ acao: 'dlg-fechar', id: b.dataset.fecharDialogo }); return; }
  descarregarCampos();
  if (b.dataset.comando) {
    if (b.dataset.comando === 'salvar' && document.querySelector('.dialogo-web input[type=password]')) { limparRascunhos('dialogo'); document.querySelectorAll<HTMLInputElement>('.dialogo-web input[type=password]').forEach(el => el.value = ''); }
    seletorRetorno = `[data-comando="${CSS.escape(b.dataset.comando)}"]`;
    postar({ acao: b.dataset.escopo === 'dialogo' ? 'dlg-acao' : 'pagina-acao', chave: b.dataset.comando, id: b.dataset.escopo === 'dialogo' ? b.dataset.contexto : undefined, contexto: b.dataset.escopo === 'pagina' ? b.dataset.contexto : undefined, tabela: b.dataset.tabela, linha: b.dataset.linha }); return;
  }
  if (b.dataset.action) {
    seletorRetorno = `[data-action="${CSS.escape(b.dataset.action)}"]`;
    if (b.dataset.action === 'navegar') { sairTreinamento(); avisosAbertos = false; menuAberto = false; usuarioAberto = false; abrirGrupo(null); }
    enviar(b.dataset.action, b.dataset.value, b.dataset.id); return;
  }
  switch (b.dataset.local) {
    case 'grupo': abrirGrupo(grupoAberto === b.dataset.grupoChave ? null : b.dataset.grupoChave!); break;
    case 'menu': grupoAberto = null; menuAberto = !menuAberto; render(); if (menuAberto) document.getElementById('busca-menu')?.focus(); break;
    case 'fechar-menu': menuAberto = false; render(); break;
    case 'privacidade': valoresOcultos = !valoresOcultos; render(); break;
    case 'usuario': usuarioAberto = !usuarioAberto; render(); break;
    case 'inicio': e.preventDefault(); sairTreinamento(); avisosAbertos = false; menuAberto = false; abrirGrupo(null); enviar('navegar', 'caixa'); break;
    case 'movimentos': document.querySelector('.movimentos')?.scrollIntoView({block:'start',behavior:'smooth'}); break;
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
  if (filtroTreinamento(input)) { render(); return; }
  if (input.dataset.campo) alterarCampo(input);
  if (input.id === 'busca') { termo = input.value; clearTimeout(filtroTimer); filtroTimer = setTimeout(() => enviar('filtrar', termo), 250); }
  if (input.id === 'busca-menu') { buscaMenu = input.value; render(); }
});
app.addEventListener('change', e => { const input = e.target as HTMLInputElement; if (filtroTreinamento(input)) { render(); return; } if (input.dataset.campo) { alterarCampo(input); descarregarCampos(); } if (input.id === 'mes' && /^\d{4}-\d{2}$/.test(input.value)) enviar('mes', input.value); });
document.addEventListener('keydown', e => {
  if (estado.dialogo) {
    if (e.key === 'Escape' && estado.dialogo.podeFechar && !estado.dialogo.ocupado) { e.preventDefault(); postar({ acao: 'dlg-fechar', id: estado.dialogo.id }); }
    if (e.key === 'Tab') {
      const elementos = [...document.querySelectorAll<HTMLElement>('.dialogo-web button:not(:disabled),.dialogo-web input:not(:disabled),.dialogo-web select:not(:disabled),.dialogo-web textarea:not(:disabled),.dialogo-web [tabindex="0"]')].filter(el => el.getClientRects().length > 0);
      const indice = elementos.indexOf(document.activeElement as HTMLElement);
      if (elementos.length && (indice < 0 || (!e.shiftKey && indice === elementos.length - 1) || (e.shiftKey && indice === 0))) { e.preventDefault(); elementos[e.shiftKey ? elementos.length - 1 : 0]?.focus(); }
    }
    return;
  }
  if (e.key === 'Escape' && (menuAberto || usuarioAberto || avisosAbertos)) { menuAberto = false; usuarioAberto = false; avisosAbertos = false; render(); }
  const grupo = (e.target as Element).closest<HTMLElement>('.grupo-topo');
  if (e.key === 'Escape' && grupoAberto) { e.preventDefault(); const chave = grupoAberto; abrirGrupo(null); document.getElementById('nav-' + chave)?.focus(); }
  if (grupo && ['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(e.key)) {
    e.preventDefault(); abrirGrupo(grupo.dataset.grupo!);
    const itens = [...grupo.querySelectorAll<HTMLButtonElement>('.submenu-topo button')];
    const atual = itens.indexOf(document.activeElement as HTMLButtonElement);
    const i = e.key === 'Home' ? 0 : e.key === 'End' ? itens.length - 1 : e.key === 'ArrowUp' ? (atual <= 0 ? itens.length - 1 : atual - 1) : (atual + 1) % itens.length;
    itens[i]?.focus();
  }
  if ((e.ctrlKey || e.metaKey) && ['k', 'f'].includes(e.key.toLowerCase())) { e.preventDefault(); grupoAberto = null; menuAberto = true; render(); document.getElementById('busca-menu')?.focus(); }
});

function receber(e: MessageEvent) {
  let dados: unknown = e.data;
  if (typeof dados === 'string') { try { dados = JSON.parse(dados); } catch { return; } }
  if (!dados || typeof dados !== 'object' || (dados as Estado).tipo !== 'estado') return;
  const recebido = dados as Estado;
  if (!Array.isArray(recebido.linhas) || !Array.isArray(recebido.rotas)) return;
  const mudouPagina = recebido.pagina?.contexto !== estado.pagina?.contexto;
  const mudouDialogo = recebido.dialogo?.id !== estado.dialogo?.id;
  if (mudouPagina) limparRascunhos('pagina');
  if (mudouDialogo) limparRascunhos('dialogo');
  estado = { ...estado, ...recebido };
  if (typeof recebido.filtroTexto === 'string' && document.activeElement?.id !== 'busca') termo = recebido.filtroTexto;
  render();
  if (mudouPagina) document.querySelector('.conteudo')?.scrollTo(0, 0);
  if (mudouDialogo) {
    requestAnimationFrame(() => {
      const alvo = estado.dialogo
        ? document.querySelector<HTMLElement>('.dialogo-corpo input:not(:disabled),.dialogo-corpo select:not(:disabled),.dialogo-corpo textarea:not(:disabled),.dialogo-cabecalho button:not(:disabled)')
        : document.querySelector<HTMLElement>(seletorRetorno);
      alvo?.focus({ preventScroll: true });
    });
  }
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
  if (m.acao === 'mes' && typeof m.valor === 'string') { carregarDemo(m.valor); return; }
  if (m.acao === 'filtrar' || m.acao === 'atualizar') { carregarDemo(estado.mes); return; }
  if (m.acao === 'pronto') return;
  avisoDemo = 'Demonstração: esta ação abre o fluxo real dentro do aplicativo da clínica.';
  clearTimeout(avisoTimer); render(); avisoTimer = setTimeout(() => { avisoDemo = ''; render(); }, 4200);
}
if (demo) carregarDemo(); else render();
enviar('pronto');
