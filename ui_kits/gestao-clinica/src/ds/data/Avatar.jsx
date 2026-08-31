import React from 'react';
export function Avatar({name='',src,size=32,style}){
  const iniciais=name.trim().split(/\s+/).slice(0,2).map(p=>p[0]||'').join('').toUpperCase();
  return <span style={{width:size,height:size,borderRadius:'50%',flexShrink:0,overflow:'hidden',
    background:src?'transparent':'var(--brand-tint)',color:'var(--brand)',display:'inline-flex',
    alignItems:'center',justifyContent:'center',fontFamily:'var(--font-ui)',fontWeight:600,
    fontSize:Math.round(size*.4),...style}}>
    {src?<img src={src} alt={name} style={{width:'100%',height:'100%',objectFit:'cover'}}/>:iniciais}
  </span>;
}
