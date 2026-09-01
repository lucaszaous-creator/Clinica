import React from 'react';
import { Heading, Button, Card, Select, KpiCard, LineChart, BarChart, RangeBar, DataTable, Avatar, Badge } from '../ds/index.js';
import { CONSULTAS, CARTEIRINHAS, SEMANAS, ATEND_SEMANA, ESPECIALIDADES, CAIXA, RECEB_CONV, GUIAS } from '../dados.js';

const chipSt = { Confirmada:'sucesso', 'Em atendimento':'info', Aguardando:'aviso', 'Check-in feito':'sucesso', Concluído:'sucesso' };

export function PainelRecepcao() {
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Quinta-feira, 16 de julho de 2026 · 42 consultas agendadas"
      actions={<><Button variant="secondary" icon="printer">Imprimir agenda</Button><Button icon="plus">Nova consulta</Button></>}>Painel do dia</Heading>
    <div style={{display:'grid',gridTemplateColumns:'repeat(4,1fr)',gap:24}}>
      <KpiCard icon="door-open" label="Chegadas até agora" value="31" progress={.74} delta={6.9} menu/>
      <KpiCard icon="armchair" label="Aguardando na recepção" value="6" progress={.3} tone="info" menu/>
      <KpiCard icon="phone" label="Confirmações pendentes" value="7" progress={.35} tone="apoio" menu/>
      <KpiCard icon="user-x" label="Faltas hoje" value="2" progress={.08} menu/>
    </div>
    <div style={{display:'grid',gridTemplateColumns:'2fr 1fr',gap:24,marginTop:24}}>
      <Card title="Fila da recepção" subtitle="Próximos pacientes por horário" padded={false}>
        <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} columns={[
          {header:'Horário',key:'h',width:'90px'},
          {header:'Paciente',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.p} size={28}/><span><div style={{fontWeight:600}}>{r.p}</div><div style={{color:'var(--text-muted)',fontSize:12}}>{r.conv}</div></span></span>},
          {header:'Profissional',key:'prof'},
          {header:'Situação',width:'150px',render:r=><Badge tone={chipSt[r.st]}>{r.st}</Badge>},
          {header:'',width:'140px',align:'right',render:r=>r.st==='Aguardando'?<Button size="sm">Fazer check-in</Button>:<Button size="sm" variant="secondary">Abrir ficha</Button>},
        ]} rows={CONSULTAS}/>
      </Card>
      <Card title="Carteirinhas a vencer" subtitle="Confirme com o paciente antes do atendimento">
        <div style={{display:'flex',flexDirection:'column',gap:12,marginTop:4}}>
          {CARTEIRINHAS.map((c,i)=><div key={i} style={{display:'flex',alignItems:'center',gap:10,fontFamily:'var(--font-ui)'}}>
            <Avatar name={c.nome} size={32}/>
            <span style={{flex:1,minWidth:0}}><div style={{fontSize:13,fontWeight:600,color:'var(--text-title)'}}>{c.nome}</div><div style={{fontSize:12,color:'var(--text-muted)'}}>{c.conv}</div></span>
            <Badge tone={c.tom}>{c.st}</Badge>
          </div>)}
        </div>
        <Button variant="secondary" size="sm" iconRight="chevron-right" style={{marginTop:16}}>Ver todas</Button>
      </Card>
    </div>
  </div>;
}

export function PainelClinico() {
  const [per, setPer] = React.useState('12 semanas');
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Seus atendimentos e pendências de prontuário · Dr. Otávio Lins"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="file-plus">Novo prontuário</Button></>}>Painel clínico</Heading>
    <div style={{display:'grid',gridTemplateColumns:'repeat(4,1fr)',gap:24}}>
      <KpiCard icon="calendar-check" label="Atendimentos hoje" value="14" progress={.7} delta={7.7} menu/>
      <KpiCard icon="pen-line" label="Prontuários a assinar" value="3" progress={.15} tone="apoio" menu/>
      <KpiCard icon="pill" label="Prescrições emitidas" value="9" progress={.45} delta={12.5} menu/>
      <KpiCard icon="rotate-ccw" label="Retornos na semana" value="21" progress={.6} tone="info" menu/>
    </div>
    <div style={{display:'grid',gridTemplateColumns:'2fr 1fr',gap:24,marginTop:24}}>
      <Card title="Atendimentos por semana" subtitle="Últimas 12 semanas"
        actions={<Select pill options={['12 semanas','6 meses']} value={per} onChange={setPer}/>}>
        <LineChart height={220} labels={SEMANAS} marker={11}
          tooltip={<span><b>Semana 12</b> · 17 atendimentos</span>}
          series={[{name:'Atendimentos',data:ATEND_SEMANA,color:'var(--serie-1)',fill:true}]}/>
      </Card>
      <Card title="Sessões por especialidade" subtitle="Julho de 2026">
        <div style={{display:'flex',flexDirection:'column',gap:18,marginTop:8}}>
          {ESPECIALIDADES.map((e,i)=><RangeBar key={i} label={e.label} value={e.value} fraction={e.fr} color={'var(--serie-'+(i+1)+')'}/>)}
        </div>
      </Card>
    </div>
    <Card title="Pacientes de hoje" subtitle="Sua agenda de 16/07/2026" padded={false} style={{marginTop:24}}
      actions={<Button variant="secondary" size="sm" iconRight="chevron-right" style={{marginRight:16}}>Ver agenda</Button>}>
      <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} columns={[
        {header:'Horário',key:'h',width:'90px'},
        {header:'Paciente',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.p} size={28}/><b style={{fontWeight:600}}>{r.p}</b></span>},
        {header:'Especialidade',key:'esp'},
        {header:'Situação',width:'150px',render:r=><Badge tone={chipSt[r.st]}>{r.st}</Badge>},
        {header:'',width:'160px',align:'right',render:()=><Button size="sm" variant="secondary">Abrir prontuário</Button>},
      ]} rows={CONSULTAS.filter(c=>c.prof==='Dr. Otávio Lins').concat(CONSULTAS.filter(c=>c.prof!=='Dr. Otávio Lins').slice(0,2))}/>
    </Card>
  </div>;
}

export function PainelFaturamento() {
  const [passo, setPasso] = React.useState('Diário');
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Guias, glosas e recebimentos dos convênios"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="file-check">Gerar lote TISS</Button></>}>Painel de faturamento</Heading>
    <div style={{display:'grid',gridTemplateColumns:'repeat(4,1fr)',gap:24}}>
      <KpiCard icon="receipt" label="Guias pendentes de baixa" value="23" progress={.4} tone="apoio" menu/>
      <KpiCard icon="wallet" label="Contas a receber" value="R$ 24.830" progress={.55} delta={9.2} menu/>
      <KpiCard icon="file-x" label="Glosas do mês" value="R$ 3.240" progress={.12} menu/>
      <KpiCard icon="send" label="Lotes TISS enviados" value="12" progress={.8} delta={20} tone="info" menu/>
    </div>
    <div style={{display:'grid',gridTemplateColumns:'2fr 1fr',gap:24,marginTop:24}}>
      <Card title="Fluxo de caixa diário" actions={<Select pill options={['Diário','Semanal']} value={passo} onChange={setPasso}/>}>
        <div style={{display:'flex',gap:20,margin:'4px 0 8px',fontSize:12,color:'var(--text-muted)'}}>
          <span style={{display:'flex',alignItems:'center',gap:6}}><span style={{width:8,height:8,borderRadius:4,background:'var(--serie-1)'}}/>Entradas</span>
          <span style={{display:'flex',alignItems:'center',gap:6}}><span style={{width:8,height:8,borderRadius:4,background:'var(--serie-3)'}}/>Saídas</span>
        </div>
        <BarChart height={200} data={CAIXA} marker={0}
          tooltip={<span><b>1 jul 2026</b> · Entradas: R$ 7,4k · Saídas: R$ 5,5k</span>}
          labels={CAIXA.map((_,i)=>i%2?null:String(i+1))}/>
      </Card>
      <Card title="Recebimentos por convênio" subtitle="Julho de 2026">
        <div style={{fontSize:'var(--text-kpi-grande-size)',fontWeight:500,letterSpacing:'-.02em',color:'var(--text-title)',margin:'8px 0 20px'}}>R$ 82.400</div>
        <div style={{display:'flex',flexDirection:'column',gap:18}}>
          {RECEB_CONV.map((e,i)=><RangeBar key={i} label={e.label} value={e.value} fraction={e.fr} color={'var(--serie-'+(i+1)+')'}/>)}
        </div>
      </Card>
    </div>
    <Card title="Pendências de baixa" subtitle="Guias por ordem de vencimento" padded={false} style={{marginTop:24}}
      actions={<Button variant="secondary" size="sm" iconRight="chevron-right" style={{marginRight:16}}>Ver todas</Button>}>
      <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} columns={[
        {header:'Guia',key:'g',width:'130px'},
        {header:'Paciente',key:'p'},
        {header:'Convênio',key:'conv'},
        {header:'Valor',key:'v',width:'110px',align:'right'},
        {header:'Vencimento',key:'venc',width:'120px'},
        {header:'Urgência',width:'140px',render:r=><Badge tone={r.tom}>{r.urg}</Badge>},
        {header:'',width:'120px',align:'right',render:()=><Button size="sm">Dar baixa</Button>},
      ]} rows={GUIAS}/>
    </Card>
  </div>;
}
