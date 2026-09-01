import React from 'react';
import { Modal, Button, Avatar, Badge, Ficha, SectionTitle, Tabs, AlertBanner, Icon } from '../ds/index.js';
import logo from '../assets/logo-cor.png';

export function ModalProntuario({ reg, onClose }) {
  const [aba, setAba] = React.useState('evolucoes');
  const evolucoes = [
    {data:'16/07/2026',prof:'Dr. Otávio Lins',txt:'Paciente refere melhora da dor lombar (EVA 6 → 4) após 3ª sessão. Mantido protocolo de acupuntura em pontos B23, B25 e VB30. Orientado alongamento diário.'},
    {data:'09/07/2026',prof:'Dr. Otávio Lins',txt:'Dor lombar crônica irradiada para MID, EVA 6. Realizada 2ª sessão sem intercorrências. Paciente tolerou bem o agulhamento.'},
    {data:'02/07/2026',prof:'Dr. Otávio Lins',txt:'Primeira sessão do plano terapêutico. Definido ciclo inicial de 8 sessões semanais.'}];
  return <Modal title={'Prontuário — '+reg.p} icon="file-text" width={720} onClose={onClose}
    footer={<>{reg.st==='A assinar'?<Button icon="pen-line">Assinar evolução</Button>:null}<Button variant="secondary" icon="printer">Imprimir</Button><Button variant="secondary" onClick={onClose}>Fechar</Button></>}>
    <div style={{display:'flex',alignItems:'center',gap:12,marginBottom:14}}>
      <Avatar name={reg.p} size={44}/>
      <span style={{flex:1}}>
        <div style={{fontSize:15,fontWeight:600,color:'var(--text-title)'}}>{reg.p}</div>
        <div style={{fontSize:12,color:'var(--text-muted)'}}>48 anos · Unimed Intercâmbio · Prontuário nº 2026-0412</div>
      </span>
      <Badge tone={reg.tom}>{reg.st}</Badge>
    </div>
    <div style={{display:'grid',gridTemplateColumns:'repeat(3,1fr)',gap:'4px 20px',marginBottom:12}}>
      <Ficha label="Diagnóstico (CID-10)">M54.5 — Dor lombar baixa</Ficha>
      <Ficha label="Plano terapêutico">Acupuntura · 8 sessões</Ficha>
      <Ficha label="Alergias">Dipirona</Ficha>
    </div>
    <Tabs tabs={[{id:'evolucoes',label:'Evoluções'},{id:'anamnese',label:'Anamnese'},{id:'anexos',label:'Anexos'}]} activeId={aba} onSelect={setAba}/>
    {aba==='evolucoes'?<div style={{display:'flex',flexDirection:'column',gap:12,paddingTop:14}}>
      {evolucoes.map((e,i)=><div key={i} style={{borderLeft:'3px solid '+(i===0&&reg.st==='A assinar'?'var(--aviso)':'var(--borda)'),paddingLeft:12,fontFamily:'var(--font-ui)'}}>
        <div style={{fontSize:12,color:'var(--text-muted)',marginBottom:2}}>{e.data} · {e.prof}{i===0&&reg.st==='A assinar'?' · aguardando assinatura':''}</div>
        <div style={{fontSize:13,color:'var(--text-body)',lineHeight:'20px'}}>{e.txt}</div>
      </div>)}
    </div>:aba==='anamnese'?<div style={{paddingTop:14,fontSize:13,color:'var(--text-body)',lineHeight:'20px',fontFamily:'var(--font-ui)'}}>
      Dor lombar há 3 anos, de caráter mecânico, com piora aos esforços e irradiação para membro inferior direito. Nega trauma. Tratamentos prévios: fisioterapia (12 sessões, melhora parcial) e AINEs esporádicos. HAS controlada com losartana. Nega DM, tabagismo e etilismo.
    </div>:<div style={{paddingTop:14}}>
      {[['Ressonância — coluna lombar (laudo).pdf','10/07/2026'],['Ficha de avaliação inicial.pdf','02/07/2026']].map((a,i)=>
      <div key={i} style={{display:'flex',alignItems:'center',gap:10,padding:'8px 0',borderBottom:'1px solid var(--cinza-100)',fontFamily:'var(--font-ui)'}}>
        <Icon name="paperclip" size={15}/><span style={{flex:1,fontSize:13,color:'var(--text-title)'}}>{a[0]}</span>
        <span style={{fontSize:12,color:'var(--text-muted)'}}>{a[1]}</span><Button size="sm" variant="secondary" icon="download">Baixar</Button>
      </div>)}
    </div>}
  </Modal>;
}

export function ModalExame({ ex, onClose }) {
  const valores = [
    {item:'Hemácias',res:'4,6 mi/mm³',ref:'4,3 – 5,7',ok:true},
    {item:'Hemoglobina',res:'13,9 g/dL',ref:'13,5 – 17,5',ok:true},
    {item:'Leucócitos',res:'11.400 /mm³',ref:'4.000 – 10.000',ok:false},
    {item:'Plaquetas',res:'262 mil/mm³',ref:'150 – 450',ok:true}];
  const laudo = ex.exame.indexOf('Hemograma') < 0;
  return <Modal title={'Resultado — '+ex.exame} icon="microscope" width={640} onClose={onClose}
    footer={<><Button variant="secondary" icon="download">Baixar PDF</Button><Button variant="secondary" icon="paperclip">Anexar ao prontuário</Button><Button variant="secondary" onClick={onClose}>Fechar</Button></>}>
    <div style={{display:'grid',gridTemplateColumns:'repeat(3,1fr)',gap:'4px 20px',marginBottom:14}}>
      <Ficha label="Paciente">{ex.p}</Ficha>
      <Ficha label="Pedido em">{ex.ped}</Ficha>
      <Ficha label="Laboratório">Lab Vida — Macaé</Ficha>
    </div>
    {laudo?<div style={{fontFamily:'var(--font-ui)'}}>
      <SectionTitle style={{marginTop:0}}>Laudo</SectionTitle>
      <p style={{fontSize:13,color:'var(--text-body)',lineHeight:'20px',margin:'8px 0'}}>Desidratação discal difusa em L4-L5 e L5-S1, com abaulamento discal posterior em L4-L5 tocando o saco dural, sem compressão radicular evidente. Demais estruturas sem alterações significativas.</p>
      <AlertBanner tone="info">Correlacionar com exame clínico. Achados compatíveis com o quadro de dor lombar mecânica.</AlertBanner>
    </div>:<div>
      <SectionTitle style={{marginTop:0}}>Valores</SectionTitle>
      <table style={{width:'100%',borderCollapse:'collapse',fontFamily:'var(--font-ui)',fontSize:13}}>
        <thead><tr>{['Item','Resultado','Referência',''].map((h,i)=><th key={i} style={{textAlign:i===3?'right':'left',padding:'8px 10px',background:'var(--cinza-100)',color:'var(--cinza-700)',fontWeight:600}}>{h}</th>)}</tr></thead>
        <tbody>{valores.map((v,i)=><tr key={i} style={{background:i%2?'var(--linha-alt)':'#fff'}}>
          <td style={{padding:'8px 10px'}}>{v.item}</td>
          <td style={{padding:'8px 10px',fontWeight:600,color:v.ok?'var(--text-title)':'var(--erro-texto)'}}>{v.res}</td>
          <td style={{padding:'8px 10px',color:'var(--text-muted)'}}>{v.ref}</td>
          <td style={{padding:'8px 10px',textAlign:'right'}}><Badge tone={v.ok?'sucesso':'aviso'}>{v.ok?'Normal':'Alterado'}</Badge></td>
        </tr>)}</tbody>
      </table>
    </div>}
  </Modal>;
}

export function ModalPrescricao({ rx, onClose }) {
  const controle = rx.tipo === 'Controle especial';
  const meds = rx.med.split(' · ');
  return <Modal title="Folha de prescrição" icon="pill" width={620} onClose={onClose}
    footer={<><Button icon="printer">Imprimir</Button><Button variant="secondary" icon="rotate-ccw">Renovar</Button><Button variant="secondary" onClick={onClose}>Fechar</Button></>}>
    <div style={{border:'1px solid var(--borda)',borderRadius:8,padding:'28px 32px',background:'#fff',fontFamily:'var(--font-ui)'}}>
      <div style={{display:'flex',alignItems:'flex-start',justifyContent:'space-between',borderBottom:'2px solid var(--acento)',paddingBottom:14,marginBottom:18}}>
        <img src={logo} alt="Clínica SemDor" style={{height:34}}/>
        <span style={{textAlign:'right',fontSize:11,color:'var(--text-muted)',lineHeight:'16px'}}>Av. Atlântica, 1240 · Macaé/RJ<br/>(22) 2762-4410 · CNES 7 654 321</span>
      </div>
      <div style={{textAlign:'center',fontSize:13,fontWeight:700,letterSpacing:'.06em',textTransform:'uppercase',color:'var(--text-title)',marginBottom:16}}>
        {controle?'Receituário de controle especial':'Receituário'}</div>
      {controle?<div style={{fontSize:11,color:'var(--aviso-texto)',background:'var(--aviso-suave)',borderRadius:6,padding:'6px 10px',marginBottom:14,textAlign:'center'}}>1ª via — farmácia · 2ª via — paciente · Válida por 30 dias</div>:null}
      <div style={{fontSize:13,color:'var(--text-body)',marginBottom:18}}>
        <b style={{fontWeight:600,color:'var(--text-title)'}}>Paciente:</b> {rx.p}
        <span style={{color:'var(--text-muted)'}}> · CPF 123.456.789-00</span>
      </div>
      <div style={{fontSize:14,color:'var(--text-title)',lineHeight:'26px',minHeight:96,marginBottom:22}}>
        1. {meds[0]} {meds[1]?'— '+meds[1]:''}<br/>
        <span style={{color:'var(--text-muted)',fontSize:13}}>&nbsp;&nbsp;&nbsp;{meds[2]?'Uso contínuo por '+meds[2]+'.':''} Uso oral.</span>
      </div>
      <div style={{display:'flex',justifyContent:'space-between',alignItems:'flex-end',marginTop:26}}>
        <span style={{fontSize:12,color:'var(--text-muted)'}}>Macaé, {rx.data}</span>
        <span style={{textAlign:'center',fontSize:12,color:'var(--text-muted)'}}>
          <span style={{display:'block',width:220,borderTop:'1px solid var(--cinza-300)',paddingTop:6}}>Dr. Otávio Lins · CRM-RJ 52.884<br/>Acupuntura</span>
        </span>
      </div>
    </div>
    <div style={{marginTop:10,display:'flex',justifyContent:'flex-end'}}><Badge tone={controle?'aviso':'neutro'}>{rx.tipo}</Badge></div>
  </Modal>;
}
