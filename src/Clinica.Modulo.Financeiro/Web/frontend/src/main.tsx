import './style.css';
import './paginas.css';
import './navegacao.css';
import './ferramentas.css';
import '../../../../Clinica.Desktop.Shell/Web/frontend/src/identidade-visual.css';
import { treinamento, filtroTreinamento, prepararVideo, type Catalogo, type Aula } from '../../../../Clinica.Desktop.Shell/Web/frontend/src/treinamento';
import { GraduationCap, Bell, ClipboardCheck } from 'lucide';
import { agruparRotas } from './navegacao';
import { guardarRascunho, limparRascunhos, type Pagina, type Contexto, type Campo } from '../../../../Clinica.Desktop.Shell/Web/frontend/src/paginas';
import { PaginaReact, CamposReact, AcoesReact, HtmlReact } from '../../../../Clinica.Desktop.Shell/Web/frontend/src/paginas-react';
import { createRoot } from 'react-dom/client';
import { flushSync } from 'react-dom';
import { createElement as criarElementoReact } from 'react';
import { Button, ActionIcon, TextInput, Paper, Badge, Group, Stack } from '@mantine/core';
import { motion, useReducedMotion } from 'motion/react';
import { useReactTable, getCoreRowModel, type ColumnDef } from '@tanstack/react-table';
import { ProvedorClinica } from '../../../../Clinica.Desktop.Shell/Web/frontend/src/ui-clinica';
import '../../../../Clinica.Desktop.Shell/Web/frontend/src/movimento-react.css';
import '../../../../Clinica.Desktop.Shell/Web/frontend/src/clinica-componentes.css';
import './financeiro-react.css';
import '../../../../Clinica.Desktop.Shell/Web/frontend/src/cores-semdor.css';
import { House, Landmark, Wallet, ChartNoAxesCombined, ArrowDownLeft, ArrowUpRight, Plus, Search, ChevronDown, ChevronLeft, ChevronRight, Menu, Download, RefreshCw, X, Receipt, Check, CalendarDays, ArrowLeftRight, Package, Settings2, Rows3, CircleHelp, CreditCard, Expand, ArrowUp, ArrowDown, History, QrCode, Eye, EyeOff, ListChecks, Users, ChartColumn, type IconNode } from 'lucide';

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
const raizReact = createRoot(app);
const icones: Record<string, IconNode> = { casa: House, banco: Landmark, carteira: Wallet, grafico: ChartNoAxesCombined, entrada: ArrowDownLeft, saida: ArrowUpRight, mais: Plus, buscar: Search, baixo: ChevronDown, anterior: ChevronLeft, proximo: ChevronRight, menu: Menu, baixar: Download, atualizar: RefreshCw, fechar: X, recibo: Receipt, confirmar: Check, calendario: CalendarDays, troca: ArrowLeftRight, estoque: Package, ajustes: Settings2, linhas: Rows3, ajuda: CircleHelp, cartao: CreditCard, expandir: Expand, subir: ArrowUp, descer: ArrowDown, historico: History, pix: QrCode, olho: Eye, oculto: EyeOff, contas: ListChecks, profissionais: Users, producao: ChartColumn };
function Icone({ nome, classe = '' }: { nome: string; classe?: string }) {
  return <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.65" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false" className={classe}>{(icones[nome] ?? Rows3).map(([tag, atributos], i) => criarElementoReact(tag, { ...atributos, key: i }))}</svg>;
}
Object.assign(icones, { treinamento: GraduationCap, avisos: Bell, infusao: ClipboardCheck });
const moeda = (valor: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor);
const hoje = new Date();
const mesInicial = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}`;
let estado: Estado = { tipo: 'estado', mes: mesInicial, entradas: '—', saidas: '—', saldo: '—', previsto: '—', liquido: '—', deducoes: '—', ultimoMovimento: 'Nenhum movimento disponível', detalheUltimoMovimento: '', graficoEntradas: '', graficoSaidas: '', serieDisponivel: false, situacaoSerie: 'Aguardando os dados do sistema.', linhas: [], usuario: '', carregando: !!ponte, erro: null, rotas: [], podeEditar: false };
let valoresOcultos = false;
const valorVisivel = (valor: string) => valoresOcultos ? '••••' : valor;
const textoPrivado = (texto: string) => valoresOcultos ? texto.replace(/R\$\s*[\d.,]+/g, 'R$ ••••') : texto;
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
function Botao({ acao, texto, icone, classe = '', desabilitado = false }: { acao: string; texto: string; icone?: string; classe?: string; desabilitado?: boolean }) {
  return <Button className={`botao ${classe}`} data-action={acao} disabled={desabilitado} variant={classe.includes('primario') ? 'filled' : 'light'} leftSection={icone ? <Icone nome={icone}/> : undefined}>{texto}</Button>;
}
function GraficoResumo({ caminho, classe, descricao }: { caminho: string; classe: string; descricao: string }) {
  const reduzirMovimento = useReducedMotion();
  // A série e os cálculos continuam pertencendo ao host; ausência de dados não gera curva ilustrativa.
  if (!estado.serieDisponivel || !caminho || !/^[MLCQSTHVZAmlcqsthvza\d.,\s+eE-]+$/.test(caminho))
    return <div className="grafico-vazio">{estado.situacaoSerie || 'Sem movimentos para desenhar a série.'}</div>;
  return <svg className={`grafico ${classe}`} viewBox="0 0 280 58" role="img" aria-label={descricao} preserveAspectRatio="none"><path className="linha-base" d="M0 54H280"/><motion.path key={caminho} className="curva" d={caminho} initial={reduzirMovimento ? false : { opacity: .35 }} animate={{ opacity: 1 }} transition={{ duration: reduzirMovimento ? 0 : .18 }}/></svg>;
}
function AcoesMovimento({ linha: l }: { linha: Linha }) {
  const acoes = [
    { chave: 'historico', texto: 'Histórico', icone: 'historico', mostrar: true },
    { chave: 'pix', texto: 'Cobrar com Pix', icone: 'pix', mostrar: l.ehEntrada && estado.podeEditar },
    { chave: 'realizar', texto: 'Realizar lançamento', icone: 'confirmar', mostrar: l.podeRealizar && estado.podeEditar },
    { chave: 'recibo', texto: 'Emitir recibo', icone: 'recibo', mostrar: l.ehEntrada && /realizado|pago|recebido/i.test(l.situacao) },
    { chave: 'cancelar', texto: 'Cancelar lançamento', icone: 'fechar', mostrar: l.podeCancelar && estado.podeEditar },
  ].filter(a => a.mostrar);
  const principal = acoes.find(a => a.chave === 'realizar') ?? acoes[0];
  const restantes = acoes.filter(a => a !== principal);
  const id = `movimento-acoes-${encodeURIComponent(l.id)}`;
  const botao = (a: typeof acoes[number], menu = false) => <Button key={a.chave} className={`botao ${a.chave === 'cancelar' ? 'perigo' : 'secundario'} acao-movimento`} variant="light" color={a.chave === 'cancelar' ? 'red' : undefined} leftSection={<Icone nome={a.icone}/>} data-action={a.chave} data-id={l.id} role={menu ? 'menuitem' : undefined} tabIndex={menu ? -1 : undefined} aria-label={`${a.texto}: ${l.descricao}`}>{a.texto}</Button>;
  return <div className="acoes-movimento">{botao(principal)}{restantes.length === 1 ? botao(restantes[0]) : restantes.length > 1 && <span className="acoes-menu-grupo"><button type="button" className="botao secundario acoes-menu-abrir" data-abrir-acoes={id} aria-haspopup="menu" aria-controls={id} aria-expanded="false" aria-label={`Mais ações: ${l.descricao}`}>Mais ações<Icone nome="baixo"/></button><div id={id} className="acoes-menu-painel" popover="auto" role="menu" aria-label={`Ações: ${l.descricao}`}>{restantes.map(a => botao(a, true))}</div></span>}</div>;
}
const colunasMovimentos: ColumnDef<Linha>[] = [
  { accessorKey: 'descricao', header: 'Descrição' },
  { accessorKey: 'data', header: 'Data' },
  { accessorKey: 'situacao', header: 'Situação' },
  { accessorKey: 'valor', header: 'Valor' },
  { id: 'acoes', header: 'Ações' },
];
function MovimentosResumo() {
  // O filtro e a ordem pertencem ao host; a tabela reconcilia as linhas pela identidade real.
  const tabela = useReactTable({ data: estado.linhas, columns: colunasMovimentos, getRowId: l => l.id, getCoreRowModel: getCoreRowModel() });
  if (estado.carregando && !estado.linhas.length) return <tr><td colSpan={5} className="vazio">Carregando os movimentos do mês…</td></tr>;
  if (!estado.linhas.length) return <tr><td colSpan={5} className="vazio"><Icone nome="linhas"/><strong>{termo ? 'Nenhum movimento encontrado' : 'O mês ainda não tem movimentos'}</strong><span>{termo ? 'Ajuste a busca para encontrar outro lançamento.' : 'Os lançamentos registrados aparecerão aqui.'}</span></td></tr>;
  return <>{tabela.getRowModel().rows.map(({original: l}) => <tr key={l.id}>
    <td><div className="descricao"><span className={`tipo-movimento ${l.ehEntrada ? 'entrada' : 'saida'}`}><Icone nome={l.ehEntrada ? 'entrada' : 'saida'}/></span><div><strong>{l.descricao}</strong><small>{l.categoria || 'Sem categoria'}</small></div></div></td>
    <td className="data" data-rotulo="Data">{l.data}</td>
    <td data-rotulo="Situação"><Badge variant="light" className={`situacao ${/realizado|pago|recebido/i.test(l.situacao) ? 'realizado' : /cancelado/i.test(l.situacao) ? 'cancelado' : 'previsto'}`}>{l.situacao}</Badge></td>
    <td data-rotulo="Valor" className={`valor ${l.ehEntrada ? 'valor-entrada' : 'valor-saida'}`}>{l.ehEntrada ? '+' : '−'} {valorVisivel(l.valor.replace(/^[+−-]\s*/, ''))}</td>
    <td className="acoes-linha"><AcoesMovimento linha={l}/>
    </td></tr>)}</>;
}
function contextoPagina(): Contexto { return { escopo: 'pagina', id: estado.pagina?.contexto ?? '', ocupado: estado.ocupado || estado.pagina?.carregando, privado: valoresOcultos }; }
function PeriodoFinanceiro({ bloqueado }: { bloqueado: boolean }) {
  return <Group gap={4} wrap="nowrap" className="periodo"><ActionIcon variant="subtle" className="seta-periodo" data-local="anterior" title="Mês anterior" aria-label="Mês anterior" disabled={bloqueado}><Icone nome="anterior"/></ActionIcon><label className="seletor-mes"><Icone nome="calendario"/><span>{nomeMes(estado.mes)}</span><Icone nome="baixo"/><input key={estado.mes} defaultValue={estado.mes} id="mes" data-testid="mes" type="month" aria-label="Mês do resumo financeiro" disabled={bloqueado}/></label><ActionIcon variant="subtle" className="seta-periodo" data-local="proximo" title="Próximo mês" aria-label="Próximo mês" disabled={bloqueado}><Icone nome="proximo"/></ActionIcon></Group>;
}
function SerieFinanceira({ tipo, titulo, valor, caminho }: { tipo: 'entrada' | 'saida'; titulo: string; valor: string; caminho: string }) {
  return <Paper component="article" className={`financeiro-serie financeiro-serie-${tipo}`}>
    <Group justify="space-between" align="flex-start" gap="md" wrap="nowrap"><Stack gap={8} className="financeiro-serie-valor"><span className="rotulo-serie"><span className={`ponto ${tipo}`}/>{titulo}</span><strong className="numero-serie">{valorVisivel(valor)}</strong></Stack><span className={`icone-serie ${tipo}`}><Icone nome={tipo}/></span></Group>
    <GraficoResumo caminho={caminho} classe={tipo} descricao={`Evolução de ${titulo.toLocaleLowerCase('pt-BR')} no mês`}/><div className="serie-legenda"><span>Movimentos do período</span><span>{nomeMes(estado.mes)}</span></div>
  </Paper>;
}
function ResumoFinanceiro({ bloqueado }: { bloqueado: boolean }) {
  const reduzirMovimento = useReducedMotion();
  return <motion.div className="resumo-react" initial={reduzirMovimento ? false : { opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: reduzirMovimento ? 0 : .18 }}>
    <Group component="section" className="financeiro-cabecalho cabecalho-pagina" justify="space-between" align="flex-start" gap="lg">
      <Stack className="titulo-periodo" gap={14}><h1>Resumo financeiro</h1><PeriodoFinanceiro bloqueado={bloqueado}/></Stack>
      <Botao acao="exportar" texto="Exportar" icone="baixar" desabilitado={bloqueado || !estado.linhas.length}/>
    </Group>
    {estado.erro && <div className="erro" role="alert">{estado.erro} <Botao acao="atualizar" texto="Tentar novamente" icone="atualizar"/></div>}
    <div className="painel-principal">
      <Paper component="article" className="cartao-resultado">
        <Group className="rotulo-resultado" gap={9} wrap="nowrap"><span className="icone-circulo"><Icone nome="carteira"/></span><span>Resultado líquido do mês</span><ActionIcon variant="subtle" className="acao-linha privacidade" data-local="privacidade" data-testid="alternar-privacidade" aria-pressed={valoresOcultos} aria-label={valoresOcultos ? 'Mostrar valores' : 'Ocultar valores'} title={valoresOcultos ? 'Mostrar valores' : 'Ocultar valores'}><Icone nome={valoresOcultos ? 'oculto' : 'olho'}/></ActionIcon><ActionIcon variant="subtle" className="acao-linha" data-action="atualizar" title="Atualizar dados" aria-label="Atualizar dados" disabled={bloqueado || estado.carregando}><Icone nome="atualizar" classe={estado.carregando ? 'girando' : ''}/></ActionIcon></Group>
        <strong data-testid="valor-resultado" className={`numero-principal ${estado.saldo.includes('-') ? 'negativo' : ''}`}>{valorVisivel(estado.saldo)}</strong>
        <p className="contexto-resultado">Receita líquida menos saídas realizadas</p>
        <Group className="ultimo-movimento" gap={10} wrap="nowrap"><span className="marcador-movimento"><Icone nome="troca"/></span><Stack gap={4}><span>Último movimento</span><strong>{textoPrivado(estado.ultimoMovimento || 'Sem movimentos no período')}</strong><small>{textoPrivado(estado.detalheUltimoMovimento)}</small></Stack></Group>
        <Group className="acoes-resultado" gap={9}><Botao acao="novo" texto="Novo lançamento" icone="mais" classe="primario" desabilitado={bloqueado || !estado.podeEditar}/><Botao acao="pix" texto="Cobrar com Pix" icone="pix" classe="secundario" desabilitado={bloqueado || !estado.podeEditar}/></Group>
        <span className="nota-saldo">O resultado do mês não representa o saldo bancário.</span>
      </Paper>
      <Stack className="painel-movimentos" gap={17}>
        <SerieFinanceira tipo="entrada" titulo="Entradas realizadas" valor={estado.entradas} caminho={estado.graficoEntradas}/>
        <SerieFinanceira tipo="saida" titulo="Saídas realizadas" valor={estado.saidas} caminho={estado.graficoSaidas}/>
      </Stack>
    </div>
    <section className="resumo-complementar" aria-label="Composição financeira">
      <Stack gap={7}><span>Resultado bruto projetado</span><strong>{valorVisivel(estado.previsto)}</strong><small>Realizados + previstos, antes das deduções</small></Stack>
      <Stack gap={7}><span>Receita líquida</span><strong>{valorVisivel(estado.liquido)}</strong><small>Receita após deduções</small></Stack>
      <Stack gap={7}><span>Deduções</span><strong>{valorVisivel(estado.deducoes)}</strong><small>Taxas e impostos vinculados</small></Stack>
      <Button variant="subtle" className="atalho-historico" data-local="movimentos" disabled={bloqueado} leftSection={<Icone nome="historico"/>} rightSection={<Icone nome="proximo"/>}>Conferir histórico</Button>
    </section>
    <section className="movimentos"><Group className="cabecalho-movimentos" justify="space-between" gap="md"><Stack gap={5}><h2>Movimentações</h2><p>{estado.resumoFiltro || 'Entradas e saídas deste mês, em um só lugar.'}</p></Stack></Group>
      <Group className="filtros" justify="space-between" gap="md"><TextInput className="busca-movimento" type="search" id="busca" data-testid="filtro-lancamentos" placeholder="Buscar descrição ou categoria" aria-label="Buscar movimentos" value={termo} onChange={e => { termo = e.currentTarget.value; render(); }} disabled={bloqueado} leftSection={<Icone nome="buscar"/>} rightSection={termo ? <ActionIcon variant="subtle" data-local="limpar" aria-label="Limpar busca"><Icone nome="fechar"/></ActionIcon> : undefined}/><span className="contagem">{estado.linhas.length} {estado.linhas.length === 1 ? 'movimento' : 'movimentos'}</span></Group>
      {estado.pagina && <CamposReact campos={estado.pagina.campos.filter(c => c.chave === 'FiltroSituacao')} contexto={contextoPagina()}/>}
      {estado.truncado && <div className="aviso-lista">Há mais movimentos no período. Refine a busca para localizar o lançamento.</div>}
      <div className="tabela-scroll" tabIndex={0} aria-label="Tabela de movimentações"><table data-testid="tabela-lancamentos"><thead><tr><th>Descrição</th><th>Data</th><th>Situação</th><th className="valor">Valor</th><th className="acoes-linha">Ações</th></tr></thead><tbody><MovimentosResumo/></tbody></table></div>
    </section>
  </motion.div>;
}
function DialogoFinanceiro() {
  const d = estado.dialogo;
  if (!d) return null;
  const contexto: Contexto = { escopo: 'dialogo', id: d.id, ocupado: d.ocupado, privado: valoresOcultos };
  return <div className="fundo-dialogo"><section className="dialogo-web" role="dialog" aria-modal="true" aria-labelledby="titulo-dialogo" data-testid="dialogo-financeiro" data-dialogo={d.id}><header className="dialogo-cabecalho"><h2 id="titulo-dialogo">{d.pagina.titulo}</h2><button className="icone-botao" data-fechar-dialogo={d.id} aria-label="Fechar formulário" disabled={!d.podeFechar || d.ocupado}><Icone nome="fechar"/></button></header><div className="dialogo-corpo" tabIndex={-1}>{d.pagina.subtitulo && <p className="mensagem-web">{d.pagina.subtitulo}</p>}{estado.erro && <div className="erro" role="alert">{estado.erro}</div>}<PaginaReact key={d.id} pagina={d.pagina} contexto={contexto}/></div><footer className="dialogo-rodape"><div className="rolagem-dialogo"><button data-rolar-dialogo="subir" aria-label="Rolar formulário para cima"><Icone nome="subir"/></button><button data-rolar-dialogo="descer" aria-label="Rolar formulário para baixo"><Icone nome="descer"/></button></div><div className="acoes-web"><AcoesReact acoes={[...d.pagina.acoes.filter(a => a.chave === 'fechar'), ...d.pagina.acoes.filter(a => a.chave !== 'fechar')]} contexto={contexto}/></div></footer></section></div>;
}
function FinanceiroReact() {
  const iniciais = estado.usuario.trim().split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase() || 'CL';
  const bloqueado = (!ponte && !demo) || !!estado.ocupado;
  const rotas = estado.rotas.filter(r => r.chave !== 'caixa' && r.rotulo.toLocaleLowerCase('pt-BR').includes(buscaMenu.toLocaleLowerCase('pt-BR')));
  const grupos = agruparRotas(estado.rotas);
  if (!grupos.some(g => g.chave === grupoAberto)) grupoAberto = null;
  const inicio = !estado.pagina || estado.pagina.chave === 'caixa';
  return <>
    <header className="topbar">
      <a className="marca" href="#resumo" data-local="inicio" aria-label="Clínica SemDor — início"><img src="./logo-clinica.png" alt="Clínica SemDor"/></a>
      <nav className="navegacao-topo" aria-label="Navegação financeira"><button className={`nav-inicio ${inicio ? 'ativo' : ''}`} data-local="inicio" aria-current={inicio ? 'page' : undefined}>Resumo</button>
      {grupos.map(g => <div key={g.chave} className="grupo-topo" data-grupo={g.chave}><button id={`nav-${g.chave}`} className={`nav-gatilho ${g.rotas.some(r => r.chave === estado.pagina?.chave) ? 'ativo' : ''}`} data-local="grupo" data-grupo-chave={g.chave} aria-expanded={grupoAberto === g.chave} aria-controls={`submenu-${g.chave}`}>{g.rotulo}<Icone nome="baixo"/></button><div className="submenu-topo" id={`submenu-${g.chave}`} aria-labelledby={`nav-${g.chave}`} hidden={grupoAberto !== g.chave}>{g.rotas.map(r => <button key={r.chave} className={`rota ${estado.pagina?.chave === r.chave ? 'ativo' : ''}`} data-action="navegar" data-value={r.chave} aria-current={estado.pagina?.chave === r.chave ? 'page' : undefined}><Icone nome={nomeIcone(r.rotulo)}/><span>{r.rotulo}</span></button>)}</div></div>)}
      </nav>
      <div className="ferramentas-topo"><button className="ferramenta-topo" data-treinamento="" aria-label="Treinamento" title="Treinamento"><Icone nome="treinamento"/></button>{estado.ferramentas?.filaInfusaoDisponivel && <button className="ferramenta-topo" data-fila-infusao="" aria-label={estado.ferramentas.resumoAssinaturasInfusao}><Icone nome="infusao"/></button>}<button className="ferramenta-topo" data-avisos="" aria-label="Avisos desta sessão" aria-expanded={avisosAbertos} title="Avisos"><Icone nome="avisos"/>{!!estado.ferramentas?.naoLidos && <small>{estado.ferramentas.naoLidos}</small>}</button><button className="busca-global" data-local="menu" aria-label="Pesquisar seção do financeiro"><Icone nome="buscar"/><span>Pesquisar no financeiro</span><kbd>Ctrl K</kbd></button></div>
      <div className="usuario-area"><button className="usuario" data-local="usuario" aria-expanded={usuarioAberto} aria-label="Menu do usuário"><span className="avatar">{iniciais}</span><span className="nome-usuario">{estado.usuario || 'Clínica SemDor'}</span><Icone nome="baixo"/></button>{usuarioAberto && <div className="menu-usuario"><Botao acao="trocar-senha" texto="Trocar minha senha" icone="ajustes" desabilitado={bloqueado || !!estado.dialogo}/><Botao acao="trocar-usuario" texto="Trocar usuário" icone="profissionais" desabilitado={bloqueado || !!estado.dialogo}/><p>Financeiro · Clínica SemDor</p></div>}</div>
    </header>
    <main className="conteudo" data-testid="resumo-financeiro" data-rota={estado.pagina?.chave ?? 'caixa'} data-contexto={estado.pagina?.contexto ?? ''} id="resumo" tabIndex={-1} aria-busy={estado.pagina?.carregando ?? estado.carregando} inert={!!estado.dialogo} onScroll={atualizarRolagem}>
      {demo && <div className="faixa-demo">Demonstração visual · dados fictícios · nenhuma operação é gravada</div>}
      {bloqueado && <div className="aviso-conexao" role="status">Abra esta tela pelo aplicativo da clínica para carregar seus dados.</div>}
      {(treinoAberto || estado.pagina?.chave !== 'caixa') && estado.erro && !estado.dialogo && <div className="erro" role="alert">{estado.erro}</div>}
      {treinoAberto ? <HtmlReact html={treinamento(estado.treinamento, estado.aula, estado.videoUrl)}/> : estado.pagina && estado.pagina.chave !== 'caixa' ? <PaginaReact key={estado.pagina.contexto} pagina={estado.pagina} contexto={contextoPagina()}/> : <ResumoFinanceiro bloqueado={bloqueado}/>}
      <footer className="rodape-pagina"><span>Clínica SemDor</span><span>{estado.carregando ? 'Atualizando dados…' : demo ? 'Ambiente de demonstração' : ponte ? 'Dados do sistema da clínica' : 'Sem conexão com o aplicativo'}</span></footer>
    </main>
    <div className="atalhos-rolagem" aria-label="Rolagem da página"><button data-local="subir" title="Rolar para cima" aria-label="Rolar para cima"><Icone nome="subir"/></button><button data-local="descer" title="Rolar para baixo" aria-label="Rolar para baixo"><Icone nome="descer"/></button></div>
    {menuAberto && <><div className="fundo-menu" data-local="fechar-menu"/><nav className="menu-expandido" aria-label="Todos os recursos financeiros"><div className="menu-cabecalho"><div><small>CLÍNICA SEMDOR</small><h2>Financeiro</h2></div><button className="icone-botao" data-local="fechar-menu" aria-label="Fechar menu"><Icone nome="fechar"/></button></div><label className="busca-menu"><Icone nome="buscar"/><input id="busca-menu" value={buscaMenu} onChange={e => { buscaMenu = e.currentTarget.value; render(); }} placeholder="Encontrar uma seção" aria-label="Pesquisar seção"/></label><div className="rotas"><button className={`rota ${inicio ? 'ativo' : ''}`} data-local="inicio"><Icone nome="carteira"/><span>Resumo financeiro</span></button>{rotas.map(r => <button key={r.chave} className={`rota ${estado.pagina?.chave === r.chave ? 'ativo' : ''}`} data-action="navegar" data-value={r.chave}><Icone nome={nomeIcone(r.rotulo)}/><span>{r.rotulo}</span><Icone nome="proximo"/></button>)}{!rotas.length && <p className="nenhuma-rota">Nenhuma seção disponível para esta busca.</p>}</div><div className="nota-menu">Todas as ferramentas financeiras, no mesmo lugar.</div></nav></>}
    {avisosAbertos && <aside className="painel-avisos" role="dialog" aria-label="Avisos desta sessão"><header><h2>Avisos</h2><button className="botao" data-avisos="">Fechar</button></header>{estado.ferramentas?.avisos.length ? estado.ferramentas.avisos.map((a,i) => <article key={`${a.hora}-${i}`}><time>{a.hora}</time><p>{textoPrivado(a.mensagem)}</p></article>) : <p>Nenhum aviso nesta sessão.</p>}</aside>}
    {estado.aviso?.texto && <div className={`toast ${estado.aviso.tipo === 'erro' ? 'toast-erro' : ''}`} role={estado.aviso.tipo === 'erro' ? 'alert' : 'status'}>{textoPrivado(estado.aviso.texto)}</div>}
    {avisoDemo && <div className="toast" role="status">{avisoDemo}</div>}<DialogoFinanceiro/>
  </>;
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
  const foco = document.activeElement as HTMLInputElement | null;
  const focoId = foco?.id;
  const selecao = foco?.selectionStart;
  const posicoes = new Map([...document.querySelectorAll<HTMLElement>('.tabela-scroll,.dialogo-corpo,.rotas')]
    .map(el => [chaveRolagem(el), [el.scrollTop, el.scrollLeft] as const] as const));
  const mainAnterior = document.querySelector<HTMLElement>('.conteudo');
  const scrollAnterior = mainAnterior?.scrollTop ?? 0;
  // A ponte continua síncrona; React reconcilia cada componente sem reconstruir a janela.
  flushSync(() => raizReact.render(<ProvedorClinica><FinanceiroReact/></ProvedorClinica>));
  const conteudo = document.querySelector<HTMLElement>('.conteudo')!;
  conteudo.scrollTop = scrollAnterior;
  document.querySelectorAll<HTMLElement>('.tabela-scroll,.dialogo-corpo,.rotas').forEach(el => { const anterior = posicoes.get(chaveRolagem(el)); if (anterior) { el.scrollTop = anterior[0]; el.scrollLeft = anterior[1]; } });
  document.querySelectorAll<HTMLElement>('.topbar,.menu-expandido,.fundo-menu,.atalhos-rolagem,.painel-avisos').forEach(el => el.inert = !!estado.dialogo);
  requestAnimationFrame(atualizarRolagem);
  if (focoId) {
    const alvo = document.getElementById(focoId) as HTMLInputElement | null;
    if (alvo && document.activeElement !== alvo) alvo.focus({ preventScroll: true });
    if (alvo?.type === 'search' || alvo?.type === 'text') alvo.setSelectionRange(selecao ?? 0, selecao ?? 0);
  }
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
  const proximo = estado.dialogo || menuAberto ? null : chave;
  if (grupoAberto === proximo) return;
  grupoAberto = proximo;
  render();
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
    const painel = b.closest<HTMLElement>('.acoes-menu-painel');
    if (painel?.matches(':popover-open')) painel.hidePopover();
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

// O contrato financeiro permite textos de até 16 mil caracteres. A adoção do
// componente comum não pode diminuir silenciosamente esse limite de edição.
function paginaFinanceira(p: Pagina): Pagina {
  const campos = (itens: Campo[]) => itens.map(f => ({ ...f, maximo: f.maximo ?? 16000 }));
  return { ...p, campos: campos(p.campos), secoes: p.secoes.map(s => ({ ...s, campos: campos(s.campos), tabelas: s.tabelas.map(t => ({ ...t, linhas: t.linhas.map(l => ({ ...l, campos: campos(l.campos) })) })) })) };
}
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
  estado = { ...estado, ...recebido, ...(recebido.pagina ? { pagina: { ...paginaFinanceira(recebido.pagina), contexto: recebido.pagina.contexto } } : {}), ...(recebido.dialogo ? { dialogo: { ...recebido.dialogo, pagina: paginaFinanceira(recebido.dialogo.pagina) } } : {}) };
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
