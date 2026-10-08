import {acoes,type Secao,type Contexto,type LinhaPagina} from './paginas';
const h=(v:unknown)=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
export function agenda(s:Secao,c:Contexto):string{
 const horarios=s.tabelas.find(t=>['horarios','ClinicoSemanaSessoes'].includes(t.chave));
 const faixas=s.tabelas.find(t=>['faixas','ClinicoSemanaGrade'].includes(t.chave));
 if(!horarios||!faixas)return '';
 const inicio=(l:LinhaPagina)=>l.celulas.InicioISO??l.celulas.DataHora??((l.celulas.DiaISO??'')+'T'+(l.celulas.Hora??'00:00'));
 const fim=(l:LinhaPagina)=>l.celulas.FimISO??l.celulas.Fim??'';
 const data=(v:string)=>/^\d{4}-\d{2}-\d{2}T/.test(v)?v.slice(0,10):'';
 const horario=(v:string)=>{const m=v.match(/T(\d\d):(\d\d)/);return m?Number(m[1])*60+Number(m[2]):480};
 const dias=[...new Set([...horarios.linhas.map(l=>data(inicio(l))),...faixas.linhas.map(l=>data(l.celulas.Quando??inicio(l)))])].filter(Boolean).sort();
 if(!dias.length)return '<div class="mensagem-web">A agenda aparecerá aqui após a consulta dos horários.</div>';
 const minutos=[...horarios.linhas.map(l=>horario(inicio(l))),...faixas.linhas.map(l=>horario(l.celulas.Quando??inicio(l)))];
 const de=Math.max(0,Math.floor(Math.min(480,...minutos)/60)*60),ate=Math.min(1440,Math.ceil(Math.max(1080,...horarios.linhas.map(l=>horario(fim(l))))/60)*60);
 const escala=2,altura=(ate-de)*escala;
 const horaTexto=(m:number)=>`${String(Math.floor(m/60)).padStart(2,'0')}:${String(m%60).padStart(2,'0')}`;
 return `<section class="agenda-visual" aria-label="Agenda por horário"><div class="agenda-rolagem"><div class="agenda-dias" style="grid-template-columns:60px repeat(${dias.length},minmax(220px,1fr))"><div class="agenda-horas" style="height:${altura+48}px">${Array.from({length:(ate-de)/60+1},(_,i)=>`<span style="top:${48+i*60*escala}px">${horaTexto(de+i*60)}</span>`).join('')}</div>${dias.map(dia=>{
  const linhas=horarios.linhas.filter(l=>data(inicio(l))===dia).sort((a,b)=>inicio(a).localeCompare(inicio(b)));
  const raias=new Map<string,number>();const fins:number[]=[];
  for(const l of linhas){const ini=horario(inicio(l)),fimL=horario(fim(l));let raia=fins.findIndex(f=>f<=ini);if(raia<0)raia=fins.length;fins[raia]=Math.max(ini+15,fimL);raias.set(l.id,raia)}
  const n=Math.max(1,fins.length);
  return `<div class="agenda-dia"><h3>${h(new Date(dia+'T12:00:00').toLocaleDateString('pt-BR',{weekday:'short',day:'2-digit',month:'2-digit'}))}</h3><div class="agenda-grade" style="height:${altura}px;--hora:${60*escala}px">${faixas.linhas.filter(l=>data(l.celulas.Quando??inicio(l))===dia&&l.celulas.Continuacao!=='Sim').map(l=>{const ini=horario(l.celulas.Quando??inicio(l)),bloqueio=l.celulas.Bloqueio;return `<div class="agenda-faixa ${bloqueio&&bloqueio!=='—'?'bloqueada':''}" style="top:${(ini-de)*escala}px" title="${h([l.celulas.Profissional,l.celulas.Sala,bloqueio,l.celulas.DicaDoVao].filter(Boolean).join(' · '))}">${bloqueio&&bloqueio!=='—'?h(bloqueio):acoes(l.acoes,{...c,tabela:faixas.chave,linha:l.id})}</div>`}).join('')}${linhas.map(l=>{const ini=horario(inicio(l)),dur=Math.max(15,horario(fim(l))-ini),raia=raias.get(l.id)!;return `<article class="agenda-compromisso" tabindex="0" aria-label="${h([horaTexto(ini),l.celulas.PacienteNome??l.celulas.Paciente,l.celulas.Profissional,l.celulas.Sala].filter(Boolean).join(' · '))}" style="top:${(ini-de)*escala}px;height:${dur*escala-3}px;left:calc(${raia/n*100}% + 3px);width:calc(${100/n}% - 6px)"><time>${h(horaTexto(ini))}–${h(horaTexto(horario(fim(l))))}</time><strong>${h(l.celulas.PacienteNome??l.celulas.Paciente)}</strong><small>${h([l.celulas.Modalidade,l.celulas.Profissional,l.celulas.Sala].filter(Boolean).join(' · '))}</small><span>${h(l.celulas.SituacaoDaFila??l.celulas.RegistroPendente??'')}</span>${acoes(l.acoes,{...c,tabela:horarios.chave,linha:l.id})}</article>`}).join('')}</div></div>`;
 }).join('')}</div></div></section>`;
}
