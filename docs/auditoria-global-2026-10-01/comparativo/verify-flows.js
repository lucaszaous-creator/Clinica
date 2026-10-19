(async()=>{
 const checks=[];const wait=()=>new Promise(r=>setTimeout(r,65));
 const navigate=async hash=>{document.querySelector('dialog').close();location.hash=hash;await wait();};
 const click=(q)=>{const el=document.querySelector(q);if(!el)throw Error('Controle ausente: '+q);el.click();};
 const assert=(value,name)=>{if(!value)throw Error(name);checks.push(name);};
 await navigate('clinico/sessao/compare');
 const text=document.querySelector('.after textarea[data-draft]');text.value='Rascunho fictício preservado entre contextos.';text.dispatchEvent(new Event('input',{bubbles:true}));
 click('.after [data-action="clinical-context"]');click('[data-action="close-modal"]');
 assert(document.querySelector('.after textarea').value===text.value,'Rascunho preservado ao abrir e fechar contexto');
 click('[data-mode="after"]');await wait();assert(document.querySelector('.after textarea').value===text.value,'Rascunho preservado ao alternar comparação');
 await navigate('financeiro/contas/after');click('.after [data-action="postpone"]');document.querySelector('#new-due').value='2026-10-20';click('[data-action="save-due"]');assert(document.querySelector('.after tbody').textContent.includes('20/10/2026'),'Adiamento respeita data escolhida');
 await navigate('gerente/campanhas/after');click('.after [data-action="whatsapp"]');assert(document.querySelector('.after').textContent.includes('Envio ainda não confirmado'),'Abertura de WhatsApp não confirma envio');click('.after [data-action="manual-send"]');click('[data-action="confirm-manual"]');assert(document.querySelector('.after').textContent.includes('Envio confirmado manualmente'),'Confirmação manual é etapa própria');
 await navigate('crm/conversa/after');click('.after [data-action="close-chat"]');document.querySelector('#fail-refresh').checked=true;click('[data-action="save-chat"]');assert(document.querySelector('.after').textContent.includes('Conversa concluída; atualização pendente'),'Conclusão e falha de atualização ficam separadas');click('.after [data-action="refresh-chat"]');assert(!document.querySelector('.after').textContent.includes('atualização pendente'),'Retomada atualiza sem repetir conclusão');
 await navigate('faturamento/retornotiss/compare');assert(document.querySelector('.before tbody').textContent.includes('Aceita')&&document.querySelector('.after tbody').textContent.includes('Sem resposta'),'Guias ausentes passam de aceite padrão para sem resposta');
 await navigate('clinico/emitir/after');click('.after [data-action="emit"]');click('[data-action="close-modal"]');assert(!document.querySelector('.after [data-action="emit"]')&&document.querySelector('.after [data-action="print"]'),'Documento emitido oferece retomada, sem botão de reemissão');
 await navigate('preservado/portal/compare');assert(document.querySelectorAll('.stage').length===1&&document.querySelector('[data-mode="after"]').disabled,'Portal não oferece redesenho');
 click('#catalog-button');assert(document.querySelectorAll('.catalog-row').length===263,'Inventário completo possui 263 referências');
 document.querySelector('dialog').close();click('#reset-button');
 await navigate('recepcao/dia/compare');
 return JSON.stringify({checks,passed:checks.length});
})()
