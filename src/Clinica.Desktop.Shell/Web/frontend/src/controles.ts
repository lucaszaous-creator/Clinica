import type {Campo} from './paginas';
const h=(v:unknown)=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
const imagens=new Map<string,string>();
let camera:MediaStream|null=null,cameraId='',desenhando=false;
export function emDesenho(){return desenhando}
export function controleEspecial(f:Campo,comum:string,id:string,valor:unknown):string|null{
 if(f.tipo==='assinatura'){
  let png='';try{png=JSON.parse(String(valor||'{}')).png??''}catch{}
  if(png.startsWith('data:image/png;base64,'))imagens.set(id,png);else imagens.delete(id);
  return `<div class="campo-web campo-largo assinatura-web"><label>${h(f.rotulo)}</label><canvas ${comum} data-assinatura tabindex="0" aria-label="Área para assinatura manuscrita"></canvas><div class="acoes-web"><button type="button" class="botao" data-limpar-assinatura="${h(id)}">Limpar traço</button><small>Assine com o mouse, caneta ou toque.</small></div></div>`;
 }
 if(f.tipo==='imagem'){
  const foto=imagens.get(id)??(typeof valor==='string'&&/^data:image\/(jpeg|png|webp);base64,/.test(valor)?valor:'');
  return `<div class="campo-web campo-largo foto-web" ${comum}><label>${h(f.rotulo)}</label>${foto?`<img class="foto-previa" src="${h(foto)}" alt="Prévia da fotografia"/>`:''}<video data-camera="${h(id)}" autoplay playsinline ${cameraId===id?'':'hidden'}></video><div class="acoes-web"><label class="botao">Escolher imagem<input type="file" data-foto-arquivo="${h(id)}" accept="image/jpeg,image/png,image/bmp"/></label><button type="button" class="botao" data-iniciar-camera="${h(id)}">Usar câmera</button><button type="button" class="botao" data-tirar-foto="${h(id)}" ${cameraId===id?'':'hidden'}>Fotografar</button><button type="button" class="botao" data-parar-camera ${cameraId===id?'':'hidden'}>Desligar câmera</button></div><p class="erro" data-erro-foto role="alert"></p></div>`;
 }
 if(f.tipo==='mapa-corporal'){
  type Forma={tipo:string;cx?:number;cy?:number;rx?:number;ry?:number;x?:number;y?:number;largura?:number;altura?:number;raio?:number};
  let mapa:{largura:number;altura:number;formas:Forma[];coluna:{x:number;topo:number;base:number};pontos:{numero:number;face:string;x:number;y:number;nome:string;tecnica:string;observacao:string;selecionado?:boolean}[]}={largura:220,altura:460,formas:[],coluna:{x:110,topo:86,base:262},pontos:[]};try{const v=JSON.parse(String(valor||'{}'));if(Array.isArray(v.formas)&&Array.isArray(v.pontos))mapa=v}catch{}
  const n=(v:unknown)=>typeof v==='number'&&Number.isFinite(v)?v:0;
  const corpo=mapa.formas.map(f=>f.tipo==='elipse'?`<ellipse cx="${n(f.cx)}" cy="${n(f.cy)}" rx="${n(f.rx)}" ry="${n(f.ry)}"/>`:`<rect x="${n(f.x)}" y="${n(f.y)}" width="${n(f.largura)}" height="${n(f.altura)}" rx="${n(f.raio)}"/>`).join('');
  return `<div class="campo-web campo-largo mapa-web"><label>${h(f.rotulo)}</label><p>Marque a região no desenho. Pelo teclado, use as setas e Enter.</p><div class="mapa-faces">${['Frente','Costas'].map(face=>`<figure><figcaption>${face}</figcaption><svg ${comum.replace(`id="${h(id)}"`,`id="${h(id)}-${face}"`)} data-mapa="${face}" data-x=".5" data-y=".5" data-largura="${n(mapa.largura)}" data-altura="${n(mapa.altura)}" viewBox="0 0 ${n(mapa.largura)} ${n(mapa.altura)}" role="button" tabindex="0" aria-label="Marcar ponto: ${face}"><g fill="#f1f4fa" stroke="#a6b4cb" stroke-width="1.5">${corpo}</g>${face==='Costas'?`<line x1="${n(mapa.coluna.x)}" x2="${n(mapa.coluna.x)}" y1="${n(mapa.coluna.topo)}" y2="${n(mapa.coluna.base)}" stroke="#9aaac5" stroke-dasharray="3 4"/>`:''}${mapa.pontos.filter(p=>p.face===face&&Number.isFinite(p.x)&&Number.isFinite(p.y)).map(p=>`<g><circle cx="${p.x*mapa.largura}" cy="${p.y*mapa.altura}" r="${p.selecionado?9:7}" fill="${p.selecionado?'#304b9e':'#637bcc'}"/><text x="${p.x*mapa.largura+10}" y="${p.y*mapa.altura+4}" font-size="10">${p.numero}</text><title>${h(p.nome)} · ${h(p.tecnica)} · ${h(p.observacao)}</title></g>`).join('')}<circle class="cursor-mapa" cx="${mapa.largura/2}" cy="${mapa.altura/2}" r="9" fill="none" stroke="#364fc7" stroke-dasharray="3 2"/></svg></figure>`).join('')}</div></div>`;
 }
 return null;
}
export function ligarControles(raiz:HTMLElement,enviar:(el:HTMLElement,valor:unknown)=>void){
 if(cameraId&&!document.getElementById(cameraId)){pararCamera()}
 for(const key of imagens.keys())if(!document.getElementById(key))imagens.delete(key);
 raiz.querySelectorAll<HTMLVideoElement>('[data-camera]').forEach(v=>{if(v.dataset.camera===cameraId&&camera){v.srcObject=camera;v.hidden=false}});
 raiz.querySelectorAll<HTMLCanvasElement>('canvas[data-assinatura]').forEach(canvas=>{
  let rect=canvas.getBoundingClientRect();canvas.width=Math.round(rect.width*2);canvas.height=Math.round(rect.height*2);
  const ctx=canvas.getContext('2d')!;ctx.scale(2,2);ctx.strokeStyle='#172033';ctx.lineWidth=2.2;ctx.lineCap='round';ctx.lineJoin='round';
  const png=imagens.get(canvas.id);if(png){const im=new Image();im.onload=()=>ctx.drawImage(im,0,0,rect.width,rect.height);im.src=png}
  let pintou=false;
  canvas.onpointerdown=e=>{if(canvas.hasAttribute('disabled'))return;e.preventDefault();rect=canvas.getBoundingClientRect();canvas.setPointerCapture(e.pointerId);desenhando=true;pintou=false;ctx.beginPath();ctx.moveTo(e.clientX-rect.left,e.clientY-rect.top)};
  canvas.onpointermove=e=>{if(!desenhando||!canvas.hasPointerCapture(e.pointerId))return;rect=canvas.getBoundingClientRect();ctx.lineTo(e.clientX-rect.left,e.clientY-rect.top);ctx.stroke();pintou=true};
  const concluir=(e:PointerEvent)=>{if(!canvas.hasPointerCapture(e.pointerId))return;canvas.releasePointerCapture(e.pointerId);desenhando=false;if(!pintou)return;const png=canvas.toDataURL('image/png');imagens.set(canvas.id,png);enviar(canvas,JSON.stringify({png,largura:rect.width,altura:rect.height,temTraco:true}))};
  canvas.onpointerup=concluir;canvas.onpointercancel=concluir;
 });
 raiz.querySelectorAll<HTMLElement>('[data-mapa]').forEach(mapa=>{
  const marcar=(x:number,y:number)=>{if(mapa.hasAttribute('disabled'))return;enviar(mapa,JSON.stringify({face:mapa.dataset.mapa,x:Math.max(0,Math.min(1,x)),y:Math.max(0,Math.min(1,y))}))};
  mapa.onclick=e=>{const svg=mapa as unknown as SVGSVGElement;const matrix=svg.getScreenCTM();if(!matrix)return;const point=new DOMPoint(e.clientX,e.clientY).matrixTransform(matrix.inverse());marcar(point.x/Number(mapa.dataset.largura),point.y/Number(mapa.dataset.altura))};
  mapa.onkeydown=e=>{if(!['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Enter',' '].includes(e.key))return;e.preventDefault();let x=Number(mapa.dataset.x),y=Number(mapa.dataset.y);if(e.key==='Enter'||e.key===' '){marcar(x,y);return}x=Math.max(0,Math.min(1,x+(e.key==='ArrowRight'?.02:e.key==='ArrowLeft'?-.02:0)));y=Math.max(0,Math.min(1,y+(e.key==='ArrowDown'?.02:e.key==='ArrowUp'?-.02:0)));mapa.dataset.x=String(x);mapa.dataset.y=String(y);mapa.querySelector('.cursor-mapa')?.setAttribute('cx',String(x*Number(mapa.dataset.largura)));mapa.querySelector('.cursor-mapa')?.setAttribute('cy',String(y*Number(mapa.dataset.altura)))};
 });
 raiz.querySelectorAll<HTMLElement>('[data-limpar-assinatura]').forEach(b=>b.onclick=()=>{const c=document.getElementById(b.dataset.limparAssinatura!) as HTMLCanvasElement;if(c.hasAttribute('disabled'))return;c.getContext('2d')?.clearRect(0,0,c.width,c.height);imagens.delete(c.id);enviar(c,'')});
 raiz.querySelectorAll<HTMLInputElement>('[data-foto-arquivo]').forEach(input=>input.onchange=async()=>{const el=document.getElementById(input.dataset.fotoArquivo!)!;const f=input.files?.[0];if(!f)return;if(f.size>8*1024*1024||!['image/jpeg','image/png','image/bmp'].includes(f.type)){erro(el,'Escolha uma imagem JPEG, PNG ou BMP de até 8 MB.');return}const data=await new Promise<string>((resolve,reject)=>{const r=new FileReader();r.onload=()=>resolve(String(r.result));r.onerror=reject;r.readAsDataURL(f)});imagens.set(el.id,data);enviar(el,data)});
 raiz.querySelectorAll<HTMLElement>('[data-iniciar-camera]').forEach(b=>b.onclick=async()=>{const el=document.getElementById(b.dataset.iniciarCamera!)!;try{pararCamera();camera=await navigator.mediaDevices.getUserMedia({video:{facingMode:'user'},audio:false});cameraId=el.id;const v=el.querySelector('video')!;v.srcObject=camera;v.hidden=false;el.querySelectorAll<HTMLElement>('[data-tirar-foto],[data-parar-camera]').forEach(n=>n.hidden=false)}catch{erro(el,'Não foi possível abrir a câmera. Verifique a permissão do Windows ou escolha uma imagem.')}});
 raiz.querySelectorAll<HTMLElement>('[data-tirar-foto]').forEach(b=>b.onclick=()=>{const el=document.getElementById(b.dataset.tirarFoto!)!,v=el.querySelector('video')!;if(!v.videoWidth)return;const c=document.createElement('canvas');c.width=v.videoWidth;c.height=v.videoHeight;c.getContext('2d')!.drawImage(v,0,0);const data=c.toDataURL('image/jpeg',.92);pararCamera();imagens.set(el.id,data);enviar(el,data)});
 raiz.querySelectorAll<HTMLElement>('[data-parar-camera]').forEach(b=>b.onclick=()=>{pararCamera();b.closest('.foto-web')?.querySelectorAll<HTMLElement>('video,[data-tirar-foto],[data-parar-camera]').forEach(e=>e.hidden=true)});
}
function erro(el:HTMLElement,texto:string){el.querySelector('[data-erro-foto]')!.textContent=texto}
function pararCamera(){camera?.getTracks().forEach(t=>t.stop());camera=null;cameraId=''}
window.addEventListener('beforeunload',pararCamera);
