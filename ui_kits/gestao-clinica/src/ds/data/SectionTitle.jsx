import React from 'react';
export function SectionTitle({children,style}){
return React.createElement('div',{style:{margin:'18px 0 10px',...style}},
React.createElement('div',{style:{fontSize:11,fontWeight:700,letterSpacing:'.08em',textTransform:'uppercase',color:'var(--texto-secundario)',fontFamily:'var(--font-ui)',marginBottom:6}},children),
React.createElement('div',{style:{height:1,background:'var(--borda)'}}));
}