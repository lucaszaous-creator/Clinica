import {Paper, Title, Button} from '@mantine/core';
import {createElement} from 'react';
import {motion,useReducedMotion} from 'motion/react';
import {ChevronDown,ChevronUp,Search,X,ArrowDown,ArrowUp,Bell,GraduationCap,ClipboardCheck,type IconNode} from 'lucide';
import {PaginaReact,HtmlReact} from './paginas-react';
import {RodapeDocumentoReact} from './documento-react';
import {RodapeInfusaoReact} from './infusao-react';
import {acoes,type Pagina,type Contexto} from './paginas';
import {treinamento,type Catalogo,type Aula} from './treinamento';

export type EstadoSuite={ferramentas?:{naoLidos:number;avisos:{mensagem:string;tipo:string;hora:string}[];filaInfusaoDisponivel:boolean;resumoAssinaturasInfusao:string;treinamentoDisponivel:boolean};treinamento?:Catalogo;aula?:Aula;videoUrl?:string;tipo:'estado';titulo:string;usuario:string;ocupado:boolean;erro?:string;rotas:{chave:string;rotulo:string;grupo:string}[];pagina?:Pagina & {contexto:string};dialogo?:{id:string;pagina:Pagina;ocupado:boolean;podeFechar:boolean};aviso?:{texto:string;tipo:string}};
type Props={estado:EstadoSuite;grupo:string|null;buscaAberta:boolean;usuarioAberto:boolean;busca:string;treinoAberto:boolean;avisosAbertos:boolean;ponteDisponivel:boolean};

function Icone({icone}:{icone:IconNode}){
 return <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{icone.map(([tag,props],i)=>createElement(tag,{...props,key:i}))}</svg>;
}

// A ponte mantém as regras no C#; o React conserva a identidade dos controles entre respostas.
export function SuiteReact({estado,grupo,buscaAberta,usuarioAberto,busca,treinoAberto,avisosAbertos,ponteDisponivel}:Props){
 const reduzirMovimento=useReducedMotion();
 const p=estado.pagina,d=estado.dialogo,grupos=[...new Set(estado.rotas.map(r=>r.grupo))];
 const ctx:Contexto={escopo:'pagina',id:p?.contexto??'',ocupado:estado.ocupado};
 return <>
  <header className="topbar" inert={!!d}>
   <a className="marca" href="#" data-inicio="" aria-label="Clínica SemDor — início"><img src="./logo-clinica.png" alt="Clínica SemDor"/></a>
   <nav className="navegacao-topo" aria-label="Navegação do aplicativo">{grupos.map((g,i)=><div className="grupo-topo" data-grupo={i} key={g}>
    <button id={'grupo-'+i} className={'nav-gatilho '+(estado.rotas.some(r=>r.grupo===g&&r.chave===p?.chave)?'ativo':'')} data-grupo-botao={i} aria-expanded={grupo===String(i)} aria-controls={'submenu-'+i}>{g}<Icone icone={ChevronDown}/></button>
    <div className="submenu-topo" id={'submenu-'+i} hidden={grupo!==String(i)}>{estado.rotas.filter(r=>r.grupo===g).map(r=><button key={r.chave} className={'rota '+(p?.chave===r.chave?'ativo':'')} data-rota={r.chave} aria-current={p?.chave===r.chave?'page':undefined}>{r.rotulo}</button>)}</div>
   </div>)}</nav>
   <button className="busca-global" data-treinamento="" aria-label="Treinamento"><Icone icone={GraduationCap}/></button>
   {estado.ferramentas?.filaInfusaoDisponivel&&<button className="busca-global" data-fila-infusao="" aria-label={estado.ferramentas.resumoAssinaturasInfusao}><Icone icone={ClipboardCheck}/></button>}
   <button className="busca-global" data-avisos="" aria-label="Notificações"><Icone icone={Bell}/>{!!estado.ferramentas?.naoLidos&&<small>{estado.ferramentas.naoLidos}</small>}</button>
   <button className="busca-global" data-busca="" aria-label="Pesquisar uma tela"><Icone icone={Search}/></button>
   <div className="usuario-area"><button className="usuario" data-usuario="" aria-expanded={usuarioAberto}><span className="avatar">{estado.usuario.slice(0,1)}</span><span className="nome-usuario">{estado.usuario}</span><Icone icone={ChevronDown}/></button>{usuarioAberto&&<div className="menu-usuario"><button className="botao" data-sessao="trocar-senha">Trocar minha senha</button><button className="botao" data-sessao="trocar-usuario">Trocar usuário</button></div>}</div>
  </header>
  <main className="conteudo" data-rota={p?.chave??''} tabIndex={-1} inert={!!d} aria-busy={estado.ocupado}>
   {estado.erro&&!d&&<div className="erro" role="alert">{estado.erro}</div>}
   {treinoAberto?<HtmlReact html={treinamento(estado.treinamento,estado.aula,estado.videoUrl)}/>:p?<PaginaReact key={p.contexto} pagina={p} contexto={ctx}/>:<div className="mensagem-web" role="status">{ponteDisponivel?'Carregando o aplicativo…':'Abra esta interface pelo aplicativo da clínica.'}</div>}
   <footer className="rodape-pagina"><span>{estado.titulo}</span><span>Clínica SemDor</span></footer>
  </main>
  <div className="atalhos-rolagem" inert={!!d}><button data-rolar="subir" aria-label="Rolar para cima"><Icone icone={ArrowUp}/></button><button data-rolar="descer" aria-label="Rolar para baixo"><Icone icone={ArrowDown}/></button></div>
  {buscaAberta&&<><div className="fundo-menu" data-fechar-busca=""/><div className="menu-expandido"><label className="busca-menu"><Icone icone={Search}/><input id="busca-tela" type="search" aria-label="Pesquisar tela" defaultValue={busca}/></label><div className="rotas">{estado.rotas.filter(r=>r.rotulo.toLocaleLowerCase('pt-BR').includes(busca.toLocaleLowerCase('pt-BR'))).map(r=><button key={r.chave} className="rota" data-rota={r.chave}>{r.rotulo}<small>{r.grupo}</small></button>)}</div></div></>}
  {d&&<div className="fundo-dialogo" key={d.id}><Paper renderRoot={props=><motion.section {...props} initial={reduzirMovimento?false:{y:8}} animate={{y:0}} transition={{duration:.18,ease:[.2,.7,.2,1]}}/>} className={`dialogo-web dialogo-react${d.pagina.chave==='PrescricaoInterna'?' dialogo-infusao':''}`} role="dialog" aria-modal="true" aria-labelledby="titulo-dialogo" aria-describedby={d.pagina.subtitulo?'descricao-dialogo':undefined} data-dialogo={d.id}>
   <header className="dialogo-cabecalho"><Title order={2} size="h4" id="titulo-dialogo">{d.pagina.titulo}</Title><Button variant="subtle" className="icone-botao" data-fechar-dialogo="" aria-label="Fechar formulário" disabled={!d.podeFechar}><Icone icone={X}/></Button></header>
   <div className="dialogo-corpo">{estado.erro&&<div className="erro" role="alert">{estado.erro}</div>}{d.pagina.subtitulo&&<p className="dialogo-descricao" id="descricao-dialogo">{d.pagina.subtitulo}</p>}<PaginaReact pagina={d.pagina} contexto={{escopo:'dialogo',id:d.id,ocupado:d.ocupado}}/></div>
   <footer className="dialogo-rodape"><div className="rolagem-dialogo"><button data-rolar-dialogo="subir" aria-label="Rolar formulário para cima"><Icone icone={ChevronUp}/></button><button data-rolar-dialogo="descer" aria-label="Rolar formulário para baixo"><Icone icone={ChevronDown}/></button></div><div className="acoes-web">{d.pagina.chave==='Documento'?<RodapeDocumentoReact pagina={d.pagina} contexto={{escopo:'dialogo',id:d.id,ocupado:d.ocupado}}/>:d.pagina.chave==='PrescricaoInterna'?<RodapeInfusaoReact pagina={d.pagina} contexto={{escopo:'dialogo',id:d.id,ocupado:d.ocupado}}/>:<HtmlReact html={acoes(d.pagina.acoes,{escopo:'dialogo',id:d.id,ocupado:d.ocupado})}/>}</div></footer>
  </Paper></div>}
  {avisosAbertos&&<aside className="painel-avisos" role="dialog" aria-label="Avisos desta sessão"><header><h2>Avisos</h2><button className="botao" data-avisos="">Fechar</button></header>{estado.ferramentas?.avisos.length?estado.ferramentas.avisos.map((a,i)=><article key={i}><time>{a.hora}</time><p>{a.mensagem}</p></article>):<p>Nenhum aviso nesta sessão.</p>}</aside>}
  {estado.aviso&&<div className={'toast '+(estado.aviso.tipo==='erro'?'toast-erro':'')} role="status">{estado.aviso.texto}</div>}
 </>;
}
