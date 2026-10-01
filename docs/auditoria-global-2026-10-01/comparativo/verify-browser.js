(async () => {
 const errors=[];const handler=e=>errors.push(e.message);window.addEventListener('error',handler);
 const failures=[];const routes=[];
 for(const s of window.PROPOSAL.screens){
  const mod=s.module==='comum'?'recepcao':s.module;
  location.hash=`${mod}/${s.id}/compare`;
  await new Promise(resolve=>setTimeout(resolve,35));
  const expected=s.module==='preservado'?1:2;
  const stages=document.querySelectorAll('.stage');
  if(stages.length!==expected||window.proposalInspect.state.screen!==s.id)failures.push({screen:s.id,expected,actual:stages.length,state:window.proposalInspect.state});
  if([...stages].some(x=>x.textContent.includes('undefined')||x.querySelector('.canvas')?.textContent.length<70))failures.push({screen:s.id,reason:'Conteúdo inválido'});
  if(document.documentElement.scrollWidth>innerWidth+2)failures.push({screen:s.id,reason:'Overflow horizontal da página'});
  routes.push(s.id);
 }
 window.removeEventListener('error',handler);
 location.hash='clinico/sessao/compare';
 return JSON.stringify({routes:routes.length,failures,errors});
})()
