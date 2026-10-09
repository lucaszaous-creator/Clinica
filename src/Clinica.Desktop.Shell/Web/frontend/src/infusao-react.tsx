import {useEffect, useLayoutEffect, useRef, useState} from 'react';
import {AcoesReact, CampoReact, CamposReact} from './paginas-react';
import {ModeloAutocompleteReact} from './modelo-autocomplete-react';
import {obterValorCampo, type Campo, type Contexto, type Pagina} from './paginas';
import './infusao.css';

const normalizar = (texto: string) => texto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');

/** Filtra somente o catálogo enviado pelo host. Texto livre continua permitido;
 * a seleção envia apenas o nome, sem preencher dose ou decidir preparo. */
function MedicamentoReact({campo, contexto}: {campo: Campo; contexto: Contexto}) {
 const valor = String(obterValorCampo(campo, contexto) ?? '');
 const input = useRef<HTMLInputElement>(null);
 const [termo, setTermo] = useState(valor);
 const [aberto, setAberto] = useState(false);
 const [indice, setIndice] = useState(0);
 const id = `medicamento-${contexto.id}-${contexto.linha}`;
 const opcoes = termo.trim().length < 2 ? [] : campo.opcoes.filter(o => normalizar(o.rotulo).includes(normalizar(termo.trim())));
 const visivel = aberto && opcoes.length > 0 && !contexto.ocupado && campo.habilitado;
 const ativo = Math.min(indice, Math.max(0, opcoes.length - 1));
 useLayoutEffect(() => {
  if (input.current && document.activeElement !== input.current) {
   input.current.value = valor;
   setTermo(valor);
  }
 }, [valor]);
 const escolher = (nome: string) => {
  if (!input.current || input.current.disabled) return;
  input.current.value = nome;
  setTermo(nome); setAberto(false);
  input.current.dispatchEvent(new Event('input', {bubbles: true}));
  input.current.dispatchEvent(new Event('change', {bubbles: true}));
  setAberto(false);
 };
 return <div className="campo-web infusao-medicamento">
  <label htmlFor={id}>Medicamento <small>Digite 2 ou mais letras para buscar no cadastro</small></label>
  <input ref={input} id={id} type="text" defaultValue={valor} autoComplete="off" maxLength={campo.maximo ?? 4000}
   data-campo={campo.chave} data-escopo={contexto.escopo} data-contexto={contexto.id} data-tabela={contexto.tabela} data-linha={contexto.linha}
   disabled={contexto.ocupado || !campo.habilitado} role="combobox" aria-autocomplete="list" aria-expanded={visivel}
   aria-controls={visivel ? `${id}-lista` : undefined} aria-activedescendant={visivel ? `${id}-opcao-${ativo}` : undefined}
   onInput={e => {setTermo(e.currentTarget.value); setIndice(0); setAberto(true);}}
   onBlur={() => setAberto(false)}
   onKeyDown={e => {
    if (e.key === 'Escape' && aberto) {e.preventDefault(); e.stopPropagation(); setAberto(false);}
    else if (['ArrowDown', 'ArrowUp'].includes(e.key) && opcoes.length) {
     e.preventDefault(); setAberto(true);
     setIndice(aberto ? (ativo + (e.key === 'ArrowDown' ? 1 : -1) + opcoes.length) % opcoes.length : 0);
    } else if (e.key === 'Enter' && visivel) {e.preventDefault(); e.stopPropagation(); escolher(opcoes[ativo].rotulo);}
   }}/>
  {visivel && <div className="infusao-sugestoes" id={`${id}-lista`} role="listbox" aria-label="Medicamentos do cadastro">
   {opcoes.map((o, n) => <div id={`${id}-opcao-${n}`} key={o.valor} role="option" aria-selected={n === ativo}
    onMouseDown={e => e.preventDefault()} onClick={() => escolher(o.rotulo)}>{o.rotulo}</div>)}
  </div>}
 </div>;
}

/** Apresentação por infusão, conservando IDs e comandos das duas coleções do host. */
export function PrescricaoInfusaoReact({pagina: p, contexto: c}: {pagina: Pagina; contexto: Contexto}) {
 const raiz = useRef<HTMLDivElement>(null);
 useEffect(() => {
  if (p.mensagemEhErro || !p.mensagem?.startsWith('Modelo aplicado.')) return;
  const frame = requestAnimationFrame(() => {
   raiz.current?.querySelector('[data-infusao]')?.scrollIntoView({block: 'start'});
  });
  return () => cancelAnimationFrame(frame);
 }, [p.mensagem, p.mensagemEhErro]);
 const tabelas = p.secoes.flatMap(s => s.tabelas);
 const grupos = tabelas.find(t => t.chave === 'Infusoes');
 const itens = tabelas.find(t => t.chave === 'Itens');
 const campos = (chaves: string[]) => p.campos.filter(f => chaves.includes(f.chave));
 const [destinoModelo, setDestinoModelo] = useState('');
 const grupoModelo = grupos?.linhas.find(g => g.id === destinoModelo) ?? grupos?.linhas[0];
 const modelo = campos(['ModeloSelecionado'])[0], aplicar = grupoModelo?.acoes.find(a => a.chave === 'UsarModeloWeb');
 return <div className="prescricao-infusao" ref={raiz}>
  <div className="infusao-identificacao">
   <CamposReact campos={campos(['Paciente'])} contexto={c}/>
   <AcoesReact acoes={p.acoes.filter(a => a.chave === 'CopiarUltimaPrescricao')} contexto={c}/>
  </div>
  <CamposReact campos={campos(['AlertasTexto'])} contexto={c}/>
  <div className="infusao-indicacao"><CamposReact campos={campos(['Indicacao'])} contexto={c}/></div>
  {modelo && aplicar && grupos && grupoModelo && <section className="infusao-modelos" aria-label="Usar modelo de infusão">
   {grupos.linhas.length > 1 && <label className="modelo-destino">Aplicar à infusão <select value={grupoModelo.id} onChange={e=>setDestinoModelo(e.currentTarget.value)}>{grupos.linhas.map(g=><option key={g.id} value={g.id}>{g.celulas.Titulo}</option>)}</select></label>}
   <ModeloAutocompleteReact campo={modelo} acao={aplicar} contexto={c} contextoAcao={{...c,tabela:grupos.chave,linha:grupoModelo.id}}/>
  </section>}
  {grupos?.linhas.map(grupo => {
   const contextoGrupo = {...c, tabela: grupos.chave, linha: grupo.id};
   return <section className="infusao-bloco" key={grupo.id} data-infusao={grupo.id} aria-label={grupo.celulas.Titulo}>
    <header><h3>{grupo.celulas.Titulo}</h3><AcoesReact acoes={grupo.acoes.filter(a => a.chave === 'RemoverInfusao')} contexto={contextoGrupo}/></header>
    <div className="infusao-preparo"><CamposReact campos={grupo.campos} contexto={contextoGrupo}/></div>
    <div className="infusao-medicamentos">
     {itens?.linhas.filter(item => item.celulas.InfusaoRotulo === grupo.celulas.Titulo).map(item => {
      const contextoItem = {...c, tabela: itens.chave, linha: item.id};
      const medicamento = item.campos.find(f => f.chave === 'Descricao');
      const dose = item.campos.find(f => f.chave === 'Dose');
      return <div className="infusao-item" key={item.id} data-item-infusao={item.id}>
       {medicamento && <MedicamentoReact campo={medicamento} contexto={contextoItem}/>}
       {dose && <CampoReact campo={{...dose, rotulo: 'Quantidade / dose'}} contexto={contextoItem}/>}
       <div className="infusao-remover"><AcoesReact acoes={item.acoes} contexto={contextoItem}/></div>
       <details className="infusao-item-opcionais" data-preservar={`${c.id}:${item.id}:observacoes`}>
        <summary>Observações do medicamento / SOS</summary>
        <CamposReact campos={item.campos.filter(f => !['Descricao', 'Dose'].includes(f.chave))} contexto={contextoItem}/>
       </details>
      </div>;
     })}
    </div>
    <div className="infusao-acoes"><AcoesReact acoes={grupo.acoes.filter(a => a.chave === 'AcrescentarItem')} contexto={contextoGrupo}/></div>
   </section>;
  })}
  <div className="infusao-nova"><AcoesReact acoes={p.acoes.filter(a => a.chave === 'CriarInfusao')} contexto={c}/><span>Adiciona outra infusão com diluente e medicamentos próprios.</span></div>
  <details className="infusao-observacoes" data-preservar={`${c.id}:observacoes`}><summary>Observações</summary><CamposReact campos={campos(['Observacoes'])} contexto={c}/></details>
  <details className="infusao-gerir-modelos" data-preservar={`${c.id}:modelos`}>
   <summary>Salvar infusão como modelo</summary>
   {grupos?.linhas.map(grupo => <div className="acoes-web" key={grupo.id}><span>{grupo.celulas.Titulo}</span><AcoesReact acoes={grupo.acoes.filter(a => a.chave === 'SalvarModeloWeb')} contexto={{...c, tabela: grupos.chave, linha: grupo.id}}/></div>)}
  </details>
  <details className="infusao-dados" data-preservar={`${c.id}:dados`}><summary>Dados da prescrição / cadastro de medicamentos</summary><CamposReact campos={campos(['Numero', 'DataPrescricao', 'HoraPrescricao'])} contexto={c}/><AcoesReact acoes={p.acoes.filter(a => a.chave === 'RecarregarMedicamentos')} contexto={c}/></details>
 </div>;
}

export function RodapeInfusaoReact({pagina, contexto}: {pagina: Pagina; contexto: Contexto}) {
 return <>{['fechar', 'SalvarRascunho', 'Assinar'].map(chave => <AcoesReact key={chave} acoes={pagina.acoes.filter(a => a.chave === chave)} contexto={contexto}/>)}</>;
}
