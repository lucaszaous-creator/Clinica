import React from 'react';
export function Tooltip({label,children,style}){
const[v,setV]=React.useState(false);
return React.createElement('span',{onMouseEnter:()=>setV(true),onMouseLeave:()=>setV(false),style:{position:'relative',display:'inline-flex',...style}},children,
v?React.createElement('span',{style:{position:'absolute',bottom:'calc(100% + 6px)',left:'50%',transform:'translateX(-50%)',background:'var(--cinza-900)',color:'#fff',fontSize:12,fontFamily:'var(--font-ui)',padding:'4px 8px',borderRadius:6,whiteSpace:'nowrap',zIndex:40,boxShadow:'var(--sombra-tooltip)'}},label):null);
}