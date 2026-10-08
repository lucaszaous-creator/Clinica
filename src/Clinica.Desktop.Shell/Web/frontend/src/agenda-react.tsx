import {useEffect,useMemo,useRef,useState} from 'react';
import {animate,useReducedMotion} from 'motion/react';
import {HtmlReact} from './html-react';
import {celulaApresentada} from './status-celula';
import FullCalendar from '@fullcalendar/react';
import timeGridPlugin from '@fullcalendar/timegrid';
import listPlugin from '@fullcalendar/list';
import dayGridPlugin from '@fullcalendar/daygrid';
import ptBr from '@fullcalendar/core/locales/pt-br';
import type {EventContentArg,EventInput} from '@fullcalendar/core';
import type {Secao,Contexto,LinhaPagina,Tabela} from './paginas';
import {AcoesReact,CamposReact} from './paginas-react';
import './agenda-react.css';
import {BuscaPacienteAgenda,buscarPacienteAgenda,FiltrosSituacaoAgenda,grupoAgenda,type FiltroAgenda,SituacaoAgenda} from './agenda-situacao-react';

const inicio=(l:LinhaPagina)=>l.celulas.InicioISO??l.celulas.DataHora??(l.celulas.DiaISO?`${l.celulas.DiaISO}T${l.celulas.Hora??'00:00'}`:'');
const fim=(l:LinhaPagina)=>l.celulas.FimISO??l.celulas.Fim??'';
const dia=(v:string)=>/^\d{4}-\d{2}-\d{2}/.test(v)?v.slice(0,10):'';
const paciente=(l:LinhaPagina)=>l.celulas.PacienteNome??l.celulas.Paciente??'Paciente';
const dataValida=(v:string)=>!!dia(v)&&Number.isFinite(new Date(v).getTime());
const resumo=(l:LinhaPagina)=>[l.celulas.Modalidade,l.celulas.Profissional,l.celulas.Sala].filter(Boolean).join(' · ');
const dataTexto=(v:string)=>new Date(`${v}T12:00:00`).toLocaleDateString('pt-BR',{day:'2-digit',month:'short'});
const privacidade=(v:string,c:Contexto)=>c.privado?v.replace(/R\$\s*[-+−]?\s*[\d.,]+/g,'R$ ••••'):v;
const colunasResumo=new Set(['GrupoSituacao','InicioISO','FimISO','DataHora','Fim','PacienteNome','Paciente','Modalidade','Profissional','Sala']);
type Modo='lista'|'semana'|'dia';

function concorrencia(linhas:LinhaPagina[]){
 const pontos=linhas.filter(l=>dataValida(inicio(l))).flatMap(l=>{const de=new Date(inicio(l)).getTime(),ate=dataValida(fim(l))?new Date(fim(l)).getTime():de+1;return [{tempo:de,tipo:1},{tempo:Math.max(de+1,ate),tipo:-1}]});
 pontos.sort((a,b)=>a.tempo-b.tempo||a.tipo-b.tipo);let quantidade=0,maior=0;for(const p of pontos){quantidade+=p.tipo;maior=Math.max(maior,quantidade)}return maior;
}
function SessaoAgenda({linha:l,tabela:t,contexto:c,aberta,aoExpandir,compacta=false}:{linha:LinhaPagina;tabela:Tabela;contexto:Contexto;aberta:boolean;aoExpandir:(aberta:boolean)=>void;compacta?:boolean}){
 const reduzirMovimento=useReducedMotion();
 const extras=t.colunas.filter(col=>!colunasResumo.has(col.chave)),ctx={...c,tabela:t.chave,linha:l.id};
 if(compacta)return <div className="agenda-evento-resumo"><strong>{paciente(l)}</strong><SituacaoAgenda linha={l}/><span>{privacidade(resumo(l),c)}</span></div>;
 return <article className="agenda-sessao agenda-fc-sessao" data-linha-id={l.id}><div className="agenda-fc-sessao-conteudo"><strong>{paciente(l)}</strong><div><SituacaoAgenda linha={l}/></div><p>{privacidade(resumo(l),c)}</p>{(extras.length>0||l.campos.some(f=>f.visivel!==false))&&<details open={aberta} data-preservar={`${c.escopo}:${c.id}:agenda:${l.id}`} onToggle={e=>{aoExpandir(e.currentTarget.open);const conteudo=e.currentTarget.querySelector('dl');if(e.currentTarget.open&&conteudo&&!reduzirMovimento)animate(conteudo,{y:[3,0]},{duration:.14,ease:'easeOut'})}}><summary>Detalhes da sessão</summary><dl>{extras.map(col=><div key={col.chave}><dt>{col.rotulo}</dt><dd><HtmlReact html={celulaApresentada(col.tipo,l.celulas[col.chave]??'',c.privado)}/></dd></div>)}</dl><CamposReact campos={l.campos} contexto={ctx}/></details>}</div><div className="acoes-na-linha"><AcoesReact acoes={l.acoes} contexto={ctx}/></div></article>;
}
/** FullCalendar controla apenas a visualização. Datas consultadas, autorizações e
 * todos os comandos continuam sendo os contratos originais do host C#. */
function CalendarioAgenda({horarios,faixas,contexto:c}:{horarios:Tabela;faixas:Tabela;contexto:Contexto}){
 const reduzirMovimento=useReducedMotion();
 const raiz=useRef<HTMLDivElement>(null),calendario=useRef<FullCalendar>(null);
 const [filtro,setFiltro]=useState<FiltroAgenda>('Todos'),[busca,setBusca]=useState('');
 const [estreito,setEstreito]=useState(false),[modo,setModo]=useState<Modo>('lista'),[diaEscolhido,setDiaEscolhido]=useState(''),[selecionado,setSelecionado]=useState<string|null>(null),[abertas,setAbertas]=useState<Set<string>>(()=>new Set());
 // Respostas de outro campo não recriam os eventos de uma agenda que não mudou.
 const assinatura=JSON.stringify(horarios.linhas),assinaturaFaixas=JSON.stringify(faixas.linhas);
 const linhas=useMemo(()=>JSON.parse(assinatura) as LinhaPagina[],[assinatura]);
 const dias=useMemo(()=>[...new Set([...linhas.map(l=>dia(inicio(l))),...(JSON.parse(assinaturaFaixas) as LinhaPagina[]).map(l=>dia(l.celulas.DiaISO??l.celulas.Quando??inicio(l)))])].filter(Boolean).sort(),[linhas,assinaturaFaixas]);
 const densidade=useMemo(()=>linhas.length>30||concorrencia(linhas)>3,[linhas]);
 const pesquisadas=useMemo(()=>buscarPacienteAgenda(linhas,busca),[linhas,busca]);
 const filtradas=useMemo(()=>filtro==='Todos'?pesquisadas:pesquisadas.filter(l=>grupoAgenda(l)===filtro),[pesquisadas,filtro]);
 const eventos=useMemo<EventInput[]>(()=>filtradas.filter(l=>dataValida(inicio(l))).map(l=>({id:l.id,title:paciente(l),start:inicio(l),end:dataValida(fim(l))&&new Date(fim(l)).getTime()>new Date(inicio(l)).getTime()?fim(l):undefined,allDay:false,extendedProps:{linha:l}})),[filtradas]);
 const semData=filtradas.filter(l=>!dataValida(inicio(l)));
 const porId=useMemo(()=>new Map(filtradas.map(l=>[l.id,l])),[filtradas]);
 const primeiro= dias[0]??'',ultimo=dias.at(-1)??'',diaAtual=dias.includes(diaEscolhido)?diaEscolhido:primeiro;
 const vista=modo==='dia'?(densidade||estreito?'listDay':'timeGridDay'):modo==='semana'?(densidade||estreito?'dayGridWeek':'timeGridWeek'):'listWeek';
 const expandir=(id:string,aberta:boolean)=>setAbertas(atuais=>{if(atuais.has(id)===aberta)return atuais;const novas=new Set(atuais);if(aberta)novas.add(id);else novas.delete(id);return novas});
 useEffect(()=>{if(!raiz.current)return;const observer=new ResizeObserver(entries=>setEstreito(entries[0].contentRect.width<1050));observer.observe(raiz.current);return()=>observer.disconnect()},[]);
 useEffect(()=>{const api=calendario.current?.getApi();if(!api||!primeiro)return;api.changeView(vista,modo==='dia'?diaAtual:primeiro);const painel=raiz.current?.querySelector<HTMLElement>('.fc-view-harness');if(painel&&!reduzirMovimento){const movimento=animate(painel,{y:[3,0]},{duration:.16,ease:'easeOut'});return()=>movimento.stop()}},[vista,primeiro,diaAtual,modo,reduzirMovimento]);
 useEffect(()=>{if(selecionado&&!porId.has(selecionado))setSelecionado(null)},[porId,selecionado]);
 const registrarRolagem=()=>{const scroller=raiz.current?.querySelector<HTMLElement>('.fc-scroller');if(scroller){scroller.classList.add('agenda-sessoes-dia');scroller.dataset.preservar=`${c.escopo}:${c.id}:fullcalendar`;scroller.dataset.rolavel='';scroller.setAttribute('aria-label','Sessões do período');scroller.tabIndex=0}};
 const conteudo=(arg:EventContentArg)=>{const l=arg.event.extendedProps.linha as LinhaPagina;return <SessaoAgenda linha={l} tabela={horarios} contexto={c} compacta={!arg.view.type.startsWith('list')} aberta={abertas.has(l.id)} aoExpandir={aberta=>expandir(l.id,aberta)}/>};
 const linhaSelecionada=selecionado?porId.get(selecionado):undefined;
 return <div className="agenda-react" ref={raiz} data-calendario="fullcalendar"><div className="agenda-fc-toolbar"><div><strong>{primeiro?primeiro===ultimo?dataTexto(primeiro):`${dataTexto(primeiro)} — ${dataTexto(ultimo)}`:'Agenda do período'}</strong><span>{linhas.length} {linhas.length===1?'sessão':'sessões'} · horários e profissionais</span></div><div className="agenda-fc-modos" role="group" aria-label="Visualização da agenda"><button type="button" data-agenda-modo="lista" aria-pressed={modo==='lista'} onClick={()=>setModo('lista')}>Lista</button><button type="button" data-agenda-modo="semana" aria-pressed={modo==='semana'} onClick={()=>setModo('semana')}>Semana</button><button type="button" data-agenda-modo="dia" aria-pressed={modo==='dia'} onClick={()=>setModo('dia')}>Dia</button></div>{modo==='dia'&&dias.length>0&&<label className="agenda-fc-dia">Dia visível<select value={diaAtual} onChange={e=>setDiaEscolhido(e.currentTarget.value)}>{dias.map(d=><option key={d} value={d}>{new Date(`${d}T12:00:00`).toLocaleDateString('pt-BR',{weekday:'short',day:'2-digit',month:'2-digit'})}</option>)}</select></label>}</div>
 <BuscaPacienteAgenda valor={busca} aoAlterar={setBusca} periodo quantidade={filtradas.length}/>
 <FiltrosSituacaoAgenda linhas={pesquisadas} valor={filtro} aoAlterar={setFiltro}/>
 {densidade&&<p className="agenda-fc-orientacao">A lista mantém cada paciente legível nos horários com várias sessões. Escolha Dia para concentrar a visualização.</p>}
 {primeiro?<FullCalendar ref={calendario} plugins={[timeGridPlugin,listPlugin,dayGridPlugin]} locale={ptBr} firstDay={1} initialDate={primeiro} initialView="listWeek" headerToolbar={false} height={560} timeZone="local" events={eventos} eventContent={conteudo} eventOrder="start,title" eventOrderStrict dayMaxEvents={2} moreLinkClick={arg=>{const data=arg.date;setDiaEscolhido(`${data.getFullYear()}-${String(data.getMonth()+1).padStart(2,'0')}-${String(data.getDate()).padStart(2,'0')}`);setModo('dia');return 'listDay'}} editable={false} selectable={false} eventStartEditable={false} eventDurationEditable={false} allDaySlot={false} slotDuration="00:30:00" slotMinTime="00:00:00" slotMaxTime="24:00:00" scrollTime="08:00:00" slotEventOverlap={false} nowIndicator eventTimeFormat={{hour:'2-digit',minute:'2-digit',hour12:false}} slotLabelFormat={{hour:'2-digit',minute:'2-digit',hour12:false}} dayHeaderFormat={{weekday:'short',day:'2-digit',month:'2-digit'}} noEventsContent={busca.trim()?"Nenhum paciente encontrado nesta agenda com o nome e os filtros informados.":"Sem sessões neste período e situação. Consulte a disponibilidade abaixo."} listDayFormat={{weekday:'long',day:'2-digit',month:'long'}} listDaySideFormat={false} views={{listWeek:{duration:{days:7}}}} datesSet={registrarRolagem} viewDidMount={registrarRolagem} eventDidMount={arg=>{if(!arg.view.type.startsWith('list')){arg.el.setAttribute('tabindex','0');arg.el.setAttribute('role','button');arg.el.setAttribute('aria-label',`${arg.event.startStr.slice(11,16)} · ${arg.event.title}. Ver sessão`);arg.el.onkeydown=e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();setSelecionado(arg.event.id)}}}}} eventClick={arg=>{if(!arg.view.type.startsWith('list')){arg.jsEvent.preventDefault();setSelecionado(arg.event.id)}}}/>:<p className="mensagem-web">A agenda aparecerá após a consulta dos horários.</p>}
 {linhaSelecionada&&<section className="agenda-fc-selecao" aria-label="Sessão selecionada"><header><div><strong>Sessão selecionada</strong><time>{inicio(linhaSelecionada).slice(11,16)}{fim(linhaSelecionada)?` — ${fim(linhaSelecionada).slice(11,16)}`:''}</time></div><button type="button" className="botao secundario" onClick={()=>setSelecionado(null)}>Fechar detalhes</button></header><SessaoAgenda linha={linhaSelecionada} tabela={horarios} contexto={c} aberta={abertas.has(linhaSelecionada.id)} aoExpandir={aberta=>expandir(linhaSelecionada.id,aberta)}/></section>}
 {semData.length>0&&<section className="agenda-fc-semdata"><h3>Sessões sem horário reconhecido</h3>{semData.map(l=><SessaoAgenda key={l.id} linha={l} tabela={horarios} contexto={c} aberta={abertas.has(l.id)} aoExpandir={aberta=>expandir(l.id,aberta)}/>)}</section>}
 <p className="agenda-fc-disponibilidade">Disponibilidade, bloqueios e todos os dados das sessões estão disponíveis abaixo.</p></div>;
}
export function AgendaReact({secao:s,contexto:c}:{secao:Secao;contexto:Contexto}){
 const horarios=s.tabelas.find(t=>['horarios','ClinicoSemanaSessoes'].includes(t.chave)),faixas=s.tabelas.find(t=>['faixas','ClinicoSemanaGrade'].includes(t.chave));
 return horarios&&faixas?<CalendarioAgenda key={`${c.escopo}:${c.id}:${s.chave}`} horarios={horarios} faixas={faixas} contexto={c}/>:null;
}
