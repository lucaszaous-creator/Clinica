const DSm=window.ClNicaFaturamentoDesignSystem_bd26af;
const {Button:BtnM,Input:InpM,Label:LblM,DatePicker:DateM,UrgencyDot:DotM,AlertBanner:BanM,Select:SelM,Icon:IcoM}=DSm;
const CAPA_URL='../../templates/capa-faturamento/CapaFaturamento.dc.html';
function Modal({title,icon,width=460,children,onClose}){
return <div style={{position:'fixed',inset:0,background:'rgba(15,23,42,.45)',display:'flex',alignItems:'center',justifyContent:'center',zIndex:50}} onClick={onClose}>
<div style={{background:'#fff',borderRadius:10,border:'1px solid var(--border)',width,maxWidth:'90vw',padding:20,fontFamily:'var(--font-ui)'}} onClick={e=>e.stopPropagation()}>
<div style={{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:12}}>
<div style={{fontSize:16,fontWeight:600,color:'var(--text-title)',display:'flex',alignItems:'center',gap:8}}>{icon?<IcoM name={icon} size={18}/>:null}{title}</div>
<button onClick={onClose} style={{border:'none',background:'transparent',cursor:'pointer',color:'var(--text-muted)',padding:2}}><IcoM name="x" size={16}/></button>
</div>
{children}
</div></div>;
}
function ModalBaixa({row,onConcluida,onClose}){
const[g,setG]=React.useState(row.g==='—'?'':row.g);const[d,setD]=React.useState('2026-07-16');
const segundo=row.tipo.indexOf('2º')>=0;
return <Modal title={"Dar baixa — "+row.p} icon="check-circle" onClose={onClose}>
<BanM tone="warning" style={{marginBottom:10}}>Confirme a guia no sistema do convênio antes de dar baixa.</BanM>
<LblM>Número da guia gerada (sistema do convênio)</LblM><InpM value={g} onChange={setG}/>
<LblM>Data da baixa</LblM><DateM value={d} onChange={setD}/>
<div style={{marginTop:16,display:'flex',gap:8,justifyContent:'flex-end'}}>
<BtnM variant="secondary" onClick={onClose}>Cancelar</BtnM>
<BtnM onClick={()=>{onClose();if(segundo)onConcluida(row);}}>Confirmar baixa</BtnM>
</div>
</Modal>;
}
function ModalGlosa({onClose}){
const[m,setM]=React.useState('');
return <Modal title="Registrar glosa" icon="ban" onClose={onClose}>
<LblM>Guia</LblM><SelM options={["88231 — Carlos Nunes (Amil)","88240 — João Pereira (Amil)"]}/>
<LblM>Motivo da glosa</LblM><InpM multiline rows={3} value={m} onChange={setM}/>
<div style={{marginTop:16,display:'flex',gap:8,justifyContent:'flex-end'}}>
<BtnM variant="secondary" onClick={onClose}>Cancelar</BtnM>
<BtnM variant="danger" onClick={onClose}>Registrar glosa</BtnM>
</div>
</Modal>;
}
function ModalAviso({pend,onVer,onClose}){
return <Modal title="Aviso de pendências" icon="bell" width={520} onClose={onClose}>
<div style={{fontSize:13,color:'var(--text-body)',marginBottom:10}}>Ao abrir o sistema foram encontradas <b>{pend.length} pendências</b>:</div>
<div style={{display:'flex',flexDirection:'column',gap:6,marginBottom:14}}>
{pend.slice(0,4).map((r,i)=><div key={i} style={{display:'flex',alignItems:'center',gap:8,background:'var(--warning-tint)',borderRadius:6,padding:'6px 10px',fontSize:13}}>
<DotM level={r.u} size={14}/><b>{r.p}</b> — {r.tipo} · {r.c}</div>)}
</div>
<div style={{display:'flex',gap:8,justifyContent:'flex-end'}}>
<BtnM variant="secondary" onClick={onClose}>Fechar</BtnM>
<BtnM onClick={onVer}>Ver painel de pendências</BtnM>
</div>
</Modal>;
}
function ModalPrimeiroAtendimento({dados,onClose}){
return <Modal title="Atendimento lançado — 1º código gerado" icon="file-check" width={520} onClose={onClose}>
<BanM tone="success" title="Atendimento nº 000124" style={{marginBottom:10}}>{dados.pac} — {dados.conv}</BanM>
<div style={{border:'1px solid var(--border)',borderRadius:8,padding:'10px 12px',fontSize:13,color:'var(--text-body)',marginBottom:10}}>
<b style={{color:'var(--text-title)'}}>1º código:</b> AUT-114612 · gerado em 16/07/2026
</div>
<BanM tone="warning" style={{marginBottom:14}}>Este convênio exige <b>2 códigos</b>. O 2º código deve ser obtido em <b>24h</b> por ligação ao convênio — ele já entrou no painel de pendências.</BanM>
<div style={{display:'flex',gap:8,justifyContent:'flex-end'}}>
<BtnM variant="secondary" onClick={onClose}>Fechar</BtnM>
<BtnM icon="printer" onClick={()=>window.open(CAPA_URL,'_blank')}>Imprimir comprovante (parcial)</BtnM>
</div>
</Modal>;
}
function ModalFaturaCompleta({row,onClose}){
return <Modal title="Fatura concluída" icon="badge-check" width={520} onClose={onClose}>
<BanM tone="success" title="Os 2 códigos foram baixados" style={{marginBottom:10}}>{row.p} — {row.c}. A fatura está completa e pronta para envio ao convênio.</BanM>
<div style={{border:'1px solid var(--border)',borderRadius:8,padding:'10px 12px',fontSize:13,color:'var(--text-body)',marginBottom:14,display:'flex',flexDirection:'column',gap:4}}>
<div><b style={{color:'var(--text-title)'}}>1º código:</b> AUT-114532 · baixado</div>
<div><b style={{color:'var(--text-title)'}}>2º código:</b> AUT-114570 · baixado</div>
</div>
<div style={{display:'flex',gap:8,justifyContent:'flex-end'}}>
<BtnM variant="secondary" onClick={onClose}>Fechar</BtnM>
<BtnM icon="printer" onClick={()=>window.open(CAPA_URL,'_blank')}>Imprimir capa de faturamento</BtnM>
</div>
</Modal>;
}
Object.assign(window,{Modal,ModalBaixa,ModalGlosa,ModalAviso,ModalPrimeiroAtendimento,ModalFaturaCompleta});
