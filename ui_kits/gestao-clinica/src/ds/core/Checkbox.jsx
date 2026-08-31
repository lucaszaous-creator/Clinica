import React from 'react';
export function Checkbox({checked,onChange,disabled,children,style}){
  return <label style={{display:'flex',alignItems:'center',gap:'8px',fontFamily:'var(--font-ui)',fontSize:'13px',
    color:'var(--text-title)',cursor:disabled?'default':'pointer',opacity:disabled?.5:1,...style}}>
    <input type="checkbox" checked={!!checked} disabled={disabled} onChange={e=>onChange&&onChange(e.target.checked)}
      style={{accentColor:'var(--brand)',width:'16px',height:'16px',margin:0}}/>
    {children}
  </label>;
}
