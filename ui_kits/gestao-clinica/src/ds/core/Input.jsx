import React from 'react';
export function Input({multiline,mono,value,onChange,placeholder,rows=3,invalid,style,...rest}){
  const [f,setF]=React.useState(false);
  const s={fontFamily:mono?'var(--font-mono)':'var(--font-ui)',fontSize:'13px',padding:'var(--space-input-pad)',
    border:'1px solid '+(invalid?'var(--danger)':f?'var(--focus-ring)':'var(--border)'),
    boxShadow:f?'0 0 0 2px var(--brand-soft)':'none',
    borderRadius:'var(--radius-pequeno)',color:'var(--text-title)',background:'#fff',width:'100%',
    boxSizing:'border-box',outline:'none',resize:'vertical',...style};
  const p={value,placeholder,onChange:e=>onChange&&onChange(e.target.value),onFocus:()=>setF(true),onBlur:()=>setF(false),style:s,...rest};
  return multiline?<textarea rows={rows} {...p}/>:<input {...p}/>;
}
