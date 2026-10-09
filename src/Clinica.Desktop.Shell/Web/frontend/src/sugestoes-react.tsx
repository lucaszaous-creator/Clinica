import {createElement,useEffect,useRef,useState,type KeyboardEvent} from 'react';
import {ArrowRight,Check,type IconNode} from 'lucide';
const Icone=({icone}:{icone:IconNode})=><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{icone.map(([tag,attrs],i)=>createElement(tag,{...attrs,key:i}))}</svg>;
import './sugestoes-react.css';

export type OpcaoPaciente={valor:string;rotulo:string};
export function ehSeletorPaciente(chave:string,temBusca=false){return chave==='Seletor.Selecionado'||temBusca&&chave==='Escolhido';}

/** O select conserva o contrato da ponte. A lista oferece uma escolha explícita,
 * apenas entre as opções autorizadas pelo host, sem selecionar ao digitar. */
export function SugestoesPacientesReact({id,opcoes,valor,estadoBusca,habilitado=true}:{id:string;opcoes:OpcaoPaciente[];valor:string;estadoBusca?:{termo:string;carregando:boolean;erro?:string|null;pacienteSelecionado?:string|null}|null;habilitado?:boolean}){
 const [termo,setTermo]=useState(''),[fechado,setFechado]=useState(!!valor),[aguardando,setAguardando]=useState(false),[todas,setTodas]=useState(false);
 const lista=useRef<HTMLDivElement>(null),busca=useRef<HTMLInputElement|null>(null),resumo=useRef<HTMLDivElement>(null),focarResumo=useRef(false);
 const idLista='sugestoes-'+id;
 useEffect(()=>{
  const select=document.getElementById(id) as HTMLSelectElement|null;if(!select)return;
  const campo=[...document.querySelectorAll<HTMLInputElement>('[data-campo="Seletor.Termo"]')].find(el=>el.dataset.escopo===select.dataset.escopo&&el.dataset.contexto===select.dataset.contexto&&el.dataset.tabela===select.dataset.tabela&&el.dataset.linha===select.dataset.linha);
  if(!campo)return;busca.current=campo;setTermo(campo.value);
  const input=()=>{setTermo(campo.value);setFechado(false);setAguardando(true);setTodas(false)};
  const tecla=(event:globalThis.KeyboardEvent)=>{
   if(event.key==='ArrowDown'){const primeiro=lista.current?.querySelector<HTMLButtonElement>('[data-sugestao-paciente]');if(primeiro){event.preventDefault();event.stopPropagation();primeiro.focus()}}
   if(event.key==='Escape'&&lista.current){event.preventDefault();event.stopPropagation();setFechado(true)}
  };
  const escolhido=()=>{setFechado(true);setAguardando(false)};
  campo.addEventListener('input',input);campo.addEventListener('keydown',tecla);select.addEventListener('change',escolhido);
  return()=>{campo.removeEventListener('input',input);campo.removeEventListener('keydown',tecla);select.removeEventListener('change',escolhido);busca.current=null};
 },[id]);
 // Somente a resposta do host libera as opções; digitar não escolhe uma pessoa.
 useEffect(()=>{
  const atual=busca.current?.value??'';
  if(!estadoBusca||estadoBusca.termo===atual&&!estadoBusca.carregando)setAguardando(false);
  if(busca.current)setTermo(atual);
 },[opcoes,estadoBusca]);
 const disponiveis=opcoes.filter(o=>o.valor!==''),selecionado=estadoBusca?.pacienteSelecionado??disponiveis.find(o=>o.valor===valor)?.rotulo;
 const carregando=aguardando||!!estadoBusca?.carregando;
 const visivel=!fechado&&!carregando&&!estadoBusca?.erro&&disponiveis.length>0;
 useEffect(()=>{const input=busca.current;if(!input)return;input.setAttribute('aria-controls',idLista);return()=>input.removeAttribute('aria-controls')},[idLista]);
 useEffect(()=>{
  if(!focarResumo.current||!resumo.current)return;
  focarResumo.current=false;
  const quadro=requestAnimationFrame(()=>resumo.current?.focus({preventScroll:true}));
  return()=>cancelAnimationFrame(quadro);
 },[valor]);
 const escolher=(valorOpcao:string)=>{
  const select=document.getElementById(id) as HTMLSelectElement|null;
  if(carregando||!select||select.disabled||!Array.from(select.options).some(o=>o.value===valorOpcao))return;
  setFechado(true);focarResumo.current=true;select.value=valorOpcao;select.dispatchEvent(new Event('change',{bubbles:true}));
  // O resultado pode abrir outra página. Se continuar aqui, o foco fica no resumo.
  if(valorOpcao===valor)resumo.current?.focus({preventScroll:true});
 };
 const teclado=(e:KeyboardEvent<HTMLButtonElement>)=>{
  const botoes=Array.from(lista.current?.querySelectorAll<HTMLButtonElement>('[data-sugestao-paciente]')??[]),index=botoes.indexOf(e.currentTarget);
  if(e.key==='Escape'){e.preventDefault();e.stopPropagation();setFechado(true);busca.current?.focus()}
  else if(['ArrowDown','ArrowUp','Home','End'].includes(e.key)){e.preventDefault();e.stopPropagation();const proximo=e.key==='Home'?0:e.key==='End'?botoes.length-1:e.key==='ArrowDown'?Math.min(index+1,botoes.length-1):index-1;if(proximo<0)busca.current?.focus();else botoes[proximo]?.focus()}
 };
 const reabrir=()=>{setFechado(false);busca.current?.focus();busca.current?.select()};
 return <div className="pacientes-selecao" id={idLista}>
  {selecionado&&<div className="paciente-confirmado" ref={resumo} tabIndex={-1}>
   <Icone icone={Check}/><div><span>Paciente selecionado</span><strong>{selecionado}</strong></div>
   <button type="button" className="botao secundario" disabled={!habilitado} onClick={reabrir}>Trocar paciente</button>
  </div>}
  {carregando?<p className="pacientes-orientacao" role="status">Buscando pacientes…</p>:visivel?<div className="pacientes-sugestoes" ref={lista}>
   <div className="pacientes-sugestoes-titulo"><strong>Resultados da busca</strong><span role="status">{disponiveis.length} {disponiveis.length===1?'paciente':'pacientes'}</span></div>
   <div className="pacientes-sugestoes-lista" role="group" aria-label="Pacientes encontrados">{(todas?disponiveis:disponiveis.slice(0,6)).map(o=><button type="button" key={o.valor} data-sugestao-paciente={o.valor} disabled={!habilitado} aria-label={'Selecionar '+o.rotulo} onClick={()=>escolher(o.valor)} onKeyDown={teclado}><span>{o.rotulo}</span><small>Selecionar <Icone icone={ArrowRight}/></small></button>)}</div>
   {!todas&&disponiveis.length>6&&<button type="button" className="pacientes-sugestoes-mais" onClick={()=>setTodas(true)}>Ver os {disponiveis.length} resultados</button>}
  </div>:estadoBusca?.erro?<p className="erro" role="alert">{estadoBusca.erro}</p>:(termo.trim()&&!fechado||!selecionado)&&<p className="pacientes-orientacao" role="status">{termo.trim()&&!fechado?'Nenhum paciente encontrado. Confira o nome ou CPF e tente novamente.':'Busque pelo nome ou CPF e selecione o paciente nos resultados.'}</p>}
  {fechado&&!selecionado&&disponiveis.length>0&&<button type="button" className="botao secundario" onClick={reabrir}>Mostrar resultados</button>}
 </div>;
}
