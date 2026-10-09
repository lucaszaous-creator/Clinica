import {useEffect, useRef} from 'react';
import {AcoesReact, CamposReact, TabelaReact} from './paginas-react';
import {ModeloAutocompleteReact} from './modelo-autocomplete-react';
import type {Contexto, Pagina} from './paginas';

export function DocumentoReact({pagina: p, contexto: c}: {pagina: Pagina; contexto: Contexto}) {
 const raiz = useRef<HTMLDivElement>(null);
 useEffect(() => {
  if (p.mensagemEhErro || !/^Modelo .*(aplicado|acrescentado)/.test(p.mensagem ?? '')) return;
  const frame = requestAnimationFrame(() => raiz.current?.querySelector('[data-campo="Corpo"]')?.scrollIntoView({block: 'center'}));
  return () => cancelAnimationFrame(frame);
 }, [p.mensagem, p.mensagemEhErro]);
 const campos = (chaves: string[]) => p.campos.filter(f => chaves.includes(f.chave));
 const acoes = (chaves: string[]) => p.acoes.filter(a => chaves.includes(a.chave));
 const tabelas = p.secoes.flatMap(s => s.tabelas);
 const itens = tabelas.find(t => t.chave === 'itens');
 const mostraItens = acoes(['AdicionarItem']).some(a => a.visivel);
 const modelo = campos(['ModeloSelecionado'])[0], aplicar = acoes(['AplicarModelo'])[0];
 return <div className="documento-composicao" ref={raiz}>
  <CamposReact campos={campos(['Subtitulo', 'TipoSelecionado', 'Profissional', 'Data'])} contexto={c}/>
  <section className="documento-modelos" aria-label="Usar modelo">
   {modelo && aplicar && <ModeloAutocompleteReact campo={modelo} acao={aplicar} contexto={c}/>}
   <details data-preservar={`${c.id}:previa-modelo`}><summary>Prévia do modelo</summary><CamposReact campos={campos(['PreviaModelo'])} contexto={c}/></details>
  </section>
  <CamposReact campos={campos(['Titulo', 'DiasAfastamentoTexto', 'PeriodoInicio', 'PeriodoFim', 'HoraChegadaTexto', 'HoraSaidaTexto', 'Corpo'])} contexto={c}/>
  {mostraItens && <section className="documento-itens" aria-label="Itens do documento">
   {itens?.linhas.map((linha, n) => {
    const contexto = {...c, tabela: itens.chave, linha: linha.id};
    return <div className="documento-item" key={linha.id}><header><h3>Item {n + 1}</h3><AcoesReact acoes={linha.acoes} contexto={contexto}/></header><CamposReact campos={linha.campos} contexto={contexto}/></div>;
   })}
   <AcoesReact acoes={acoes(['AdicionarItem'])} contexto={c}/>
  </section>}
  {tabelas.filter(t => t.chave !== 'itens' && t.linhas.length > 0).map(t => <TabelaReact key={t.chave} tabela={t} contexto={c}/>)}
  <CamposReact campos={campos(['AlergiaConferida'])} contexto={c}/>
  <details data-preservar={`${c.id}:observacoes`}><summary>Observações e CID</summary><CamposReact campos={campos(['Observacoes', 'Cid', 'CidAutorizado'])} contexto={c}/><AcoesReact acoes={acoes(['BuscarCid'])} contexto={c}/></details>
  <CamposReact campos={campos(['EnderecoDaReceita', 'ResponsavelDocumento'])} contexto={c}/>
  <details data-preservar={`${c.id}:gerir-modelos`}><summary>Salvar e organizar modelos</summary><div className="acoes-web">{acoes(['PedirNomeModelo', 'AtualizarModelo', 'ExcluirModelo']).map(a => <AcoesReact key={a.chave} acoes={[a]} contexto={c}/>)}</div></details>
 </div>;
}

export function RodapeDocumentoReact({pagina, contexto}: {pagina: Pagina; contexto: Contexto}) {
 return <>{['fechar', 'Emitir'].map(chave => <AcoesReact key={chave} acoes={pagina.acoes.filter(a => a.chave === chave)} contexto={contexto}/>)}</>;
}
