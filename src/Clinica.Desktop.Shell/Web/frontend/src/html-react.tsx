import {Button,Input,Textarea,NativeSelect} from '@mantine/core';
import {IconeAcao,iconeDoComando} from './icones-acoes';
import {createElement, Fragment, useLayoutEffect, useMemo, useRef, type ReactNode, type Ref, type CSSProperties} from 'react';
import {SugestoesPacientesReact,ehSeletorPaciente} from './sugestoes-react';

/** Ponte de apresentação para os controles especializados existentes. Não injeta HTML:
 * cada nó recebe identidade e é reconciliado pelo React, inclusive vídeos e campos. */
const nomes:Record<string,string>={class:'className',for:'htmlFor',tabindex:'tabIndex',readonly:'readOnly',maxlength:'maxLength',colspan:'colSpan',rowspan:'rowSpan',contenteditable:'contentEditable',autocomplete:'autoComplete',inputmode:'inputMode',autoplay:'autoPlay',playsinline:'playsInline',spellcheck:'spellCheck','stroke-width':'strokeWidth','stroke-linecap':'strokeLinecap','stroke-linejoin':'strokeLinejoin','stroke-dasharray':'strokeDasharray','font-size':'fontSize','fill-rule':'fillRule','clip-rule':'clipRule','viewbox':'viewBox'};
const booleanos=new Set(['disabled','required','hidden','multiple','readOnly','autoPlay','playsInline','controls','muted','loop','checked','selected','open']);
function estilo(el:Element):CSSProperties {
 const style=(el as HTMLElement).style, result:Record<string,string>={};
 if(!style)return result;
 for(const property of Array.from(style))result[property.startsWith('--')?property:property.replace(/-([a-z])/g,(_,c:string)=>c.toUpperCase())]=style.getPropertyValue(property);
 return result;
}
function propsDoNo(el:Element):Record<string,unknown>{
 const props:Record<string,unknown>={};
 for(const attr of Array.from(el.attributes)){
  if(/^on/i.test(attr.name))continue;
  const name=nomes[attr.name]??attr.name;
  props[name]=attr.name==='style'?estilo(el):booleanos.has(name)?true:attr.value;
 }
 return props;
}
function identidade(el:Element,index:number):string{
 const campo=el.matches('.campo-web,.editor-web')?el.querySelector('[data-campo],output[id]'):null;
 return el.id||el.getAttribute('data-preservar')||el.getAttribute('data-linha-id')||el.getAttribute('data-secao')||el.getAttribute('data-etapa')||el.getAttribute('data-tabela-container')||el.getAttribute('data-comando')||campo?.id||`${el.localName}:${el.getAttribute('class')??''}:${index}`;
}
function ControleReconciliado({tag,atributos,children,valor,marcado}:{tag:string;atributos:Record<string,unknown>;children?:ReactNode;valor:string;marcado?:boolean}){
 const ref=useRef<HTMLInputElement|HTMLTextAreaElement|HTMLSelectElement>(null);
 useLayoutEffect(()=>{
  const el=ref.current;if(!el)return;
  // Textos em edição pertencem ao usuário; o host confirma pelo rascunho compartilhado.
  if(el instanceof HTMLInputElement&&['checkbox','radio'].includes(el.type)){el.checked=!!marcado;return;}
  if((el instanceof HTMLSelectElement||document.activeElement!==el)&&el.value!==valor)el.value=valor;
 },[valor,marcado]);
 const props={...atributos,ref,defaultValue:valor,defaultChecked:marcado};
 delete (props as Record<string,unknown>).value;delete (props as Record<string,unknown>).checked;
 if(tag==='input'&&atributos.type==='file')delete (props as Record<string,unknown>).defaultValue;
 if(tag==='textarea')return <Textarea {...props} ref={ref as Ref<HTMLTextAreaElement>}/>;
 if(tag==='select')return <NativeSelect {...props} ref={ref as Ref<HTMLSelectElement>}>{children}</NativeSelect>;
 if(tag==='input'&&!['checkbox','radio','file','hidden'].includes(String(atributos.type??'')))return <Input {...props} ref={ref as Ref<HTMLInputElement>}/>;
 return createElement(tag,props,tag==='input'?undefined:children);
}
function EditorReconciliado({atributos,html}:{atributos:Record<string,unknown>;html:string}){
 const ref=useRef<HTMLDivElement>(null),ultimo=useRef('');
 useLayoutEffect(()=>{
  const el=ref.current;if(!el||el===document.activeElement||ultimo.current===html)return;
  // Ilha do editor: o navegador mantém a seleção e os comandos de formatação.
  const template=document.createElement('template');template.innerHTML=html;
  el.replaceChildren(template.content.cloneNode(true));ultimo.current=html;
 },[html]);
 return createElement('div',{...atributos,ref,suppressContentEditableWarning:true});
}
function DetalhesReconciliados({atributos,children,aberto}:{atributos:Record<string,unknown>;children:ReactNode;aberto:boolean}){
 const ref=useRef<HTMLDetailsElement>(null);
 // Apenas o estado inicial vem da composição; depois a expansão pertence ao usuário.
 useLayoutEffect(()=>{if(ref.current)ref.current.open=aberto},[]);
 return createElement('details',{...atributos,ref},children);
}
function noReact(no:Node,index:number):ReactNode{
 if(no.nodeType===Node.TEXT_NODE)return no.textContent;
 if(!(no instanceof Element))return null;
 if(['script','iframe','object','embed'].includes(no.localName))return null;
 const tag=no.localName,props=propsDoNo(no),key=identidade(no,index);
 if(no.classList.contains('campo-web')&&no.querySelector('[data-campo="Seletor.Termo"]'))props.className=String(props.className??'')+' campo-busca';
 if(tag==='input'&&no.getAttribute('data-campo')==='Seletor.Termo')props.placeholder='Digite o nome ou CPF';
 if(no.hasAttribute('contenteditable'))return <EditorReconciliado key={key} atributos={props} html={no.innerHTML}/>;
 const temBusca=!!no.closest('.campos-web')?.querySelector('[data-campo="Seletor.Termo"]');
 const paciente=tag==='select'&&ehSeletorPaciente(no.getAttribute('data-campo')??'',temBusca);
 if(paciente){props.hidden=true;props.tabIndex=-1;props['aria-hidden']=true}
 if(no.classList.contains('campo-web')&&Array.from(no.querySelectorAll('select[data-campo]')).some(el=>ehSeletorPaciente(el.getAttribute('data-campo')??'',temBusca)))props.className=String(props.className??'')+' campo-paciente-selecao';
 let children=Array.from(no.childNodes).map(noReact);
 if(no.classList.contains('campos-web')){
  const campos=Array.from(no.children),busca=campos.find(el=>el.querySelector('[data-campo="Seletor.Termo"]'));
  const selecao=campos.find(el=>Array.from(el.querySelectorAll('select[data-campo]')).some(c=>ehSeletorPaciente(c.getAttribute('data-campo')??'',!!busca)));
  if(busca&&selecao)children=campos.map((el,i)=>el===selecao?null:el===busca?<section key={identidade(el,i)} className="busca-paciente-unificada" aria-label="Localizar paciente">{noReact(busca,i)}{noReact(selecao,campos.indexOf(selecao))}</section>:noReact(el,i));
 }
 if(['input','textarea','select'].includes(tag)){
  const valor=tag==='textarea'?no.textContent??'':tag==='select'?(no.querySelector('option[selected]') as HTMLOptionElement|null)?.value??(no.querySelector('option') as HTMLOptionElement|null)?.value??'':no.getAttribute('value')??'';
  const controle=<ControleReconciliado tag={tag} atributos={props} valor={valor} marcado={no.hasAttribute('checked')} children={children}/>;
  if(paciente)return <Fragment key={key}><div hidden>{controle}</div><SugestoesPacientesReact estadoBusca={no.hasAttribute('data-busca-paciente')?JSON.parse(no.getAttribute('data-busca-paciente')!):undefined} valor={valor} id={no.id} opcoes={Array.from(no.querySelectorAll('option')).map(o=>({valor:o.value,rotulo:o.textContent??''}))} habilitado={!no.hasAttribute('disabled')}/></Fragment>;
  return <Fragment key={key}>{controle}</Fragment>;
 }
 if(tag==='button'&&no.classList.contains('botao')){
  const comando=no.getAttribute('data-comando')??'';
  return <Button {...props} key={key} variant={no.classList.contains('primario')?'filled':'default'} leftSection={!no.querySelector('svg')&&iconeDoComando(comando)?<IconeAcao comando={comando}/>:undefined}>{children}</Button>;
 }
 if(tag==='option')delete props.selected;
 if(tag==='details'){delete props.open;return <DetalhesReconciliados key={key} atributos={props} aberto={no.hasAttribute('open')}>{children}</DetalhesReconciliados>;}
 const vazios=['area','base','br','col','hr','img','link','meta','param','source','track','wbr'];
 return createElement(tag,{...props,key},vazios.includes(tag)?undefined:children);
}
export function HtmlReact({html}:{html:string}){
 const arvore=useMemo(()=>{const template=document.createElement('template');template.innerHTML=html;return Array.from(template.content.childNodes).map(noReact)},[html]);
 return <Fragment>{arvore}</Fragment>;
}
export const HTMLReact=HtmlReact;
