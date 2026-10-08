import {useEffect,useRef,useState,type KeyboardEvent} from 'react';
import './sugestoes-react.css';
export type OpcaoPaciente={valor:string;rotulo:string};
export function ehSeletorPaciente(chave:string){return chave==='Seletor.Selecionado';}
/** Sugestões são exclusivamente as opções autorizadas retornadas pelo host. A
 * escolha explícita percorre o mesmo change do seletor, incluindo permissões. */
export function SugestoesPacientesReact({id,opcoes,habilitado=true}:{id:string;opcoes:OpcaoPaciente[];habilitado?:boolean}){
 const [termo,setTermo]=useState(''),[fechado,setFechado]=useState(false),[aguardando,setAguardando]=useState(false),[todas,setTodas]=useState(false);
 const lista=useRef<HTMLDivElement>(null),busca=useRef<HTMLInputElement|null>(null);
 const idLista=`sugestoes-${id}`;
 useEffect(()=>{
  const select=document.getElementById(id) as HTMLSelectElement|null;if(!select)return;
  const campo=[...document.querySelectorAll<HTMLInputElement>('[data-campo="Seletor.Termo"]')].find(el=>el.dataset.escopo===select.dataset.escopo&&el.dataset.contexto===select.dataset.contexto&&el.dataset.tabela===select.dataset.tabela&&el.dataset.linha===select.dataset.linha);
  if(!campo)return;busca.current=campo;setTermo(campo.value);
  const input=()=>{setTermo(campo.value);setFechado(false);setAguardando(true);setTodas(false)};
  const tecla=(event:globalThis.KeyboardEvent)=>{if(event.key==='ArrowDown'){const primeiro=lista.current?.querySelector<HTMLButtonElement>('[data-sugestao-paciente]');if(primeiro){event.preventDefault();event.stopPropagation();primeiro.focus()}}if(event.key==='Escape'&&lista.current){event.preventDefault();event.stopPropagation();setFechado(true)}};
  const escolhido=()=>setFechado(true);
  campo.addEventListener('input',input);campo.addEventListener('keydown',tecla);select.addEventListener('change',escolhido);
  return()=>{campo.removeEventListener('input',input);campo.removeEventListener('keydown',tecla);select.removeEventListener('change',escolhido);busca.current=null};
 },[id]);
 // Nova resposta do host libera a lista; re-render local ao digitar não faz isso.
 useEffect(()=>{setAguardando(false);if(busca.current)setTermo(busca.current.value)},[opcoes]);
 const disponiveis=opcoes.filter(o=>o.valor!==''),visivel=habilitado&&!fechado&&!aguardando&&termo.trim().length>0&&disponiveis.length>0;
 useEffect(()=>{const input=busca.current;if(!input)return;if(visivel){input.setAttribute('aria-controls',idLista);input.setAttribute('aria-expanded','true')}else{input.removeAttribute('aria-controls');input.removeAttribute('aria-expanded')}return()=>{input.removeAttribute('aria-controls');input.removeAttribute('aria-expanded')}},[visivel,idLista]);
 if(!visivel)return null;
 const escolher=(valor:string)=>{const select=document.getElementById(id) as HTMLSelectElement|null;if(!select||select.disabled||!Array.from(select.options).some(o=>o.value===valor))return;setFechado(true);select.value=valor;select.dispatchEvent(new Event('change',{bubbles:true}));select.focus({preventScroll:true})};
 const teclado=(e:KeyboardEvent<HTMLButtonElement>)=>{const botoes=Array.from(lista.current?.querySelectorAll<HTMLButtonElement>('[data-sugestao-paciente]')??[]),index=botoes.indexOf(e.currentTarget);if(e.key==='Escape'){e.preventDefault();e.stopPropagation();setFechado(true);busca.current?.focus()}else if(['ArrowDown','ArrowUp','Home','End'].includes(e.key)){e.preventDefault();e.stopPropagation();const proximo=e.key==='Home'?0:e.key==='End'?botoes.length-1:e.key==='ArrowDown'?Math.min(index+1,botoes.length-1):index-1;if(proximo<0)busca.current?.focus();else botoes[proximo]?.focus()}};
 return <div className="pacientes-sugestoes" id={idLista} ref={lista} onKeyDown={e=>{if(e.key==='Escape'){e.preventDefault();e.stopPropagation();setFechado(true);busca.current?.focus()}}}><div className="pacientes-sugestoes-titulo"><strong>Pacientes encontrados</strong><span role="status">{disponiveis.length} {disponiveis.length===1?'resultado':'resultados'}</span></div><div className="pacientes-sugestoes-lista">{(todas?disponiveis:disponiveis.slice(0,6)).map(o=><button type="button" key={o.valor} data-sugestao-paciente={o.valor} onClick={()=>escolher(o.valor)} onKeyDown={teclado}><span>{o.rotulo}</span><small>Selecionar</small></button>)}</div>{!todas&&disponiveis.length>6&&<button type="button" className="pacientes-sugestoes-mais" onClick={()=>setTodas(true)}>Ver os {disponiveis.length} resultados</button>}</div>;
}
