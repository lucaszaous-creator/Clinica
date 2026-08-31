import React from 'react';
export function Switch({checked,onChange,label,disabled,style}){
  return <label style={{display:'inline-flex',alignItems:'center',gap:10,cursor:disabled?'default':'pointer',
    fontFamily:'var(--font-ui)',fontSize:13,color:'var(--text-title)',opacity:disabled?.5:1,...style}}>
    <span onClick={()=>!disabled&&onChange&&onChange(!checked)} style={{width:38,height:20,borderRadius:999,
      background:checked?'var(--brand)':'var(--cinza-300)',position:'relative',flexShrink:0,
      transition:'background var(--duracao-normal) ease'}}>
      <span style={{position:'absolute',top:2,left:checked?20:2,width:16,height:16,borderRadius:999,background:'#fff',
        transition:'left var(--duracao-normal) ease'}}/>
    </span>
    {label}
  </label>;
}
