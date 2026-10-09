import {useId} from 'react';
import type {LinhaPagina} from './paginas';
import './agenda-situacao.css';
// GrupoSituacao é um contrato explícito do host, calculado a partir dos enums.
// Texto de observações, evolução ou rótulos livres nunca determina a situação.
export const gruposAgenda=['Todos','A atender','Em atendimento','Conclusão pendente','Concluídos','Cancelados','Faltas','Substituídos','Outros'] as const;
export type FiltroAgenda=typeof gruposAgenda[number];
export function grupoAgenda(l:LinhaPagina):FiltroAgenda {
 const grupo=l.celulas.GrupoSituacao;
 return gruposAgenda.slice(1).includes(grupo as FiltroAgenda)?grupo as FiltroAgenda:'Outros';
}
export const tomAgenda=(grupo:FiltroAgenda)=>grupo==='Concluídos'?'sucesso':grupo==='A atender'||grupo==='Conclusão pendente'?'atencao':grupo==='Cancelados'||grupo==='Faltas'?'erro':grupo==='Em atendimento'?'info':'neutro';
export const rotuloGrupoAgenda=(grupo:FiltroAgenda)=>grupo==='Concluídos'?'Concluído':grupo==='Cancelados'?'Cancelado':grupo==='Faltas'?'Faltou':grupo==='Substituídos'?'Substituído':grupo;
export function SituacaoAgenda({linha}:{linha:LinhaPagina}){
 const grupo=grupoAgenda(linha);
 return <span className="agenda-situacao" data-tom={tomAgenda(grupo)}>{rotuloGrupoAgenda(grupo)}</span>;
}
export function FiltrosSituacaoAgenda({linhas,valor,aoAlterar}:{linhas:LinhaPagina[];valor:FiltroAgenda;aoAlterar:(v:FiltroAgenda)=>void}){
 const contagens=new Map<FiltroAgenda,number>();for(const l of linhas){const grupo=grupoAgenda(l);contagens.set(grupo,(contagens.get(grupo)??0)+1)}
 return <div className="agenda-filtros-situacao" role="group" aria-label="Filtrar agenda por situação">{gruposAgenda.filter(g=>['Todos','A atender','Em atendimento','Concluídos'].includes(g)||contagens.has(g)||g===valor).map(g=><button type="button" key={g} data-agenda-situacao={g} data-tom={tomAgenda(g)} aria-pressed={valor===g} onClick={()=>aoAlterar(g)}><span className="agenda-situacao-ponto" aria-hidden="true"/>{g}<span className="agenda-filtro-contagem">{g==='Todos'?linhas.length:contagens.get(g)??0}</span></button>)}</div>;
}

const normalizarNome=(valor:string)=>valor.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('pt-BR').trim();
export function buscarPacienteAgenda(linhas:LinhaPagina[],busca:string){
 const termos=normalizarNome(busca).split(/\s+/).filter(Boolean);
 return termos.length?linhas.filter(l=>{const nome=normalizarNome(l.celulas.PacienteNome??l.celulas.Paciente??'');return termos.every(termo=>nome.includes(termo))}):linhas;
}
export function BuscaPacienteAgenda({valor,aoAlterar,periodo=false,quantidade}:{valor:string;aoAlterar:(valor:string)=>void;periodo?:boolean;quantidade:number}){
 const id=useId();return <div className="agenda-busca-paciente"><label htmlFor={id}>{periodo?'Buscar paciente na agenda exibida':'Buscar paciente na agenda do dia'}</label><div><input id={id} type="search" data-agenda-busca-paciente value={valor} onChange={e=>aoAlterar(e.currentTarget.value)} placeholder="Digite o nome do paciente" autoComplete="off"/><button type="button" className="botao secundario" data-agenda-limpar-busca disabled={!valor} onClick={()=>aoAlterar('')}>Limpar busca</button></div><small role="status" aria-live="polite">{quantidade} {quantidade===1?'horário encontrado':'horários encontrados'} nos filtros atuais</small></div>;
}
