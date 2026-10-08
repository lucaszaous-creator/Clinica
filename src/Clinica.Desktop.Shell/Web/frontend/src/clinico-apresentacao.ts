import {campo, type Pagina, type Contexto, type Campo} from './paginas';
import {acoesClinicas} from './acoes-clinicas';
import './clinico-apresentacao.css';

const h=(v:unknown)=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
const identidade=new Set(['FotoWeb','Paciente','Contexto','Cabecalho.Linha']);

/** A identidade clínica tem lugar próprio; os demais campos continuam no formulário original. */
export function apresentacaoClinica(p:Pagina,c:Contexto):{cabecalho:string;campos:Campo[]}|null{
 const navegacao=acoesClinicas(p,c);
 if(navegacao===null)return null;
 const porChave=(chave:string)=>p.campos.find(f=>f.chave===chave&&f.visivel!==false);
 const paciente=porChave('Paciente'), identificacao=porChave('Cabecalho.Linha'), contexto=porChave('Contexto'), foto=porChave('FotoWeb');
 const fotoValida=typeof foto?.valor==='string'&&/^data:image\/(jpeg|png);base64,/.test(foto.valor);
 const render=(f:Campo|undefined)=>f?campo(f,c):'';
 const outroSubtitulo=p.subtitulo&&p.subtitulo!==String(paciente?.valor??'')?`<p class="clinico-subtitulo">${h(p.subtitulo)}</p>`:'';
 return {
  cabecalho:`<section class="clinico-cabecalho" aria-label="Paciente e navegação clínica"><div class="clinico-titulo"><h1>${h(p.titulo)}</h1>${outroSubtitulo}</div><div class="clinico-identidade">${fotoValida?`<div class="clinico-foto">${render(foto)}</div>`:''}<div class="clinico-identidade-texto"><div class="clinico-nome">${render(paciente)}</div><div class="clinico-identificacao">${render(identificacao)}</div><div class="clinico-contexto">${render(contexto)}</div></div></div>${navegacao}</section>`,
  campos:p.campos.filter(f=>!identidade.has(f.chave))
 };
}
