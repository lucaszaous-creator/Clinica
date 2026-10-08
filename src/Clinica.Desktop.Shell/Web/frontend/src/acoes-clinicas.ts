import {menuAcoes,acoesCompactas} from './acoes-menu';
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
 return menuAcoes(itens,c,titulo);
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
 const barra=`<div class="clinico-barra"><div class="clinico-voltar">${acoes(voltar,c)}</div><div class="clinico-menus">${menu('Documentos',docs,c)}${menu('Mais ações',outros,c)}</div><div class="clinico-fluxo">${acoesCompactas(fluxo,c,'','Atendimento')}</div></div>`;
 const navegacao=`<nav class="clinico-abas" aria-label="Seções do paciente">${abas.map(a=>{
  const ativa=destinos[a.chave]===p.chave;
  return acoes([a],c,`clinico-aba${ativa?' clinico-aba-ativa':''}`).replace('data-comando=',ativa?'aria-current="page" data-comando=':'data-comando=');
 }).join('')}</nav>`;
 return `<div class="clinico-acoes">${barra}${navegacao}</div>`;
}
