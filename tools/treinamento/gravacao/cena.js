// A CENA da videoaula (out/2026), injetada na página da aula: o cursor, o destaque do elemento
// da vez, a legenda de cada passo e as cartelas de abertura e fim. Fica FORA do <body> (no
// <html>), para o Blazor nunca a tocar ao redesenhar a tela. Sem narração: a legenda é quem
// ensina, então ela fica na tela o tempo de ler — quem decide o tempo é o gravar.js.
(() => {
  if (window.cenaDaAula) return;
  const raiz = document.createElement('div');
  raiz.id = 'cena-aula';
  raiz.innerHTML = `
<style>
  #cena-aula { position: fixed; inset: 0; pointer-events: none; z-index: 2147483000;
    font-family: Inter, "Segoe UI", system-ui, sans-serif; }
  #cena-aula .cursor { position: absolute; left: 50%; top: 60%; width: 26px; height: 26px;
    transition: left .85s cubic-bezier(.45,.05,.25,1), top .85s cubic-bezier(.45,.05,.25,1), opacity .3s;
    filter: drop-shadow(0 2px 3px rgba(15,23,42,.35)); opacity: 0; }
  #cena-aula .clique { position: absolute; width: 18px; height: 18px; margin: -9px 0 0 -9px; border-radius: 50%;
    background: rgba(18,57,149,.35); transform: scale(0); opacity: 0; }
  #cena-aula .clique.vai { animation: cena-clique .55s ease-out; }
  @keyframes cena-clique { 0% { transform: scale(.4); opacity: .9 } 100% { transform: scale(3.2); opacity: 0 } }
  #cena-aula .destaque { position: absolute; border-radius: 10px; opacity: 0;
    box-shadow: 0 0 0 3px #F59E0B, 0 0 0 9px rgba(245,158,11,.22), 0 0 0 4000px rgba(15,23,42,.18);
    transition: left .5s ease, top .5s ease, width .5s ease, height .5s ease, opacity .35s ease; }
  #cena-aula .legenda { position: absolute; left: 50%; transform: translate(-50%, 12px); width: min(1060px, 86vw);
    background: rgba(17,34,74,.96); color: #fff; border-radius: 14px; padding: 18px 26px 20px;
    box-shadow: 0 18px 40px rgba(15,23,42,.35); opacity: 0; transition: opacity .35s ease, transform .35s ease; }
  #cena-aula .legenda.visivel { opacity: 1; transform: translate(-50%, 0); }
  #cena-aula .legenda.embaixo { bottom: 26px; } #cena-aula .legenda.emcima { top: 96px; }
  #cena-aula .legenda .passo { font-size: 13px; letter-spacing: .12em; text-transform: uppercase; color: #FCD34D; font-weight: 700; }
  #cena-aula .legenda .titulo { font-size: 24px; font-weight: 700; margin-top: 4px; line-height: 1.25; }
  #cena-aula .legenda .texto { font-size: 18px; line-height: 1.5; margin-top: 8px; color: #E2E8F0; }
  #cena-aula .legenda .barra { position: absolute; left: 0; bottom: 0; height: 4px; border-radius: 0 0 14px 14px; background: #F59E0B; width: 0; }
  #cena-aula .cartela { position: absolute; inset: 0; display: flex; flex-direction: column; justify-content: center;
    padding: 0 12vw; color: #fff; opacity: 0; transition: opacity .6s ease;
    background: linear-gradient(135deg, #0F2C73 0%, #123995 55%, #1E4FC2 100%); }
  #cena-aula .cartela.visivel { opacity: 1; }
  #cena-aula .cartela .modulo { font-size: 16px; letter-spacing: .18em; text-transform: uppercase; color: #FCD34D; font-weight: 700; }
  #cena-aula .cartela .titulo { font-size: 52px; font-weight: 800; margin-top: 14px; line-height: 1.1; }
  #cena-aula .cartela .texto { font-size: 22px; line-height: 1.5; margin-top: 18px; max-width: 900px; color: #DBE4FF; }
  #cena-aula .cartela .lista { margin-top: 26px; font-size: 18px; color: #DBE4FF; line-height: 1.7; }
  #cena-aula .cartela .rodape { position: absolute; left: 12vw; bottom: 7vh; font-size: 14px; color: #A9BCEB; letter-spacing: .04em; }
</style>
<div class="destaque"></div>
<div class="clique"></div>
<svg class="cursor" viewBox="0 0 26 26"><path d="M4 2 L4 21 L9.2 16.4 L12.6 24 L15.8 22.6 L12.5 15.2 L19.5 15.2 Z" fill="#fff" stroke="#0F172A" stroke-width="1.6" stroke-linejoin="round"/></svg>
<div class="legenda embaixo"><div class="passo"></div><div class="titulo"></div><div class="texto"></div><div class="barra"></div></div>
<div class="cartela"><div class="modulo"></div><div class="titulo"></div><div class="texto"></div><div class="lista"></div><div class="rodape"></div></div>`;
  document.documentElement.appendChild(raiz);
  const $ = s => raiz.querySelector(s);
  const cursor = $('.cursor'), destaque = $('.destaque'), clique = $('.clique'), legenda = $('.legenda'), cartela = $('.cartela');

  window.cenaDaAula = {
    cartela(modulo, titulo, texto, lista, rodape) {
      $('.cartela .modulo').textContent = modulo || '';
      $('.cartela .titulo').textContent = titulo || '';
      $('.cartela .texto').textContent = texto || '';
      $('.cartela .lista').innerHTML = (lista || []).map(l => `<div>• ${l.replace(/[<>&]/g, '')}</div>`).join('');
      $('.cartela .rodape').textContent = rodape || '';
      cartela.classList.add('visivel');
    },
    semCartela() { cartela.classList.remove('visivel'); },
    cursorEm(x, y) { cursor.style.opacity = '1'; cursor.style.left = (x - 4) + 'px'; cursor.style.top = (y - 2) + 'px'; },
    clicar(x, y) {
      clique.style.left = x + 'px'; clique.style.top = y + 'px';
      clique.classList.remove('vai'); void clique.offsetWidth; clique.classList.add('vai');
    },
    destacar(r) {
      if (!r) { destaque.style.opacity = '0'; return; }
      const f = 6;
      destaque.style.left = (r.x - f) + 'px'; destaque.style.top = (r.y - f) + 'px';
      destaque.style.width = (r.width + 2 * f) + 'px'; destaque.style.height = (r.height + 2 * f) + 'px';
      destaque.style.opacity = '1';
    },
    legenda(passo, titulo, texto, emCima, ms) {
      $('.legenda .passo').textContent = passo;
      $('.legenda .titulo').textContent = titulo;
      $('.legenda .texto').textContent = texto || '';
      legenda.classList.toggle('emcima', !!emCima);
      legenda.classList.toggle('embaixo', !emCima);
      const barra = $('.legenda .barra');
      barra.style.transition = 'none'; barra.style.width = '0';
      legenda.classList.add('visivel');
      void barra.offsetWidth;
      barra.style.transition = `width ${ms}ms linear`; barra.style.width = '100%';
    },
    semLegenda() { legenda.classList.remove('visivel'); },
  };
})();
