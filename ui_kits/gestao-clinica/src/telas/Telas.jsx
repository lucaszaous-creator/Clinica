import React from 'react';
import { Heading, Button, Card, DataTable, Avatar, Badge, SearchInput, Select, EmptyState } from '../ds/index.js';
import { AGENDA_PROFS, PACIENTES } from '../dados.js';

export function TelaAgenda() {
  const horas = ['08:00','09:00','10:00','11:00','12:00','13:00','14:00'];
  const blocos = [
    {prof:0,i:0,dur:1,p:'Maria da Silva',t:'Acupuntura',st:'Confirmada'},
    {prof:0,i:2,dur:1,p:'João Pereira',t:'Acupuntura',st:'Aguardando'},
    {prof:0,i:5,dur:2,p:'Sérgio Antunes',t:'Eletroacupuntura',st:'Confirmada'},
    {prof:1,i:0,dur:2,p:'Carlos Nunes',t:'Fisiatria',st:'Em atendimento'},
    {prof:1,i:3,dur:1,p:'Rita Campos',t:'BSV',st:'Confirmada'},
    {prof:2,i:2,dur:1,p:'Ana Souza',t:'Psiquiatria',st:'Aguardando'},
  ];
  const cor = {Confirmada:'var(--success-tint)','Em atendimento':'var(--info-tint)',Aguardando:'var(--warning-tint)'};
  const borda = {Confirmada:'var(--success-text)','Em atendimento':'var(--info-text)',Aguardando:'var(--warning-text)'};
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Quinta-feira, 16 de julho de 2026 · 3 profissionais em atendimento"
      actions={<><Button variant="secondary" icon="chevron-left"/><Button variant="secondary" icon="chevron-right"/><Button icon="plus">Novo agendamento</Button></>}>Agenda</Heading>
    <Card padded={false}>
      <div style={{display:'grid',gridTemplateColumns:'80px repeat(3,1fr)',borderBottom:'1px solid var(--border)'}}>
        <div/>
        {AGENDA_PROFS.map(p=><div key={p} style={{padding:'12px 14px',fontSize:13,fontWeight:600,color:'var(--text-title)',borderLeft:'1px solid var(--border)',display:'flex',alignItems:'center',gap:8}}><Avatar name={p.replace(/^Dra?\. /,'')} size={24}/>{p}</div>)}
      </div>
      <div style={{position:'relative',display:'grid',gridTemplateColumns:'80px repeat(3,1fr)'}}>
        <div>{horas.map(h=><div key={h} style={{height:64,fontSize:12,color:'var(--text-muted)',padding:'6px 12px',textAlign:'right',borderBottom:'1px solid var(--cinza-100)'}}>{h}</div>)}</div>
        {[0,1,2].map(c=><div key={c} style={{position:'relative',borderLeft:'1px solid var(--border)'}}>
          {horas.map(h=><div key={h} style={{height:64,borderBottom:'1px solid var(--cinza-100)'}}/>)}
          {blocos.filter(b=>b.prof===c).map((b,i)=><div key={i} style={{position:'absolute',left:6,right:6,top:b.i*64+4,height:b.dur*64-8,
            background:cor[b.st],borderLeft:'3px solid '+borda[b.st],borderRadius:8,padding:'8px 10px',boxSizing:'border-box',fontFamily:'var(--font-ui)'}}>
            <div style={{fontSize:13,fontWeight:600,color:'var(--text-title)'}}>{b.p}</div>
            <div style={{fontSize:12,color:'var(--text-body)'}}>{b.t}</div>
          </div>)}
        </div>)}
      </div>
    </Card>
  </div>;
}

export function TelaPacientes() {
  const [q, setQ] = React.useState('');
  const rows = PACIENTES.filter(p=>p.nome.toLowerCase().indexOf(q.toLowerCase())>=0);
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="2.847 pacientes ativos · 14 com carteirinha vencida"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="user-plus">Novo paciente</Button></>}>Pacientes</Heading>
    <div style={{display:'flex',gap:12,marginBottom:16}}>
      <SearchInput value={q} onChange={setQ} placeholder="Buscar por nome ou CPF…" style={{flex:1,maxWidth:420}}/>
      <Select pill options={['Todos os convênios','Unimed Intercâmbio','Amil','Petrobras']}/>
    </div>
    <Card padded={false}>
      <DataTable style={{border:'none',borderRadius:0}} empty="Nenhum paciente encontrado." columns={[
        {header:'Paciente',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.nome} size={28}/><b style={{fontWeight:600}}>{r.nome}</b></span>},
        {header:'CPF',key:'cpf',width:'160px'},
        {header:'Convênio',key:'conv'},
        {header:'Último atendimento',key:'ult',width:'170px'},
        {header:'Carteirinha',width:'170px',render:r=><Badge tone={r.cart==='Em dia'?'sucesso':r.cart==='Vencida'?'erro':'aviso'}>{r.cart}</Badge>},
        {header:'',width:'120px',align:'right',render:()=><Button size="sm" variant="secondary">Abrir ficha</Button>},
      ]} rows={rows}/>
    </Card>
  </div>;
}

export function TelaNaoRecriada({ nome }) {
  return <div style={{padding:'24px'}}><Heading>{nome}</Heading>
    <Card><EmptyState icon="layers" title="Tela não recriada neste UI kit"
      description="Existe no sistema, mas não foi reconstruída aqui. Consulte src/Clinica.Desktop/Views e src/Clinica.Modulo.* no repositório."/></Card></div>;
}
