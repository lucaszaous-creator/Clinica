// A moldura da Recepção (MolduraDoApp.razor) como ela aparece no app — logo, menu do topo, busca,
// sino e conta. Só para os mockups: o CSS é o da prévia (tools/PreviaBlazor, porta 5199).
document.body.insertAdjacentHTML('afterbegin', `
<header class="moldura-topo">
  <div class="moldura-marca">
    <img src="img/logo-cor.png" alt="Clínica SemDor" />
    <span class="moldura-separador" aria-hidden="true"></span>
    <span class="moldura-app">Recepção</span>
  </div>
  <nav class="moldura-nav"><ul role="menubar">
    <li class="moldura-grupo"><button type="button">Início</button></li>
    <li class="moldura-grupo ativo"><button type="button">Agenda <svg class="icone" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6"/></svg></button></li>
    <li class="moldura-grupo"><button type="button">Pacientes <svg class="icone" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6"/></svg></button></li>
    <li class="moldura-grupo"><button type="button">Marcar <svg class="icone" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6"/></svg></button></li>
    <li class="moldura-grupo"><button type="button">Relacionamento <svg class="icone" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6"/></svg></button></li>
    <li class="moldura-grupo"><button type="button">WhatsApp</button></li>
  </ul><button type="button" class="moldura-treinamento">Treinamento</button></nav>
  <div class="moldura-busca"><div class="busca"><svg class="icone" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg><input class="campo" type="search" placeholder="Buscar tela (Ctrl+F)"></div></div>
  <div class="moldura-acoes">
    <div class="moldura-sino"><button type="button" class="botao icone fantasma"><svg class="icone" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg></button><span class="moldura-contador sobre">3</span></div>
    <span class="avatar">CR</span>
  </div>
</header>`);
document.body.classList.add('mockup-com-moldura');
