const DS=window.ClNicaFaturamentoDesignSystem_bd26af;
const {Button,Card,Input,Select,Checkbox,DatePicker,Heading,Label,DataTable,UrgencyDot,KpiCard,AlertBanner,Icon}=DS;
const CONVENIOS=["Unimed Costa do Sol (Padrão)","Unimed Intercâmbio","Amil","Petrobras"];
const PEND=[
{u:'vermelho',p:'Maria da Silva',c:'Unimed Intercâmbio',d:'14/07/2026',g:'—',tipo:'2º código (24h)'},
{u:'vermelho',p:'Carlos Nunes',c:'Amil',d:'14/07/2026',g:'88231',tipo:'Baixa da guia'},
{u:'amarelo',p:'João Pereira',c:'Amil',d:'15/07/2026',g:'88240',tipo:'Baixa da guia'},
{u:'amarelo',p:'Rita Campos',c:'Petrobras',d:'15/07/2026',g:'—',tipo:'2º código (24h)'},
{u:'verde',p:'Ana Souza',c:'Petrobras',d:'16/07/2026',g:'88255',tipo:'Baixa da guia'},
];
function TelaPendencias({onBaixa}){
return <div>
<Heading>Pendências de faturamento</Heading>
<AlertBanner tone="danger" icon="bell" style={{marginBottom:14}}>Existem {PEND.length} guias pendentes de baixa. Dê baixa assim que possível para não perder o faturamento.</AlertBanner>
<Card title="Guias e códigos pendentes">
<DataTable columns={[
{header:'',width:'30px',render:r=><UrgencyDot level={r.u}/>},
{header:'Paciente',key:'p'},
{header:'Convênio',key:'c'},
{header:'Atendimento',key:'d'},
{header:'Guia',key:'g'},
{header:'Pendência',key:'tipo'},
{header:'',width:'110px',render:r=><Button size="sm" onClick={()=>onBaixa(r)}>Dar baixa</Button>},
]} rows={PEND}/>
</Card>
</div>;
}
function TelaPacientes(){
const[nome,setNome]=React.useState('');const[cpf,setCpf]=React.useState('');const[conv,setConv]=React.useState(CONVENIOS[0]);const[sexo,setSexo]=React.useState('Feminino');
const[lista,setLista]=React.useState([
{nome:'Maria da Silva',cpf:'123.456.789-00',conv:'Unimed Intercâmbio'},
{nome:'João Pereira',cpf:'987.654.321-00',conv:'Amil'},
{nome:'Ana Souza',cpf:'456.789.123-00',conv:'Petrobras'},
]);
return <div>
<Heading>Pacientes</Heading>
<div style={{display:'flex',gap:16,alignItems:'flex-start'}}>
<Card title="Cadastrar paciente" style={{width:360,flexShrink:0}}>
<Label>Nome completo</Label><Input value={nome} onChange={setNome}/>
<Label>CPF</Label><Input value={cpf} onChange={setCpf}/>
<Label>Sexo (usado pela Petrobras)</Label><Select options={["Feminino","Masculino"]} value={sexo} onChange={setSexo}/>
<Label>Convênio</Label><Select options={CONVENIOS} value={conv} onChange={setConv}/>
<div style={{marginTop:12}}><Button onClick={()=>{if(nome){setLista([{nome,cpf,conv},...lista]);setNome('');setCpf('');}}}>Salvar paciente</Button></div>
</Card>
<div style={{flex:1}}>
<Label style={{margin:'0 0 3px'}}>Buscar paciente (nome ou CPF)</Label>
<div style={{display:'flex',gap:8,marginBottom:10}}><Input placeholder="Digite para buscar…" style={{flex:1}}/><Button variant="secondary" icon="refresh-cw">Atualizar</Button></div>
<DataTable columns={[{header:'Nome',key:'nome'},{header:'CPF',key:'cpf'},{header:'Convênio',key:'conv'}]} rows={lista}/>
</div>
</div>
</div>;
}
function TelaNovoAtendimento({onGerar}){
const[pac,setPac]=React.useState('Maria da Silva');const[conv,setConv]=React.useState(CONVENIOS[1]);const[data,setData]=React.useState('2026-07-16');const[app,setApp]=React.useState(false);const[obs,setObs]=React.useState('');
return <div>
<Heading>Novo atendimento</Heading>
<div style={{display:'flex',gap:16,alignItems:'flex-start'}}>
<Card style={{width:380,flexShrink:0}}>
<Label>Buscar paciente (nome ou CPF)</Label><Input value={pac} onChange={setPac}/>
<Label>Convênio</Label><Select options={CONVENIOS} value={conv} onChange={setConv}/>
<Label>Data do atendimento</Label><DatePicker value={data} onChange={setData}/>
<div style={{margin:'10px 0'}}><Checkbox checked={app} onChange={setApp}>Possui app da Unimed (gera QR Code)</Checkbox></div>
<Label>Observações</Label><Input multiline rows={3} value={obs} onChange={setObs}/>
<div style={{marginTop:12,display:'flex',gap:8}}><Button icon="stethoscope" onClick={()=>onGerar({pac,conv,data})}>Lançar e gerar códigos</Button><Button variant="secondary">Limpar</Button></div>
</Card>
<Card title="Regras por convênio" style={{flex:1}}>
<div style={{fontSize:13,color:'var(--text-body)',lineHeight:'22px'}}>
• Unimed Padrão: código no app ou por ligação.<br/>
• Unimed Intercâmbio: <b>2 códigos</b> — o 2º deve ser obtido em +24h por ligação.<br/>
• Amil: guia gerada no portal; baixa após atendimento.<br/>
• Petrobras: exige sexo do paciente no cadastro.
</div>
</Card>
</div>
</div>;
}
function TelaConsultarGuias(){
const rows=[
{g:'88231',p:'Carlos Nunes',c:'Amil',d:'14/07/2026',s:'Pendente'},
{g:'88240',p:'João Pereira',c:'Amil',d:'15/07/2026',s:'Pendente'},
{g:'88255',p:'Ana Souza',c:'Petrobras',d:'16/07/2026',s:'Baixada'},
{g:'88198',p:'Maria da Silva',c:'Unimed Intercâmbio',d:'10/07/2026',s:'Baixada'},
];
return <div>
<Heading>Consultar guias</Heading>
<Card>
<div style={{display:'flex',gap:8,marginBottom:12,alignItems:'flex-end'}}>
<div style={{flex:1}}><Label style={{margin:'0 0 3px'}}>Guia ou paciente</Label><Input placeholder="Número da guia ou nome…"/></div>
<div style={{width:220}}><Label style={{margin:'0 0 3px'}}>Convênio</Label><Select options={['Todos'].concat(CONVENIOS)}/></div>
<Button icon="search">Buscar</Button>
</div>
<DataTable columns={[
{header:'Guia',key:'g',width:'80px'},{header:'Paciente',key:'p'},{header:'Convênio',key:'c'},{header:'Data',key:'d'},
{header:'Situação',render:r=><span style={{background:r.s==='Baixada'?'var(--success-tint)':'var(--warning-tint)',color:r.s==='Baixada'?'var(--success-text)':'var(--text-title)',borderRadius:6,padding:'2px 8px',fontSize:12,fontWeight:600}}>{r.s}</span>},
]} rows={rows}/>
</Card>
</div>;
}
function TelaAgenda(){
const[dia,setDia]=React.useState(16);
const rows=[
{h:'08:00',p:'Maria da Silva',c:'Unimed Intercâmbio',t:'Acupuntura'},
{h:'09:00',p:'João Pereira',c:'Amil',t:'Acupuntura'},
{h:'10:30',p:'Rita Campos',c:'Petrobras',t:'BSV'},
{h:'14:00',p:'Ana Souza',c:'Petrobras',t:'Acupuntura'},
];
return <div>
<Heading>Agenda</Heading>
<Card>
<div style={{display:'flex',alignItems:'center',gap:10,marginBottom:12}}>
<Button variant="secondary" size="sm" icon="chevron-left" onClick={()=>setDia(dia-1)}></Button>
<div style={{fontSize:16,fontWeight:600,color:'var(--text-title)',minWidth:190,textAlign:'center'}}>{String(dia).padStart(2,'0')}/07/2026 — quinta-feira</div>
<Button variant="secondary" size="sm" icon="chevron-right" onClick={()=>setDia(dia+1)}></Button>
<div style={{flex:1}}></div>
<Button icon="plus">Novo agendamento</Button>
</div>
<DataTable columns={[
{header:'Hora',key:'h',width:'70px'},{header:'Paciente',key:'p'},{header:'Convênio',key:'c'},{header:'Procedimento',key:'t'},
{header:'',width:'130px',render:()=><Button size="sm" variant="secondary">Iniciar atendimento</Button>},
]} rows={rows}/>
</Card>
</div>;
}
function TelaParametros(){
const[cs,setCs]=React.useState('Data Source=clinica.db');
return <div>
<Heading>Parâmetros</Heading>
<div style={{display:'flex',gap:16,alignItems:'flex-start'}}>
<Card title="Banco de dados" style={{width:420,flexShrink:0}}>
<Label>Connection string</Label><Input mono value={cs} onChange={setCs}/>
<div style={{marginTop:12,display:'flex',gap:8}}><Button>Salvar</Button><Button variant="secondary" icon="refresh-cw">Testar conexão</Button></div>
</Card>
<Card title="Alertas de pendências" style={{flex:1}}>
<div style={{display:'flex',flexDirection:'column',gap:8}}>
<Checkbox checked>Exibir aviso de pendências ao abrir o sistema</Checkbox>
<Checkbox checked>Alertar 2º código (Intercâmbio) após 24h</Checkbox>
<Checkbox>Enviar resumo diário por e-mail</Checkbox>
</div>
</Card>
</div>
</div>;
}
function TelaRelatorios({onCapa}){
return <div>
<Heading>Relatórios</Heading>
<div style={{display:'grid',gridTemplateColumns:'repeat(4,1fr)',gap:12,marginBottom:16}}>
<KpiCard label="Códigos gerados" value={139}/>
<KpiCard label="Baixados" value={36} tone="success"/>
<KpiCard label="Pendentes" value={103} tone="danger"/>
<KpiCard label="Taxa de baixa" value={26} suffix="%" tone="brand"/>
</div>
<Card title="Faturamento por convênio (mês atual)">
<DataTable columns={[
{header:'Convênio',key:'c'},{header:'Atendimentos',key:'a'},{header:'Baixados',key:'b'},{header:'Pendentes',key:'p'},
{header:'',width:'130px',render:()=><Button size="sm" icon="printer" onClick={onCapa}>Gerar capa</Button>},
]} rows={[
{c:'Unimed Costa do Sol (Padrão)',a:48,b:14,p:34},
{c:'Unimed Intercâmbio',a:37,b:9,p:28},
{c:'Amil',a:32,b:8,p:24},
{c:'Petrobras',a:22,b:5,p:17},
]}/>
</Card>
</div>;
}
function TelaNaoRecriada({nome}){
return <div><Heading>{nome}</Heading><Card><div style={{fontSize:13,color:'var(--text-muted)'}}>Tela existente no app, não recriada neste UI kit. Consulte src/Clinica.Desktop/Views/ no repositório.</div></Card></div>;
}
Object.assign(window,{TelaPendencias,TelaPacientes,TelaNovoAtendimento,TelaConsultarGuias,TelaAgenda,TelaParametros,TelaRelatorios,TelaNaoRecriada,PEND,CONVENIOS});
