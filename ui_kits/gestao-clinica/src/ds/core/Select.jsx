import React from 'react';
export function Select({options=[],value,onChange,pill,style,...rest}){
  return <select value={value} onChange={e=>onChange&&onChange(e.target.value)}
    style={{fontFamily:'var(--font-ui)',fontSize:'13px',fontWeight:pill?600:400,
      padding:pill?'6px 28px 6px 12px':'8px 12px',border:'1px solid var(--border)',
      borderRadius:pill?'var(--radius-pilula)':'var(--radius-pequeno)',
      color:'var(--text-title)',background:'#fff',width:pill?'auto':'100%',boxSizing:'border-box',
      appearance:'none',backgroundImage:'linear-gradient(45deg,transparent 50%,var(--text-muted) 50%),linear-gradient(135deg,var(--text-muted) 50%,transparent 50%)',
      backgroundPosition:'calc(100% - 14px) 50%,calc(100% - 9px) 50%',backgroundSize:'5px 5px,5px 5px',backgroundRepeat:'no-repeat',
      cursor:'pointer',...style}} {...rest}>
    {options.map(o=>typeof o==='string'?<option key={o} value={o}>{o}</option>:<option key={o.value} value={o.value}>{o.label}</option>)}
  </select>;
}
