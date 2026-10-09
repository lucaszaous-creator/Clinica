import {Group, Paper, Stack, Text, Title} from '@mantine/core';
import {CamposReact,IndicadoresReact,SecaoReact,campoApresentavel} from './paginas-react';
import type {Pagina,Contexto} from './paginas';

const rotas=new Set(['painel-direcao','indicadores','configuracoes','auditoria','guarda-prontuario','custo-transacao','documentos-emitidos','faturamento-resumo','precos-convenio','rentabilidade-convenio','campanhas','acessos','metas','retencao','origens-pacientes','importar-pacientes']);
export const ehGestaoOperacional=(p:Pagina)=>rotas.has(p.chave);

export function GestaoOperacionalReact({pagina:p,contexto:c}:{pagina:Pagina;contexto:Contexto}){
 const configuracoes=p.chave==='configuracoes';
 const historico=['auditoria','guarda-prontuario','documentos-emitidos'].includes(p.chave);
 return <div className={`gestao-estacao ${configuracoes?'gestao-configuracoes':historico?'gestao-registros':'gestao-painel'}`}>
  {!!p.indicadores.length&&<Paper className="gestao-resultados" p="lg" withBorder><Group justify="space-between" mb="md"><Title order={2} size="h4">{historico?'Registros do período':'Visão da operação'}</Title></Group><IndicadoresReact indicadores={p.indicadores} contexto={c}/></Paper>}
  {p.campos.some(f=>campoApresentavel(f,c))&&<Paper className="gestao-consulta" withBorder p="lg"><Text size="sm" fw={600} mb="md">{historico?'Localize os registros':'Período e critérios'}</Text><CamposReact campos={p.campos} contexto={c}/></Paper>}
  {configuracoes&&<nav className="ancoras-secoes" aria-label="Configurações da clínica">{p.secoes.map(s=><button type="button" key={s.chave} data-ir-secao={s.chave}>{s.titulo}</button>)}</nav>}
  <Stack gap="lg" className="gestao-conteudo">{p.secoes.map(s=><div key={s.chave} className={configuracoes?'configuracao-area':''}>{configuracoes&&<div className="configuracao-descricao"><Text fw={600}>{s.titulo}</Text><Text size="sm" c="dimmed">{s.descricao||'Confira os dados e use a ação correspondente para aplicar as alterações.'}</Text></div>}<SecaoReact secao={s} contexto={c}/></div>)}</Stack>
 </div>;
}
