import React from 'react';
import {Icon} from '../core/Icon.jsx';
export function Modal({title,icon,width=460,children,footer,onClose}){
return React.createElement('div',{onClick:onClose,style:{position:'fixed',inset:0,background:'rgba(15,23,42,.45)',display:'flex',alignItems:'center',justifyContent:'center',zIndex:50}},
React.createElement('div',{onClick:e=>e.stopPropagation(),style:{background:'var(--superficie)',borderRadius:'var(--radius-control)',border:'1px solid var(--borda)',boxShadow:'var(--sombra-popup)',width,maxWidth:'90vw',maxHeight:'85vh',overflowY:'auto',padding:20,fontFamily:'var(--font-ui)',boxSizing:'border-box'}},
React.createElement('div',{style:{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:12}},
React.createElement('div',{style:{fontSize:16,fontWeight:600,color:'var(--texto-primario)',display:'flex',alignItems:'center',gap:8}},icon?React.createElement(Icon,{name:icon,size:18}):null,title),
React.createElement('button',{onClick:onClose,style:{border:'none',background:'transparent',cursor:'pointer',color:'var(--texto-secundario)',padding:2,display:'inline-flex'}},React.createElement(Icon,{name:'x',size:16}))),
children,
footer?React.createElement('div',{style:{marginTop:16,display:'flex',gap:8,justifyContent:'flex-end'}},footer):null));
}