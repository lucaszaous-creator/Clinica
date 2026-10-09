import {useEffect,useState} from 'react';
import {createRoot} from 'react-dom/client';
import Agenda,{type EstadoAgenda} from './Agenda';
import Infusao,{type InfusaoEstado} from './Infusao';
import '@fontsource/inter/latin-400.css';
import '@fontsource/inter/latin-500.css';
import '@fontsource/inter/latin-600.css';
import './estilo.css';

type Envelope = {ocupado:boolean;erro?:string;contextoHost:string} & ({tela:'infusao';estado:InfusaoEstado}|{tela:'agenda'|'filtros-agenda';estado:EstadoAgenda});
declare global {interface Window {chrome?:{webview?:{postMessage:(m:unknown)=>void;addEventListener:(tipo:string,callback:(e:MessageEvent<Envelope>)=>void)=>void;removeEventListener:(tipo:string,callback:(e:MessageEvent<Envelope>)=>void)=>void}}}}
function App(){
 const [dados,setDados]=useState<Envelope>();
 useEffect(()=>{const ponte=window.chrome?.webview;const receber=(e:MessageEvent<Envelope>)=>setDados(e.data);ponte?.addEventListener('message',receber);ponte?.postMessage({acao:'pronto'});return()=>ponte?.removeEventListener('message',receber)},[]);
 if(!dados)return <p className="vazio" role="status">Aguardando os dados do aplicativo…</p>;
 const enviar=(m:Record<string,unknown>)=>window.chrome?.webview?.postMessage({...m,contextoHost:dados.contextoHost});
 return <>{dados.erro&&<div className="aviso" role="alert">{dados.erro}</div>}{dados.tela==='infusao'?<Infusao key={dados.contextoHost} estado={dados.estado} ocupado={dados.ocupado} enviar={enviar}/>:<Agenda somenteFiltros={dados.tela==='filtros-agenda'} key={dados.contextoHost} estado={dados.estado} ocupado={dados.ocupado} enviar={enviar}/>}</>;
}
createRoot(document.getElementById('root')!).render(<App/>);
