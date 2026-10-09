import {useId,useMemo,useState} from 'react';

export interface LinhaAgenda {
 comando?:string; acoes?:{chave:string;rotulo:string;habilitada:boolean}[]; selos?:{texto:string;tom:string}[];
 id:string; data:string; hora:string; paciente:string; modalidade:string; profissional:string;
 sala:string; situacao:string; grupo:string; detalhe:string; registro:string; observacoes:string; acao:string; habilitada:boolean;
}
export interface EstadoAgenda {contexto:string; linhas:LinhaAgenda[]; carregando:boolean; naoVerificado:boolean; mensagem?:string}
export const situacoes=[['todos','Todos'],['pendente','Pendentes'],['no-local','No local'],['em-atendimento','Em atendimento'],['atendido','Atendidos'],['cancelado','Cancelados'],['faltou','Faltas'],['substituido','Substituídos']] as const;
const normalizar=(texto:string)=>texto.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('pt-BR');
export function filtrarAgenda(linhas:LinhaAgenda[],busca:string,modalidade:string,situacao:string){
 const termos=normalizar(busca).trim().split(/\s+/).filter(Boolean);
 return linhas.filter(l=>(!modalidade||l.modalidade===modalidade)&&(situacao==='todos'||l.grupo===situacao)&&termos.every(t=>normalizar(l.paciente).includes(t)));
}
export default function Agenda({estado,ocupado,enviar,somenteFiltros=false}:{estado:EstadoAgenda;ocupado:boolean;enviar:(m:Record<string,unknown>)=>void;somenteFiltros?:boolean}){
 const [busca,setBusca]=useState(''),[situacao,setSituacao]=useState('todos'),[modalidade,setModalidade]=useState('');
 const id=useId();
 const modalidades=useMemo(()=>[...new Set(estado.linhas.map(l=>l.modalidade))].filter(Boolean).sort((a,b)=>a.localeCompare(b,'pt-BR')),[estado.linhas]);
 const pesquisadas=useMemo(()=>filtrarAgenda(estado.linhas,busca,modalidade,'todos'),[estado.linhas,busca,modalidade]);
 const linhas= situacao==='todos'?pesquisadas:pesquisadas.filter(l=>l.grupo===situacao);
 const temFiltros=!!(busca||modalidade||situacao!=='todos');
 const alterar=(b:string,s:string,m:string)=>{setBusca(b);setSituacao(s);setModalidade(m);if(somenteFiltros)enviar({acao:'filtrar',busca:b,situacao:s,modalidade:m,contexto:estado.contexto})};
 const limpar=()=>alterar('','todos','');
 return <main className={`agenda ${somenteFiltros?'somente-filtros':'agenda-lista'}`} aria-busy={estado.carregando}>
  <div className="filtros-agenda"><label htmlFor={`${id}-busca`} className="busca-agenda">Buscar paciente<div className="campo-busca"><svg aria-hidden="true" width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></svg><input id={`${id}-busca`} type="search" value={busca} onChange={e=>alterar(e.target.value,situacao,modalidade)} placeholder="Digite o nome do paciente" autoComplete="off"/></div></label>
  <label htmlFor={`${id}-modalidade`}>Modalidade<select id={`${id}-modalidade`} value={modalidade} onChange={e=>alterar(busca,situacao,e.target.value)}><option value="">Todas as modalidades</option>{modalidades.map(m=><option key={m}>{m}</option>)}{modalidade&&!modalidades.includes(modalidade)&&<option>{modalidade}</option>}</select></label>
  <label className="situacao-compacta" htmlFor={`${id}-situacao`}>Situação<select id={`${id}-situacao`} value={situacao} onChange={e=>alterar(busca,e.target.value,modalidade)}>{situacoes.map(([chave,rotulo])=><option key={chave} value={chave}>{rotulo} ({chave==='todos'?pesquisadas.length:pesquisadas.filter(l=>l.grupo===chave).length})</option>)}</select></label>
  <button className="secundario" onClick={limpar} disabled={!temFiltros}>Limpar filtros</button></div>
  <div className="situacoes" role="group" aria-label="Filtrar por situação">{situacoes.map(([chave,rotulo])=><button key={chave} className={`filtro-situacao ${chave}`} aria-pressed={situacao===chave} onClick={()=>alterar(busca,chave,modalidade)}><span className="ponto" aria-hidden="true"/>{rotulo}<span className="contagem">{chave==='todos'?pesquisadas.length:pesquisadas.filter(l=>l.grupo===chave).length}</span></button>)}</div>
  <div className="resumo-agenda"><p role="status" aria-live="polite">{estado.carregando?'Atualizando agenda…':`${linhas.length} de ${estado.linhas.length} horários no período selecionado`}</p><span>Cores indicam a situação do atendimento.</span></div>
  {!somenteFiltros&&(estado.naoVerificado?<div className="aviso" role="alert">Não foi possível verificar a agenda. Use Atualizar para tentar novamente.</div>:estado.carregando?<div className="vazio">Carregando os horários do período…</div>:<>
  {!linhas.length?<section className="vazio"><h2>{temFiltros?'Nenhum paciente corresponde aos filtros':'Nenhum horário neste período'}</h2><p>{temFiltros?'Confira o nome, a modalidade ou a situação escolhida.':'Os agendamentos aparecerão aqui após serem registrados.'}</p>{temFiltros&&<button onClick={limpar}>Limpar filtros</button>}</section>:<div className={`lista-agenda ${linhas.some(l=>l.acoes?.length)?'lista-com-acoes':''}`}>{linhas.map(l=><article key={l.id} className={`linha-agenda ${l.grupo}`}>
   <div className="horario"><time>{l.hora}</time><span>{l.data}</span></div><div className="paciente"><h2>{l.paciente}</h2><p>{[l.modalidade,l.profissional,l.sala].filter(Boolean).join(' · ')}</p>{l.selos?.length? <div className="selos-agenda">{l.selos.map((s,i)=><span key={`${s.texto}-${i}`} className={`selo tom-${s.tom}`}>{s.texto}</span>)}</div>:null}{l.observacoes&&<details><summary>Observações</summary><p>{l.observacoes}</p></details>}</div>
   <div className="situacao"><span className={`selo ${l.grupo}`}><span className="ponto" aria-hidden="true"/>{l.situacao}</span>{l.detalhe&&<small>{l.detalhe}</small>}{l.registro&&<small>Prontuário: <strong>{l.registro}</strong></small>}</div>
   <div className="acoes-agenda">{l.acao&&<button className="abrir-agenda" disabled={ocupado||!l.habilitada} onClick={()=>enviar({acao:l.comando??'abrir',id:l.id,contexto:estado.contexto})} aria-label={`${l.acao}: ${l.paciente}, ${l.data} às ${l.hora}`}>{l.acao}<span aria-hidden="true"> →</span></button>}{l.acoes?.map(a=><button key={a.chave} className="abrir-agenda" disabled={ocupado||!a.habilitada} onClick={()=>enviar({acao:a.chave,id:l.id,contexto:estado.contexto})} aria-label={`${a.rotulo}: ${l.paciente}`}>{a.rotulo}</button>)}</div>
  </article>)}</div>}</>)}
 </main>;
}
