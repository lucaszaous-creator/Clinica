import './style.css';
import './paginas.css';
import './navegacao.css';
import './suite.css';
import './identidade-visual.css';
import './movimento-react.css';
import {createElement as elementoReact} from 'react';
import {createRoot} from 'react-dom/client';
import {flushSync} from 'react-dom';
import {SuiteReact,type EstadoSuite} from './suite-react';
import {ligarControles,emDesenho} from './controles';
import {filtroTreinamento,prepararVideo} from './treinamento';
import {guardarRascunho,limparRascunhos,type Contexto} from './paginas';

type Mensagem={acao:string;valor?:unknown;chave?:string;contexto?:string;id?:string;tabela?:string;linha?:string};
type Estado=EstadoSuite;
type Ponte={postMessage:(m:Mensagem)=>void;addEventListener:(tipo:'message',acao:(e:MessageEvent)=>void)=>void};
declare global{interface Window{chrome?:{webview?:Ponte}}}
const ponte=window.chrome?.webview,app=document.querySelector<HTMLDivElement>('#app')!;
const raizReact=createRoot(app);
const h=(v:unknown)=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));

let estado:Estado={tipo:'estado',titulo:'Clínica SemDor',usuario:'',ocupado:false,rotas:[]};
let treinoAberto=false,avisosAbertos=false;
let grupo:string|null=null,buscaAberta=false,usuarioAberto=false,busca='',retorno='';
const pendentes=new Map<string,Mensagem>();let temporizador:ReturnType<typeof setTimeout>;
function postar(m:Mensagem){ponte?.postMessage(m)}
function descarregar(){clearTimeout(temporizador);for(const m of pendentes.values())postar(m);pendentes.clear()}
function contexto(el:HTMLElement):Contexto{return{escopo:el.dataset.escopo==='dialogo'?'dialogo':'pagina',id:el.dataset.contexto??'',tabela:el.dataset.tabela,linha:el.dataset.linha}}
function alterar(el:HTMLInputElement|HTMLSelectElement|HTMLTextAreaElement|HTMLElement){
 if(!el.dataset.campo)return;
 const c=contexto(el);const valor=el.isContentEditable?lerEditor(el):el instanceof HTMLInputElement&&el.type==='checkbox'?el.checked:(el as HTMLInputElement).value;
 guardarRascunho(c,el.dataset.campo,valor);pendentes.set(el.id,{acao:c.escopo==='dialogo'?'dlg-campo':'pagina-campo',id:c.escopo==='dialogo'?c.id:undefined,contexto:c.escopo==='pagina'?c.id:undefined,chave:el.dataset.campo,valor,tabela:c.tabela,linha:c.linha});clearTimeout(temporizador);temporizador=setTimeout(descarregar,250);
}
function alterarEspecial(el:HTMLElement,valor:unknown){const c=contexto(el);if(el.dataset.mapa===undefined)guardarRascunho(c,el.dataset.campo!,valor);descarregar();postar({acao:c.escopo==='dialogo'?'dlg-campo':'pagina-campo',id:c.escopo==='dialogo'?c.id:undefined,contexto:c.escopo==='pagina'?c.id:undefined,chave:el.dataset.campo,valor,tabela:c.tabela,linha:c.linha})}
function selecaoEditor(el:HTMLElement){const s=window.getSelection();if(!s?.rangeCount||!s.anchorNode||!el.contains(s.anchorNode))return null;const r=s.getRangeAt(0),antes=r.cloneRange();antes.selectNodeContents(el);antes.setEnd(r.startContainer,r.startOffset);const inicio=antes.toString().length;return{inicio,fim:inicio+r.toString().length}}
function restaurarSelecao(el:HTMLElement,pos:{inicio:number;fim:number}|null){if(!pos)return;const w=document.createTreeWalker(el,NodeFilter.SHOW_TEXT);let n:Node|null,total=0;const r=document.createRange();let inicio=false;while((n=w.nextNode())){const fim=total+(n.textContent?.length??0);if(!inicio&&pos.inicio<=fim){r.setStart(n,Math.max(0,pos.inicio-total));inicio=true}if(inicio&&pos.fim<=fim){r.setEnd(n,Math.max(0,pos.fim-total));const s=window.getSelection();s?.removeAllRanges();s?.addRange(r);return}total=fim}}
function lerEditor(el:HTMLElement){
 const trechos:{texto:string;negrito:boolean;italico:boolean}[]=[];
 function visitar(n:Node,nf=false,it=false){if(n.nodeType===Node.TEXT_NODE){if(n.textContent)trechos.push({texto:n.textContent,negrito:nf,italico:it});return}if(!(n instanceof HTMLElement))return;if(n.tagName==='BR'){trechos.push({texto:'\n',negrito:nf,italico:it});return}const bloco=['DIV','P'].includes(n.tagName);if(bloco&&trechos.length&&!trechos.at(-1)!.texto.endsWith('\n'))trechos.push({texto:'\n',negrito:false,italico:false});for(const f of n.childNodes)visitar(f,nf||['B','STRONG'].includes(n.tagName),it||['I','EM'].includes(n.tagName))}
 for(const n of el.childNodes)visitar(n);const texto=trechos.map(t=>t.texto).join('');return{texto,formato:trechos.some(t=>t.negrito||t.italico)?JSON.stringify(trechos):''};
}
function abrirGrupo(chave:string|null){grupo=estado.dialogo?null:chave;document.querySelectorAll<HTMLElement>('.grupo-topo').forEach(el=>{const aberto=el.dataset.grupo===grupo;el.querySelector('button')?.setAttribute('aria-expanded',String(aberto));el.querySelector<HTMLElement>('.submenu-topo')!.hidden=!aberto})}
function chaveRolagem(el:HTMLElement){
 const pagina=el.matches('.conteudo,.dialogo-corpo')?el.querySelector<HTMLElement>('[data-pagina]'):el.closest<HTMLElement>('[data-pagina]');
 const dialogo=el.closest<HTMLElement>('[data-dialogo]')?.dataset.dialogo??'pagina';
 return JSON.stringify([dialogo,pagina?.dataset.pagina??'',el.matches('.conteudo')?'conteudo':el.matches('.dialogo-corpo')?'dialogo':el.closest<HTMLElement>('[data-secao]')?.dataset.secao??'',el.closest<HTMLElement>('[data-tabela-container]')?.dataset.tabelaContainer??'',el.matches('.agenda-sessoes-dia')?el.getAttribute('aria-label'):el.matches('.agenda-rolagem')?'grade':'']);
}
function render(){
 if(emDesenho())return;
 const foco=document.activeElement as HTMLInputElement|null,id=foco?.id;const inicio=foco?.selectionStart,fim=foco?.selectionEnd;
 const editor=foco?.isContentEditable?{html:foco.innerHTML,selecionado:selecaoEditor(foco)}:null;
 const videoAnterior=document.querySelector<HTMLVideoElement>('#video-aula'),videoRodando=videoAnterior&&!videoAnterior.paused;if(videoAnterior)videoAnterior.dataset.movendo='true';
 const areasRolagem='.conteudo,.dialogo-corpo,.tabela-scroll,.agenda-sessoes-dia,.agenda-rolagem';
 const pos=new Map([...document.querySelectorAll<HTMLElement>(areasRolagem)].map(e=>[chaveRolagem(e),[e.scrollTop,e.scrollLeft]]));
 const detalhes=new Map([...document.querySelectorAll<HTMLDetailsElement>('details[data-preservar]')].map(e=>[JSON.stringify([e.closest<HTMLElement>('[data-pagina]')?.dataset.pagina,e.dataset.preservar]),e.open]));
 // A resposta do WebView2 deve estar no DOM antes de restaurar foco/rolagem e ligar canvas.
 flushSync(()=>raizReact.render(elementoReact(SuiteReact,{estado,grupo,buscaAberta,usuarioAberto,busca,treinoAberto,avisosAbertos,ponteDisponivel:!!ponte})));
 document.querySelectorAll<HTMLElement>(areasRolagem).forEach(el=>{const anterior=pos.get(chaveRolagem(el));if(anterior){el.scrollTop=anterior[0];el.scrollLeft=anterior[1]}});
 document.querySelectorAll<HTMLDetailsElement>('details[data-preservar]').forEach(el=>{const anterior=detalhes.get(JSON.stringify([el.closest<HTMLElement>('[data-pagina]')?.dataset.pagina,el.dataset.preservar]));if(anterior!==undefined)el.open=anterior});
 if(id){const alvo=document.getElementById(id) as HTMLInputElement|null;if(alvo){if(editor&&alvo!==foco)alvo.innerHTML=editor.html;alvo.focus({preventScroll:true});if(editor&&alvo!==foco)restaurarSelecao(alvo,editor.selecionado);else if(['text','search','textarea','password'].includes(alvo.type))alvo.setSelectionRange(inicio??0,fim??0)}}
 const videoNovo=document.querySelector<HTMLVideoElement>('#video-aula');if(videoAnterior&&videoNovo&&videoNovo.src===videoAnterior.src){if(videoNovo!==videoAnterior){videoNovo.currentTime=videoAnterior.currentTime;}if(videoRodando)void videoAnterior.play().catch(()=>{});queueMicrotask(()=>delete videoAnterior.dataset.movendo)}
 prepararVideo((acao,chave,valor)=>postar({acao,chave,valor,contexto:estado.pagina?.contexto}));
 ligarControles(app,alterarEspecial);
 document.querySelectorAll<HTMLElement>('.tabela-web').forEach(t=>{const sc=t.querySelector<HTMLElement>('.tabela-scroll'),ct=t.querySelector<HTMLElement>('.controle-tabela');if(sc&&ct)ct.hidden=sc.scrollWidth<=sc.clientWidth+1});
}
app.addEventListener('pointerover',e=>{if(e.pointerType!=='mouse')return;const g=(e.target as Element).closest<HTMLElement>('.grupo-topo');if(g)abrirGrupo(g.dataset.grupo!)});
app.addEventListener('pointerout',e=>{const g=(e.target as Element).closest<HTMLElement>('.grupo-topo');if(g&&!(e.relatedTarget instanceof Node&&g.contains(e.relatedTarget))&&!g.contains(document.activeElement))abrirGrupo(null)});
app.addEventListener('click',e=>{
 const b=(e.target as Element).closest<HTMLElement>('button,[data-inicio],[data-fechar-busca]');if(!b||(b as HTMLButtonElement).disabled)return;
 if(b.hasAttribute('data-treinamento')){treinoAberto=true;postar({acao:'treinamento',contexto:estado.pagina?.contexto});render();return}
 if(b.hasAttribute('data-sair-treinamento')){document.querySelector<HTMLVideoElement>('#video-aula')?.pause();treinoAberto=false;render();return}
 if(b.hasAttribute('data-avisos')){avisosAbertos=!avisosAbertos;if(avisosAbertos)postar({acao:'avisos-lidos',contexto:estado.pagina?.contexto});render();return}
 if(b.hasAttribute('data-fila-infusao')){document.querySelector<HTMLVideoElement>('#video-aula')?.pause();treinoAberto=false;postar({acao:'fila-infusao',contexto:estado.pagina?.contexto});return}
 if(b.dataset.aula){document.querySelector<HTMLVideoElement>('#video-aula')?.pause();postar({acao:'abrir-aula',chave:b.dataset.aula,contexto:estado.pagina?.contexto});return}
 if(b.dataset.capitulo){const v=document.querySelector<HTMLVideoElement>('#video-aula');if(v)v.currentTime=Number(b.dataset.capitulo);return}
 if(b.dataset.aulaReiniciar){const v=document.querySelector<HTMLVideoElement>('#video-aula');if(v)v.currentTime=0;postar({acao:'reiniciar-aula',chave:b.dataset.aulaReiniciar,contexto:estado.pagina?.contexto});return}
 if(b.dataset.aulaConcluir){postar({acao:'progresso-aula',chave:b.dataset.aulaConcluir,valor:{posicao:document.querySelector<HTMLVideoElement>('#video-aula')?.currentTime??0,concluida:true},contexto:estado.pagina?.contexto});return}
 if(b.hasAttribute('data-formato')){document.execCommand(b.dataset.formato!);const ed=b.closest('.editor-web')?.querySelector<HTMLElement>('[contenteditable]');if(ed)alterar(ed);return}
 if(b.dataset.grupoBotao!==undefined){abrirGrupo(grupo===b.dataset.grupoBotao?null:b.dataset.grupoBotao);return}
 if(b.hasAttribute('data-fechar-dialogo')){pendentes.clear();clearTimeout(temporizador);postar({acao:'dlg-fechar',id:estado.dialogo?.id});return}
 descarregar();
 if(b.dataset.comando){limparRascunhos(contexto(b).escopo);retorno=`[data-comando="${CSS.escape(b.dataset.comando)}"]`;const c=contexto(b);postar({acao:c.escopo==='dialogo'?'dlg-acao':'pagina-acao',chave:b.dataset.comando,id:c.escopo==='dialogo'?c.id:undefined,contexto:c.escopo==='pagina'?c.id:undefined,tabela:c.tabela,linha:c.linha});return}
 if(b.dataset.rota||b.hasAttribute('data-inicio')){e.preventDefault();document.querySelector<HTMLVideoElement>('#video-aula')?.pause();treinoAberto=false;postar({acao:'navegar',valor:b.dataset.rota??estado.rotas[0]?.chave,contexto:estado.pagina?.contexto});grupo=null;buscaAberta=false;render();return}
 if(b.dataset.sessao){postar({acao:b.dataset.sessao,contexto:estado.pagina?.contexto});usuarioAberto=false;render();return}
 if(b.hasAttribute('data-busca')){buscaAberta=!buscaAberta;grupo=null;render();document.getElementById('busca-tela')?.focus()}
 if(b.hasAttribute('data-fechar-busca')){buscaAberta=false;render()}
 if(b.hasAttribute('data-usuario')){usuarioAberto=!usuarioAberto;render()}
 if(b.dataset.irSecao)document.getElementById('secao-'+b.dataset.irSecao)?.scrollIntoView({block:'start',behavior:'smooth'});
 if(b.dataset.rolar||b.dataset.rolarDialogo)document.querySelector(b.dataset.rolarDialogo?'.dialogo-corpo':'.conteudo')?.scrollBy({top:(b.dataset.rolar??b.dataset.rolarDialogo)==='subir'?-400:400,behavior:'smooth'});
 if(b.dataset.rolarTabela)b.closest('.tabela-web')?.querySelector('.tabela-scroll')?.scrollBy({left:b.dataset.rolarTabela==='direita'?350:-350,behavior:'smooth'});
});
app.addEventListener('mousedown',e=>{if((e.target as Element).closest('[data-formato]'))e.preventDefault()});
app.addEventListener('input',e=>{const el=e.target as HTMLInputElement;if(filtroTreinamento(el)){render();return}if(el.dataset.campo)alterar(el);if(el.id==='busca-tela'){busca=el.value;render()}});
app.addEventListener('change',e=>{const el=e.target as HTMLInputElement;if(filtroTreinamento(el)){render();return}if(el.dataset.campo){alterar(el);descarregar()}});
document.addEventListener('focusin',e=>{if(!(e.target as Element).closest('.grupo-topo'))abrirGrupo(null)});
// Atalhos preservados da tela original; o botão mantém permissões e validações.
function executarAtalho(e:KeyboardEvent):boolean {
 const tecla=e.key.toLowerCase(),ctrl=e.ctrlKey||e.metaKey;
 const mapa:Record<string,Record<string,string>>={
  'faturamento-parametros':{s:'Salvar'},'faturamento-faturados':{p:'ExportarCsv',f5:'Buscar'},
  'faturamento-relatorios':{p:'ExportarCsv',f5:'Gerar'},'faturamento-tiss':{p:'Exportar',f5:'Atualizar'},
  'faturamento-guias':{f5:'Buscar'},'faturamento-pendencias':{f5:'Atualizar'},'faturamento-glosas':{f5:'Buscar',p:'GerarRecursoXml'},'faturamento-nc':{f5:'Atualizar'}
 };
 if(!(ctrl&&['s','p'].includes(tecla)||tecla==='f5'))return false;
 e.preventDefault();
 const chave=estado.dialogo?(ctrl&&tecla==='s'&&estado.dialogo.pagina.chave==='FaturamentoBaixa'?'confirmar':undefined):treinoAberto?undefined:mapa[estado.pagina?.chave??'']?.[tecla];
 if(chave)document.querySelector<HTMLButtonElement>(`${estado.dialogo?'.dialogo-rodape':'.conteudo'} button[data-comando="${CSS.escape(chave)}"]:not(:disabled)`)?.click();
 return true;
}
document.addEventListener('keydown',e=>{
 if(executarAtalho(e))return;
 if(estado.dialogo){if(e.key==='Escape'&&estado.dialogo.podeFechar){e.preventDefault();postar({acao:'dlg-fechar',id:estado.dialogo.id})}if(e.key==='Tab'){const el=[...document.querySelectorAll<HTMLElement>('.dialogo-web button:not(:disabled),.dialogo-web input:not(:disabled),.dialogo-web select:not(:disabled),.dialogo-web textarea:not(:disabled),.dialogo-web [contenteditable="true"],.dialogo-web [tabindex="0"]')].filter(n=>n.getClientRects().length);const i=el.indexOf(document.activeElement as HTMLElement);if(i<0||e.shiftKey&&i===0||!e.shiftKey&&i===el.length-1){e.preventDefault();el[e.shiftKey?el.length-1:0]?.focus()}}return}
 if(e.key==='Escape'){const anterior=grupo;abrirGrupo(null);buscaAberta=false;usuarioAberto=false;avisosAbertos=false;render();if(anterior!==null)document.getElementById('grupo-'+anterior)?.focus()}
 const g=(e.target as Element).closest<HTMLElement>('.grupo-topo');if(g&&['ArrowDown','ArrowUp','Home','End'].includes(e.key)){e.preventDefault();abrirGrupo(g.dataset.grupo!);const items=[...g.querySelectorAll<HTMLButtonElement>('.submenu-topo button')],i=items.indexOf(document.activeElement as HTMLButtonElement);items[e.key==='Home'?0:e.key==='End'?items.length-1:e.key==='ArrowUp'?(i<=0?items.length-1:i-1):(i+1)%items.length]?.focus()}
 if((e.ctrlKey||e.metaKey)&&['k','f'].includes(e.key.toLowerCase())){e.preventDefault();buscaAberta=true;render();document.getElementById('busca-tela')?.focus()}
});
ponte?.addEventListener('message',e=>{let recebido=e.data;if(typeof recebido==='string'){try{recebido=JSON.parse(recebido)}catch{return}}if(recebido?.tipo!=='estado'||!Array.isArray(recebido.rotas))return;const paginaMudou=recebido.pagina?.contexto!==estado.pagina?.contexto,dialogoMudou=recebido.dialogo?.id!==estado.dialogo?.id;if(paginaMudou)limparRascunhos('pagina');if(dialogoMudou)limparRascunhos('dialogo');estado=recebido;render();if(paginaMudou)document.querySelector('.conteudo')?.scrollTo(0,0);if(dialogoMudou)requestAnimationFrame(()=>{const el=estado.dialogo?document.querySelector<HTMLElement>('.dialogo-corpo input:not(:disabled),.dialogo-corpo textarea:not(:disabled),.dialogo-corpo select:not(:disabled),.dialogo-cabecalho button:not(:disabled)'):retorno?document.querySelector<HTMLElement>(retorno):null;el?.focus({preventScroll:true})})});
let menuCopia:HTMLElement|undefined;
const fecharCopia=()=>{menuCopia?.remove();menuCopia=undefined};
document.addEventListener('click',fecharCopia);
document.addEventListener('keydown',e=>{if(e.key==='Escape')fecharCopia()});
app.addEventListener('contextmenu',e=>{
 const alvo=(e.target as Element).closest<HTMLElement>('td,output,.indicador-web,.registro-cartao h4,.registro-cartao dd,.agenda-sessao strong,.agenda-sessao p,.agenda-sessao dd,.agenda-sessao time');if(!alvo)return;
 e.preventDefault();fecharCopia();const menu=document.createElement('div');menu.className='menu-copia';menu.setAttribute('role','menu');
 const opcoes:[string,string][]=[['Copiar valor',alvo.innerText]];const row=alvo.closest('tr'),table=alvo.closest('table');
 const linha=(tr:Element)=>[...tr.querySelectorAll('th,td')].filter(td=>!td.querySelector('button,input,select,textarea')).map(td=>td.textContent?.trim().replace(/\s+/g,' ')??'').join('\t');
 if(row)opcoes.push(['Copiar linha',linha(row)]);if(table)opcoes.push(['Copiar tabela',[...table.rows].map(linha).join('\n')]);
 const cartao=alvo.closest<HTMLElement>('[data-linha-id]'),lista=cartao?.closest('.registros-cartoes,.agenda-sessoes-dia');
 const textoCartao=(el:Element)=>[...el.querySelectorAll('h4,strong,time,p,dt,dd')].map(n=>n.textContent?.trim().replace(/\s+/g,' ')??'').filter(Boolean).join('\t');
 if(cartao)opcoes.push(['Copiar linha',textoCartao(cartao)]);if(lista)opcoes.push(['Copiar tabela',[...lista.querySelectorAll('[data-linha-id]')].map(textoCartao).join('\n')]);
 for(const [rotulo,texto] of opcoes){const b=document.createElement('button');b.textContent=rotulo;b.setAttribute('role','menuitem');b.onclick=async()=>{try{await navigator.clipboard.writeText(texto)}catch{const t=document.createElement('textarea');t.value=texto;document.body.append(t);t.select();document.execCommand('copy');t.remove()}fecharCopia()};menu.append(b)}
 document.body.append(menu);menu.style.left=Math.min(e.clientX,innerWidth-menu.offsetWidth-8)+'px';menu.style.top=Math.min(e.clientY,innerHeight-menu.offsetHeight-8)+'px';menuCopia=menu;menu.querySelector('button')?.focus();
});
render();postar({acao:'pronto'});
