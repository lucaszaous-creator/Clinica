import React from 'react';
import {Icon} from './Icon.jsx';
export function SearchInput({value,onChange,placeholder='Pesquisar…',pill,width,style}){
  const [f,setF]=React.useState(false);
  return <div style={{display:'flex',alignItems:'center',gap:8,width,
    padding:'0 12px',height:36,background:'#fff',boxSizing:'border-box',
    border:'1px solid '+(f?'var(--focus-ring)':'var(--border)'),
    boxShadow:f?'0 0 0 2px var(--brand-soft)':'none',
    borderRadius:pill?'var(--radius-pilula)':'var(--radius-pequeno)',...style}}>
    <Icon name="search" size={16} style={{color:'var(--text-muted)'}}/>
    <input value={value} placeholder={placeholder} onFocus={()=>setF(true)} onBlur={()=>setF(false)}
      onChange={e=>onChange&&onChange(e.target.value)}
      style={{border:'none',outline:'none',flex:1,fontFamily:'var(--font-ui)',fontSize:13,color:'var(--text-title)',background:'transparent',minWidth:0}}/>
  </div>;
}
