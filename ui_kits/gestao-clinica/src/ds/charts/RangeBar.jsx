import React from 'react';
export function RangeBar({label,value,fraction=0,color='var(--serie-1)',style}){
  const pct=Math.max(0,Math.min(1,fraction))*100;
  return <div style={{fontFamily:'var(--font-ui)',...style}}>
    <div style={{display:'flex',alignItems:'baseline',gap:8,marginBottom:6}}>
      <span style={{flex:1,fontSize:13,color:'var(--text-body)'}}>{label}</span>
      <b style={{fontSize:13,color:'var(--text-title)',fontWeight:600}}>{value}</b>
    </div>
    <div style={{position:'relative',height:10,borderRadius:999,background:'var(--cinza-100)',
      backgroundImage:'repeating-linear-gradient(135deg,var(--cinza-200) 0 1px,transparent 1px 6px)'}}>
      <div style={{position:'absolute',inset:'0 auto 0 0',width:pct+'%',borderRadius:999,
        background:'linear-gradient(90deg,color-mix(in oklch,'+'var(--surface-card)'+' 35%,'+color+'),'+color+')'}}/>
      <span style={{position:'absolute',top:-2,left:'calc('+pct+'% - 7px)',width:14,height:14,borderRadius:999,
        background:'#fff',border:'2px solid '+color,boxSizing:'border-box'}}/>
    </div>
  </div>;
}
