import React from 'react';
function suave(pts){
  if(pts.length<2)return '';
  let d='M'+pts[0][0]+','+pts[0][1];
  for(let i=0;i<pts.length-1;i++){
    const p0=pts[i-1]||pts[i],p1=pts[i],p2=pts[i+1],p3=pts[i+2]||p2;
    const c1x=p1[0]+(p2[0]-p0[0])/6,c1y=p1[1]+(p2[1]-p0[1])/6;
    const c2x=p2[0]-(p3[0]-p1[0])/6,c2y=p2[1]-(p3[1]-p1[1])/6;
    d+='C'+c1x+','+c1y+' '+c2x+','+c2y+' '+p2[0]+','+p2[1];
  }
  return d;
}
export function LineChart({series=[],labels=[],height=240,yFormat=v=>v,marker,tooltip,style}){
  const W=1000,H=height,pad={t:12,r:8,b:26,l:38};
  const todos=series.flatMap(s=>s.data);
  const min=Math.min(...todos),max=Math.max(...todos);
  const lo=min-(max-min)*.25,hi=max+(max-min)*.25;
  const x=i=>pad.l+i*(W-pad.l-pad.r)/Math.max(1,labels.length-1);
  const y=v=>pad.t+(hi-v)*(H-pad.t-pad.b)/(hi-lo||1);
  const grades=[0,.25,.5,.75,1].map(f=>lo+(hi-lo)*f);
  return <div style={{position:'relative',...style}}>
    <svg viewBox={'0 0 '+W+' '+H} width="100%" height={H} preserveAspectRatio="none" style={{display:'block',overflow:'visible'}}>
      <defs>{series.map((s,i)=><linearGradient key={i} id={'g'+i+'-'+(s.name||i).replace(/\W/g,'')} x1="0" x2="0" y1="0" y2="1">
        <stop offset="0%" stopColor={s.color} stopOpacity=".18"/><stop offset="100%" stopColor={s.color} stopOpacity="0"/>
      </linearGradient>)}</defs>
      {grades.map((g,i)=><g key={i}>
        <line x1={pad.l} x2={W-pad.r} y1={y(g)} y2={y(g)} stroke="var(--cinza-100)" strokeWidth="1" vectorEffect="non-scaling-stroke"/>
        <text x={pad.l-8} y={y(g)+4} textAnchor="end" fontSize="11" fill="var(--text-muted)" fontFamily="var(--font-ui)">{yFormat(Math.round(g*10)/10)}</text>
      </g>)}
      {series.map((s,i)=>{const pts=s.data.map((v,j)=>[x(j),y(v)]);const d=suave(pts);
        return <g key={i}>
          {s.fill?<path d={d+'L'+x(s.data.length-1)+','+(H-pad.b)+'L'+pad.l+','+(H-pad.b)+'Z'} fill={'url(#g'+i+'-'+(s.name||i).replace(/\W/g,'')+')'}/>:null}
          <path d={d} fill="none" stroke={s.color} strokeWidth="2.5" strokeDasharray={s.dashed?'6 5':undefined}
            strokeLinecap="round" vectorEffect="non-scaling-stroke"/>
        </g>;})}
      {marker!=null?<g>
        <line x1={x(marker)} x2={x(marker)} y1={pad.t} y2={H-pad.b} stroke={series[0]&&series[0].color} strokeWidth="1"
          strokeDasharray="4 4" vectorEffect="non-scaling-stroke"/>
        {series.map((s,i)=><circle key={i} cx={x(marker)} cy={y(s.data[marker])} r="4.5" fill="#fff" stroke={s.color} strokeWidth="2.5" vectorEffect="non-scaling-stroke"/>)}
      </g>:null}
      {labels.map((l,i)=><text key={i} x={x(i)} y={H-6} textAnchor="middle" fontSize="11" fill="var(--text-muted)" fontFamily="var(--font-ui)">{l}</text>)}
    </svg>
    {tooltip&&marker!=null?<div style={{position:'absolute',left:(x(marker)/W*100)+'%',top:8,transform:'translateX(-50%)',
      background:'#fff',border:'1px solid var(--border)',borderRadius:'var(--radius-control)',boxShadow:'var(--sombra-tooltip)',
      padding:'8px 10px',fontFamily:'var(--font-ui)',fontSize:12,color:'var(--text-title)',whiteSpace:'nowrap',pointerEvents:'none'}}>{tooltip}</div>:null}
  </div>;
}
