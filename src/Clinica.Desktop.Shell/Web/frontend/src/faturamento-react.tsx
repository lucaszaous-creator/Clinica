import {useEffect,useRef} from 'react';
import {animate,useReducedMotion} from 'motion/react';
import { Badge, Grid, Group, Paper, Stack, Text, Title } from '@mantine/core';
import { CamposReact, IndicadoresReact, SecaoReact } from './paginas-react';
import type { Contexto, Pagina, Secao } from './paginas';
import './faturamento-react.css';

const rotasFaturamento = new Set([
  'faturamento-guias', 'faturamento-pendencias', 'faturamento-faturados',
  'faturamento-glosas', 'faturamento-nc', 'faturamento-relatorios', 'faturamento-tiss',
]);
export const ehFaturamento = (p: Pagina) => rotasFaturamento.has(p.chave);
const contextoDaRota: Record<string, { filtros: string; operacao: string }> = {
  'faturamento-guias': { filtros: 'Localizar guias', operacao: 'Consulta de guias' },
  'faturamento-pendencias': { filtros: 'Organize sua fila de pendências', operacao: 'Guias pendentes' },
  'faturamento-faturados': { filtros: 'Localizar baixas', operacao: 'Guias faturadas' },
  'faturamento-glosas': { filtros: 'Localizar glosas e recursos', operacao: 'Glosas e recursos' },
  'faturamento-nc': { filtros: 'Localizar não conformidades', operacao: 'Não conformidades' },
  'faturamento-relatorios': { filtros: 'Período de análise', operacao: 'Indicadores do período' },
  'faturamento-tiss': { filtros: 'Período dos lotes', operacao: 'Lotes TISS' },
};

/** Apenas composição: campos, indicadores, comandos e estados vêm integralmente do host. */
export function ComposicaoFaturamentoReact({ pagina: p, contexto: c }: { pagina: Pagina; contexto: Contexto }) {
  const reduzirMovimento=useReducedMotion();
 const raiz=useRef<HTMLDivElement>(null);
 useEffect(()=>{const el=raiz.current;if(!el||reduzirMovimento)return;const abrir=(e:Event)=>{const detalhes=e.target;if(!(detalhes instanceof HTMLDetailsElement)||!detalhes.open)return;const conteudo=detalhes.querySelector('dl');if(conteudo)animate(conteudo,{y:[3,0]},{duration:.14,ease:'easeOut'})};el.addEventListener('toggle',abrir,true);return()=>el.removeEventListener('toggle',abrir,true)},[reduzirMovimento]);
  const texto = contextoDaRota[p.chave] ?? { filtros: 'Filtros', operacao: p.titulo };
  const usadas = new Set<string>();
  const separar = (chaves: string[]) => p.secoes.filter(s => chaves.includes(s.chave)).map(s => { usadas.add(s.chave); return s; });
  const alertas = separar(['rodada', 'falha-rodada']);
  const principal = separar(['Codigos', 'Resultados', 'Baixados', 'Glosas', 'Itens', 'Lotes', 'PorConvenio']);
  const apoioRelatorio = p.chave === 'faturamento-relatorios' ? separar(['ConsultasEspecialidades', 'Envelhecimento']) : [];
  const prazos = p.chave === 'faturamento-pendencias' ? separar(['Recursos', 'Consultas']) : [];
  const orientacoes = p.chave === 'faturamento-glosas' ? separar(['motivos']) : [];
  const restantes = p.secoes.filter(s => !usadas.has(s.chave));
  const secao = (s: Secao) => <SecaoReact key={s.chave} secao={s} contexto={c}/>;
  const temFiltros = p.campos.some(f => f.visivel !== false);
  const periodo = ['faturamento-relatorios', 'faturamento-tiss'].includes(p.chave);
  return <div ref={raiz} className={`faturamento-operacao faturamento-operacao-${p.chave.replace('faturamento-', '')}`}>
    {p.indicadores.length > 0 && <Paper component="section" className="faturamento-resumo" aria-label={texto.operacao}>
      <Group justify="space-between" gap="md" className="faturamento-resumo-titulo"><Text fw={600} size="sm">{texto.operacao}</Text><Badge variant="light" color="gray">{p.chave === 'faturamento-relatorios' ? 'Relatório' : 'Faturamento'}</Badge></Group>
      <IndicadoresReact indicadores={p.indicadores} contexto={c}/>
    </Paper>}
    {temFiltros && <Paper component="section" className={`faturamento-filtros ${periodo ? 'faturamento-filtros-periodo' : ''}`} aria-label={texto.filtros}>
      <Group justify="space-between" gap="md" mb="sm"><Text fw={600} size="sm">{texto.filtros}</Text>{p.chave === 'faturamento-tiss' && <Text size="xs" c="dimmed">XML, protocolos e retornos da operadora</Text>}</Group>
      <CamposReact campos={p.campos} contexto={c}/>
    </Paper>}
    {alertas.length > 0 && <Stack gap="sm" className="faturamento-alertas">{alertas.map(secao)}</Stack>}
    <Stack gap="md" className="faturamento-trabalho">{principal.map(secao)}</Stack>
    {apoioRelatorio.length > 0 && <Grid gap="lg" className="faturamento-analises">{apoioRelatorio.map(s => <Grid.Col key={s.chave} span={{ base: 12, lg: 6 }}>{secao(s)}</Grid.Col>)}</Grid>}
    {prazos.length > 0 && <section className="faturamento-prazos" aria-labelledby="titulo-prazos-faturamento"><Group justify="space-between" mb="md"><Title order={2} size="h4" id="titulo-prazos-faturamento">Prazos e renovações</Title><Text size="xs" c="dimmed">Recursos e consultas</Text></Group><Stack gap="md">{prazos.map(secao)}</Stack></section>}
    {orientacoes.length > 0 && <Stack gap="md" className="faturamento-orientacoes">{orientacoes.map(secao)}</Stack>}
    {/* Novas seções do contrato sempre continuam acessíveis, mesmo sem composição específica. */}
    {restantes.length > 0 && <Stack gap="md" className="faturamento-complementos">{restantes.map(secao)}</Stack>}
  </div>;
}
