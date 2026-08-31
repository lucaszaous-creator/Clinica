import React from 'react';
import { Heading, Button, Card, Select, KpiCard, LineChart, BarChart, RangeBar, DataTable, Avatar, Badge } from '../ds/index.js';
import { MESES, OCUPACAO, SIMILARES, CAIXA, VOLUME, CONSULTAS } from '../dados.js';

const chip = { Confirmada: 'sucesso', 'Em atendimento': 'info', Aguardando: 'aviso' };

export function VisaoGeral() {
  const [periodo, setPeriodo] = React.useState('Mensal');
  const [ano, setAno] = React.useState('2026');
  const [passo, setPasso] = React.useState('Diário');
  return <div style={{padding:'24px 24px 32px'}}>
    <Heading subtitle="Acompanhe atendimentos, agenda e saúde financeira da clínica"
      actions={<><Button variant="secondary" icon="download">Exportar</Button><Button icon="plus">Nova consulta</Button></>}>
      Visão geral da clínica
    </Heading>

    <div style={{display:'grid',gridTemplateColumns:'repeat(4,1fr)',gap:24}}>
      <KpiCard icon="calendar-check" label="Consultas hoje" value="42" progress={.62} delta={8.3} menu/>
      <KpiCard icon="users" label="Pacientes ativos" value="2.847" progress={.74} delta={12.6} tone="apoio" menu/>
      <KpiCard icon="wallet" label="Faturamento do mês" value="R$ 148.290" progress={.81} delta={18.4} menu/>
      <KpiCard icon="gauge" label="Taxa de ocupação da agenda" value="87" suffix="%" progress={.87} delta={4.8} tone="info" menu/>
    </div>

    <div style={{display:'grid',gridTemplateColumns:'2fr 1fr',gap:24,marginTop:24}}>
      <Card title="Comparativo de 12 meses" subtitle="Crescimento de atendimentos ante clínicas de porte semelhante"
        actions={<Select pill options={['Mensal','Trimestral']} value={periodo} onChange={setPeriodo}/>}>
        <div style={{display:'flex',gap:20,margin:'4px 0 8px',fontSize:12,color:'var(--text-muted)'}}>
          <span style={{display:'flex',alignItems:'center',gap:6}}><span style={{width:14,height:3,borderRadius:2,background:'var(--serie-1)'}}/>Sua clínica</span>
          <span style={{display:'flex',alignItems:'center',gap:6}}><span style={{width:14,height:0,borderTop:'2px dashed var(--serie-comparacao)'}}/>Clínicas similares</span>
        </div>
        <LineChart height={230} labels={MESES} marker={5} yFormat={v=>String(v).replace('.',',')+'%'}
          tooltip={<span><b>Jun 2026</b> · Sua clínica: 5,7% · Similares: 3,9%</span>}
          series={[{name:'Sua clínica',data:OCUPACAO,color:'var(--serie-1)',fill:true},
                   {name:'Similares',data:SIMILARES,color:'var(--serie-comparacao)',dashed:true}]}/>
      </Card>

      <Card title="Receita por origem" subtitle="Julho de 2026">
        <div style={{fontSize:'var(--text-kpi-grande-size)',fontWeight:500,letterSpacing:'-.02em',color:'var(--text-title)',margin:'8px 0 20px'}}>R$ 148.290</div>
        <div style={{display:'flex',flexDirection:'column',gap:18}}>
          <RangeBar label="Convênios" value="R$ 82.400" fraction={.56} color="var(--serie-1)"/>
          <RangeBar label="Particular" value="R$ 46.800" fraction={.32} color="var(--serie-2)"/>
          <RangeBar label="Procedimentos e exames" value="R$ 19.090" fraction={.13} color="var(--serie-3)"/>
        </div>
      </Card>
    </div>

    <div style={{display:'grid',gridTemplateColumns:'1fr 2fr',gap:24,marginTop:24}}>
      <Card title="Fluxo de caixa diário" actions={<Select pill options={['Diário','Semanal']} value={passo} onChange={setPasso}/>}>
        <div style={{display:'flex',gap:20,margin:'4px 0 8px',fontSize:12,color:'var(--text-muted)'}}>
          <span style={{display:'flex',alignItems:'center',gap:6}}><span style={{width:8,height:8,borderRadius:4,background:'var(--serie-1)'}}/>Entradas</span>
          <span style={{display:'flex',alignItems:'center',gap:6}}><span style={{width:8,height:8,borderRadius:4,background:'var(--serie-3)'}}/>Saídas</span>
        </div>
        <BarChart height={210} data={CAIXA} marker={0}
          tooltip={<span><b>1 mai 2026</b> · Entradas: R$ 7,4k · Saídas: R$ 5,5k</span>}
          labels={CAIXA.map((_,i)=>i%2?null:String(i+1))}/>
      </Card>

      <Card title="Volume de atendimentos" subtitle="Consultas concluídas por mês"
        actions={<Select pill options={['2026','2025']} value={ano} onChange={setAno}/>}>
        <LineChart height={210} labels={MESES.slice(0,11)} marker={9}
          tooltip={<span><b>Out 2026</b> · 118 atendimentos</span>}
          series={[{name:'Atendimentos',data:VOLUME,color:'var(--serie-3)',fill:true}]}/>
      </Card>
    </div>

    <Card title="Próximas consultas" subtitle="Quinta-feira, 16 de julho de 2026" padded={false} style={{marginTop:24}}
      actions={<Button variant="secondary" size="sm" iconRight="chevron-right" style={{marginRight:16}}>Ver agenda</Button>}>
      <DataTable style={{border:'none',borderRadius:0,borderTop:'1px solid var(--border)'}} columns={[
        {header:'Horário',key:'h',width:'90px'},
        {header:'Paciente',render:r=><span style={{display:'flex',alignItems:'center',gap:10}}><Avatar name={r.p} size={28}/><span><div style={{fontWeight:600}}>{r.p}</div><div style={{color:'var(--text-muted)',fontSize:12}}>{r.conv}</div></span></span>},
        {header:'Profissional',key:'prof'},
        {header:'Especialidade',key:'esp'},
        {header:'Situação',width:'150px',render:r=><Badge tone={chip[r.st]}>{r.st}</Badge>},
        {header:'',width:'120px',align:'right',render:()=><Button size="sm" variant="secondary">Abrir ficha</Button>},
      ]} rows={CONSULTAS}/>
    </Card>
  </div>;
}
