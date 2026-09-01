import React from 'react';
import {Icon} from '../core/Icon.jsx';
export function EmptyState({icon='inbox',title,description,action,style}){
  return <div style={{display:'flex',flexDirection:'column',alignItems:'center',justifyContent:'center',gap:6,
    padding:'32px 24px',textAlign:'center',fontFamily:'var(--font-ui)',...style}}>
    <Icon name={icon} size={22} style={{color:'var(--text-muted)'}}/>
    <div style={{fontSize:14,fontWeight:600,color:'var(--text-title)'}}>{title}</div>
    {description?<div style={{fontSize:13,color:'var(--text-muted)',maxWidth:360}}>{description}</div>:null}
    {action?<div style={{marginTop:8}}>{action}</div>:null}
  </div>;
}
