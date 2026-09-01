import React from 'react';
export function Ficha({label,children,style}){
return React.createElement('div',{style:{fontFamily:'var(--font-ui)',...style}},
React.createElement('div',{style:{fontSize:12,fontWeight:600,color:'var(--texto-secundario)',marginBottom:2}},label),
React.createElement('div',{style:{fontSize:14,color:'var(--texto-primario)'}},children==null||children===''?'—':children));
}