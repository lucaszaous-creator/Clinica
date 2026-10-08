import {acoes, campos, tabela, type Contexto, type Pagina, type Secao, type Tabela} from './paginas';
const h=(v:unknown)=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
const texto=(v:unknown,c:Contexto)=>h(c.privado?String(v??'').replace(/R\$\s*[-+−]?\s*[\d.,]+/g,'R$ ••••'):v);
const rotulos:Record<string,string>={MetaPaciente:'CPF e telefone',ConvenioPaciente:'Convênio',CarteirinhaPaciente:'Carteirinha',CategoriaPaciente:'Categoria',Duracao:'Duração em minutos',Sala:'Sala (opcional)',Hora:'Hora da sessão',Observacoes:'Observações',AvisoConsulta:'Validade da consulta',AvisoPendencias:'Pendências do paciente',NotaGuiaNaMarcacao:'Emissão das guias',AvisoCarteirinha:'Carteirinha',ResumoCurtoPrevia:'Resumo do atendimento',ValidadePaciente:'Validade',ExecutanteGuia:'Profissional executante',ResumoPrevia:'Prévia das guias',ResumoBaixas:'Resultado do lançamento',SaldoAutorizacao:'Saldo da autorização',CabecalhoDosAvisos:'Conflitos de horário',AvisoJaLancado:'Atendimento já lançado',AvisoHorarioDoDia:'Horário de hoje'};
/** Os títulos que já vinham do C# passam a estruturar o formulário, sem duplicar campos. */
export function camposDaPagina(p:Pagina,c:Contexto):string{
 if(p.chave!=='marcar-horario')return campos(p.campos,c);
 const usados=new Set<string>();
 const bloco=(titulo:string,chaves:string[],descricao?:string)=>{
  const fs=p.campos.filter(f=>chaves.includes(f.chave)&&f.visivel!==false);fs.forEach(f=>usados.add(f.chave));
  if(!fs.length)return '';
  return `<section class="passo-formulario"><header><h2>${h(titulo)}</h2>${descricao?`<p>${h(descricao)}</p>`:''}</header>${campos(fs.map(f=>({...f,rotulo:rotulos[f.chave]??f.rotulo,ajuda:f.chave==='Duracao'?'Deixe em branco para usar a duração padrão do profissional.':f.chave==='Observacoes'?'O recado acompanha o atendimento e a capa.':f.ajuda})),c)}</section>`;
 };
 const titulo=p.campos.find(f=>f.chave==='TituloTela'&&f.visivel!==false);
 const subtitulo=p.campos.find(f=>f.chave==='SubtituloTela'&&f.visivel!==false);
 for(const key of ['TituloTela','SubtituloTela','TituloPassoQuando','TituloPasso2','TituloResultado'])usados.add(key);
 const modalidades=p.secoes.find(s=>s.chave==='Cartoes');
 const chavesDados=['PacienteSelecionado.Nome','MetaPaciente','ConvenioPaciente','CarteirinhaPaciente','CategoriaPaciente'];
 const dados=p.campos.filter(f=>chavesDados.includes(f.chave)&&f.visivel!==false);
 dados.forEach(f=>usados.add(f.chave));
 const resumoDados=['PacienteSelecionado.Nome','MetaPaciente','ConvenioPaciente'].map(k=>dados.find(f=>f.chave===k)?.valor).filter(v=>String(v??'').trim()).join(' · ');
 const dadosPaciente=dados.length?`<details class="dados-paciente-resumo" data-preservar="${h(c.escopo+':'+c.id+':dados-paciente')}"><summary><span><strong>Dados do paciente</strong><small>${texto(resumoDados,c)}</small></span><span class="dados-paciente-conferir">Conferir dados<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg></span></summary>${campos(dados.map(f=>({...f,rotulo:rotulos[f.chave]??f.rotulo})),c)}</details>`:'';
 return `${titulo||subtitulo?`<div class="orientacao-formulario"><strong>${h(titulo?.valor)}</strong><p>${h(subtitulo?.valor)}</p></div>`:''}
 ${bloco('1 · Escolha o paciente',['Seletor.Termo','Seletor.Selecionado','Seletor.Erro','Seletor.ResumoDaLista'])}
 ${dadosPaciente}
 ${bloco('2 · Quando e com quem',['Data','Hora','Profissional','Duracao','Sala','ComoEncaixe','CabecalhoDosAvisos'])}
 ${modalidades?`<section class="passo-formulario"><header><h2>3 · O que será feito</h2><p>Escolha a modalidade e confira os detalhes da sessão.</p></header>${modalidades.tabelas.map(t=>tabela(t,c)).join('')}${campos(p.campos.filter(f=>['EspecialidadeSelecionada','PrimeiroCodigo','AtendimentoJaRealizado','CriarEncaixeSeparado'].includes(f.chave)).map(f=>({...f,rotulo:rotulos[f.chave]??f.rotulo})),c)}</section>`:bloco('3 · O que será feito',['EspecialidadeSelecionada','PrimeiroCodigo','AtendimentoJaRealizado','CriarEncaixeSeparado'])}
 ${(()=>{if(modalidades)for(const k of ['EspecialidadeSelecionada','PrimeiroCodigo','AtendimentoJaRealizado','CriarEncaixeSeparado'])usados.add(k);return ''})()}
 ${bloco('Observações',['Observacoes'])}
 ${bloco('Confira antes de confirmar',['AvisoJaLancado','AvisoHorarioDoDia','AvisoCarteirinha','AvisoConsulta','SaldoAutorizacao','AvisoPendencias','ValorPrevisto','ResumoCurtoPrevia','ValidadePaciente','ExecutanteGuia','ResumoPrevia','NotaGuiaNaMarcacao','ResumoBaixas'])}
 ${campos(p.campos.filter(f=>!usados.has(f.chave)),c)}`;
}
function valorCelula(v:string){
 if(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(v))return `${v.slice(8,10)}/${v.slice(5,7)}/${v.slice(0,4)} ${v.slice(11,16)}`;
 return v;
}
/** Em listas centradas no paciente, identidade e ações ficam visíveis em qualquer largura. */
export function listaResponsiva(t:Tabela,c:Contexto):string|null{
 const identidade=t.colunas.find(col=>['PacienteNome','Paciente','Nome','NomePaciente'].includes(col.chave));
 const ficha=t.chave==='Linhas'||t.colunas.length>=8;
 const modalidade=t.chave==='Cartoes';
 if(!modalidade&&(!identidade||!ficha))return null;
 const id=identidade??t.colunas[0];
 const prioridade=['Horario','DataHora','InicioISO','Data','Profissional','ContextoDaLista','Status','Situacao','ProximoContato','ProximoPasso','Responsavel','QuantasGuias','Quando'];
 const principais=t.colunas.filter(col=>col!==id&&prioridade.includes(col.chave)).slice(0,5);
 const detalhes=t.colunas.filter(col=>col!==id&&!principais.includes(col));
 return `<div class="tabela-web lista-responsiva ${modalidade?'modalidades-lista':''}" data-tabela-container="${h(t.chave)}"><div class="titulo-tabela"><h3>${h(t.titulo)}</h3><span>${t.linhas.length} registros</span></div><div class="registros-cartoes" data-testid="tabela-${h(t.chave)}">${t.linhas.length?t.linhas.map(l=>{
  const contexto={...c,tabela:t.chave,linha:l.id};
  const definicao=(cols:typeof t.colunas)=>cols.map(col=>`<div><dt>${h(col.rotulo)}</dt><dd>${texto(valorCelula(l.celulas[col.chave]??''),c)}</dd></div>`).join('');
  return `<article class="registro-cartao ${l.selecionada?'linha-selecionada':''}" data-linha-id="${h(l.id)}"><div class="registro-conteudo"><h4>${texto(l.celulas[id?.chave]??'Registro',c)}</h4><dl class="registro-resumo">${definicao(principais)}</dl>${detalhes.length?`<details class="registro-detalhes" data-preservar="${h(c.escopo+':'+c.id+':'+t.chave+':'+l.id)}"><summary>Ver detalhes</summary><dl>${definicao(detalhes)}</dl></details>`:''}${campos(l.campos,contexto)}</div><div class="acoes-na-linha">${acoes(l.acoes,contexto)}</div></article>`;
 }).join(''):`<div class="vazio"><strong>${h(t.vazio)}</strong></div>`}</div></div>`;
}
export function tabelasDaAgenda(s:Secao,c:Contexto):string|null{
 const temAgenda=s.tabelas.some(t=>t.chave==='horarios'||t.chave==='ClinicoSemanaSessoes');
 if(!temAgenda)return null;
 return s.tabelas.map(t=>`<details class="agenda-dados" data-preservar="${h(c.escopo+':'+c.id+':agenda-dados:'+t.chave)}"><summary>${h(t.titulo)} · ${t.linhas.length} registros</summary>${tabela(t,c)}</details>`).join('');
}
