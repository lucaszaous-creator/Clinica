import React from 'react';
import { Heading, Button, Card, KpiCard, DataTable, Avatar, Badge, Select, RangeBar,
         SectionTitle, Ficha, Switch, Input, Label, Expander, Icon } from '../ds/index.js';
import { PROFISSIONAIS, CONVENIOS, RECEB_CONV, RECEBER, GUIAS, LOTES, GLOSAS, GLOSAS_MOTIVOS } from '../dados.js';

export function TelaProfissionais() {
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="4 profissionais ativos · taxa média de ocupação 80%"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="user-plus">Novo profissional</Button></>}>Profissionais</Heading>
    <Card padded={false}>
      <DataTable style={{border:'none',borderRadius:0}} columns={[
        {header:'Profissional',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.nome.replace(/^Dra?\. /,'')} size={28}/><span><div style={{fontWeight:600}}>{r.nome}</div><div style={{color:'var(--text-muted)',fontSize:12}}>{r.crm}</div></span></span>},
        {header:'Especialidade',key:'esp'},
        {header:'Dias de agenda',key:'ag',width:'150px'},
        {header:'Atendimentos no mês',key:'at',width:'170px',align:'right'},
        {header:'Ocupação',width:'180px',render:r=><span style={{display:'flex',alignItems:'center',gap:8}}>
          <span style={{flex:1,height:6,borderRadius:999,background:'var(--cinza-100)',overflow:'hidden'}}><span style={{display:'block',height:'100%',width:(r.ocup*100)+'%',borderRadius:999,background:r.ocup>.85?'var(--serie-1)':'var(--serie-2)'}}/></span>
          <b style={{fontSize:13,fontWeight:600}}>{Math.round(r.ocup*100)}%</b></span>},
        {header:'',width:'130px',align:'right',render:()=><Button size="sm" variant="secondary">Ver agenda</Button>},
      ]} rows={PROFISSIONAIS}/>
    </Card>
  </div>;
}

export function TelaConvenios() {
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Operadoras credenciadas, prazos e receita do mês"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="building-2">Novo convênio</Button></>}>Convênios</Heading>
    <div style={{display:'grid',gridTemplateColumns:'2fr 1fr',gap:24}}>
      <Card title="Operadoras credenciadas" padded={false}>
        <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} columns={[
          {header:'Convênio',render:r=><span><div style={{fontWeight:600}}>{r.nome}</div><div style={{color:'var(--text-muted)',fontSize:12}}>{r.ans}</div></span>},
          {header:'Prazo de repasse',key:'prazo',width:'140px'},
          {header:'Tabela',key:'tabela',width:'110px'},
          {header:'Pacientes',key:'pac',width:'100px',align:'right'},
          {header:'Receita no mês',key:'receita',width:'130px',align:'right'},
          {header:'Situação',width:'150px',render:r=><Badge tone={r.tom}>{r.st}</Badge>},
        ]} rows={CONVENIOS}/>
      </Card>
      <Card title="Receita por convênio" subtitle="Julho de 2026">
        <div style={{display:'flex',flexDirection:'column',gap:18,marginTop:8}}>
          {RECEB_CONV.map((e,i)=><RangeBar key={i} label={e.label} value={e.value} fraction={e.fr} color={'var(--serie-'+(i+1)+')'}/>)}
        </div>
      </Card>
    </div>
  </div>;
}

export function TelaReceber() {
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Repasses de convênio e parcelamentos em aberto"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="check">Conciliar recebimentos</Button></>}>Contas a receber</Heading>
    <div style={{display:'grid',gridTemplateColumns:'repeat(4,1fr)',gap:24,marginBottom:24}}>
      <KpiCard icon="wallet" label="Total em aberto" value="R$ 24.830" progress={.55} menu/>
      <KpiCard icon="clock" label="Vence em 7 dias" value="R$ 9.840" progress={.4} tone="info" menu/>
      <KpiCard icon="alert-triangle" label="Vencido" value="R$ 4.560" progress={.18} tone="apoio" menu/>
      <KpiCard icon="calendar-clock" label="Prazo médio de repasse" value="38" suffix="dias" progress={.62} menu/>
    </div>
    <Card title="Títulos em aberto" padded={false}>
      <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} columns={[
        {header:'Origem',key:'origem'},
        {header:'Vencimento',key:'venc',width:'130px'},
        {header:'Valor',key:'valor',width:'110px',align:'right'},
        {header:'Situação',width:'170px',render:r=><Badge tone={r.tom}>{r.st}</Badge>},
        {header:'',width:'150px',align:'right',render:r=>r.tom==='erro'?<Button size="sm">Cobrar</Button>:<Button size="sm" variant="secondary">Detalhes</Button>},
      ]} rows={RECEBER}/>
    </Card>
  </div>;
}

export function TelaPendencias() {
  const [f, setF] = React.useState('Todas');
  const rows = f==='Todas'?GUIAS:GUIAS.filter(g=>f==='Urgentes'?g.tom!=='neutro':g.tom==='neutro');
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="23 guias aguardando baixa · dê baixa assim que possível para não perder o faturamento"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="check">Dar baixa em lote</Button></>}>Pendências de baixa</Heading>
    <Card title="Guias por ordem de vencimento" padded={false}
      actions={<Select pill options={['Todas','Urgentes','Sem urgência']} value={f} onChange={setF} style={{marginRight:16}}/>}>
      <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} empty="Nenhuma guia com esse filtro." columns={[
        {header:'Guia',key:'g',width:'130px'},
        {header:'Paciente',key:'p'},
        {header:'Convênio',key:'conv'},
        {header:'Valor',key:'v',width:'110px',align:'right'},
        {header:'Vencimento',key:'venc',width:'120px'},
        {header:'Urgência',width:'140px',render:r=><Badge tone={r.tom}>{r.urg}</Badge>},
        {header:'',width:'120px',align:'right',render:()=><Button size="sm">Dar baixa</Button>},
      ]} rows={rows}/>
    </Card>
  </div>;
}

export function TelaLotes() {
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Lotes TISS enviados às operadoras neste mês"
      actions={<><Button variant="secondary" icon="download">Exportar XML</Button><Button icon="send">Enviar lote L-119</Button></>}>Guias e lotes TISS</Heading>
    <Card padded={false}>
      <DataTable style={{border:'none',borderRadius:0}} columns={[
        {header:'Lote',key:'lote',width:'90px'},
        {header:'Convênio',key:'conv'},
        {header:'Guias',key:'guias',width:'80px',align:'right'},
        {header:'Valor',key:'valor',width:'110px',align:'right'},
        {header:'Envio',key:'envio',width:'120px'},
        {header:'Situação',width:'150px',render:r=><Badge tone={r.tom}>{r.st}</Badge>},
        {header:'',width:'130px',align:'right',render:r=>r.st==='Em preparação'?<Button size="sm">Fechar lote</Button>:<Button size="sm" variant="secondary">Detalhes</Button>},
      ]} rows={LOTES}/>
    </Card>
  </div>;
}

export function TelaGlosas() {
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="R$ 3.240 glosados em julho · 2 guias a recursar"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="reply">Recursar selecionadas</Button></>}>Glosas</Heading>
    <div style={{display:'grid',gridTemplateColumns:'2fr 1fr',gap:24}}>
      <Card title="Guias glosadas" padded={false}>
        <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} columns={[
          {header:'Guia',key:'guia',width:'130px'},
          {header:'Paciente',key:'p'},
          {header:'Motivo',render:r=><span><div style={{fontWeight:600,fontSize:13}}>{r.motivo}</div><div style={{color:'var(--text-muted)',fontSize:12}}>{r.conv}</div></span>},
          {header:'Valor',key:'valor',width:'110px',align:'right'},
          {header:'Situação',width:'150px',render:r=><Badge tone={r.tom}>{r.st}</Badge>},
          {header:'',width:'110px',align:'right',render:r=>r.st==='A recursar'?<Button size="sm">Recursar</Button>:<Button size="sm" variant="secondary">Abrir</Button>},
        ]} rows={GLOSAS}/>
      </Card>
      <Card title="Glosas por motivo" subtitle="Julho de 2026">
        <div style={{display:'flex',flexDirection:'column',gap:18,marginTop:8}}>
          {GLOSAS_MOTIVOS.map((e,i)=><RangeBar key={i} label={e.label} value={e.value} fraction={e.fr} color={'var(--serie-'+(i%3+1)+')'}/>)}
        </div>
      </Card>
    </div>
  </div>;
}

export function TelaConfig() {
  const [aviso, setAviso] = React.useState(true);
  const [lembrete, setLembrete] = React.useState(true);
  return <div style={{padding:'24px 24px 32px',maxWidth:860}}>
    <Heading subtitle="Dados cadastrais, agenda e preferências de notificação"
      actions={<Button icon="save">Salvar alterações</Button>}>Configurações da clínica</Heading>
    <Card>
      <SectionTitle style={{marginTop:0}}>Dados da clínica</SectionTitle>
      <div style={{display:'grid',gridTemplateColumns:'repeat(3,1fr)',gap:'4px 24px'}}>
        <Ficha label="Razão social">Clínica SemDor Ltda.</Ficha>
        <Ficha label="CNPJ">12.345.678/0001-90</Ficha>
        <Ficha label="CNES">7 654 321</Ficha>
      </div>
      <div style={{display:'grid',gridTemplateColumns:'2fr 1fr',gap:'0 24px'}}>
        <span><Label>Endereço</Label><Input value="Av. Atlântica, 1240 · Macaé/RJ" onChange={()=>{}}/></span>
        <span><Label>Telefone</Label><Input value="(22) 2762-4410" onChange={()=>{}}/></span>
      </div>
      <SectionTitle>Agenda</SectionTitle>
      <div style={{display:'grid',gridTemplateColumns:'repeat(3,1fr)',gap:'0 24px'}}>
        <span><Label>Duração padrão da consulta</Label><Select options={['40 minutos','30 minutos','60 minutos']}/></span>
        <span><Label>Início do expediente</Label><Select options={['08:00','07:00','09:00']}/></span>
        <span><Label>Fim do expediente</Label><Select options={['18:00','17:00','19:00']}/></span>
      </div>
      <SectionTitle>Notificações</SectionTitle>
      <div style={{display:'flex',flexDirection:'column',gap:12,paddingTop:4}}>
        <Switch label="Avisar recepção quando o profissional chamar o próximo paciente" checked={aviso} onChange={setAviso}/>
        <Switch label="Enviar lembrete de consulta por WhatsApp na véspera" checked={lembrete} onChange={setLembrete}/>
      </div>
    </Card>
  </div>;
}

export function TelaAjuda() {
  return <div style={{padding:'24px 24px 32px',maxWidth:860}}>
    <Heading subtitle="Dúvidas frequentes e canais de atendimento"
      actions={<Button variant="secondary" icon="message-circle">Falar com o suporte</Button>}>Ajuda &amp; suporte</Heading>
    <div style={{display:'flex',flexDirection:'column',gap:10}}>
      <Expander title="Como dar baixa em uma guia de convênio?" defaultOpen>Abra Faturamento › Pendências de baixa, localize a guia e clique em Dar baixa. Guias com vencimento próximo aparecem primeiro.</Expander>
      <Expander title="O que fazer quando o convênio glosa uma guia?">Abra Financeiro › Glosas, verifique o motivo e clique em Recursar. Recursos de autorização exigem o 2º código obtido em até 24h.</Expander>
      <Expander title="Como cadastrar um novo profissional?">Em Principal › Profissionais, clique em Novo profissional e informe CRM, especialidade e dias de agenda.</Expander>
      <Expander title="Como exportar relatórios?">Todas as telas com o botão Exportar geram planilha; os lotes TISS exportam XML no padrão ANS.</Expander>
    </div>
    <Card style={{marginTop:16}}>
      <div style={{display:'flex',alignItems:'center',gap:12,fontFamily:'var(--font-ui)'}}>
        <Icon name="life-buoy" size={20} style={{color:'var(--brand)'}}/>
        <span style={{flex:1}}><div style={{fontSize:14,fontWeight:600,color:'var(--text-title)'}}>Suporte SemDor</div>
        <div style={{fontSize:13,color:'var(--text-muted)'}}>Seg–Sex, 8h–18h · (22) 2762-4410 · suporte@semdor.com.br</div></span>
        <Button size="sm" variant="secondary" icon="phone">Ligar</Button>
      </div>
    </Card>
  </div>;
}
