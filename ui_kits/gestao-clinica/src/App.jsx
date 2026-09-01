import React from 'react';
import { Sidebar, Topbar, Breadcrumb, Select } from './ds/index.js';
import logo from './assets/logo-cor.png';
import { VisaoGeral } from './telas/VisaoGeral.jsx';
import { PainelRecepcao, PainelClinico, PainelFaturamento } from './telas/Paineis.jsx';
import { TelaAgenda, TelaPacientes, TelaNaoRecriada } from './telas/Telas.jsx';
import { TelaConfirmacoes, TelaProntuarios, TelaExames, TelaPrescricoes } from './telas/TelasClinicas.jsx';
import { TelaProfissionais, TelaConvenios, TelaReceber, TelaPendencias, TelaLotes, TelaGlosas, TelaConfig, TelaAjuda } from './telas/TelasFinanceiras.jsx';

/* Registro de telas: tela nova entra AQUI e vira item de `groups` do módulo dono
   (novas-telas.md §7). Item de menu sem tela cai no EmptyState de "não recriada". */
const TELAS = {
  VisaoGeral, PainelRecepcao, PainelClinico, PainelFaturamento,
  TelaAgenda, TelaPacientes, TelaConfirmacoes, TelaProntuarios, TelaExames, TelaPrescricoes,
  TelaProfissionais, TelaConvenios, TelaReceber, TelaPendencias, TelaLotes, TelaGlosas,
  TelaConfig, TelaAjuda,
};

/* TODO item de sidebar tem ícone lucide — nunca item só-texto (README §2, novas-telas.md §1). */
const AJUSTES = {label:'Configurações',items:[
  {id:'ajuda',label:'Ajuda & suporte',icon:'life-buoy'},
  {id:'config',label:'Configurações da clínica',icon:'settings'}]};

export const MODULOS = [
 {id:'recepcao',nome:'Recepção',user:'Rita Campos',cargo:'Recepção',
  groups:[{label:'Principal',items:[
    {id:'painel',label:'Painel do dia',icon:'layout-dashboard'},
    {id:'agenda',label:'Agenda',icon:'calendar'},
    {id:'pacientes',label:'Pacientes',icon:'users'},
    {id:'confirmacoes',label:'Confirmações',icon:'phone',badge:'7'}]},AJUSTES],
  telas:{painel:'PainelRecepcao',agenda:'TelaAgenda',pacientes:'TelaPacientes',confirmacoes:'TelaConfirmacoes',ajuda:'TelaAjuda',config:'TelaConfig'}},
 {id:'clinico',nome:'Clínico',user:'Otávio Lins',cargo:'Médico · Acupuntura',
  groups:[{label:'Principal',items:[
    {id:'painel',label:'Painel clínico',icon:'layout-dashboard'},
    {id:'agenda',label:'Agenda',icon:'calendar'}]},
   {label:'Clínico',items:[
    {id:'prontuarios',label:'Prontuários',icon:'file-text',badge:'3'},
    {id:'exames',label:'Exames',icon:'microscope'},
    {id:'prescricoes',label:'Prescrições',icon:'pill'}]},AJUSTES],
  telas:{painel:'PainelClinico',agenda:'TelaAgenda',prontuarios:'TelaProntuarios',exames:'TelaExames',prescricoes:'TelaPrescricoes',ajuda:'TelaAjuda',config:'TelaConfig'}},
 {id:'gerente',nome:'Gerente geral',user:'Beatriz Rocha',cargo:'Gerente geral',
  groups:[{label:'Principal',items:[
    {id:'painel',label:'Visão geral',icon:'layout-dashboard'},
    {id:'agenda',label:'Agenda',icon:'calendar'},
    {id:'pacientes',label:'Pacientes',icon:'users'},
    {id:'profissionais',label:'Profissionais',icon:'stethoscope'}]},
   {label:'Clínico',items:[
    {id:'prontuarios',label:'Prontuários',icon:'file-text'},
    {id:'exames',label:'Exames',icon:'microscope'},
    {id:'prescricoes',label:'Prescrições',icon:'pill'}]},
   {label:'Financeiro',items:[
    {id:'faturamento',label:'Faturamento',icon:'receipt'},
    {id:'convenios',label:'Convênios',icon:'building-2'},
    {id:'receber',label:'Contas a receber',icon:'wallet',badge:'R$ 24,8k'}]},AJUSTES],
  telas:{painel:'VisaoGeral',agenda:'TelaAgenda',pacientes:'TelaPacientes',profissionais:'TelaProfissionais',prontuarios:'TelaProntuarios',exames:'TelaExames',prescricoes:'TelaPrescricoes',faturamento:'PainelFaturamento',convenios:'TelaConvenios',receber:'TelaReceber',ajuda:'TelaAjuda',config:'TelaConfig'}},
 {id:'faturamento',nome:'Faturamento',user:'Lucas Andrade',cargo:'Faturamento',
  groups:[{label:'Principal',items:[
    {id:'painel',label:'Painel de faturamento',icon:'layout-dashboard'},
    {id:'pendencias',label:'Pendências de baixa',icon:'inbox',badge:'23'}]},
   {label:'Financeiro',items:[
    {id:'lotes',label:'Guias e lotes TISS',icon:'file-check'},
    {id:'glosas',label:'Glosas',icon:'file-x'},
    {id:'convenios',label:'Convênios',icon:'building-2'},
    {id:'receber',label:'Contas a receber',icon:'wallet',badge:'R$ 24,8k'}]},AJUSTES],
  telas:{painel:'PainelFaturamento',pendencias:'TelaPendencias',lotes:'TelaLotes',glosas:'TelaGlosas',convenios:'TelaConvenios',receber:'TelaReceber',ajuda:'TelaAjuda',config:'TelaConfig'}},
];

const rotulo = (mod, tela) => {
  let l = tela;
  mod.groups.forEach(g => g.items.forEach(i => { if (i.id === tela) l = i.label; }));
  return l;
};

/* ?modulo=recepcao|clinico|gerente|faturamento — o equivalente aos quatro .html da
   referência (cada um só muda o módulo inicial); o seletor no rodapé da sidebar troca
   de papel em qualquer um. ?tela=<id> abre direto numa tela (usado nas capturas). */
const doEndereco = chave => new URLSearchParams(window.location.search).get(chave);

export function App() {
  const inicial = MODULOS.some(m => m.id === doEndereco('modulo')) ? doEndereco('modulo') : 'gerente';
  const [modId, setModId] = React.useState(inicial);
  const mod = MODULOS.find(m => m.id === modId);
  const [tela, setTela] = React.useState(doEndereco('tela') || 'painel');
  const trocar = nome => { const m = MODULOS.find(x => x.nome === nome); if (m) { setModId(m.id); setTela('painel'); } };
  const Tela = TELAS[mod.telas[tela]];
  const ini = mod.user.trim().split(/\s+/).slice(0,2).map(p=>p[0]).join('').toUpperCase();

  return <div style={{display:'flex',height:'100%',background:'var(--surface-app)'}}>
    <Sidebar logoSrc={logo} productName={mod.nome} groups={mod.groups} activeId={tela} onSelect={setTela}
      footer={<div style={{borderTop:'1px solid var(--border)',fontFamily:'var(--font-ui)',paddingTop:8}}>
        <div style={{fontSize:11,fontWeight:700,letterSpacing:'.08em',textTransform:'uppercase',color:'var(--text-muted)',padding:'4px 12px 6px'}}>Módulo</div>
        <div style={{padding:'0 8px 10px'}}><Select options={MODULOS.map(m=>m.nome)} value={mod.nome} onChange={trocar}/></div>
        <div style={{display:'flex',alignItems:'center',gap:10,padding:'8px 12px 4px',borderTop:'1px solid var(--border)'}}>
          <span style={{width:28,height:28,borderRadius:'50%',background:'var(--brand-tint)',color:'var(--brand)',display:'inline-flex',alignItems:'center',justifyContent:'center',fontSize:11,fontWeight:600}}>{ini}</span>
          <span style={{minWidth:0}}><div style={{fontSize:13,fontWeight:600,color:'var(--text-title)'}}>{mod.user}</div><div style={{fontSize:11,color:'var(--text-muted)'}}>{mod.cargo}</div></span>
        </div>
      </div>}/>
    <div style={{flex:1,display:'flex',flexDirection:'column',minWidth:0,overflow:'auto'}}>
      {/* A Topbar SEMPRE traz a busca em pílula com lupa + sino + avatar — nunca remover
          nem simplificar (README §1). Tudo isso já vem do componente. */}
      <Topbar left={<Breadcrumb items={[mod.nome, rotulo(mod, tela)]}/>}
        searchPlaceholder="Buscar paciente, guia ou tela…" notifications={3} user={mod.user}/>
      {Tela ? <Tela/> : <TelaNaoRecriada nome={rotulo(mod, tela)}/>}
    </div>
  </div>;
}
