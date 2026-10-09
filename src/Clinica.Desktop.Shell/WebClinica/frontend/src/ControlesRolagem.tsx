import {useEffect, useState} from 'react';

/** Controles da lista: apenas deslocam a página, sem acionar a ponte clínica. */
export default function ControlesRolagem() {
 const [posicao, setPosicao] = useState({visivel:false, inicio:true, fim:false});
 useEffect(() => {
  let quadro = 0;
  const medir = () => {
   const pagina = document.scrollingElement;
   if (!pagina) return;
   const limite = pagina.scrollHeight - pagina.clientHeight;
   const proxima = {visivel:limite > 2, inicio:pagina.scrollTop <= 1, fim:pagina.scrollTop >= limite - 1};
   setPosicao(anterior => anterior.visivel === proxima.visivel && anterior.inicio === proxima.inicio && anterior.fim === proxima.fim ? anterior : proxima);
  };
  const agendar = () => {cancelAnimationFrame(quadro); quadro = requestAnimationFrame(medir);};
  const observador = new ResizeObserver(agendar);
  observador.observe(document.getElementById('root')!);
  window.addEventListener('scroll', agendar, {passive:true});
  window.addEventListener('resize', agendar);
  medir();
  return () => {observador.disconnect(); cancelAnimationFrame(quadro); window.removeEventListener('scroll', agendar); window.removeEventListener('resize', agendar);};
 }, []);
 const rolar = (direcao:number) => window.scrollBy({top:direcao * Math.max(120, window.innerHeight * .7), behavior:window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth'});
 if (!posicao.visivel) return null;
 return <nav className="controles-rolagem" aria-label="Rolagem da lista">
  <button type="button" aria-label="Rolar para cima" title="Rolar para cima" disabled={posicao.inicio} onClick={() => rolar(-1)}><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M12 19V5m-6 6 6-6 6 6"/></svg></button>
  <span className="rolagem-divisor" aria-hidden="true"/>
  <button type="button" aria-label="Rolar para baixo" title="Rolar para baixo" disabled={posicao.fim} onClick={() => rolar(1)}><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M12 5v14m-6-6 6 6 6-6"/></svg></button>
 </nav>;
}
