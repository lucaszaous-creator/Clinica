import {useEffect, useRef, useState} from 'react';
import {obterValorCampo, type Acao, type Campo, type Contexto} from './paginas';
import {CamposReact} from './paginas-react';
import type {Pagina} from './paginas';

export function ModelosEvolucaoReact({pagina, contexto}: {pagina: Pagina; contexto: Contexto}) {
 const campo=pagina.campos.find(f=>f.chave==='Selecionado'), acao=pagina.acoes.find(a=>a.chave==='Aplicar');
 return <div className="modelos-evolucao">
  {campo && acao && <ModeloAutocompleteReact campo={campo} acao={acao} contexto={contexto}/>}
  <CamposReact campos={pagina.campos.filter(f=>f.chave!=='Selecionado')} contexto={contexto}/>
 </div>;
}

const normalizar = (s: string) => s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');

/** A sugestão escolhe o token do host e executa o comando normal. Digitar não aplica. */
export function ModeloAutocompleteReact({campo, acao, contexto, contextoAcao = contexto}: {
 campo: Campo; acao: Acao; contexto: Contexto; contextoAcao?: Contexto;
}) {
 const raiz = useRef<HTMLDivElement>(null), seletor = useRef<HTMLSelectElement>(null);
 const [termo, setTermo] = useState(''), [aberto, setAberto] = useState(false), [indice, setIndice] = useState(0);
 const id = `modelo-${contexto.id}-${campo.chave}`;
 const opcoes = campo.opcoes.filter(o => normalizar(o.rotulo).includes(normalizar(termo.trim())));
 const ativo = Math.min(indice, Math.max(0, opcoes.length - 1));
 const inativo = contexto.ocupado || campo.habilitado === false || acao.habilitada === false;
 const attrs = (c: Contexto) => ({'data-escopo': c.escopo, 'data-contexto': c.id, 'data-tabela': c.tabela, 'data-linha': c.linha});
 useEffect(() => {if (aberto) raiz.current?.querySelector('[aria-selected=true]')?.scrollIntoView({block: 'nearest'});}, [ativo, aberto]);
 const escolher = (valor: string, rotulo: string) => {
  if (!seletor.current || inativo) return;
  seletor.current.value = valor;
  seletor.current.dispatchEvent(new Event('change', {bubbles: true}));
  setTermo(rotulo); setAberto(false);
 };
 return <div className="campo-web modelo-autocomplete" ref={raiz}>
  <label htmlFor={id}>Buscar modelo pelo nome</label>
  <input id={id} data-busca-modelo type="text" autoComplete="off" placeholder="Digite e escolha um modelo para aplicar"
   value={termo} disabled={inativo} role="combobox" aria-autocomplete="list" aria-expanded={aberto && !inativo}
   aria-controls={aberto ? `${id}-lista` : undefined} aria-activedescendant={aberto && opcoes.length ? `${id}-${ativo}` : undefined}
   onInput={e => {setTermo(e.currentTarget.value);setIndice(0);setAberto(true);}}
   onFocus={() => setAberto(true)} onBlur={() => setAberto(false)}
   onKeyDown={e => {
    if(e.key==='Escape'){e.preventDefault();e.stopPropagation();setAberto(false);}
    else if(['ArrowDown','ArrowUp'].includes(e.key)){e.preventDefault();setAberto(true);setIndice(aberto ? (ativo+(e.key==='ArrowDown'?1:-1)+Math.max(1,opcoes.length))%Math.max(1,opcoes.length):0);}
    else if(e.key==='Enter' && aberto){e.preventDefault();e.stopPropagation();raiz.current?.querySelector<HTMLButtonElement>('[role=option][aria-selected=true]')?.click();}
   }}/>
  <select hidden aria-hidden="true" tabIndex={-1} ref={seletor} data-campo={campo.chave} {...attrs(contexto)} defaultValue={String(obterValorCampo(campo, contexto) ?? '')}>
   <option value=""/>{campo.opcoes.map(o => <option key={o.valor} value={o.valor}>{o.rotulo}</option>)}
  </select>
  {aberto && !inativo && <div className="modelo-sugestoes" id={`${id}-lista`} role="listbox" aria-label="Modelos disponíveis">
   {opcoes.length ? opcoes.map((o,n) => <button type="button" key={o.valor} id={`${id}-${n}`} role="option" aria-selected={n===ativo}
    data-comando={acao.chave} {...attrs(contextoAcao)} onMouseDown={e=>e.preventDefault()}
    onClickCapture={()=>escolher(String(o.valor),o.rotulo)}>{o.rotulo}<small>Aplicar</small></button>) : <p role="status">Nenhum modelo encontrado.</p>}
  </div>}
  <small>Selecione por clique ou use as setas e Enter para aplicar.</small>
 </div>;
}
