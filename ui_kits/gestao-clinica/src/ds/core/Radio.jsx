import React from 'react';
export function Radio({options=[],value,onChange,name='radio',row,style}){
  return <div style={{display:'flex',flexDirection:row?'row':'column',gap:row?16:8,...style}}>
    {options.map(o=>{const v=typeof o==='string'?o:o.value,l=typeof o==='string'?o:o.label;
      return <label key={v} style={{display:'flex',alignItems:'center',gap:8,fontFamily:'var(--font-ui)',fontSize:13,color:'var(--text-title)',cursor:'pointer'}}>
        <input type="radio" name={name} checked={value===v} onChange={()=>onChange&&onChange(v)}
          style={{accentColor:'var(--brand)',width:16,height:16,margin:0}}/>{l}
      </label>;})}
  </div>;
}
