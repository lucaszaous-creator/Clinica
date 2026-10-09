import {useId, useLayoutEffect, useRef, useState} from 'react';
import './infusao.css';

type Enviar = (mensagem: Record<string, unknown>) => void;
type Trecho = {texto: string; negrito?: boolean; italico?: boolean};
type Item = {id: string; descricao: string; dose: string; dicaDose: string; observacoes: string; observacoesFormatadas: string | null; seNecessario: boolean};
type Grupo = {id: string; titulo: string; diluente: string; volume: string; via: string; tempo: string; horario: string; itens: Item[]};
type Modelo = {id: number; nome: string; previa: string};
export type InfusaoEstado = {
 paciente: string; numero: string; indicacao: string; indicacaoFormatada: string | null; observacoes: string; observacoesFormatadas: string | null;
 data: string; hora: string; ocupado: boolean; podeEditar: boolean; podeAssinar: boolean; operacao: string; mensagem: string | null; erro: boolean;
 liberacao: string; alertas: string[]; vias: {valor: string; rotulo: string}[]; medicamentos: {codigo: string; nome: string; apresentacao: string | null}[];
 modelos: Modelo[]; grupos: Grupo[];
};

function Campo({rotulo, valor, mudar, maximo = 100000, tipo = 'text', dica}: {
 rotulo: string; valor: string; mudar: (v: string) => void; maximo?: number; tipo?: string; dica?: string;
}) {
 const id = useId(), input = useRef<HTMLInputElement>(null);
 useLayoutEffect(() => { if (input.current && document.activeElement !== input.current) input.current.value = valor; }, [valor]);
 return <label className="inf-campo" htmlFor={id}><span>{rotulo}</span><input ref={input} id={id} type={tipo} defaultValue={valor} maxLength={maximo} placeholder={dica} onChange={e => mudar(e.currentTarget.value)}/></label>;
}

function trechosDoEditor(raiz: HTMLElement): Trecho[] {
 const trechos: Trecho[] = [];
 const adicionar = (texto: string, negrito = false, italico = false) => {
  if (!texto) return;
  const ultimo = trechos.at(-1);
  if (ultimo && ultimo.negrito === negrito && ultimo.italico === italico) ultimo.texto += texto;
  else trechos.push({texto, negrito, italico});
 };
 const visitar = (no: Node, negrito = false, italico = false) => {
  if (no.nodeType === Node.TEXT_NODE) {adicionar(no.textContent ?? '', negrito, italico); return;}
  if (!(no instanceof HTMLElement)) return;
  if (no.tagName === 'BR') {adicionar('\n'); return;}
  const bloco = ['DIV', 'P', 'LI'].includes(no.tagName);
  if (bloco && no.previousSibling && !trechos.at(-1)?.texto.endsWith('\n')) adicionar('\n');
  const estilo = getComputedStyle(no);
  no.childNodes.forEach(filho => visitar(filho, negrito || ['B', 'STRONG'].includes(no.tagName) || Number(estilo.fontWeight) >= 600,
   italico || ['I', 'EM'].includes(no.tagName) || estilo.fontStyle === 'italic'));
 };
 raiz.childNodes.forEach(no => visitar(no));
 return trechos;
}

/** Texto e dois estilos apenas; HTML nunca atravessa a ponte clínica. */
function TextoClinico({rotulo, texto, formato, mudar, desabilitado}: {
 rotulo: string; texto: string; formato: string | null; mudar: (texto: string, formato: string) => void; desabilitado: boolean;
}) {
 const id = useId(), editor = useRef<HTMLDivElement>(null);
 useLayoutEffect(() => {
  const campo = editor.current;
  if (!campo || document.activeElement === campo) return;
  let trechos: Trecho[] = [{texto}];
  try {const lista: Trecho[] = JSON.parse(formato ?? 'null'); if (Array.isArray(lista) && lista.every(t => typeof t?.texto === 'string') && lista.map(t => t.texto).join('') === texto) trechos = lista;} catch { /* O texto canônico prevalece. */ }
  campo.replaceChildren(...trechos.map(t => {const span = document.createElement('span'); span.textContent = t.texto; span.style.fontWeight = t.negrito ? '700' : '400'; span.style.fontStyle = t.italico ? 'italic' : 'normal'; return span;}));
 }, [texto, formato]);
 const guardar = () => {if (!editor.current) return; const lista = trechosDoEditor(editor.current); mudar(lista.map(t => t.texto).join(''), JSON.stringify(lista));};
 const formatar = (acao: 'bold' | 'italic') => {editor.current?.focus(); document.execCommand(acao); guardar();};
 return <div className="inf-campo"><span id={`${id}-rotulo`}>{rotulo}</span><div className="inf-editor">
  <div className="inf-formatacao" role="toolbar" aria-label={`Formatação de ${rotulo}`}>
   <button type="button" disabled={desabilitado} onMouseDown={e => e.preventDefault()} onClick={() => formatar('bold')} title="Negrito (Ctrl+B)"><strong>Negrito</strong></button>
   <button type="button" disabled={desabilitado} onMouseDown={e => e.preventDefault()} onClick={() => formatar('italic')} title="Itálico (Ctrl+I)"><em>Itálico</em></button>
  </div>
  <div ref={editor} className="inf-texto-rico" role="textbox" aria-multiline="true" aria-labelledby={`${id}-rotulo`} aria-disabled={desabilitado} contentEditable={!desabilitado} suppressContentEditableWarning onInput={guardar}
   onPaste={e => {e.preventDefault(); document.execCommand('insertText', false, e.clipboardData.getData('text/plain')); guardar();}}
   onKeyDown={e => {if ((e.ctrlKey || e.metaKey) && ['b', 'i'].includes(e.key.toLowerCase())) {e.preventDefault(); formatar(e.key.toLowerCase() === 'b' ? 'bold' : 'italic');}}}/>
 </div></div>;
}

function Medicamento({item, catalogo, mudar}: {item: Item; catalogo: InfusaoEstado['medicamentos']; mudar: (v: string) => void}) {
 const id = useId(), input = useRef<HTMLInputElement>(null);
 const [busca, setBusca] = useState(item.descricao), [aberto, setAberto] = useState(false), [indice, setIndice] = useState(0);
 useLayoutEffect(() => {if (input.current && document.activeElement !== input.current) {input.current.value = item.descricao; setBusca(item.descricao);}}, [item.descricao]);
 const normalizar = (v: string) => v.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
 const sugestoes = busca.trim().length < 2 ? [] : catalogo.filter(m => normalizar(`${m.nome} ${m.apresentacao ?? ''}`).includes(normalizar(busca.trim()))).slice(0, 30);
 const ativo = Math.min(indice, Math.max(0, sugestoes.length - 1));
 const visivel = aberto && sugestoes.length > 0;
 const escolher = (nome: string) => {if (input.current) input.current.value = nome; setBusca(nome); setAberto(false); mudar(nome);};
 return <label className="inf-campo inf-medicamento" htmlFor={id}><span>Medicamento</span>
  <input id={id} ref={input} defaultValue={item.descricao} maxLength={4000} placeholder="Busque por nome ou escreva livremente" autoComplete="off" role="combobox" aria-autocomplete="list" aria-expanded={visivel} aria-controls={visivel ? `${id}-lista` : undefined} aria-activedescendant={visivel ? `${id}-${ativo}` : undefined}
   onChange={e => {setBusca(e.currentTarget.value); setAberto(true); setIndice(0); mudar(e.currentTarget.value);}} onBlur={() => setAberto(false)}
   onKeyDown={e => {
    if (e.key === 'Escape') {e.preventDefault(); e.stopPropagation(); setAberto(false);}
    if (['ArrowDown', 'ArrowUp'].includes(e.key) && sugestoes.length) {e.preventDefault(); setAberto(true); setIndice((ativo + (e.key === 'ArrowDown' ? 1 : -1) + sugestoes.length) % sugestoes.length);}
    if (e.key === 'Enter' && visivel) {e.preventDefault(); escolher(sugestoes[ativo].nome);}
   }}/>
  {visivel && <div className="inf-sugestoes" id={`${id}-lista`} role="listbox" aria-label="Medicamentos cadastrados">{sugestoes.map((m, n) => <div key={m.codigo} id={`${id}-${n}`} role="option" aria-selected={n === ativo} onMouseDown={e => e.preventDefault()} onClick={() => escolher(m.nome)}>{m.nome}{m.apresentacao && <small>{m.apresentacao}</small>}</div>)}</div>}
 </label>;
}

function Modelos({grupo, modelos, enviar}: {grupo: Grupo; modelos: Modelo[]; enviar: Enviar}) {
 const id = useId(), [busca, setBusca] = useState(''), [nome, setNome] = useState(''), [salvando, setSalvando] = useState(false);
 const filtrados = modelos.filter(m => m.nome.toLocaleLowerCase('pt-BR').includes(busca.toLocaleLowerCase('pt-BR')));
 return <div className="inf-modelos"><details><summary>Usar modelo salvo <span className="inf-contagem">{modelos.length}</span></summary><div className="inf-modelos-conteudo">
  <label className="inf-campo" htmlFor={id}><span>Buscar modelo</span><input id={id} value={busca} onChange={e => setBusca(e.currentTarget.value)} placeholder="Nome do modelo"/></label>
  <div className="inf-modelos-lista">{filtrados.map(m => <button type="button" key={m.id} onClick={() => enviar({acao: 'aplicarModelo', grupo: grupo.id, modelo: m.id})}><strong>{m.nome}</strong><small>{m.previa || 'Aplicar preparo e medicamentos à infusão'}</small><span>Usar e revisar</span></button>)}</div>
  {!filtrados.length && <p>{modelos.length ? 'Nenhum modelo encontrado.' : 'Nenhum modelo cadastrado. Você pode salvar esta infusão como modelo.'}</p>}
 </div></details><button type="button" className="inf-acao" onClick={() => setSalvando(!salvando)} aria-expanded={salvando}>Salvar esta infusão como modelo</button>
 {salvando && <div className="inf-salvar-modelo"><label className="inf-campo"><span>Nome do modelo</span><input maxLength={100} value={nome} onChange={e => setNome(e.currentTarget.value)} placeholder="Nome para encontrar depois"/></label><p>Guarda o preparo e os medicamentos, sem dados do paciente ou horário.</p><button type="button" className="inf-primario" disabled={!nome.trim()} onClick={() => enviar({acao: 'salvarModelo', grupo: grupo.id, nome})}>Salvar modelo</button><button type="button" onClick={() => setSalvando(false)}>Cancelar</button></div>}
 </div>;
}

export default function Infusao({estado: e, enviar, ocupado}: {estado: InfusaoEstado; enviar: Enviar; ocupado: boolean}) {
 const bloqueado = ocupado || e.ocupado || !e.podeEditar;
 const campo = (chave: string, valor: unknown, extra: Record<string, unknown> = {}) => enviar({acao: 'campo', campo: chave, valor, ...extra});
 return <main className="infusao-pagina" aria-busy={ocupado || e.ocupado}>
  <header className="inf-cabecalho"><div><p className="inf-contexto">Documentos / Prescrição de infusão</p><h1>Prescrição de infusão</h1><p className="inf-paciente">{e.paciente}</p><small>{e.numero}</small></div><button type="button" className="inf-acao" disabled={bloqueado} onClick={() => enviar({acao: 'copiarUltima'})}>Copiar última prescrição deste paciente</button></header>
  {(e.ocupado || ocupado) && <div role="status" className="inf-aviso">{e.ocupado ? e.operacao : 'Aguarde…'}</div>}
  {!!e.alertas.length && <section className="inf-alertas" aria-label="Alertas do paciente"><strong>Confira antes de prescrever</strong>{e.alertas.map((a, n) => <p key={n}>{a}</p>)}</section>}
  {e.mensagem && <div className={`inf-aviso ${e.erro ? 'inf-erro' : ''}`} role={e.erro ? 'alert' : 'status'}>{e.mensagem}</div>}
  <fieldset disabled={bloqueado} className="inf-conteudo"><legend className="inf-sr">Editar prescrição</legend>
   <TextoClinico rotulo="Indicação" texto={e.indicacao} formato={e.indicacaoFormatada} desabilitado={bloqueado} mudar={(v, formato) => campo('indicacao', v, {formato})}/>
   {e.grupos.map(g => <section className="inf-grupo" key={g.id} aria-label={g.titulo}>
    <header><div><span className="inf-etiqueta">Preparo e medicamentos</span><h2>{g.titulo}</h2></div><button type="button" className="inf-perigo" onClick={() => enviar({acao: 'removerInfusao', grupo: g.id})}>Remover infusão</button></header>
    <Modelos grupo={g} modelos={e.modelos} enviar={enviar}/>
    <div className="inf-preparo"><Campo rotulo="Diluente" valor={g.diluente} maximo={120} mudar={v => campo('diluente', v, {grupo: g.id})}/><Campo rotulo="Volume (mL)" valor={g.volume} maximo={60} mudar={v => campo('volume', v, {grupo: g.id})}/><label className="inf-campo"><span>Via de administração</span><select value={g.via} onChange={event => campo('via', event.currentTarget.value, {grupo: g.id})}>{e.vias.map(v => <option key={v.valor} value={v.valor}>{v.rotulo}</option>)}</select></label><Campo rotulo="Tempo" valor={g.tempo} maximo={60} mudar={v => campo('tempo', v, {grupo: g.id})}/><Campo rotulo="Horário (opcional)" valor={g.horario} maximo={5} dica="HH:mm" mudar={v => campo('horario', v, {grupo: g.id})}/></div>
    <div className="inf-itens">{g.itens.map(i => <div className="inf-item" key={i.id}>
     <div className="inf-item-linha"><Medicamento item={i} catalogo={e.medicamentos} mudar={v => campo('descricao', v, {grupo: g.id, item: i.id})}/><Campo rotulo="Quantidade / dose" valor={i.dose} maximo={60} dica={i.dicaDose} mudar={v => campo('dose', v, {grupo: g.id, item: i.id})}/><button type="button" className="inf-perigo" aria-label={`Remover ${i.descricao || 'medicamento'}`} onClick={() => enviar({acao: 'removerItem', grupo: g.id, item: i.id})}>Remover</button></div>
     <details className="inf-opcoes"><summary>Observações do medicamento / SOS</summary><label className="inf-checkbox"><input type="checkbox" checked={i.seNecessario} onChange={event => campo('seNecessario', event.currentTarget.checked, {grupo: g.id, item: i.id})}/> Se necessário (SOS)</label><TextoClinico rotulo="Observação do medicamento" texto={i.observacoes} formato={i.observacoesFormatadas} desabilitado={bloqueado} mudar={(v, formato) => campo('observacoes', v, {grupo: g.id, item: i.id, formato})}/></details>
    </div>)}</div>
    <p className="inf-ajuda">Busque medicamentos a partir de 2 letras. A dose e o preparo são preenchidos pelo profissional.</p>
    <button type="button" className="inf-acao" onClick={() => enviar({acao: 'adicionarItem', grupo: g.id})}>Adicionar medicamento</button>
   </section>)}
   <button type="button" className="inf-nova" onClick={() => enviar({acao: 'criarInfusao'})}>Adicionar outra infusão <small>Com preparo e medicamentos próprios</small></button>
   <details className="inf-complemento"><summary>Observações gerais</summary><TextoClinico rotulo="Observações" texto={e.observacoes} formato={e.observacoesFormatadas} desabilitado={bloqueado} mudar={(v, formato) => campo('observacoes', v, {formato})}/></details>
   <details className="inf-complemento"><summary>Data, hora e cadastro de medicamentos</summary><div className="inf-data"><Campo rotulo="Data da prescrição" valor={e.data} tipo="date" mudar={v => campo('data', v)}/><Campo rotulo="Hora da prescrição" valor={e.hora} maximo={5} dica="HH:mm" mudar={v => campo('hora', v)}/><button type="button" className="inf-acao" onClick={() => enviar({acao: 'recarregarMedicamentos'})}>Atualizar catálogo de medicamentos</button></div></details>
  </fieldset>
  <footer className="inf-rodape"><button type="button" disabled={ocupado || e.ocupado} onClick={() => enviar({acao: 'fechar'})}>Fechar</button><p>Salvar rascunho mantém a prescrição em edição.</p><button type="button" className="inf-acao" disabled={bloqueado} onClick={() => enviar({acao: 'salvar'})}>Salvar rascunho</button><button type="button" className="inf-primario" disabled={bloqueado || !e.podeAssinar} onClick={() => enviar({acao: 'assinar'})}>{e.liberacao}</button></footer>
 </main>;
}
