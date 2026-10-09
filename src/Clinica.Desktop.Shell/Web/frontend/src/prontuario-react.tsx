import {Avatar, Badge, Button, Group, Paper, Text, Title, Stack, Divider} from '@mantine/core';
import {AcoesReact, CamposReact, IndicadoresReact, SecaoReact, TabelaReact, HtmlReact} from './paginas-react';
import {graficos, type Contexto, type Pagina, type Secao, type Tabela} from './paginas';

const destinos:Record<string,string>={VerAtendimentoWeb:'consultorio-atendimento',VerEnfermagemWeb:'consultorio-atendimento-enfermagem',VerFichaWeb:'consultorio-paciente',VerHistoricoWeb:'consultorio-prontuario',VerExamesWeb:'consultorio-exames-anexos',VerDorWeb:'consultorio-evolucao-dor',VerMedidasWeb:'consultorio-medidas',VerAvaliacoesWeb:'consultorio-avaliacoes'};
const documentos=new Set(['EmitirDocumentos','emitir-receita','emitir-atestado','emitir-comparecimento','emitir-exame','Atendimento.PrescreverInfusao','DocumentosPacienteWeb']);
const sessao=new Set(['IniciarSessao','ReabrirSessao','FinalizarSessao']);
const identidade=new Set(['FotoWeb','Paciente','Contexto','Cabecalho.Linha']);
const tabelasForaDaEvolucao=new Set(['Atendimento.AlertasClinicos','Atendimento.Alertas','Atendimento.CamposPersonalizados']);
export const ehProntuario=(p:Pagina,c:Contexto)=>c.escopo==='pagina'&&Object.values(destinos).includes(p.chave);
export const camposDoProntuario=(p:Pagina)=>p.campos.filter(f=>!identidade.has(f.chave));

export function ProntuarioCorpoReact({pagina:p,contexto:c}:{pagina:Pagina;contexto:Contexto}){
 const cabecalho=camposDoProntuario(p);
 return <div className="prontuario-trabalho">
  {!!cabecalho.some(f=>f.visivel!==false&&String(f.valor??'').trim())&&<Paper className="prontuario-contexto-sessao" withBorder p="md"><CamposReact campos={cabecalho} contexto={c}/></Paper>}
  <IndicadoresReact indicadores={p.indicadores} contexto={c}/>
  {p.secoes.length>1&&<nav className="ancoras-secoes" aria-label="Seções desta página">{p.secoes.map(s=><Button key={s.chave} variant="subtle" size="compact-sm" data-ir-secao={s.chave}>{s.titulo}</Button>)}</nav>}
  {p.secoes.map(s=>s.chave.split('.').at(-1)==='Capa'?<DadosPacienteReact key={s.chave} secao={s} contexto={c}/>:<SecaoReact key={s.chave} secao={{...s,tabelas:s.tabelas.filter(t=>!tabelasForaDaEvolucao.has(t.chave))}} contexto={c}/>)}
 </div>;
}

function DadosPacienteReact({secao:s,contexto:c}:{secao:Secao;contexto:Contexto}){
 const grupos=[{titulo:'Identificação e contato',campos:['Nascimento','Sexo','Documento','Telefone','Email','Endereco']},{titulo:'Tratamento e convênio',campos:['Convenio','Carteirinha','ValidadeCarteirinha','EmTratamento','Modalidade']},{titulo:'Observações e alertas',campos:['ObservacoesCadastro','ResumoProblemas']}];
 const usados=new Set(grupos.flatMap(g=>g.campos));
 return <Paper component="section" withBorder p="lg" className="secao-web ficha-dados" id={`secao-${s.chave}`} data-secao={s.chave}>
  <Group justify="space-between" className="cabecalho-secao"><Title order={2} size="h4">{s.titulo}</Title><div className="acoes-web"><AcoesReact acoes={s.acoes} contexto={c}/></div></Group>
  {s.descricao&&<Text size="sm" c="dimmed">{s.descricao}</Text>}
  <div className="ficha-blocos">{grupos.map(g=>{const campos=s.campos.filter(f=>g.campos.includes(f.chave.split('.').at(-1)??''));return campos.some(f=>f.visivel!==false)?<section key={g.titulo} className="ficha-bloco"><Text fw={600} size="sm" mb="md">{g.titulo}</Text><CamposReact campos={campos} contexto={c}/></section>:null})}</div>
  <Divider my="lg"/><CamposReact campos={s.campos.filter(f=>!usados.has(f.chave.split('.').at(-1)??''))} contexto={c}/><IndicadoresReact indicadores={s.indicadores} contexto={c}/><HtmlReact html={graficos(s.graficos??[],c)}/>{s.tabelas.map(t=><TabelaReact key={t.chave} tabela={t} contexto={c}/>)}
 </Paper>;
}

export const ehHistoricoSessoes=(t:Tabela)=>t.chave.endsWith('Sessoes')&&t.colunas.some(col=>col.chave==='Queixa')&&t.colunas.some(col=>col.chave==='Eva');
export function HistoricoSessoesReact({tabela:t,contexto:c}:{tabela:Tabela;contexto:Contexto}){
 return <div className="tabela-web historico-sessoes" data-tabela-container={t.chave}><Group justify="space-between" mb="md"><Text fw={600}>{t.titulo}</Text><Text size="sm" c="dimmed">{t.linhas.length} registros</Text></Group><Stack gap="md" data-testid={`tabela-${t.chave}`}>{t.linhas.map(l=>{const ctx={...c,tabela:t.chave,linha:l.id};return <Paper component="article" key={l.id} withBorder p="lg" data-linha-id={l.id} className="historico-sessao"><Group justify="space-between" align="flex-start"><div><Text fw={600}>{l.celulas.Data||'Data não informada'}</Text><Text size="sm" c="dimmed">{l.celulas.Profissional||'Profissional não informado'}</Text></div><Badge variant="light" color="gray">{l.celulas.Eva||'Sem avaliação de dor'}</Badge></Group><dl className="historico-descricao">{t.colunas.filter(col=>!['Data','Profissional','Eva'].includes(col.chave)).map(col=><div key={col.chave}><dt>{col.rotulo}</dt><dd>{l.celulas[col.chave]||'—'}</dd></div>)}</dl><CamposReact campos={l.campos} contexto={ctx}/><Group justify="flex-end" mt="md"><AcoesReact acoes={l.acoes} contexto={ctx}/></Group></Paper>})}{!t.linhas.length&&<Text c="dimmed">{t.vazio}</Text>}</Stack></div>;
}

/** O prontuário tem identidade e navegação próprias. Nenhuma ação é reimplementada aqui. */
export function ProntuarioCabecalhoReact({pagina:p,contexto:c}:{pagina:Pagina;contexto:Contexto}){
 const campo=(chave:string)=>p.campos.find(f=>f.chave===chave&&f.visivel!==false);
 const nome=String(campo('Paciente')?.valor??p.subtitulo??'Paciente');
 const foto=campo('FotoWeb')?.valor;
 const fotoValida=typeof foto==='string'&&/^data:image\/(jpeg|png);base64,/.test(foto)?foto:undefined;
 const itens=p.acoes.filter(a=>a.visivel!==false),abas=itens.filter(a=>a.chave in destinos),voltar=itens.filter(a=>a.chave==='Voltar'),fluxo=itens.filter(a=>sessao.has(a.chave)),docs=itens.filter(a=>documentos.has(a.chave)),outros=itens.filter(a=>!(a.chave in destinos)&&a.chave!=='Voltar'&&!sessao.has(a.chave)&&!documentos.has(a.chave));
 return <section className="clinico-cabecalho prontuario-estacao" aria-label="Paciente e navegação clínica">
  <Group justify="space-between" className="clinico-titulo" mb="md"><Title order={1} size="h3">{p.titulo}</Title><div className="clinico-voltar"><AcoesReact acoes={voltar} contexto={c}/></div></Group>
  <Paper className="clinico-identidade" p="lg" withBorder>
   <Avatar src={fotoValida} color="gray" size={44} radius="xl">{nome.split(/\s+/).filter(Boolean).slice(0,2).map(x=>x[0]).join('')}</Avatar>
   <div className="clinico-identidade-texto"><div className="clinico-nome"><output>{nome}</output></div><div className="clinico-identificacao"><CamposReact campos={p.campos.filter(f=>f.chave==='Cabecalho.Linha')} contexto={c}/></div><div className="clinico-contexto"><CamposReact campos={p.campos.filter(f=>f.chave==='Contexto')} contexto={c}/></div></div>
   <div className="prontuario-operacoes"><div className="clinico-fluxo" role="group" aria-label="Ações do atendimento"><AcoesReact acoes={fluxo} contexto={c} todasVisiveis/></div></div>
  </Paper>
  <div className="clinico-acoes"><nav className="clinico-abas" aria-label="Seções do paciente">{abas.map(a=><Button key={a.chave} variant="subtle" className={`botao clinico-aba${destinos[a.chave]===p.chave?' clinico-aba-ativa':''}`} aria-current={destinos[a.chave]===p.chave?'page':undefined} data-comando={a.chave} data-escopo={c.escopo} data-contexto={c.id} disabled={c.ocupado||!a.habilitada}>{a.rotulo}</Button>)}</nav>
   {!!docs.length&&<div className="clinico-faixa-acoes" role="group" aria-label="Documentos"><span>Documentos</span><div><AcoesReact acoes={docs} contexto={c} todasVisiveis/></div></div>}
   {!!outros.length&&<div className="clinico-faixa-acoes" role="group" aria-label="Ações da ficha"><span>Ficha</span><div><AcoesReact acoes={outros} contexto={c} todasVisiveis/></div></div>}
  </div>
 </section>;
}
