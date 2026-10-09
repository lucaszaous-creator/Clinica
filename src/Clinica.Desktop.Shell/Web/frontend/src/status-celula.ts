import './status-celula.css';
const h=(v:string)=>v.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
const concluido=new Set(['concluido','concluida','realizado','realizada','baixado','baixada']);
const pendente=new Set(['pendente','em aberto','atrasado','atrasada','conclusao pendente']);
const interrompido=new Set(['cancelado','cancelada','falta','glosado','glosada']);
const agendado=new Set(['marcado','marcada','confirmado','confirmada','agendado','agendada']);
const emCurso=new Set(['em atendimento','em sala','em andamento','iniciado','iniciada']);
const aguardando=new Set(['aguardando','aguardando atendimento','aguardando confirmacao','a confirmar','nao confirmado','nao confirmada']);
/** Somente estados explicitamente registrados recebem cor. O texto do host permanece integral. */
export function celulaApresentada(tipo:string,valor:string,privado=false):string{
 const texto=h(privado?valor.replace(/R\$\s*[-+−]?\s*[\d.,]+/g,'R$ ••••'):valor);
 if(tipo!=='status')return texto;
 const chave=valor.trim().normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase();
 const classe=concluido.has(chave)?'estado-concluido':pendente.has(chave)||aguardando.has(chave)?'estado-pendente':interrompido.has(chave)?'estado-interrompido':agendado.has(chave)?'estado-agendado':emCurso.has(chave)?'estado-em-curso':'estado-neutro';
 return `<span class="etiqueta-web ${classe}">${texto}</span>`;
}
