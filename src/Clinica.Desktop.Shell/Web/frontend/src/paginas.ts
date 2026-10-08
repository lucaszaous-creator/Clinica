import {acoesClinicas} from './acoes-clinicas';
import {controleEspecial} from './controles';
import {agenda} from './agenda';
export type Opcao = { valor: string; rotulo: string };
export type Campo = { chave: string; rotulo: string; tipo: string; valor: unknown; opcoes: Opcao[]; visivel: boolean; habilitado: boolean; obrigatorio: boolean; ajuda?: string; maximo?:number };
export type Acao = { chave: string; rotulo: string; habilitada: boolean; estilo: string; visivel: boolean };
export type Indicador = { rotulo: string; valor: string; detalhe?: string };
export type LinhaPagina = { id: string; celulas: Record<string, string>; campos: Campo[]; acoes: Acao[]; selecionada: boolean };
export type Tabela = { chave: string; titulo: string; colunas: { chave: string; rotulo: string; tipo: string }[]; linhas: LinhaPagina[]; vazio: string };
export type Grafico = { chave: string; rotulo: string; tipo: 'linha' | 'barra'; unidade: string; pontos: { rotulo: string; valor: number | null; valorFormatado: string }[] };
export type Secao = { chave: string; titulo: string; descricao?: string; campos: Campo[]; indicadores: Indicador[]; tabelas: Tabela[]; acoes: Acao[]; graficos?: Grafico[] | null };
export type Pagina = { chave: string; titulo: string; subtitulo?: string; campos: Campo[]; indicadores: Indicador[]; secoes: Secao[]; acoes: Acao[]; carregando: boolean; naoVerificado: boolean; mensagem?: string; mensagemEhErro: boolean; truncado: boolean };
export type Contexto = { escopo: 'pagina' | 'dialogo'; id: string; tabela?: string; linha?: string; ocupado?: boolean; privado?: boolean };

const h = (v: unknown) => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
const atributos = (c: Contexto) => `data-escopo="${c.escopo}" data-contexto="${h(c.id)}"${c.tabela ? ` data-tabela="${h(c.tabela)}"` : ''}${c.linha ? ` data-linha="${h(c.linha)}"` : ''}`;
const privado = (v: string, c: Contexto) => h(c.privado ? v.replace(/R\$\s*[-+−]?\s*[\d.,]+/g, 'R$ ••••') : v);
const rascunhos = new Map<string, unknown>();
const chaveRascunho = (c: Contexto, chave: string) => JSON.stringify([c.escopo, c.id, c.tabela, c.linha, chave]);
export function guardarRascunho(c: Contexto, chave: string, valor: unknown) { rascunhos.set(chaveRascunho(c, chave), valor); }
export function limparRascunhos(escopo?: Contexto['escopo']) {
  for (const key of rascunhos.keys()) if (!escopo || JSON.parse(key)[0] === escopo) rascunhos.delete(key);
}

export function acoes(itens: Acao[], contexto: Contexto, classe = '') {
  return itens.filter(a => a.visivel !== false).map(a => `<button type="button" class="botao ${['primario', 'perigo', 'secundario'].includes(a.estilo) ? a.estilo : ''} ${classe}" data-comando="${h(a.chave)}" ${atributos(contexto)} ${a.habilitada === false || contexto.ocupado ? 'disabled' : ''}>${h(a.rotulo)}</button>`).join('');
}

export function campo(f: Campo, c: Contexto): string {
  if (f.visivel === false) return '';
  const key = chaveRascunho(c, f.chave);
  // Uma resposta de outro campo não pode apagar o que está sendo digitado.
  if (rascunhos.has(key) && JSON.stringify(rascunhos.get(key) ?? '') === JSON.stringify(f.valor ?? '')) rascunhos.delete(key);
  const valor = rascunhos.has(key) ? rascunhos.get(key) : f.valor;
  const id = `campo-${encodeURIComponent(key)}`;
  const tipo = f.tipo.toLowerCase();
  if (['leitura', 'readonly', 'texto-estatico'].includes(tipo) && !String(valor ?? '').trim() && !f.ajuda) return '';
  const comum = `id="${h(id)}" data-campo="${h(f.chave)}" ${atributos(c)} ${f.habilitado === false || c.ocupado ? 'disabled' : ''} ${f.obrigatorio ? 'required aria-required="true"' : ''} ${f.ajuda ? `aria-describedby="${h(id)}-ajuda"` : ''}`;
  const ajuda = f.ajuda ? `<small id="${h(id)}-ajuda" class="ajuda-campo">${h(f.ajuda)}</small>` : '';
  const especial=controleEspecial(f,comum,id,valor);if(especial!==null)return especial;
  if(tipo==='imagem-leitura')return `<div class="campo-web"><label>${h(f.rotulo)}</label>${typeof valor==='string'&&/^data:image\/(jpeg|png);base64,/.test(valor)?`<img class="foto-previa" src="${h(valor)}" alt="Foto do paciente"/>`:'<span>Sem fotografia</span>'}</div>`;
  if (['checkbox', 'booleano', 'bool'].includes(tipo))
    return `<div class="campo-web campo-check"><label for="${h(id)}"><input type="checkbox" ${comum} ${valor === true || valor === 'true' ? 'checked' : ''}/><span>${h(f.rotulo)}</span></label>${ajuda}</div>`;
  let controle: string;
  if (tipo === 'texto-rico'||tipo==='texto-rico-leitura') {
    const v=valor as {texto?:string;formato?:string}|null;
    let trechos:{texto:string;negrito?:boolean;italico?:boolean}[]=[{texto:v?.texto??''}];
    try { const lista=JSON.parse(v?.formato||'null'); if(Array.isArray(lista)&&lista.every(t=>typeof t.texto==='string')&&lista.map(t=>t.texto).join('')===(v?.texto??''))trechos=lista; } catch { /* Texto canônico permanece legível. */ }
    return `<div class="editor-web"><label id="${h(id)}-rotulo">${h(f.rotulo)}</label><div class="editor-ferramentas" ${tipo==='texto-rico-leitura'?'hidden':''}><button type="button" data-formato="bold" aria-label="Negrito"><strong>N</strong></button><button type="button" data-formato="italic" aria-label="Itálico"><em>I</em></button></div><div ${comum} class="editor-conteudo" contenteditable="${tipo!=='texto-rico-leitura'&&f.habilitado!==false&&!c.ocupado}" role="textbox" aria-multiline="true" aria-labelledby="${h(id)}-rotulo">${trechos.map(t=>`${t.negrito?'<strong>':''}${t.italico?'<em>':''}${h(t.texto)}${t.italico?'</em>':''}${t.negrito?'</strong>':''}`).join('')}</div>${ajuda}</div>`;
  }
  if (tipo==='sugestao') controle=`<input type="text" ${comum} value="${h(valor)}" list="${h(id)}-opcoes" autocomplete="off"/><datalist id="${h(id)}-opcoes">${f.opcoes.map(o=>`<option value="${h(o.rotulo)}"></option>`).join('')}</datalist>`;
  else if (['select', 'selecao', 'enum'].includes(tipo) || f.opcoes.length)
    controle = `<select ${comum}>${f.opcoes.some(o => String(o.valor) === String(valor ?? '')) ? '' : '<option value="">Selecionar…</option>'}${f.opcoes.map(o => `<option value="${h(o.valor)}" ${String(o.valor) === String(valor ?? '') ? 'selected' : ''}>${h(o.rotulo)}</option>`).join('')}</select>`;
  else if (['textarea', 'multilinha'].includes(tipo)) controle = `<textarea ${comum} rows="4" maxlength="${f.maximo??5000}">${h(valor)}</textarea>`;
  else if (['leitura', 'readonly', 'texto-estatico'].includes(tipo)) controle = `<output id="${h(id)}">${privado(String(valor ?? ''), c)}</output>`;
  else {
    const htmlTipo = ({ data: 'date', date: 'date', mes: 'month', month: 'month', numero: 'number', number: 'number', busca: 'search', search: 'search', email: 'email', senha: 'password' } as Record<string, string>)[tipo] ?? 'text';
    controle = `<input type="${htmlTipo}" ${comum} value="${h(valor)}" ${htmlTipo === 'number' ? 'step="any"' : `maxlength="${f.maximo??5000}"`} ${['decimal', 'moeda', 'dinheiro'].includes(tipo) ? 'inputmode="decimal"' : ''} autocomplete="off"/>`;
  }
  return `<div class="campo-web ${['textarea', 'multilinha'].includes(tipo)||tipo==='leitura'&&String(valor??'').length>140 ? 'campo-largo' : ''}"><label for="${h(id)}">${h(f.rotulo)}${f.obrigatorio ? '<span class="obrigatorio" aria-hidden="true"> *</span>' : ''}</label>${controle}${ajuda}</div>`;
}
export const campos = (itens: Campo[], c: Contexto) => itens.some(f => f.visivel !== false) ? `<div class="campos-web">${itens.map(f => campo(f, c)).join('')}</div>` : '';
export function indicadores(itens: Indicador[], c: Contexto) {
  if (!itens.length) return '';
  return `<div class="indicadores-web">${itens.map(i => `<article class="indicador-web"><span>${h(i.rotulo)}</span><strong>${privado(i.valor, c)}</strong>${i.detalhe ? `<small>${privado(i.detalhe, c)}</small>` : ''}</article>`).join('')}</div>`;
}
export function tabela(t: Tabela, c: Contexto) {
  const possuiCampos = t.linhas.some(l => l.campos.some(f => f.visivel !== false));
  const possuiAcoes = t.linhas.some(l => l.acoes.some(a => a.visivel !== false));
  const colunas = t.colunas.length + Number(possuiCampos) + Number(possuiAcoes);
  return `<div class="tabela-web" data-tabela-container="${h(t.chave)}"><div class="titulo-tabela"><h3>${h(t.titulo)}</h3><span>${t.linhas.length} ${t.linhas.length === 1 ? 'registro' : 'registros'}</span></div>
    <div class="tabela-scroll" tabindex="0" aria-label="${h(t.titulo)}" data-rolavel><table data-testid="tabela-${h(t.chave)}"><thead><tr>${t.colunas.map(col => `<th class="${col.tipo === 'moeda' || col.tipo === 'numero' ? 'valor' : ''}" scope="col">${h(col.rotulo)}</th>`).join('')}${possuiCampos ? '<th scope="col">Preenchimento</th>' : ''}${possuiAcoes ? '<th scope="col">Ações</th>' : ''}</tr></thead><tbody>
    ${t.linhas.length ? t.linhas.map(l => `<tr data-linha-id="${h(l.id)}" class="${l.selecionada ? 'linha-selecionada' : ''}">${t.colunas.map(col => `<td class="${col.tipo === 'moeda' || col.tipo === 'numero' ? 'valor' : ''}">${col.tipo === 'status' ? `<span class="etiqueta-web">${privado(l.celulas[col.chave] ?? '', c)}</span>` : privado(l.celulas[col.chave] ?? '', c)}</td>`).join('')}${possuiCampos ? `<td class="campos-na-linha">${campos(l.campos, { ...c, tabela: t.chave, linha: l.id })}</td>` : ''}${possuiAcoes ? `<td><div class="acoes-na-linha">${acoes(l.acoes, { ...c, tabela: t.chave, linha: l.id })}</div></td>` : ''}</tr>`).join('') : `<tr><td colspan="${Math.max(colunas, 1)}" class="vazio"><strong>${h(t.vazio)}</strong><span>Os registros disponíveis aparecerão aqui.</span></td></tr>`}
    </tbody></table></div><div class="controle-tabela" hidden><button type="button" data-rolar-tabela="esquerda" aria-label="Rolar tabela para a esquerda">←</button><span>Mais colunas</span><button type="button" data-rolar-tabela="direita" aria-label="Rolar tabela para a direita">→</button></div></div>`;
}
export function secao(s: Secao, c: Contexto) {
  return `<section class="secao-web" id="secao-${h(s.chave)}" data-secao="${h(s.chave)}"><div class="cabecalho-secao"><div><h2>${h(s.titulo)}</h2>${s.descricao ? `<p>${privado(s.descricao, c)}</p>` : ''}</div><div class="acoes-web">${acoes(s.acoes, c)}</div></div>${campos(s.campos, c)}${indicadores(s.indicadores, c)}${graficos(s.graficos ?? [], c)}${agenda(s,c)}${s.tabelas.map(t => tabela(t, c)).join('')}</section>`;
}
function graficos(itens: Grafico[], c: Contexto) {
  if (!itens.length) return '';
  return `<div class="graficos-web">${itens.map(g => {
    const existentes = g.pontos.flatMap(p => p.valor !== null && Number.isFinite(p.valor) ? [p.valor] : []);
    if (!existentes.length) return `<figure class="grafico-web"><figcaption>${h(g.rotulo)}</figcaption><p class="grafico-sem-dados">Sem dados suficientes para apresentar esta série.</p></figure>`;
    if (g.tipo === 'barra') {
      const max = Math.max(...existentes.map(Math.abs), 1);
      return `<figure class="grafico-web"><figcaption>${h(g.rotulo)}</figcaption><div class="barras-web">${g.pontos.map(p => `<div class="barra-web"><span>${h(p.rotulo)}</span><div class="barra-trilho"><i style="width:${p.valor === null ? 0 : Math.min(100, Math.abs(p.valor) / max * 100)}%" class="${(p.valor ?? 0) < 0 ? 'negativa' : ''}"></i></div><strong>${privado(p.valorFormatado, c)}</strong></div>`).join('')}</div></figure>`;
    }
    const min = Math.min(0, ...existentes), max = Math.max(0, ...existentes);
    const amplitude = max - min || 1;
    const y = (v: number) => 150 - (v - min) / amplitude * 130;
    const x = (i: number) => 28 + i / Math.max(g.pontos.length - 1, 1) * 484;
    let caminho = '', conectar = false;
    g.pontos.forEach((p, i) => { if (p.valor === null || !Number.isFinite(p.valor)) { conectar = false; return; } caminho += `${conectar ? 'L' : 'M'}${x(i).toFixed(2)} ${y(p.valor).toFixed(2)} `; conectar = true; });
    return `<figure class="grafico-web"><figcaption>${h(g.rotulo)}</figcaption><svg viewBox="0 0 540 180" class="serie-web" role="img" aria-label="${h(g.rotulo)}. Valores detalhados na tabela abaixo."><line x1="28" y1="${y(0)}" x2="512" y2="${y(0)}" stroke="#e4e9f2" stroke-dasharray="3 5"/><path d="${caminho}" fill="none" stroke="#5576d7" stroke-width="2.4" stroke-linejoin="round"/>${g.pontos.map((p, i) => p.valor !== null && Number.isFinite(p.valor) ? `<circle cx="${x(i)}" cy="${y(p.valor)}" r="3.5" fill="#fff" stroke="#5576d7" stroke-width="2"><title>${h(p.rotulo)}: ${privado(p.valorFormatado, c)}</title></circle>` : '').join('')}</svg><div class="legenda-serie"><span>${h(g.pontos[0]?.rotulo)}</span><span>${h(g.pontos.at(-1)?.rotulo)}</span></div></figure>`;
  }).join('')}</div>`;
}
export function pagina(p: Pagina, c: Contexto) {
  const assinatura=p.chave==='AssinaturaPaciente';
  const termo=assinatura?p.campos.slice(0,4):p.campos, evidencia=assinatura?p.campos.slice(4):[];
  const miolo=assinatura?campos(termo,c)+p.secoes.map(s=>secao(s,c)).join('')+campos(evidencia,c):campos(p.campos,c);
  return `<div class="pagina-web" data-pagina="${h(p.chave)}" aria-busy="${p.carregando}"><section class="cabecalho-pagina"><div><div class="sobretitulo">FINANCEIRO</div><h1>${h(p.titulo)}</h1>${p.subtitulo ? `<p>${h(p.subtitulo)}</p>` : ''}</div><div class="acoes-web">${acoesClinicas(p,c)??acoes(p.acoes, c)}</div></section>
    ${p.naoVerificado ? '<div class="erro" role="alert">Não foi possível verificar os dados. Use Atualizar para tentar novamente.</div>' : ''}
    ${p.mensagem ? `<div class="${p.mensagemEhErro ? 'erro' : 'mensagem-web'}" role="${p.mensagemEhErro ? 'alert' : 'status'}">${privado(p.mensagem, c)}</div>` : ''}
    ${p.carregando ? '<div class="carregando-web" role="status">Atualizando dados…</div>' : ''}
    ${miolo}${indicadores(p.indicadores, c)}
    ${p.truncado ? '<div class="aviso-lista">Existem mais registros. Refine os filtros para localizar o que precisa.</div>' : ''}
    ${p.secoes.length > 1 ? `<nav class="ancoras-secoes" aria-label="Seções desta página">${p.secoes.map(s => `<button type="button" data-ir-secao="${h(s.chave)}">${h(s.titulo)}</button>`).join('')}</nav>` : ''}
    ${assinatura?'':p.secoes.map(s => secao(s, c)).join('')}</div>`;
}
