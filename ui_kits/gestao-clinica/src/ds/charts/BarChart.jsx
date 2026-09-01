import React from 'react';
export function BarChart({data=[],labels=[],height=220,positiveColor='var(--serie-1)',negativeColor='var(--serie-3)',marker,tooltip,style}){
  const W=1000,H=height,pad={t:12,r:8,b:22,l:34};
  const max=Math.max(...data.map(d=>Math.abs(d.entrada||d.valor||0)),...data.map(d=>Math.abs(d.saida||0)))||1;
  const zero=pad.t+(H-pad.t-pad.b)/2;
  const passo=(W-pad.l-pad.r)/Math.max(1,data.length);
  const larg=Math.min(10,passo*.45);
  const esc=v=>v/max*((H-pad.t-pad.b)/2-4);
  return <div style={{position:'relative',...style}}>
    <svg viewBox={'0 0 '+W+' '+H} width="100%" height={H} preserveAspectRatio="none" style={{display:'block',overflow:'visible'}}>
      <line x1={pad.l} x2={W-pad.r} y1={zero} y2={zero} stroke="var(--cinza-200)" strokeWidth="1" vectorEffect="non-scaling-stroke"/>
      {data.map((d,i)=>{const cx=pad.l+passo*(i+.5);const e=esc(d.entrada||d.valor||0),s=esc(d.saida||0);
        return <g key={i} opacity={marker!=null&&marker!==i?.45:1}>
          <rect x={cx-larg/2} y={zero-e} width={larg} height={Math.max(2,e)} rx={larg/2} fill={positiveColor}/>
          {d.saida?<rect x={cx-larg/2} y={zero} width={larg} height={Math.max(2,s)} rx={larg/2} fill={negativeColor}/>:null}
        </g>;})}
      {labels.map((l,i)=>l?<text key={i} x={pad.l+passo*(i+.5)} y={H-4} textAnchor="middle" fontSize="10"
        fill="var(--text-muted)" fontFamily="var(--font-ui)">{l}</text>:null)}
    </svg>
    {tooltip&&marker!=null?<div style={{position:'absolute',left:((pad.l+passo*(marker+.5))/W*100)+'%',top:0,transform:'translateX(-50%)',
      background:'#fff',border:'1px solid var(--border)',borderRadius:'var(--radius-control)',boxShadow:'var(--sombra-tooltip)',
      padding:'8px 10px',fontFamily:'var(--font-ui)',fontSize:12,color:'var(--text-title)',whiteSpace:'nowrap',pointerEvents:'none'}}>{tooltip}</div>:null}
  </div>;
}
