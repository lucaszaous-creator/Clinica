import React from 'react';
import {Avatar} from '../data/Avatar.jsx';
import {Badge} from '../feedback/Badge.jsx';
export function PatientSelectorItem({nome,cpf,convenio,foto,carteirinhaVencida,selected,onClick,style}){
const[h,setH]=React.useState(false);
return React.createElement('button',{type:'button',onClick,onMouseEnter:()=>setH(true),onMouseLeave:()=>setH(false),
style:{display:'flex',alignItems:'center',gap:10,width:'100%',textAlign:'left',padding:'8px 10px',boxSizing:'border-box',background:selected?'var(--acento-suave)':h?'var(--superficie-hover)':'var(--superficie)',border:'1px solid '+(selected?'var(--acento-tint)':'var(--borda)'),borderRadius:'var(--radius-control)',cursor:'pointer',fontFamily:'var(--font-ui)',transition:'background var(--duracao-rapida)',...style}},
React.createElement(Avatar,{src:foto,name:nome,size:36}),
React.createElement('span',{style:{flex:1,minWidth:0}},
React.createElement('span',{style:{display:'block',fontSize:14,fontWeight:600,color:'var(--texto-primario)',whiteSpace:'nowrap',overflow:'hidden',textOverflow:'ellipsis'}},nome),
React.createElement('span',{style:{display:'block',fontSize:12,color:'var(--texto-secundario)'}},cpf)),
React.createElement('span',{style:{display:'flex',flexDirection:'column',alignItems:'flex-end',gap:3}},
convenio?React.createElement(Badge,{tone:'marca'},convenio):null,
carteirinhaVencida?React.createElement(Badge,{tone:'erro'},'Carteirinha vencida'):null));
}