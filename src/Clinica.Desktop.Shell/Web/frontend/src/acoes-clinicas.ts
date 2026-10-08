import {acoes,type Pagina,type Contexto,type Acao} from './paginas';
import './acoes-clinicas.css';

const destinos:Record<string,string>={
 VerAtendimentoWeb:'consultorio-atendimento',VerEnfermagemWeb:'consultorio-atendimento-enfermagem',
 VerFichaWeb:'consultorio-paciente',VerHistoricoWeb:'consultorio-prontuario',
 VerExamesWeb:'consultorio-exames-anexos',VerDorWeb:'consultorio-evolucao-dor',
 VerMedidasWeb:'consultorio-medidas',VerAvaliacoesWeb:'consultorio-avaliacoes'
};
const documentos=new Set(['EmitirDocumentos','emitir-receita','emitir-atestado','emitir-comparecimento','emitir-exame','Atendimento.PrescreverInfusao','DocumentosPacienteWeb']);
const sessao=new Set(['IniciarSessao','ReabrirSessao','FinalizarSessao']);
function menu(titulo:string,itens:Acao[],c:Contexto){
 return itens.length?`<details class="clinico-menu"><summary>${titulo}<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg></summary><div class="clinico-menu-conteudo">${acoes(itens,c)}</div></details>`:'';
}
/** Apresenta cada ação existente uma única vez, conservando o contrato da ponte. */
export function acoesClinicas(p:Pagina,c:Contexto):string|null{
 if(c.escopo!=='pagina'||!Object.values(destinos).includes(p.chave))return null;
 const itens=p.acoes.filter(a=>a.visivel!==false);
 const abas=itens.filter(a=>a.chave in destinos);
 const voltar=itens.filter(a=>a.chave==='Voltar');
 const fluxo=itens.filter(a=>sessao.has(a.chave));
 const docs=itens.filter(a=>documentos.has(a.chave));
 const outros=itens.filter(a=>!(a.chave in destinos)&&a.chave!=='Voltar'&&!sessao.has(a.chave)&&!documentos.has(a.chave));
 return `<div class="clinico-acoes"><nav class="clinico-abas" aria-label="Seções do paciente">${abas.map(a=>{
  const ativa=destinos[a.chave]===p.chave;
  return acoes([a],c,`clinico-aba${ativa?' clinico-aba-ativa':''}`).replace('data-comando=',ativa?'aria-current="page" data-comando=':'data-comando=');
 }).join('')}</nav><div class="clinico-barra"><div class="clinico-voltar">${acoes(voltar,c)}</div><div class="clinico-menus">${menu('Documentos',docs,c)}${menu('Mais ações',outros,c)}</div><div class="clinico-fluxo">${acoes(fluxo,c)}</div></div></div>`;
}
// Details conserva Enter/Espaço nativos; Escape devolve o foco ao acionador.
document.addEventListener('keydown',e=>{
 if(e.key!=='Escape')return;
 const abertos=document.querySelectorAll<HTMLDetailsElement>('.clinico-menu[open]');
 if(!abertos.length)return;
 abertos.forEach(d=>d.open=false);
 abertos[abertos.length-1].querySelector<HTMLElement>('summary')?.focus();
});
