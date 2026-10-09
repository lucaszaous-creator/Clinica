import {ehRecepcaoOperacional,RecepcaoOperacionalReact,ListaOperacionalReact} from './composicao-recepcao-react';
import {ComposicaoFaturamentoReact,ehFaturamento} from './faturamento-react';
import {GestaoOperacionalReact,ehGestaoOperacional} from './gestao-react';
import {getCoreRowModel, getSortedRowModel, useReactTable, type ColumnDef} from '@tanstack/react-table';
import {Button, Input, Textarea, NativeSelect, Paper, Group, Title} from '@mantine/core';
import {motion} from 'motion/react';
import {useLayoutEffect,useRef,useMemo,type ReactNode,type Ref} from 'react';
import {campo as campoEspecial,obterValorCampo,graficos,type Campo,type Contexto,type Acao,type Indicador,type Pagina,type Secao,type Tabela,type LinhaPagina} from './paginas';
import {HtmlReact} from './html-react';
import {ProntuarioCabecalhoReact,ProntuarioCorpoReact,HistoricoSessoesReact,ehHistoricoSessoes,ehProntuario,camposDoProntuario} from './prontuario-react';
import {acoesClinicas} from './acoes-clinicas';
import {tituloDaTabela} from './layouts';
import {AgendamentoReact} from './agendamento-react';
import {ListaReact,ProfissionaisReact,usaListaReact,usaProfissionaisReact} from './listas-react';
import {celulaApresentada} from './status-celula';
import {AgendaReact} from './agenda-react';
import {SugestoesPacientesReact,ehSeletorPaciente} from './sugestoes-react';
export {HtmlReact,HTMLReact} from './html-react';
const texto=(v:unknown,c:Contexto)=>c.privado?String(v??'').replace(/R\$\s*[-+−]?\s*[\d.,]+/g,'R$ ••••'):String(v??'');
const atributos=(c:Contexto)=>({'data-escopo':c.escopo,'data-contexto':c.id,'data-tabela':c.tabela,'data-linha':c.linha});
const chave=(f:Campo,c:Contexto)=>JSON.stringify([c.escopo,c.id,c.tabela,c.linha,f.chave]);
/** O host confirma o rascunho sem substituir os elementos que estão em edição. */
export function CampoReact({campo:f,contexto:c,seletorPaciente=false}:{campo:Campo;contexto:Contexto;seletorPaciente?:boolean}){
 const paciente=seletorPaciente||ehSeletorPaciente(f.chave);
 const valor=obterValorCampo(f,c),id=`campo-${encodeURIComponent(chave(f,c))}`,tipo=f.tipo.toLowerCase();
 const buscaCampo=!f.opcoes.length&&!['leitura','readonly','texto-estatico','textarea','multilinha'].includes(tipo)&&(/busca|search/.test(tipo)||/buscar|pesquisar/i.test(f.rotulo)||f.chave==='Seletor.Termo');
 const ref=useRef<HTMLInputElement|HTMLTextAreaElement|HTMLSelectElement>(null);
 useLayoutEffect(()=>{const el=ref.current;if(!el)return;if(el instanceof HTMLInputElement&&el.type==='checkbox'){el.checked=valor===true||valor==='true';return;}if((el instanceof HTMLSelectElement||document.activeElement!==el)&&el.value!==String(valor??''))el.value=String(valor??'');},[valor]);
 if(f.visivel===false)return null;
 const leitura=['leitura','readonly','texto-estatico'].includes(tipo);
 if(leitura&&!String(valor??'').trim()&&!f.ajuda)return null;
 if(['assinatura','imagem','mapa-corporal','texto-rico','texto-rico-leitura'].includes(tipo))return <HtmlReact html={campoEspecial(f,c)}/>;
 const comum={id,'data-busca-termo':f.buscaPaciente?.termo,'data-busca-carregando':f.buscaPaciente?.carregando,'data-campo':f.chave,...atributos(c),disabled:f.habilitado===false||c.ocupado||!!f.buscaPaciente?.carregando,required:f.obrigatorio,'aria-required':f.obrigatorio||undefined,'aria-describedby':f.ajuda?`${id}-ajuda`:undefined};
 const ajuda=f.ajuda?<small id={`${id}-ajuda`} className="ajuda-campo">{f.ajuda}</small>:null;
 if(tipo==='imagem-leitura')return <div className="campo-web"><label>{f.rotulo}</label>{typeof valor==='string'&&/^data:image\/(jpeg|png);base64,/.test(valor)?<img className="foto-previa" src={valor} alt="Foto do paciente"/>:<span>Sem fotografia</span>}</div>;
 if(['checkbox','booleano','bool'].includes(tipo))return <div className="campo-web campo-check"><label htmlFor={id}><input type="checkbox" {...comum} ref={ref as Ref<HTMLInputElement>} defaultChecked={valor===true||valor==='true'}/><span>{f.rotulo}</span></label>{ajuda}</div>;
 let controle:ReactNode;
 if(tipo==='sugestao')controle=<><input type="text" {...comum} ref={ref as Ref<HTMLInputElement>} defaultValue={String(valor??'')} list={`${id}-opcoes`} autoComplete="off"/><datalist id={`${id}-opcoes`}>{f.opcoes.map(o=><option key={o.valor} value={o.rotulo}/>)}</datalist></>;
 else if(['select','selecao','enum'].includes(tipo)||f.opcoes.length)controle=<><div hidden={paciente}><NativeSelect hidden={paciente} tabIndex={paciente?-1:undefined} aria-hidden={paciente||undefined} {...comum} ref={ref as Ref<HTMLSelectElement>} defaultValue={String(valor??'')}>{!f.opcoes.some(o=>String(o.valor)===String(valor??''))&&<option value="">Selecionar…</option>}{f.opcoes.map(o=><option key={o.valor} value={o.valor}>{o.rotulo}</option>)}</NativeSelect></div>{paciente&&!c.pacientesEmTabela&&<SugestoesPacientesReact estadoBusca={f.buscaPaciente} valor={String(valor??'')} id={id} opcoes={f.opcoes} habilitado={!comum.disabled}/>}</>;
 else if(['textarea','multilinha'].includes(tipo))controle=<Textarea {...comum} ref={ref as Ref<HTMLTextAreaElement>} rows={4} maxLength={f.maximo??5000} defaultValue={String(valor??'')}/>;
 else if(leitura)controle=<output id={id}>{texto(valor,c)}</output>;
 else {const htmlTipo:Record<string,string>={data:'date',date:'date',mes:'month',month:'month',numero:'number',number:'number',busca:'search',search:'search',email:'email',senha:'password'};const t=htmlTipo[tipo]??'text';controle=<Input type={t} {...comum} ref={ref as Ref<HTMLInputElement>} defaultValue={String(valor??'')} step={t==='number'?'any':undefined} maxLength={t==='number'?undefined:f.maximo??5000} inputMode={['decimal','moeda','dinheiro'].includes(tipo)?'decimal':undefined} autoComplete="off" placeholder={buscaCampo?(f.chave==='Seletor.Termo'?'Digite o nome ou CPF':'Digite para buscar'):undefined}/>;}
 return <div hidden={paciente&&c.pacientesEmTabela} className={`campo-web ${leitura?'campo-leitura':''} ${paciente?'campo-paciente-selecao':''} ${buscaCampo?'campo-busca':''} ${['textarea','multilinha'].includes(tipo)||tipo==='leitura'&&String(valor??'').length>140?'campo-largo':''}`}><label htmlFor={id}>{f.rotulo}{f.obrigatorio&&<span className="obrigatorio" aria-hidden="true"> *</span>}</label>{controle}{ajuda}</div>;
}
export const campoApresentavel=(f:Campo,c:Contexto)=>f.visivel!==false&&(!['leitura','readonly','texto-estatico'].includes(f.tipo.toLowerCase())||!!String(obterValorCampo(f,c)??'').trim()||!!f.ajuda);
export function CamposReact({campos,contexto}:{campos:Campo[];contexto:Contexto}){
 const visiveis=campos.filter(f=>campoApresentavel(f,contexto));
 if(!visiveis.length)return null;
 const busca=visiveis.find(f=>f.chave==='Seletor.Termo'),paciente=visiveis.find(f=>ehSeletorPaciente(f.chave,!!busca));
 const temPar=!!busca&&!!paciente;
 return <div className={'campos-web '+(visiveis.some(f=>/buscar|pesquisar/i.test(f.rotulo)||f.chave==='Seletor.Termo')?'campos-filtros':'')}>{visiveis.map(f=>{
  if(temPar&&f===paciente)return null;
  if(temPar&&f===busca)return <section className="busca-paciente-unificada" aria-label="Localizar paciente" key={chave(f,contexto)}><CampoReact campo={{...busca,rotulo:'Buscar paciente por nome ou CPF'}} contexto={contexto}/><CampoReact campo={paciente} contexto={contexto} seletorPaciente/></section>;
  return <CampoReact key={chave(f,contexto)} campo={f} contexto={contexto}/>;
 })}</div>;
}
function BotaoReact({acao:a,contexto:c,classe='',principal=false,menu=false}:{acao:Acao;contexto:Contexto;classe?:string;principal?:boolean;menu?:boolean}){return <Button type="button" variant={principal?'filled':'default'} className={`botao ${a.estilo==='perigo'?'perigo':principal?'primario':'secundario'} ${classe}`} role={menu?'menuitem':undefined} tabIndex={menu?-1:undefined} data-comando={a.chave} {...atributos(c)} disabled={a.habilitada===false||c.ocupado}>{a.rotulo}</Button>;}
export function AcoesReact({acoes,contexto:c,classe='',tituloMenu='Mais ações',somenteMenu=false}:{acoes:Acao[];contexto:Contexto;classe?:string;tituloMenu?:string;somenteMenu?:boolean}){
 const visiveis=acoes.filter(a=>a.visivel!==false);
 if(!somenteMenu&&visiveis.length<=2)return <>{visiveis.map((a,i)=><BotaoReact key={a.chave} acao={a} contexto={c} classe={classe} principal={a.estilo==='primario'&&!visiveis.slice(0,i).some(x=>x.estilo==='primario')}/>)}</>;
 const principal=visiveis.find(a=>a.estilo==='primario'&&a.habilitada!==false)??visiveis.find(a=>a.estilo==='primario');
 const secundaria=(c.escopo==='dialogo'&&!c.linha?visiveis.find(a=>/^(cancelar|fechar|voltar)$/i.test(a.rotulo)):undefined)??visiveis.find(a=>a!==principal&&a.estilo!=='perigo');
 const diretas=(somenteMenu?[]:[principal,secundaria]).filter((a):a is Acao=>!!a),restantes=visiveis.filter(a=>!diretas.includes(a));
 const id=`acoes-${encodeURIComponent(JSON.stringify([c.escopo,c.id,c.tabela,c.linha,tituloMenu,restantes.map(a=>a.chave)]))}`;
 const normais=restantes.filter(a=>a.estilo!=='perigo'),perigosas=restantes.filter(a=>a.estilo==='perigo');
 return <>{diretas.map(a=><BotaoReact key={a.chave} acao={a} contexto={c} classe={classe} principal={a===principal}/>)}{restantes.length>0&&<span className="acoes-menu-grupo"><button type="button" className="botao secundario acoes-menu-abrir" data-abrir-acoes={id} aria-haspopup="menu" aria-expanded="false" aria-controls={id} disabled={c.ocupado}>{tituloMenu}<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg></button><div id={id} className="acoes-menu-painel" popover="auto" role="menu" aria-label={tituloMenu}>{normais.map(a=><BotaoReact key={a.chave} acao={a} contexto={c} classe={classe} menu/>)}{!!normais.length&&!!perigosas.length&&<div className="acoes-menu-divisor" role="separator"/>}{perigosas.map(a=><BotaoReact key={a.chave} acao={a} contexto={c} classe={classe} menu/>)}</div></span>}</>;
}
// A cor identifica a categoria do indicador, sem recalcular ou interpretar seu valor.
const tonsIndicadores:Record<string,string>={
 'Entradas no mês':'positivo','Receita líquida':'positivo','Atendimentos':'positivo','Atendidos':'positivo','Baixadas':'positivo','Baixados':'positivo','Assinados':'positivo',
 'Saídas no mês':'saida','Glosadas':'saida','Cancelados':'saida','Faltas':'saida','Não conformidades':'saida',
 'Contas vencidas':'atencao','Pendências de faturamento':'atencao','Depósitos atrasados':'atencao','Atrasadas':'atencao','Guias em aberto':'atencao','Pendentes':'atencao','A receber':'atencao'
};
export function IndicadoresReact({indicadores,contexto:c}:{indicadores:Indicador[];contexto:Contexto}){if(!indicadores.length)return null;return <div className="indicadores-web">{indicadores.map((i,index)=><Paper component="article" className="indicador-web" data-tom={tonsIndicadores[i.rotulo]} key={`${i.rotulo}:${index}`}><span>{i.rotulo}</span><strong>{texto(i.valor,c)}</strong>{i.detalhe&&<small>{texto(i.detalhe,c)}</small>}</Paper>)}</div>;}
export function TabelaReact({tabela:t,contexto}:{tabela:Tabela;contexto:Contexto}){
 const c=t.chave==='Seletor.Resultados'?{...contexto,ocupado:contexto.ocupado||contexto.buscaPacienteCarregando}:contexto;
 const colunas=useMemo<ColumnDef<LinhaPagina>[]>(()=>t.colunas.map(col=>({id:col.chave,accessorFn:l=>l.celulas[col.chave]??'',sortingFn:'alphanumeric'})),[t.colunas]);
 const modelo=useReactTable({data:t.linhas,columns:colunas,getRowId:l=>l.id,getCoreRowModel:getCoreRowModel(),getSortedRowModel:getSortedRowModel()});
 if(c.pacienteEmSeletor&&!c.pacientesEmTabela&&t.chave==='Seletor.Resultados'&&t.colunas.every(col=>col.chave==='Nome')&&t.linhas.every(l=>!l.acoes.length&&!l.campos.length))return null;
 if(t.colunas.some(col=>col.chave==='GrupoSituacao')&&t.colunas.some(col=>col.chave==='Hora')&&t.colunas.some(col=>col.chave==='Prontuario'))return <ListaOperacionalReact tabela={t} contexto={c} acompanhamento={false}/>;
 if(ehHistoricoSessoes(t))return <HistoricoSessoesReact tabela={t} contexto={c}/>;
 if(usaProfissionaisReact(t))return <ProfissionaisReact tabela={t} contexto={c}/>;
 if(usaListaReact(t))return <ListaReact tabela={t} contexto={c}/>;
 const comCampos=t.linhas.some(l=>l.campos.some(f=>f.visivel!==false)),comAcoes=t.linhas.some(l=>l.acoes.some(a=>a.visivel!==false));
 return <div className="tabela-web" data-tabela-container={t.chave}><HtmlReact html={tituloDaTabela(t,c)}/><div className="tabela-scroll" tabIndex={0} aria-label={t.titulo} data-rolavel><table data-testid={`tabela-${t.chave}`}><thead><tr>{t.colunas.map(col=><th key={col.chave} aria-sort={modelo.getColumn(col.chave)?.getIsSorted()==='asc'?'ascending':modelo.getColumn(col.chave)?.getIsSorted()==='desc'?'descending':'none'} className={['moeda','numero'].includes(col.tipo)?'valor':''} scope="col"><button type="button" className="ordenar-coluna" onClick={modelo.getColumn(col.chave)?.getToggleSortingHandler()} aria-label={`Ordenar por ${col.rotulo}`}>{col.rotulo}<span aria-hidden="true"><svg width="12" height="14" viewBox="0 0 12 14" fill="none" stroke="currentColor" strokeWidth="1.5">{modelo.getColumn(col.chave)?.getIsSorted()!=='desc'&&<path d="m2 5 4-4 4 4"/>}{modelo.getColumn(col.chave)?.getIsSorted()!=='asc'&&<path d="m2 9 4 4 4-4"/>}</svg></span></button></th>)}{comCampos&&<th scope="col">Preenchimento</th>}{comAcoes&&<th scope="col">Ações</th>}</tr></thead><tbody>{t.linhas.length?modelo.getRowModel().rows.map(({original:l})=>{const contexto={...c,tabela:t.chave,linha:l.id};return <tr key={l.id} data-linha-id={l.id} className={l.selecionada?'linha-selecionada':''}>{t.colunas.map(col=><td key={col.chave} data-rotulo={col.rotulo} className={['moeda','numero'].includes(col.tipo)?'valor':''}><HtmlReact html={celulaApresentada(col.tipo,l.celulas[col.chave]??'',c.privado)}/></td>)}{comCampos&&<td className="campos-na-linha"><CamposReact campos={l.campos} contexto={contexto}/></td>}{comAcoes&&<td><div className="acoes-na-linha"><AcoesReact acoes={l.acoes} contexto={contexto}/></div></td>}</tr>}):<tr><td colSpan={Math.max(1,t.colunas.length+Number(comCampos)+Number(comAcoes))} className="vazio"><strong>{t.vazio}</strong><span>Os registros disponíveis aparecerão aqui.</span></td></tr>}</tbody></table></div></div>;
}
export function SecaoReact({secao:s,contexto}:{secao:Secao;contexto:Contexto}){
 const c={...contexto,tituloSecao:s.titulo},temAgenda=s.tabelas.some(t=>t.chave==='horarios'||t.chave==='ClinicoSemanaSessoes');
 return <Paper component="section" withBorder className="secao-web" id={`secao-${s.chave}`} data-secao={s.chave}><Group className="cabecalho-secao" justify="space-between"><div><Title order={2} size="h4">{s.titulo}</Title>{s.descricao&&<p>{texto(s.descricao,c)}</p>}</div><div className="acoes-web"><AcoesReact acoes={s.acoes} contexto={c}/></div></Group><CamposReact campos={s.campos} contexto={c}/><IndicadoresReact indicadores={s.indicadores} contexto={c}/><HtmlReact html={graficos(s.graficos??[],c)}/><AgendaReact secao={s} contexto={c}/>{s.tabelas.map(t=>temAgenda?<details className="agenda-dados" key={t.chave} data-preservar={`${c.escopo}:${c.id}:agenda-dados:${t.chave}`}><summary>{t.titulo} · {t.linhas.length} registros</summary><TabelaReact tabela={t} contexto={c}/></details>:<TabelaReact key={t.chave} tabela={t} contexto={c}/>)}</Paper>;
}
export function PaginaReact({pagina:original,contexto}:{pagina:Pagina;contexto:Contexto}){
 const c={...contexto,pacienteEmSeletor:[...original.campos,...original.secoes.flatMap(s=>s.campos)].some(f=>f.visivel!==false&&f.chave==='Seletor.Selecionado'),buscaPacienteCarregando:original.campos.some(f=>f.buscaPaciente?.carregando),pacientesEmTabela:original.secoes.some(s=>s.tabelas.some(t=>t.chave==='Seletor.Resultados'&&t.linhas.some(l=>l.acoes.some(a=>a.visivel!==false&&['EscolherPaciente','AbrirPaciente'].includes(a.chave)))))};
 const p=original.chave==='fila'?{...original,secoes:original.secoes.map(s=>s.chave==='Linhas'?{...s,titulo:'Atendimentos',tabelas:s.tabelas.map(t=>t.chave==='Linhas'?{...t,titulo:'Atendimentos'}:t)}:s)}:original;
 const assinatura=p.chave==='AssinaturaPaciente',clinico=ehProntuario(p,c),secoes=p.chave==='marcar-horario'?p.secoes.filter(s=>s.chave!=='Cartoes'):p.secoes,acaoClinica=acoesClinicas(p,c);
 return <motion.div initial={{y:4}} animate={{y:0}} transition={{duration:.14,ease:[.2,.7,.2,1]}} className={`pagina-web${clinico?' pagina-clinica':''}`} data-pagina={p.chave} aria-busy={p.carregando}>
 {clinico?<ProntuarioCabecalhoReact pagina={p} contexto={c}/>:<section className="cabecalho-pagina"><div><h1>{p.titulo}</h1>{p.subtitulo&&<p>{p.subtitulo}</p>}</div><div className="acoes-web">{c.escopo!=='dialogo'&&(acaoClinica!==null?<HtmlReact html={acaoClinica}/>:<AcoesReact acoes={p.acoes} contexto={c}/>)}</div></section>}
 {p.naoVerificado&&<div className="erro" role="alert">Não foi possível verificar os dados. Use Atualizar para tentar novamente.</div>}{p.mensagem&&<div className={p.mensagemEhErro?'erro':'mensagem-web'} role={p.mensagemEhErro?'alert':'status'}>{texto(p.mensagem,c)}</div>}{p.carregando&&<div className="carregando-web" role="status">Atualizando dados…</div>}
 {p.truncado&&<div className="aviso-lista">Existem mais registros. Refine os filtros para localizar o que precisa.</div>}
 {clinico?<ProntuarioCorpoReact pagina={p} contexto={c}/>:c.escopo==='pagina'&&ehRecepcaoOperacional(p)?<RecepcaoOperacionalReact pagina={p} contexto={c}/>:c.escopo==='pagina'&&ehFaturamento(p)?<ComposicaoFaturamentoReact pagina={p} contexto={c}/>:c.escopo==='pagina'&&ehGestaoOperacional(p)?<GestaoOperacionalReact pagina={p} contexto={c}/>:<>{assinatura?<><CamposReact campos={p.campos.slice(0,4)} contexto={c}/>{secoes.map(s=><SecaoReact key={s.chave} secao={s} contexto={c}/>)}<CamposReact campos={p.campos.slice(4)} contexto={c}/></>:p.chave==='marcar-horario'?<AgendamentoReact pagina={p} contexto={c}/>:<CamposReact campos={clinico?camposDoProntuario(p):p.campos} contexto={c}/>}
 <IndicadoresReact indicadores={p.indicadores} contexto={c}/>{secoes.length>1&&<nav className="ancoras-secoes" aria-label="Seções desta página">{secoes.map(s=><button key={s.chave} type="button" data-ir-secao={s.chave}>{s.titulo}</button>)}</nav>}{!assinatura&&secoes.map(s=><SecaoReact key={s.chave} secao={s} contexto={c}/>)}</>}
 </motion.div>;
}
