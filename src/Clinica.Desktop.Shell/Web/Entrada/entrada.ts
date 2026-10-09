(() => {
 type Estado={tipo:string;modo:string;nomeApp:string;mensagem:string;erro:boolean;ocupado:boolean;podeSalvar:boolean;limparSenhas:number};
 type Bridge={postMessage:(value:unknown)=>void;addEventListener:(event:string,callback:(event:MessageEvent<Estado>)=>void)=>void};
 const bridge=(window as unknown as {chrome:{webview:Bridge}}).chrome.webview;
 const el=<T extends HTMLElement=HTMLElement>(id:string)=>document.getElementById(id) as T;
 const input=(id:string)=>el<HTMLInputElement>(id);let modo='',limpar=-1,ocupado=false;
 const textos:Record<string,[string,string,string]>={entrar:['Bem-vindo de volta','Entre com seu usuário e senha para continuar.','Entrar'],primeiro:['Vamos começar','Crie o primeiro acesso da direção para configurar sua clínica.','Criar acesso'],trocar:['Defina sua nova senha','Sua senha é provisória. Escolha uma nova senha pessoal para continuar.','Salvar nova senha'],conexao:['Conecte sua clínica','Configure a conexão usada por este computador.','Testar conexão'],aviso:['Atenção','','Entendi'],pergunta:['Precisamos da sua escolha','','Continuar']};
 const enviar=(acao:string)=>bridge.postMessage({acao,...((modo==='entrar'||modo==='primeiro'||modo==='trocar')?{nome:input('nome').value,login:input('login').value,senha:input('senha').value,repetida:input('repetida').value}:modo==='conexao'?{conexao:el<HTMLTextAreaElement>('conexao').value}:{})});
 bridge.addEventListener('message',({data:s})=>{
  if(s.tipo!=='estado')return;const mudou=modo!==s.modo;modo=s.modo;ocupado=s.ocupado;document.body.dataset.modo=modo;
  const [titulo,descricao,confirmar]=textos[modo]??textos.entrar;el('app').textContent=s.nomeApp;el('titulo').textContent=titulo;el('descricao').textContent=descricao;
  const exibir=(grupo:string,sim:boolean)=>{el('grupo-'+grupo).hidden=!sim;const campo=el('grupo-'+grupo).querySelector('input,textarea') as HTMLInputElement;campo.required=sim;campo.disabled=!sim||s.ocupado;};
  exibir('nome',modo==='primeiro');exibir('login',modo==='entrar'||modo==='primeiro');exibir('senha',['entrar','primeiro','trocar'].includes(modo));exibir('repetida',modo==='primeiro'||modo==='trocar');exibir('conexao',modo==='conexao');
  el<HTMLButtonElement>('confirmar').disabled=s.ocupado;el('confirmar').textContent=s.ocupado?'Aguarde…':confirmar;el('salvar').hidden=modo!=='conexao';el<HTMLButtonElement>('salvar').disabled=!s.podeSalvar||s.ocupado;el<HTMLButtonElement>('cancelar').disabled=s.ocupado;el('cancelar').hidden=modo==='aviso';el('cancelar').textContent=modo==='pergunta'?'Agora não':'Sair';el<HTMLButtonElement>('mostrar').disabled=s.ocupado;el('mensagem').textContent=s.mensagem;el('mensagem').hidden=!s.mensagem;el('mensagem').classList.toggle('erro',s.erro);el('rodape').hidden=['conexao','aviso','pergunta'].includes(modo);
  if(limpar!==s.limparSenhas||mudou){input('senha').value='';input('repetida').value='';input('senha').type='password';el('mostrar').textContent='Mostrar';el('mostrar').setAttribute('aria-pressed','false');limpar=s.limparSenhas;}
  if(mudou)requestAnimationFrame(()=>input(modo==='primeiro'?'nome':modo==='trocar'?'senha':modo==='conexao'?'conexao':modo==='entrar'?'login':'confirmar').focus());
 });
 el('form').addEventListener('submit',e=>{e.preventDefault();if(!ocupado)enviar(modo==='conexao'?'testar':'confirmar');});
 el('salvar').addEventListener('click',()=>{if(!ocupado)enviar('salvar');});el('cancelar').addEventListener('click',()=>{if(!ocupado)bridge.postMessage({acao:'fechar'});});
 el('mostrar').addEventListener('click',()=>{const visivel=input('senha').type==='password';input('senha').type=visivel?'text':'password';el('mostrar').textContent=visivel?'Ocultar':'Mostrar';el('mostrar').setAttribute('aria-label',visivel?'Ocultar senha':'Mostrar senha');el('mostrar').setAttribute('aria-pressed',String(visivel));});
 el('conexao').addEventListener('input',()=>bridge.postMessage({acao:'invalidar'}));
 document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!ocupado){e.preventDefault();bridge.postMessage({acao:'fechar'});}});
 bridge.postMessage({acao:'pronto'});
})();
