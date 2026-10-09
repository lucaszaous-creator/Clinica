import {useEffect,useRef,useState} from 'react';
import {animate,useReducedMotion} from 'motion/react';
import {Badge,Group,Paper,Stack,Text} from '@mantine/core';
import type {Campo,Contexto,LinhaPagina,Pagina,Secao,Tabela} from './paginas';
import {graficos} from './paginas';
import {AcoesReact,CampoReact,CamposReact,IndicadoresReact,SecaoReact,TabelaReact} from './paginas-react';
import {HtmlReact} from './html-react';
import {celulaApresentada} from './status-celula';
import './composicao-recepcao-react.css';
import {BuscaPacienteAgenda,buscarPacienteAgenda,FiltrosSituacaoAgenda,grupoAgenda,rotuloGrupoAgenda,type FiltroAgenda,SituacaoAgenda} from './agenda-situacao-react';

export function ehRecepcaoOperacional(p:Pagina){return p.chave==='fila'||p.chave==='retorno-pacientes';}
const visivel=(f:Campo)=>f.visivel!==false;
const texto=(v:string,c:Contexto)=>c.privado?v.replace(/R\$\s*[-+−]?\s*[\d.,]+/g,'R$ ••••'):v;
function CelulaOperacional({tabela:t,linha:l,chave,contexto:c}:{tabela:Tabela;linha:LinhaPagina;chave:string;contexto:Contexto}){
 const col=t.colunas.find(col=>col.chave===chave);if(!col)return null;
 return <span data-coluna={chave}><HtmlReact html={celulaApresentada(col.tipo,l.celulas[chave]??'',c.privado)}/></span>;
}
/** Fila de trabalho: o paciente e o próximo passo ficam visíveis; todo o registro
 * segue nos detalhes, com os mesmos comandos e o mesmo contexto autorizado. */
export function ListaOperacionalReact({tabela:t,contexto:c,acompanhamento}:{tabela:Tabela;contexto:Contexto;acompanhamento:boolean}){
 const [filtro,setFiltro]=useState<FiltroAgenda>('Todos');
 const [busca,setBusca]=useState('');
 const pesquisadas=acompanhamento?t.linhas:buscarPacienteAgenda(t.linhas,busca);
 const linhas=acompanhamento||filtro==='Todos'?pesquisadas:pesquisadas.filter(l=>grupoAgenda(l)===filtro);
 const presentes=new Set(t.colunas.map(col=>col.chave));
 const horario=acompanhamento?'ProximoContato':presentes.has('Horario')?'Horario':'Hora',paciente='Paciente',situacao=presentes.has('Status')?'Status':'Situacao';
 const metas=(acompanhamento?['ModalidadeTexto','Convenio','Telefone']:['ContextoDaLista','Contexto','Profissional','Sala']).filter(key=>presentes.has(key));
 const proximo=presentes.has('ProximoPasso')?'ProximoPasso':undefined;
 const exibidas=new Set([horario,paciente,situacao,...metas,...(proximo?[proximo]:[])]),extras=t.colunas.filter(col=>!exibidas.has(col.chave)&&col.chave!=='GrupoSituacao');
 return <div className="tabela-web lista-responsiva recepcao-lista-operacional" data-tabela-container={t.chave}>{!acompanhamento&&<><BuscaPacienteAgenda valor={busca} aoAlterar={setBusca} quantidade={linhas.length}/><FiltrosSituacaoAgenda linhas={pesquisadas} valor={filtro} aoAlterar={setFiltro}/></>}<div className="recepcao-lista-legenda" aria-hidden="true"><span>{acompanhamento?'Contato previsto':'Horário'}</span><span>Paciente e atendimento</span><span>Situação e próximo passo</span><span>Ações</span></div><div className="registros-cartoes" data-testid={`tabela-${t.chave}`}>{linhas.length?linhas.map(l=>{
  const ctx={...c,tabela:t.chave,linha:l.id},acoes=l.acoes.filter(a=>a.visivel!==false),principal=acoes.find(a=>a.estilo==='primario'&&a.habilitada!==false)??acoes.find(a=>a.estilo==='primario')??acoes.find(a=>a.estilo!=='perigo'),demais=acoes.filter(a=>a!==principal),temCampos=l.campos.some(visivel);
  return <article className={`registro-cartao recepcao-linha ${l.selecionada?'linha-selecionada':''}`} key={l.id} data-linha-id={l.id}><div className="recepcao-linha-principal"><div className="recepcao-linha-horario"><small>{acompanhamento?'Próximo contato':'Horário'}</small><CelulaOperacional tabela={t} linha={l} chave={horario} contexto={c}/></div><div className="recepcao-linha-identidade"><h4><CelulaOperacional tabela={t} linha={l} chave={paciente} contexto={c}/></h4><div className="recepcao-linha-meta">{metas.filter(key=>String(l.celulas[key]??'').trim()).map(key=><div key={key} className={key==='Profissional'?'recepcao-profissional-da-linha':''}>{key==='Profissional'&&<small>Profissional: </small>}{key==='Sala'&&<small>Sala: </small>}<CelulaOperacional tabela={t} linha={l} chave={key} contexto={c}/></div>)}</div></div><div className="recepcao-linha-situacao">{acompanhamento?<CelulaOperacional tabela={t} linha={l} chave={situacao} contexto={c}/>:<><SituacaoAgenda linha={l}/>{String(l.celulas[situacao]??'').trim()!==rotuloGrupoAgenda(grupoAgenda(l))&&<div><CelulaOperacional tabela={t} linha={l} chave={situacao} contexto={c}/></div>}</>}{proximo&&<div className="recepcao-proximo-passo"><small>Próximo passo</small><CelulaOperacional tabela={t} linha={l} chave={proximo} contexto={c}/></div>}</div><div className="acoes-na-linha recepcao-linha-acoes">{principal&&<AcoesReact acoes={[principal.chave==='Avancar'&&l.celulas.ProximoPasso==='Chegou'?{...principal,rotulo:'Registrar chegada'}:principal]} contexto={ctx}/>}<AcoesReact acoes={demais} contexto={ctx} somenteMenu tituloMenu="Mais ações"/></div></div>{(extras.length>0||temCampos)&&<details className="registro-detalhes recepcao-linha-detalhes" data-preservar={`${c.escopo}:${c.id}:${t.chave}:${l.id}`}><summary>Ver detalhes{temCampos?' e preenchimento':''}</summary>{extras.length>0&&<dl>{extras.map(col=><div key={col.chave} data-coluna={col.chave}><dt>{col.rotulo}</dt><dd><HtmlReact html={celulaApresentada(col.tipo,l.celulas[col.chave]??'',c.privado)}/></dd></div>)}</dl>}<CamposReact campos={l.campos} contexto={ctx}/></details>}</article>;
 }):<div className="vazio"><strong>{busca.trim()?'Nenhum paciente encontrado nesta agenda com o nome e os filtros informados.':filtro==='Todos'?t.vazio:'Nenhum horário nesta situação para os filtros atuais.'}</strong></div>}</div></div>;
}
function SecaoOperacionalReact({secao:s,contexto:c,acompanhamento}:{secao:Secao;contexto:Contexto;acompanhamento:boolean}){
 const alvo=acompanhamento?'Pacientes':'Linhas',quantidade=s.tabelas.find(t=>t.chave===alvo)?.linhas.length??0;
 return <Paper component="section" withBorder radius="md" className="secao-web recepcao-painel-fila" id={`secao-${s.chave}`} data-secao={s.chave}><Group className="recepcao-painel-cabecalho" justify="space-between" gap="sm"><Group gap="sm"><h2>{s.titulo}</h2><Badge variant="light" color="gray" size="sm">{quantidade} {quantidade===1?'registro':'registros'}</Badge></Group><div className="acoes-web"><AcoesReact acoes={s.acoes} contexto={c}/></div></Group>{s.descricao&&<Text size="sm" c="dimmed" px="md">{texto(s.descricao,c)}</Text>}<CamposReact campos={s.campos} contexto={c}/><IndicadoresReact indicadores={s.indicadores} contexto={c}/><HtmlReact html={graficos(s.graficos??[],c)}/>{s.tabelas.map(t=>t.chave===alvo&&t.colunas.some(col=>col.chave==='Paciente')?<ListaOperacionalReact key={t.chave} tabela={t} contexto={c} acompanhamento={acompanhamento}/>:<TabelaReact key={t.chave} tabela={t} contexto={{...c,tituloSecao:s.titulo}}/>)}</Paper>;
}
function ProfissionaisBarraReact({secao:s,contexto:c}:{secao:Secao;contexto:Contexto}){
 return <section className="recepcao-profissionais-barra" id={`secao-${s.chave}`} data-secao={s.chave}><Group gap="sm" align="center" wrap="wrap" className="recepcao-profissionais-linha"><h2>{s.titulo}</h2><div className="recepcao-profissionais-conteudo">{s.tabelas.map(t=><TabelaReact key={t.chave} tabela={t} contexto={{...c,tituloSecao:s.titulo}}/>)}</div>{s.acoes.some(a=>a.visivel!==false)&&<div className="acoes-web"><AcoesReact acoes={s.acoes} contexto={c}/></div>}</Group>{s.descricao&&<Text size="xs" c="dimmed">{texto(s.descricao,c)}</Text>}<CamposReact campos={s.campos} contexto={c}/><IndicadoresReact indicadores={s.indicadores} contexto={c}/><HtmlReact html={graficos(s.graficos??[],c)}/></section>;
}
export function RecepcaoOperacionalReact({pagina:p,contexto:c}:{pagina:Pagina;contexto:Contexto}){
 const reduzirMovimento=useReducedMotion();
 const raiz=useRef<HTMLDivElement>(null);
 useEffect(()=>{const el=raiz.current;if(!el||reduzirMovimento)return;const abrir=(e:Event)=>{const detalhes=e.target;if(!(detalhes instanceof HTMLDetailsElement)||!detalhes.open)return;const conteudo=detalhes.querySelector('dl');if(conteudo)animate(conteudo,{y:[3,0]},{duration:.14,ease:'easeOut'})};el.addEventListener('toggle',abrir,true);return()=>el.removeEventListener('toggle',abrir,true)},[reduzirMovimento]);
 const acompanhamento=p.chave==='retorno-pacientes';
 const resumoKeys=new Set(acompanhamento?['Resumo','FiltrosAtivos']:['Dia','Atendidos','EmSala','FaltasCancelamentos']);
 const resumo=p.campos.filter(f=>resumoKeys.has(f.chave)&&visivel(f)),restantes=p.campos.filter(f=>!resumoKeys.has(f.chave));
 const filtrosKeys=new Set(['Busca','Situacao','Modalidade','DiasRecall']);
 const filtros=restantes.filter(f=>filtrosKeys.has(f.chave)&&visivel(f)),outros=restantes.filter(f=>!filtrosKeys.has(f.chave));
 const profissionais=p.secoes.filter(s=>s.chave==='Profissionais'),secoes=p.secoes.filter(s=>s.chave!=='Profissionais');
 return <Stack gap="sm" ref={raiz} className={`recepcao-operacional ${acompanhamento?'recepcao-acompanhamento':'recepcao-agenda-dia'}`}>
 {!acompanhamento&&resumo.length>0&&<Paper withBorder radius="md" className="recepcao-resumo-dia"><Group grow align="start" gap="md">{resumo.map(f=><CampoReact key={f.chave} campo={f} contexto={c}/>)}</Group></Paper>}
 {acompanhamento&&filtros.length>0&&<Paper withBorder radius="md" p="md" className="recepcao-filtros-acompanhamento"><CamposReact campos={filtros} contexto={c}/>{resumo.length>0&&<div className="recepcao-resumo-acompanhamento"><CamposReact campos={resumo} contexto={c}/></div>}</Paper>}
 {acompanhamento&&!filtros.length&&resumo.length>0&&<CamposReact campos={resumo} contexto={c}/>}
 {!acompanhamento&&filtros.length>0&&<CamposReact campos={filtros} contexto={c}/>}
 {outros.some(visivel)&&<Paper withBorder radius="md" p="md" className="recepcao-campos-complementares"><CamposReact campos={outros} contexto={c}/></Paper>}
 <IndicadoresReact indicadores={p.indicadores} contexto={c}/>
 {profissionais.map(s=><ProfissionaisBarraReact key={s.chave} secao={s} contexto={c}/>)}
 {secoes.map(s=>s.tabelas.some(t=>t.chave===(acompanhamento?'Pacientes':'Linhas'))?<SecaoOperacionalReact key={s.chave} secao={s} contexto={c} acompanhamento={acompanhamento}/>:<SecaoReact key={s.chave} secao={s} contexto={c}/>)}
 </Stack>;
}
