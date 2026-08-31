import React from 'react';
import {Icon} from '../core/Icon.jsx';
export function Expander({title,defaultOpen,children,style}){
const[open,setOpen]=React.useState(!!defaultOpen);
return React.createElement('div',{style:{border:'1px solid var(--borda)',borderRadius:'var(--radius-control)',background:'var(--superficie)',fontFamily:'var(--font-ui)',...style}},
React.createElement('button',{type:'button',onClick:()=>setOpen(!open),style:{width:'100%',display:'flex',alignItems:'center',justifyContent:'space-between',gap:8,padding:'10px 12px',background:'transparent',border:'none',cursor:'pointer',fontSize:14,fontWeight:600,color:'var(--texto-primario)',fontFamily:'inherit'}},title,
React.createElement(Icon,{name:'chevron-down',size:16,style:{transform:open?'rotate(180deg)':'none',transition:'transform var(--duracao-normal)',color:'var(--texto-secundario)'}})),
open?React.createElement('div',{style:{padding:'0 12px 12px',fontSize:14,color:'var(--cinza-700)'}},children):null);
}