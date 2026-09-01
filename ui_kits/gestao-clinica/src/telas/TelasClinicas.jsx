import React from 'react';
import { Heading, Button, Card, KpiCard, DataTable, Avatar, Badge, Select, SearchInput } from '../ds/index.js';
import { ModalProntuario, ModalExame, ModalPrescricao } from './ModaisClinicos.jsx';
import { CONFIRMACOES, PRONTUARIOS, EXAMES, PRESCRICOES } from '../dados.js';

export function TelaConfirmacoes() {
  const [f, setF] = React.useState('Todas');
  const rows = f==='Todas'?CONFIRMACOES:CONFIRMACOES.filter(c=>c.st===f);
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Consultas de amanhã, sexta-feira, 17 de julho de 2026 · 7 pendentes"
      actions={<><Button variant="secondary" icon="message-circle">Enviar lembretes</Button><Button icon="phone">Ligar para o próximo</Button></>}>Confirmações</Heading>
    <div style={{display:'grid',gridTemplateColumns:'repeat(4,1fr)',gap:24,marginBottom:24}}>
      <KpiCard icon="calendar" label="Consultas de amanhã" value="38" progress={.9} menu/>
      <KpiCard icon="check-circle" label="Confirmadas" value="31" progress={.82} tone="info" menu/>
      <KpiCard icon="phone-missed" label="Sem resposta" value="3" progress={.08} tone="apoio" menu/>
      <KpiCard icon="user-x" label="Recusadas" value="1" progress={.03} menu/>
    </div>
    <Card title="Pendentes de confirmação" padded={false}
      actions={<Select pill options={['Todas','Sem resposta','Não confirmada','Recusou']} value={f} onChange={setF} style={{marginRight:16}}/>}>
      <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} empty="Nenhuma pendência com esse filtro." columns={[
        {header:'Horário',key:'h',width:'90px'},
        {header:'Paciente',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.p} size={28}/><span><div style={{fontWeight:600}}>{r.p}</div><div style={{color:'var(--text-muted)',fontSize:12}}>{r.tel}</div></span></span>},
        {header:'Profissional',key:'prof'},
        {header:'Especialidade',key:'esp'},
        {header:'Situação',width:'150px',render:r=><Badge tone={r.tom}>{r.st}</Badge>},
        {header:'',width:'220px',align:'right',render:()=><span style={{display:'inline-flex',gap:8}}><Button size="sm">Confirmar</Button><Button size="sm" variant="secondary">Reagendar</Button></span>},
      ]} rows={rows}/>
    </Card>
  </div>;
}

export function TelaProntuarios() {
  const [q, setQ] = React.useState('');
  const [aberto, setAberto] = React.useState(null);
  const rows = PRONTUARIOS.filter(r=>r.p.toLowerCase().indexOf(q.toLowerCase())>=0);
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Evoluções e anamneses dos seus atendimentos · Dr. Otávio Lins"
      actions={<><Button variant="secondary" icon="pen-line">Assinar pendentes (3)</Button><Button icon="file-plus">Novo prontuário</Button></>}>Prontuários</Heading>
    <div style={{display:'flex',gap:12,marginBottom:16}}>
      <SearchInput value={q} onChange={setQ} placeholder="Buscar por paciente…" style={{flex:1,maxWidth:420}}/>
    </div>
    <Card padded={false}>
      <DataTable style={{border:'none',borderRadius:0}} empty="Nenhum prontuário encontrado." columns={[
        {header:'Paciente',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.p} size={28}/><b style={{fontWeight:600}}>{r.p}</b></span>},
        {header:'Data',key:'data',width:'120px'},
        {header:'Especialidade',key:'esp'},
        {header:'Tipo',key:'tipo',width:'120px'},
        {header:'Situação',width:'140px',render:r=><Badge tone={r.tom}>{r.st}</Badge>},
        {header:'',width:'200px',align:'right',render:r=>r.st==='A assinar'?<span style={{display:'inline-flex',gap:8}}><Button size="sm" onClick={()=>setAberto(r)}>Assinar</Button><Button size="sm" variant="secondary" onClick={()=>setAberto(r)}>Abrir</Button></span>:<Button size="sm" variant="secondary" onClick={()=>setAberto(r)}>Abrir</Button>},
      ]} rows={rows}/>
    </Card>
    {aberto?<ModalProntuario reg={aberto} onClose={()=>setAberto(null)}/>:null}
  </div>;
}

export function TelaExames() {
  const [aberto, setAberto] = React.useState(null);
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Pedidos e resultados dos seus pacientes"
      actions={<Button icon="file-plus">Novo pedido de exame</Button>}>Exames</Heading>
    <Card padded={false}>
      <DataTable style={{border:'none',borderRadius:0}} columns={[
        {header:'Paciente',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.p} size={28}/><b style={{fontWeight:600}}>{r.p}</b></span>},
        {header:'Exame',key:'exame'},
        {header:'Pedido em',key:'ped',width:'130px'},
        {header:'Situação',width:'190px',render:r=><Badge tone={r.tom}>{r.st}</Badge>},
        {header:'',width:'160px',align:'right',render:r=>r.st==='Resultado disponível'?<Button size="sm" onClick={()=>setAberto(r)}>Ver resultado</Button>:<Button size="sm" variant="secondary" onClick={()=>setAberto(r)}>Detalhes</Button>},
      ]} rows={EXAMES}/>
    </Card>
    {aberto?<ModalExame ex={aberto} onClose={()=>setAberto(null)}/>:null}
  </div>;
}

export function TelaPrescricoes() {
  const [aberto, setAberto] = React.useState(null);
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Receitas emitidas nos últimos 7 dias · Dr. Otávio Lins"
      actions={<Button icon="pill">Nova prescrição</Button>}>Prescrições</Heading>
    <Card padded={false}>
      <DataTable style={{border:'none',borderRadius:0}} columns={[
        {header:'Paciente',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.p} size={28}/><b style={{fontWeight:600}}>{r.p}</b></span>},
        {header:'Data',key:'data',width:'120px'},
        {header:'Medicação',key:'med'},
        {header:'Tipo',width:'160px',render:r=><Badge tone={r.tipo==='Controle especial'?'aviso':'neutro'}>{r.tipo}</Badge>},
        {header:'',width:'190px',align:'right',render:r=><span style={{display:'inline-flex',gap:8}}><Button size="sm" onClick={()=>setAberto(r)}>Ver folha</Button><Button size="sm" variant="secondary" icon="printer" onClick={()=>setAberto(r)}>Imprimir</Button></span>},
      ]} rows={PRESCRICOES}/>
    </Card>
    {aberto?<ModalPrescricao rx={aberto} onClose={()=>setAberto(null)}/>:null}
  </div>;
}
