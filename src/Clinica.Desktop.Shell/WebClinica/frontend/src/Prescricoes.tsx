import {useEffect, useRef, useState} from 'react';
import ControlesRolagem from './ControlesRolagem';
import './prescricoes.css';

type Acao = {chave:string;rotulo:string;habilitada:boolean;perigosa:boolean};
type Linha = {id:number;titulo:string;numero:string;data:string;descricao:string;situacao:string;detalheSituacao:string;grupo:string;detalhe:string;codigo:string;link:string;principal:string;acoes:Acao[]};
export type EstadoPrescricoes = {
 modo:'documentos'|'infusoes';contexto:string;pacienteId:number;paciente:string;mostrarCabecalho:boolean;selecionando:boolean;
 carregando:boolean;naoVerificado:boolean;mensagem?:string;mensagemEhErro:boolean;podeCriar:boolean;temInfusao:boolean;
 busca:{termo:string;buscando:boolean;erro?:string;ocioso:boolean;resumo:string;revisao:number;resultados:{id:number;nome:string;documento:string;convenio:string;nascimento?:string}[]};
 tipos?:{chave:string;rotulo:string;descricao:string}[];linhas:Linha[];
};
type Enviar = (mensagem:Record<string,unknown>)=>void;
type IconeNome = 'busca'|'pessoa'|'trocar'|'folha'|'infusao'|'mais'|'seta'|'relogio'|'copiar'|'atualizar'|'fechar'|'check'|'receita';
const caminhos:Record<IconeNome,string> = {
 busca:'m21 21-4.4-4.4 M19 10.5a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0',
 pessoa:'M20 21v-2a7 7 0 0 0-14 0v2 M17 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
 trocar:'M4 7h16m-4-4 4 4-4 4 M20 17H4m4-4-4 4 4 4',
 folha:'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M8 13h8 M8 17h5',
 infusao:'M9 3h6 M12 3v3 M8 6h8v9a4 4 0 0 1-8 0z M12 19v3 M10 10h4 M12 8v4',
 mais:'M12 5v14 M5 12h14',seta:'M5 12h14m-5-5 5 5-5 5',
 relogio:'M22 12A10 10 0 1 1 2 12a10 10 0 0 1 20 0 M12 6v6l4 2',
 copiar:'M8 8h12v13H8z M16 8V3H3v13h5',
 atualizar:'M20 7v5h-5 M4 17v-5h5 M6.1 6a8 8 0 0 1 13.2 3 M4.7 15a8 8 0 0 0 13.2 3',
 fechar:'m6 6 12 12 M6 18 18 6',check:'m5 12 4 4L19 6',
 receita:'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M8 17v-6h3a2 2 0 0 1 0 4H8 M11 15l5 5 M16 15l-5 5'
};
function Icone({nome}:{nome:IconeNome}){return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.65" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={caminhos[nome]}/></svg>}
const normalizar=(valor:string)=>valor.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('pt-BR');
const iniciais=(nome:string)=>nome.trim().split(/\s+/).filter(Boolean).filter((_,i,partes)=>i===0||i===partes.length-1).map(p=>p[0]).join('').slice(0,2);

function EscolhaPaciente({estado,ocupado,agir}:{estado:EstadoPrescricoes;ocupado:boolean;agir:Enviar}){
 const {busca}=estado;
 const [termo,setTermo]=useState(busca.termo);
 const campo=useRef<HTMLInputElement>(null);
 useEffect(()=>{window.scrollTo(0,0);campo.current?.focus({preventScroll:true})},[]);
 // Não sobrescreve digitação local com uma resposta anterior da ponte.
 const atual=termo===busca.termo;
 const escolher=(id:number)=>{if(atual&&!busca.buscando)agir({acao:'selecionar-paciente',id,termo,revisao:busca.revisao})};
 return <section className="rx-escolha rx-painel" aria-labelledby="rx-escolha-titulo">
  <div className="rx-secao-topo"><div className="rx-titulo-icone"><span className="rx-icone-bloco"><Icone nome="pessoa"/></span><div><h2 id="rx-escolha-titulo">{estado.pacienteId?'Trocar paciente':'Selecione o paciente'}</h2><p>{estado.pacienteId?`Em uso: ${estado.paciente}. Escolha quem deseja abrir.`:'Busque por nome ou CPF para começar.'}</p></div></div>{estado.pacienteId>0&&<button className="rx-botao rx-neutro" disabled={ocupado} onClick={()=>agir({acao:'voltar-paciente'})}>Manter paciente<Icone nome="fechar"/></button>}</div>
  <div className="rx-linha-busca"><label className="rx-campo"><span>Nome ou CPF do paciente</span><div className="rx-input-icone"><Icone nome="busca"/><input ref={campo} value={termo} maxLength={120} autoComplete="off" placeholder="Digite para encontrar um paciente" disabled={ocupado} onChange={e=>{setTermo(e.target.value);agir({acao:'campo',valor:e.target.value})}} onKeyDown={e=>{if(e.key==='Enter'&&atual&&!busca.buscando&&busca.resultados.length===1){e.preventDefault();escolher(busca.resultados[0].id)}}}/></div></label><button className="rx-botao rx-neutro" disabled={ocupado} onClick={()=>{setTermo('');agir({acao:'listar-pacientes'})}}>Todos os pacientes<Icone nome="seta"/></button></div>
  <div className="rx-resultados" aria-busy={busca.buscando||!atual}>
   {busca.erro?<p className="rx-erro" role="alert">{busca.erro}</p>:(!atual||busca.buscando)?<p className="rx-busca-status" role="status">Buscando pacientes…</p>:busca.resultados.length>0?<><p className="rx-legenda">{busca.resumo || `${busca.resultados.length} pacientes encontrados`} · selecione para abrir</p><ul>{busca.resultados.map(p=><li key={p.id}><button className="rx-resultado" disabled={ocupado} onClick={()=>escolher(p.id)}><span className="rx-avatar">{iniciais(p.nome)}</span><span className="rx-resultado-nome"><strong>{p.nome}</strong><span>{p.documento||'CPF não informado'}{p.nascimento&&` · ${p.nascimento}`}</span></span><span className="rx-convenio">{p.convenio||'Convênio não informado'}</span><span className="rx-abrir-paciente">Selecionar<Icone nome="seta"/></span></button></li>)}</ul></>:<p className="rx-busca-status" role="status">{busca.ocioso?'A busca usa os pacientes cadastrados na clínica.':'Nenhum paciente encontrado. Confira o nome ou CPF e tente novamente.'}</p>}
  </div>
 </section>;
}

function AcoesLinha({linha,bloqueado,agir}:{linha:Linha;bloqueado:boolean;agir:Enviar}){
 const menu=useRef<HTMLDetailsElement>(null);
 const principal=linha.acoes.find(a=>a.chave===linha.principal&&a.habilitada);
 const outras=linha.acoes.filter(a=>a.chave!==principal?.chave&&a.habilitada);
 useEffect(()=>{
  const fechar=(e:PointerEvent)=>{if(menu.current&&!menu.current.contains(e.target as Node))menu.current.open=false};
  document.addEventListener('pointerdown',fechar);return()=>document.removeEventListener('pointerdown',fechar);
 },[]);
 const executar=(acao:string)=>{if(menu.current)menu.current.open=false;agir({acao,id:linha.id})};
 return <div className="rx-acoes-linha">
  {principal&&<button className="rx-botao rx-neutro rx-acao-principal" disabled={bloqueado} onClick={()=>executar(principal.chave)}>{principal.rotulo}</button>}
  {outras.length>0&&<details ref={menu} className="rx-menu" onKeyDown={e=>{if(e.key==='Escape'&&menu.current){menu.current.open=false;menu.current.querySelector('summary')?.focus()}}}><summary aria-label={`Mais ações: ${linha.titulo} ${linha.numero}`}><svg viewBox="0 0 24 24" aria-hidden="true" fill="currentColor"><circle cx="5" cy="12" r="1.5"/><circle cx="12" cy="12" r="1.5"/><circle cx="19" cy="12" r="1.5"/></svg></summary><div className="rx-menu-itens">{outras.map(a=><button key={a.chave} className={a.perigosa?'rx-perigosa':''} disabled={bloqueado} onClick={()=>executar(a.chave)}>{a.rotulo}</button>)}</div></details>}
 </div>;
}

export default function Prescricoes({estado,ocupado,enviar}:{estado:EstadoPrescricoes;ocupado:boolean;enviar:Enviar}){
 const infusao=estado.modo==='infusoes';
 const [filtro,setFiltro]=useState('');const [situacao,setSituacao]=useState('');const [tipo,setTipo]=useState('');
 const [pacienteAnterior,setPacienteAnterior]=useState(estado.pacienteId);
 if(pacienteAnterior!==estado.pacienteId){setPacienteAnterior(estado.pacienteId);setFiltro('');setSituacao('');setTipo('')}
 const agir:Enviar=m=>enviar({...m,contexto:estado.contexto});
 const bloqueado=ocupado||!estado.podeCriar;
 const pronto=estado.pacienteId>0&&!estado.selecionando&&!estado.carregando&&!estado.naoVerificado;
 const tipos=[...new Set(estado.linhas.map(l=>l.titulo))];
 const situacoes=[...new Set(estado.linhas.map(l=>l.situacao))];
 const linhas=estado.linhas.filter(l=>(!situacao||l.situacao===situacao)&&(!tipo||l.titulo===tipo)&&normalizar(`${l.titulo} ${l.numero} ${l.descricao} ${l.codigo}`).includes(normalizar(filtro)));
 const temFiltros=Boolean(filtro||situacao||tipo);
 return <main className="prescricoes">
  <header className="rx-cabecalho"><div><p className="rx-sobretitulo">ATENDIMENTO CLÍNICO</p><h1>{infusao?'Prescrição de infusão':'Receitas e documentos'}</h1><p>{infusao?'Prescreva, acompanhe e consulte as infusões do paciente.':'Crie documentos e acompanhe o histórico do paciente em um só lugar.'}</p></div><span className="rx-marca-secao"><Icone nome={infusao?'infusao':'folha'}/></span></header>
  {estado.mostrarCabecalho&&(estado.selecionando?<EscolhaPaciente key={`${estado.pacienteId}:escolha`} estado={estado} ocupado={ocupado} agir={agir}/>:<section className="rx-paciente rx-painel" aria-label="Paciente selecionado"><span className="rx-avatar">{iniciais(estado.paciente)}</span><div><span className="rx-legenda">PACIENTE SELECIONADO</span><h2>{estado.paciente}</h2></div><button className="rx-botao rx-neutro" disabled={ocupado} onClick={()=>agir({acao:'trocar-paciente'})}><Icone nome="trocar"/>Trocar paciente</button></section>)}
  {estado.mensagem&&<p className={estado.mensagemEhErro?'rx-erro':'rx-mensagem'} role={estado.mensagemEhErro?'alert':'status'}>{estado.mensagem}</p>}
  <section className="rx-criacao" aria-labelledby="rx-criacao-titulo"><div className="rx-secao-topo"><div><h2 id="rx-criacao-titulo">{infusao?'Preparar prescrição':'Novo documento'}</h2><p>{pronto?'Escolha uma ação para continuar.':'Selecione um paciente para liberar as ações.'}</p></div>{!infusao&&estado.temInfusao&&<button className="rx-botao rx-link" disabled={bloqueado} onClick={()=>agir({acao:'ir-infusao'})}><Icone nome="infusao"/>Prescrição de infusão<Icone nome="seta"/></button>}</div>
   {infusao?<div className="rx-preparar rx-painel"><span className="rx-icone-bloco"><Icone nome="infusao"/></span><div><h3>Prescrição para a enfermagem</h3><p>Organize os itens da infusão e as orientações para a equipe de enfermagem.</p></div><div className="rx-preparar-acoes"><button className="rx-botao rx-neutro" disabled={bloqueado||estado.linhas.length===0} onClick={()=>agir({acao:'copiar-ultima'})}><Icone nome="copiar"/>Copiar última</button><button className="rx-botao" disabled={bloqueado} onClick={()=>agir({acao:'nova'})}><Icone nome="mais"/>Nova prescrição</button></div></div>:<div className="rx-tipos">{estado.tipos?.map((t,i)=><button key={t.chave} className="rx-tipo" disabled={bloqueado} onClick={()=>agir({acao:'emitir',tipo:t.chave})}><span className={`rx-icone-bloco rx-tom-${i%3}`}><Icone nome={i===0?'receita':'folha'}/></span><span><strong>{t.rotulo}</strong><small>{t.descricao}</small></span><Icone nome="mais"/></button>)}</div>}
  </section>
  <section className="rx-historico rx-painel" aria-labelledby="rx-historico-titulo" aria-busy={estado.carregando}>
   <div className="rx-secao-topo"><div className="rx-titulo-icone"><Icone nome="relogio"/><div><h2 id="rx-historico-titulo">{infusao?'Histórico de infusões':'Histórico de documentos'}{pronto&&<span className="rx-contador">{estado.linhas.length}</span>}</h2><p>{pronto?'Consulte os registros e as ações disponíveis para cada item.':'Os registros aparecem após a seleção do paciente.'}</p></div></div><button className="rx-botao rx-neutro rx-atualizar" disabled={ocupado||!estado.pacienteId||estado.selecionando||estado.carregando} onClick={()=>agir({acao:'atualizar'})}><Icone nome="atualizar"/>Atualizar</button></div>
   {pronto&&estado.linhas.length>0&&<div className="rx-filtros"><label className="rx-campo"><span>Buscar no histórico</span><div className="rx-input-icone"><Icone nome="busca"/><input value={filtro} onChange={e=>setFiltro(e.target.value)} placeholder={infusao?'Número, itens ou código de conferência':'Tipo, número ou profissional'}/></div></label>{!infusao&&<label><span>Tipo de documento</span><select value={tipo} onChange={e=>setTipo(e.target.value)}><option value="">Todos os tipos</option>{tipos.map(t=><option key={t}>{t}</option>)}</select></label>}<label><span>Situação</span><select value={situacao} onChange={e=>setSituacao(e.target.value)}><option value="">Todas as situações</option>{situacoes.map(s=><option key={s}>{s}</option>)}</select></label>{temFiltros&&<button className="rx-botao rx-link" onClick={()=>{setFiltro('');setSituacao('');setTipo('')}}>Limpar filtros</button>}</div>}
   {estado.carregando?<div className="rx-vazio" role="status"><Icone nome="relogio"/><h3>Carregando histórico…</h3><p>Aguarde enquanto buscamos os registros do paciente.</p></div>:estado.naoVerificado?<div className="rx-vazio" role="alert"><h3>Não foi possível carregar o histórico</h3><p>Use Atualizar para tentar novamente.</p></div>:!pronto?<div className="rx-vazio"><Icone nome="pessoa"/><h3>{estado.selecionando&&estado.pacienteId?'Escolha o próximo paciente':'Tudo começa pelo paciente'}</h3><p>Após selecionar, você poderá criar e consultar {infusao?'prescrições de infusão':'receitas e documentos'}.</p></div>:linhas.length===0?<div className="rx-vazio"><Icone nome={infusao?'infusao':'folha'}/><h3>{temFiltros?'Nenhum registro com esses filtros':infusao?'Nenhuma infusão prescrita':'Nenhum documento emitido'}</h3><p>{temFiltros?'Ajuste a busca ou limpe os filtros para ver o histórico.':infusao?'Use Nova prescrição para preparar a primeira folha deste paciente.':'Escolha um tipo de documento acima para começar.'}</p></div>:<><p className="rx-total" role="status">{linhas.length} de {estado.linhas.length} registros</p><div className="rx-colunas" aria-hidden="true"><span>{infusao?'Prescrição / itens':'Documento / profissional'}</span><span>Emissão</span><span>Situação</span><span>Ações</span></div><ul className="rx-lista">{linhas.map(l=><li className="rx-linha" key={l.id}><div className="rx-documento"><span className="rx-icone-documento"><Icone nome={infusao?'infusao':'folha'}/></span><div><h3>{l.titulo}<span>{l.numero}</span></h3><p>{l.descricao||'Profissional não informado'}</p>{l.detalhe&&<small>{l.detalhe}</small>}<small className="rx-codigo">Conferência: {l.codigo||'—'}</small></div></div><time>{l.data}</time><div className="rx-situacao"><span className={`rx-selo rx-estado-${normalizar(l.grupo).replace(/[^a-z-]/g,'')}`}><span/>{l.situacao}</span>{l.detalheSituacao&&<small>{l.detalheSituacao}</small>}{l.link&&<small>{l.link}</small>}</div><AcoesLinha linha={l} bloqueado={ocupado} agir={agir}/></li>)}</ul></>}
  </section><ControlesRolagem/>
 </main>;
}
