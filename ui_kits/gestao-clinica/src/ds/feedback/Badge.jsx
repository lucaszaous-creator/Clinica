import React from 'react';
export function Badge({tone='neutro',children,style}){
  const t={
    neutro:{bg:'var(--cinza-100)',fg:'var(--text-body)'},
    sucesso:{bg:'var(--success-tint)',fg:'var(--success-text)'},
    aviso:{bg:'var(--warning-tint)',fg:'var(--warning-text)'},
    erro:{bg:'var(--danger-tint)',fg:'var(--danger-text)'},
    info:{bg:'var(--info-tint)',fg:'var(--info-text)'},
    marca:{bg:'var(--brand-soft)',fg:'var(--brand)'},
  }[tone]||{};
  return <span style={{display:'inline-flex',alignItems:'center',gap:6,background:t.bg,color:t.fg,
    borderRadius:'var(--radius-pilula)',padding:'2px 10px',fontFamily:'var(--font-ui)',fontSize:'var(--text-small-size)',
    fontWeight:600,lineHeight:'18px',whiteSpace:'nowrap',...style}}>{children}</span>;
}
