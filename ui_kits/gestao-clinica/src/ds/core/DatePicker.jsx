import React from 'react';
export function DatePicker({value,onChange,style,...rest}){
  return <input type="date" value={value||''} onChange={e=>onChange&&onChange(e.target.value)}
    style={{fontFamily:'var(--font-ui)',fontSize:'13px',padding:'8px 12px',border:'1px solid var(--border)',
      borderRadius:'var(--radius-pequeno)',color:'var(--text-title)',background:'#fff',boxSizing:'border-box',...style}} {...rest}/>;
}
