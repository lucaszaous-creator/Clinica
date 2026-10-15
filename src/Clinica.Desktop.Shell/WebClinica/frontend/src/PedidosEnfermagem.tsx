import {useEffect,useRef,useState} from 'react';
import ControlesRolagem from './ControlesRolagem';
import './pedidos-enfermagem.css';

interface Pedido {
 id:number; inicio:string; fim:string; validade:string; modalidade:string; quantidade:number;
 autora:string; situacao:string; observacao:string; vistaEm:string|null; vistaPor:string;
 totalReferencia:number; ativa:boolean; podeTratar:boolean; versao:string; criadaEm?:string|null;
}
interface Periodo {
 total:number; bsv:number; bsvAcupuntura:number; outrasModalidades:number;
 bloqueios:{inicio:string;fim:string;recurso:string}[];
}
interface Detalhe {pedido:Pedido;periodo:Periodo;agendaAlterada:boolean;historico:{em:string;autor:string;acao:string;observacao:string}[]}
export interface EstadoPedidos {contexto:string;dia:string;pedidos:Pedido[];detalhe:Detalhe|null;carregando:boolean;erro:string|null;agora?:string;lidoEm?:string|null}
const data=(v:string)=>v.slice(0,10).split('-').reverse().join('/');
const hora=(v:string)=>v.slice(11,16);
// "Hoje"/"Amanhã" antes da data: a recepção lê o pedido do dia de relance, sem fazer conta.
const rotuloDia=(v:string,agora?:string)=>{
 if(!agora)return '';
 const d=(x:string)=>Date.UTC(+x.slice(0,4),+x.slice(5,7)-1,+x.slice(8,10));
 const dias=Math.round((d(v)-d(agora))/86400000);
 return dias===0?'Hoje':dias===1?'Amanhã':dias===-1?'Ontem':'';
};
// Há quanto tempo o pedido chegou — calculado pela hora da máquina que leu, não do navegador.
const chegou=(criada:string|null|undefined,agora?:string)=>{
 if(!criada||!agora)return '';
 const min=Math.floor((new Date(agora).getTime()-new Date(criada).getTime())/60000);
 if(min<1)return 'chegou agora';
 if(min<60)return `chegou há ${min} min`;
 if(min<24*60)return `chegou às ${hora(criada)}`;
 return `enviado em ${data(criada)}`;
};
const status=(v:string)=>({Novo:'Novo',Visto:'Visto pela recepção',Encerrado:'Aviso encerrado',Retirado:'Retirado pela enfermagem',Expirado:'Expirado'}[v]??v);

export default function PedidosEnfermagem({estado,ocupado,enviar}:{estado:EstadoPedidos;ocupado:boolean;enviar:(m:Record<string,unknown>)=>void}){
 const [filtro,setFiltro]=useState('abertos'),[soDia,setSoDia]=useState(false),[observacao,setObservacao]=useState(''),[confirmar,setConfirmar]=useState(false);
 const envio=useRef<{texto:string;chave:string}|undefined>(undefined);
 const titulo=useRef<HTMLHeadingElement>(null);
 const d=estado.detalhe,p=d?.pedido;
 const bloqueado=ocupado||estado.carregando;
 const novos=estado.pedidos.filter(p=>p.ativa&&p.situacao==='Novo').length;
 const abertos=estado.pedidos.filter(p=>p.ativa).length;
 const lista=estado.pedidos.filter(p=>(filtro==='novos'?p.ativa&&p.situacao==='Novo':filtro==='abertos'?p.ativa:!p.ativa)&&(!soDia||p.inicio.slice(0,10)===estado.dia));
 useEffect(()=>{setObservacao('');setConfirmar(false);envio.current=undefined;titulo.current?.focus();},[p?.id]);
 const acao=(acao:string,id?:number,extra:Record<string,unknown>={})=>enviar({acao,id,contexto:estado.contexto,...extra});
 function encerrar(){
  if(!p)return;
  const texto=JSON.stringify({id:p.id,versao:p.versao,observacao});
  if(envio.current?.texto!==texto)envio.current={texto,chave:crypto.randomUUID()};
  acao('encerrar',p.id,{versao:p.versao,observacao,idempotencia:envio.current!.chave});
 }
 return <main className="pedidos-enfermagem" aria-busy={bloqueado}>
  <header className="pedidos-cabecalho"><div><span className="pedidos-sobretitulo">RECEPÇÃO · ENFERMAGEM</span><h1 ref={titulo} tabIndex={-1}>{p?'Pedido de atendimentos':'Pedidos da enfermagem'}</h1><p>{p?'Consulte o aviso e confira o período antes de marcar.':'Avisos compartilhados com toda a recepção, sem distribuição de responsáveis.'}</p></div><div className="pedidos-acoes">{p&&<button className="secundario" disabled={bloqueado} onClick={()=>acao('voltar')}>Todos os avisos</button>}<button className="secundario" disabled={bloqueado} onClick={()=>acao('atualizar')}>Atualizar</button></div></header>
  <p className="pedidos-ao-vivo" role="status"><span className="pedidos-pulso" aria-hidden="true"/>{estado.carregando?'Atualizando…':`Atualiza sozinho quando a enfermagem envia${estado.lidoEm?` · última leitura às ${estado.lidoEm}`:''}`}</p>
  {estado.erro?<div className="aviso" role="alert">{estado.erro}</div>:p&&d?<>
   <section className="pedido-destaque"><div className="pedido-faixa"><span className={`pedido-status estado-${p.situacao.toLowerCase()}`}>{status(p.situacao)}</span><span>Pedido #{p.id} · {p.autora}{chegou(p.criadaEm,estado.agora)?` · ${chegou(p.criadaEm,estado.agora)}`:''}</span></div><h2>{rotuloDia(p.inicio,estado.agora)?`${rotuloDia(p.inicio,estado.agora)}, `:''}{data(p.inicio)} · {hora(p.inicio)} às {hora(p.fim)}</h2><p className="pedido-quantidade">Até <strong>{p.quantidade}</strong> atendimentos adicionais · {p.modalidade}</p><p className="pedido-observacao">{p.observacao||'Sem observação adicional.'}</p><p className="pedido-meta">Válido até {data(p.validade)} às {hora(p.validade)}{p.vistaEm?` · Visto por ${p.vistaPor} em ${data(p.vistaEm)} às ${hora(p.vistaEm)}`:''}</p>
    <div className="pedidos-acoes"><button disabled={bloqueado} onClick={()=>acao('ver-agenda',p.id)}>Ver este dia na agenda</button>{p.podeTratar&&!confirmar&&<button className="secundario" disabled={bloqueado} onClick={()=>setConfirmar(true)}>Encerrar aviso</button>}</div>
   </section>
   {d.agendaAlterada&&<div className="pedido-mudanca" role="status"><strong>A agenda mudou desde o envio.</strong><p>Havia {p.totalReferencia} agendados; agora há {d.periodo.total}. Confira horários, modalidades e bloqueios antes de marcar. A quantidade solicitada não representa vagas reservadas.</p></div>}
   <section className="pedido-periodo"><h2>Agenda atual do período</h2><p><strong>{d.periodo.total} agendados</strong> · {d.periodo.bsv} BSV · {d.periodo.bsvAcupuntura} BSV + acupuntura · {d.periodo.outrasModalidades} outras modalidades</p>{d.periodo.bloqueios.length>0&&<><h3>Bloqueios no período</h3><ul>{d.periodo.bloqueios.map((b,i)=><li key={`${b.inicio}-${i}`}>{hora(b.inicio)}–{hora(b.fim)} · {b.recurso}</li>)}</ul></>}</section>
   {confirmar&&p.podeTratar&&<section className="pedido-encerramento"><h2>Encerrar este aviso?</h2><p>Ele ficará no histórico. Encerrar o aviso não confirma que houve agendamento.</p><label htmlFor="pedido-retorno">Retorno para a enfermagem (opcional)<textarea id="pedido-retorno" rows={3} maxLength={1000} value={observacao} onChange={e=>setObservacao(e.target.value)} placeholder="Ex.: avisamos pacientes interessados para esse período."/></label><div className="pedidos-acoes"><button disabled={bloqueado} onClick={encerrar}>Confirmar encerramento</button><button className="secundario" disabled={bloqueado} onClick={()=>setConfirmar(false)}>Continuar com o aviso aberto</button></div></section>}
   <section className="pedido-historico"><h2>Histórico do aviso</h2><ol>{d.historico.map((h,i)=><li key={`${h.em}-${i}`}><strong>{h.acao}</strong><span>{data(h.em)} às {hora(h.em)} · {h.autor}</span>{h.observacao&&<p>{h.observacao}</p>}</li>)}</ol></section>
  </>:<>
   <div className="pedidos-filtros"><div role="group" aria-label="Filtrar avisos">{[['novos',`Novos (${novos})`],['abertos',`Em aberto (${abertos})`],['historico','Histórico']].map(([id,nome])=><button key={id} className="secundario" aria-pressed={filtro===id} onClick={()=>setFiltro(id)}>{nome}</button>)}</div><label className="pedidos-so-dia"><input type="checkbox" checked={soDia} onChange={e=>setSoDia(e.target.checked)}/>Somente {data(estado.dia)}</label></div>
   <p className="pedido-meta" role="status">{estado.carregando?'Atualizando avisos…':`${lista.length} avisos · ${soDia?'dia consultado':'todas as datas'}`}</p>
   {!lista.length?<div className="vazio"><h2>{filtro==='novos'?'Nenhum aviso novo':'Nenhum aviso neste filtro'}</h2><p>{soDia?'Desmarque o filtro do dia para consultar as outras datas.':'Os pedidos enviados pela enfermagem aparecerão aqui.'}</p></div>:<ul className="pedidos-lista">{lista.map(p=><li key={p.id} className={p.situacao==='Novo'&&p.ativa?'pedido-novo':''}><div className="pedido-data">{rotuloDia(p.inicio,estado.agora)&&<em>{rotuloDia(p.inicio,estado.agora)}</em>}<strong>{data(p.inicio)}</strong><span>{hora(p.inicio)}–{hora(p.fim)}</span></div><div className="pedido-conteudo"><div className="pedido-faixa"><span className={`pedido-status estado-${p.situacao.toLowerCase()}`}>{status(p.situacao)}</span>{chegou(p.criadaEm,estado.agora)&&<span>{chegou(p.criadaEm,estado.agora)}</span>}</div><h2><span className="pedido-qtd" aria-hidden="true">+{p.quantidade}</span>Até {p.quantidade} atendimentos adicionais · {p.modalidade}</h2><p className="pedido-observacao">{p.observacao||'Sem observação adicional.'}</p><p className="pedido-meta">{p.autora} · válido até {data(p.validade)} {hora(p.validade)}</p></div><button className="secundario" disabled={bloqueado} onClick={()=>acao('abrir-aviso',p.id)} aria-label={`Ver pedido de ${p.quantidade} ${p.modalidade}, ${data(p.inicio)} às ${hora(p.inicio)}`}>Ver pedido</button></li>)}</ul>}
  </>}
  <ControlesRolagem/>
 </main>;
}
