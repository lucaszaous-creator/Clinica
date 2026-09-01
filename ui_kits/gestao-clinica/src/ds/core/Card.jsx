import React from 'react';
export function Card({title,subtitle,actions,padded=true,children,style,bodyStyle}){
  return <div style={{background:'var(--surface-card)',border:'1px solid var(--border)',
    borderRadius:'var(--radius-card)',padding:padded?'var(--space-card-pad)':0,boxSizing:'border-box',...style}}>
    {(title||actions)?<div style={{display:'flex',alignItems:'center',gap:12,margin:padded?'0 0 12px':'16px 16px 12px'}}>
      <div style={{flex:1,minWidth:0}}>
        {title?<div style={{fontFamily:'var(--font-ui)',fontSize:'var(--text-h3-size)',fontWeight:600,color:'var(--text-title)'}}>{title}</div>:null}
        {subtitle?<div style={{fontFamily:'var(--font-ui)',fontSize:13,color:'var(--text-muted)',marginTop:2}}>{subtitle}</div>:null}
      </div>
      {actions?<div style={{display:'flex',alignItems:'center',gap:8,flexShrink:0}}>{actions}</div>:null}
    </div>:null}
    <div style={bodyStyle}>{children}</div>
  </div>;
}
