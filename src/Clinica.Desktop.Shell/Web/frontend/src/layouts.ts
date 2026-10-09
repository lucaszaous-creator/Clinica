import {celulaApresentada} from './status-celula';
import './tabelas-responsivas.css';
import './formulario-agendamento.css';
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
  return `<section class="passo-formulario" data-etapa="${h(titulo)}"><header><h2>${h(titulo)}</h2>${descricao?`<p>${h(descricao)}</p>`:''}</header>${campos(fs.map(f=>({...f,rotulo:rotulos[f.chave]??f.rotulo,ajuda:f.chave==='Duracao'?'Deixe em branco para usar a duração padrão do profissional.':f.chave==='Observacoes'?'O recado acompanha o atendimento e a capa.':f.ajuda})),c)}</section>`;
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
 return `<div class="form-agendamento">${titulo||subtitulo?`<div class="orientacao-formulario">${titulo&&String(titulo.valor)!==p.titulo?`<strong>${h(titulo.valor)}</strong>`:''}<p>${h(subtitulo?.valor)}</p></div>`:''}
 ${bloco('Paciente',['Seletor.Termo','Seletor.Selecionado','Seletor.Erro','Seletor.ResumoDaLista'])}
 ${dadosPaciente}
 ${bloco('Data, horário e profissional',['Data','Hora','Profissional','Duracao','Sala','ComoEncaixe','CabecalhoDosAvisos'])}
 ${modalidades?`<section class="passo-formulario" data-etapa="Modalidade"><header><h2>Modalidade do atendimento</h2><p>Escolha a modalidade e confira os detalhes da sessão.</p></header>${modalidades.tabelas.map(t=>tabela(t,c)).join('')}${campos(p.campos.filter(f=>['EspecialidadeSelecionada','PrimeiroCodigo','AtendimentoJaRealizado','CriarEncaixeSeparado'].includes(f.chave)).map(f=>({...f,rotulo:rotulos[f.chave]??f.rotulo})),c)}</section>`:bloco('Modalidade',['EspecialidadeSelecionada','PrimeiroCodigo','AtendimentoJaRealizado','CriarEncaixeSeparado'])}
 ${(()=>{if(modalidades)for(const k of ['EspecialidadeSelecionada','PrimeiroCodigo','AtendimentoJaRealizado','CriarEncaixeSeparado'])usados.add(k);return ''})()}
 ${bloco('Observações',['Observacoes'])}
 ${bloco('Confira antes de confirmar',['AvisoJaLancado','AvisoHorarioDoDia','AvisoCarteirinha','AvisoConsulta','SaldoAutorizacao','AvisoPendencias','ValorPrevisto','ResumoCurtoPrevia','ValidadePaciente','ExecutanteGuia','ResumoPrevia','NotaGuiaNaMarcacao','ResumoBaixas'])}
 ${campos(p.campos.filter(f=>!usados.has(f.chave)),c)}</div>`;
}
function valorCelula(v:string){
 if(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(v))return `${v.slice(8,10)}/${v.slice(5,7)}/${v.slice(0,4)} ${v.slice(11,16)}`;
 return v;
}
/** Reconhece também caminhos do contrato (Atendimento.Paciente.Nome) e rótulos. */
const normalizar=(s:string)=>s.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]/g,'');
export function tituloDaTabela(t:Tabela,c:Contexto & {tituloSecao?:string}):string{
 const repetido=normalizar(c.tituloSecao??'')===normalizar(t.titulo);
 return `<div class="titulo-tabela ${repetido?'titulo-tabela-repetido':''}"><h3>${h(t.titulo)}</h3><span>${t.linhas.length} ${t.linhas.length===1?'registro':'registros'}</span></div>`;
}
function filtrosProfissionais(t:Tabela,c:Contexto,renderizadores:{campos:typeof campos;acoes:typeof acoes}):string|null{
 if(t.chave!=='Profissionais'||!t.colunas.some(col=>col.chave==='Ativo')||!t.linhas.every(l=>l.acoes.some(a=>a.chave==='Filtrar')))return null;
 return `<div class="tabela-web profissionais-filtros" data-tabela-container="${h(t.chave)}">${tituloDaTabela(t,c)}<div class="profissionais-opcoes" data-testid="tabela-${h(t.chave)}">${t.linhas.map(l=>{
  const contexto={...c,tabela:t.chave,linha:l.id};
  const filtro=l.acoes.find(a=>a.chave==='Filtrar')!;
  const ativo=['true','sim'].includes((l.celulas.Ativo??'').trim().toLowerCase());
  const conteudo=`<strong data-coluna="Nome">${texto(l.celulas.Nome??'',c)}</strong><span data-coluna="Quantidade">${texto(l.celulas.Quantidade??'',c)} horários</span><small data-coluna="Ativo">${ativo?'Filtro ativo':'Ver agenda'}</small>`;
  const botao=renderizadores.acoes([filtro],contexto).replace(`>${h(filtro.rotulo)}</button>`,` aria-pressed="${ativo}" aria-label="${h(filtro.rotulo+': '+(l.celulas.Nome??''))}">${conteudo}</button>`);
  const outras=t.colunas.filter(col=>!['Nome','Quantidade','Ativo'].includes(col.chave));
  return `<div class="profissional-filtro ${ativo?'profissional-ativo':''}" data-linha-id="${h(l.id)}">${botao}${outras.map(col=>`<span data-coluna="${h(col.chave)}">${h(col.rotulo)}: ${celulaApresentada(col.tipo,l.celulas[col.chave]??'',c.privado)}</span>`).join('')}${renderizadores.campos(l.campos,contexto)}${renderizadores.acoes(l.acoes.filter(a=>a!==filtro),contexto)}</div>`;
 }).join('')||`<p class="vazio">${h(t.vazio)}</p>`}</div></div>`;
}
export function listaResponsiva(t:Tabela,c:Contexto,renderizadores={campos,acoes}):string|null{
 const profissionais=filtrosProfissionais(t,c,renderizadores);if(profissionais!==null)return profissionais;
 const identidade=t.colunas.find(col=>normalizar(col.rotulo)==='paciente'||['pacientenome','nomepaciente','paciente'].includes(normalizar(col.chave))||normalizar(col.chave).endsWith('pacientenome'))
  ??t.colunas.find(col=>normalizar(col.chave)==='nome');
 const modalidade=t.chave==='Cartoes';
 const preenchimento=t.linhas.some(l=>l.campos.some(f=>f.visivel!==false));
 const temAcoes=t.linhas.some(l=>l.acoes.some(a=>a.visivel!==false));
 // Relatórios comparativos numéricos continuam em tabela. Registros extensos ganham detalhe vertical.
 const numericas=t.colunas.filter(col=>['numero','moeda'].includes(col.tipo)||t.linhas.length>0&&t.linhas.every(l=>/^[\d.,% −+–-]+$/.test(l.celulas[col.chave]??''))).length;
 const comparativo=!temAcoes&&!preenchimento&&numericas>=t.colunas.length-2;
 if(!modalidade&&!preenchimento&&!(t.chave==='Linhas'||t.colunas.length>=7&&!comparativo))return null;
 const id=identidade??t.colunas[0];
 const prioridade=['horario','datahora','inicioiso','data','databaixa','dataprevista','profissional','contextodalista','status','situacao','situacaoguia','urgencia','numeroguiareal','numeroguia','tipocodigo','convenionome','tipo','proximocontato','proximopasso','responsavel','quantasguias','quando'];
 const restantes=t.colunas.filter(col=>col!==id);
 const preferidas=restantes.filter(col=>prioridade.includes(normalizar(col.chave.split('.').at(-1)??col.chave)));
 const principais=[...preferidas,...restantes.filter(col=>!preferidas.includes(col))].slice(0,modalidade?3:4);
 const detalhes=restantes.filter(col=>!principais.includes(col));
 return `<div class="tabela-web lista-responsiva ${modalidade?'modalidades-lista':'lista-compacta'}" data-tabela-container="${h(t.chave)}">${tituloDaTabela(t,c)}<div class="registros-cartoes" data-testid="tabela-${h(t.chave)}">${t.linhas.length?t.linhas.map(l=>{
  const contexto={...c,tabela:t.chave,linha:l.id};
  const definicao=(cols:typeof t.colunas)=>cols.map(col=>`<div data-coluna="${h(col.chave)}"><dt>${h(col.rotulo)}</dt><dd>${celulaApresentada(col.tipo,valorCelula(l.celulas[col.chave]??''),c.privado)}</dd></div>`).join('');
  const temCampos=l.campos.some(f=>f.visivel!==false);
  return `<article class="registro-cartao ${l.selecionada?'linha-selecionada':''}" data-linha-id="${h(l.id)}"><div class="registro-conteudo"><h4 data-coluna="${h(id?.chave)}">${celulaApresentada(id?.tipo??'texto',valorCelula(l.celulas[id?.chave]??'Registro'),c.privado)}</h4><dl class="registro-resumo">${definicao(principais)}</dl>${detalhes.length||temCampos?`<details class="registro-detalhes" data-preservar="${h(c.escopo+':'+c.id+':'+t.chave+':'+l.id)}"><summary>${temCampos?'Abrir detalhes e preenchimento':'Ver detalhes'}</summary>${detalhes.length?`<dl>${definicao(detalhes)}</dl>`:''}${renderizadores.campos(l.campos,contexto)}</details>`:''}</div><div class="acoes-na-linha">${renderizadores.acoes(l.acoes,contexto)}</div></article>`;
 }).join(''):`<div class="vazio"><strong>${h(t.vazio)}</strong></div>`}</div></div>`;
}
export function tabelasDaAgenda(s:Secao,c:Contexto):string|null{
 const temAgenda=s.tabelas.some(t=>t.chave==='horarios'||t.chave==='ClinicoSemanaSessoes');
 if(!temAgenda)return null;
 return s.tabelas.map(t=>`<details class="agenda-dados" data-preservar="${h(c.escopo+':'+c.id+':agenda-dados:'+t.chave)}"><summary>${h(t.titulo)} · ${t.linhas.length} registros</summary>${tabela(t,c)}</details>`).join('');
}
