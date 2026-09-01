import React from 'react';
export function UrgencyDot({level='verde',size=12,label,style}){
  const c={verde:'var(--semaforo-verde)',amarelo:'var(--semaforo-amarelo)',vermelho:'var(--semaforo-vermelho)'}[level]||'var(--cinza-300)';
  const t={verde:'No prazo',amarelo:'Atenção',vermelho:'Urgente'}[level]||level;
  return <span title={t} style={{display:'inline-flex',alignItems:'center',gap:8,...style}}>
    <span style={{width:size,height:size,borderRadius:'50%',background:c,flexShrink:0}}/>
    {label?<span style={{fontFamily:'var(--font-ui)',fontSize:13,color:'var(--text-body)'}}>{t}</span>:null}
  </span>;
}
