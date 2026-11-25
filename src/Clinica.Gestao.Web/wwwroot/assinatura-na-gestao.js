// Assinatura digital NA GESTÃO (nov/2026). Autorização expressa do dono em 24/11/2026 — "Autorizo, na Gestão"
// (docs/safeid-congelado.md). O MOTOR não muda: este arquivo fala com as MESMAS rotas da API do portal que o
// Consultório usa (clinica-site, portal/profissional/safeid.js e a1.js), na MESMA ordem, com a mesma sessão (o cookie
// do portal vale em /gestao, mesma origem) e o mesmo CSRF (/api/sessao):
//   SafeID: POST /api/clinico/atendimentos/0/documento/{id}/safeid → página da Safeweb NA MESMA ABA (sem pop-up: o
//           Safari do iPhone bloqueia janela aberta depois de um await) → /safeid/retorno → /profissional/?assinatura=
//           → (o portal devolve à gestão) /gestao/?assinatura= → GET /api/clinico/safeid/{id} → "Assinar e arquivar"
//           (confirmação explícita) → POST /api/clinico/safeid/{id}/concluir.
//   A1:     POST /api/clinico/atendimentos/0/documento/{id}/a1 → senha → POST /api/clinico/a1/{id}/assinar.
// Agenda 0 = o documento pela ficha (já emitido, com ou sem atendimento), a mesma porta da ficha do Consultório.
(() => {
  'use strict';
  const SAFEWEB = ['pscsafeweb.safewebpss.com.br', 'pscsafeweb-homologacao.safewebpss.com.br'];
  // A marca de "esta autorização começou na gestão": o portal a lê para devolver a pessoa a /gestao.
  const MARCA = 'gestao-assinatura-safeid';
  let csrf = null;

  // O diálogo usa os tokens do design system (clinica.css) e os botões da casa (.botao, .primario).
  const estilo = document.createElement('style');
  estilo.textContent = `dialog.assinatura-gestao{max-width:min(34rem,calc(100vw - 2*var(--space-4)));border:1px solid var(--borda);border-radius:var(--radius-control);background:var(--superficie);color:var(--texto-primario);padding:var(--space-6)}
dialog.assinatura-gestao::backdrop{background:rgba(0,0,0,.35)}
dialog.assinatura-gestao h2{margin:0 0 var(--space-3)}
dialog.assinatura-gestao .ag-corpo p{margin:0 0 var(--space-3)}
dialog.assinatura-gestao .ag-alergias,dialog.assinatura-gestao .ag-atencao{color:var(--texto-secundario)}
dialog.assinatura-gestao .ag-campo{display:flex;flex-direction:column;gap:var(--space-1);margin:var(--space-2) 0}
dialog.assinatura-gestao .ag-campo input{padding:var(--space-2);border:1px solid var(--borda);border-radius:var(--radius-control);font:inherit}
dialog.assinatura-gestao .ag-check{display:flex;gap:var(--space-2);align-items:flex-start;margin:var(--space-3) 0}
dialog.assinatura-gestao .ag-erro{color:var(--erro-texto);min-height:1em;margin:var(--space-2) 0}
dialog.assinatura-gestao .ag-acoes{display:flex;flex-wrap:wrap;gap:var(--space-2);justify-content:flex-end}`;
  document.head.append(estilo);

  const esc = t => String(t ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

  async function api(caminho, corpo) {
    let r;
    try {
      r = await fetch('/api' + caminho, {
        method: corpo === undefined ? 'GET' : 'POST', credentials: 'same-origin', cache: 'no-store',
        headers: corpo === undefined ? {} : { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf || '' },
        body: corpo === undefined ? undefined : JSON.stringify(corpo)
      });
    } catch { throw new Error('Não foi possível falar com o portal. Confira a conexão e tente de novo.'); }
    const dados = r.status === 204 ? null : await r.json().catch(() => null);
    if (!r.ok) {
      const falha = new Error(dados?.erro || (r.status === 401 || r.status === 403
        ? 'Sua sessão do portal não permite assinar agora. Entre de novo no portal e tente outra vez.'
        : r.status === 429 ? 'Aguarde um minuto antes de tentar novamente.' : 'Não foi possível concluir. Tente novamente.'));
      falha.status = r.status; throw falha;
    }
    return dados;
  }
  const renovarCsrf = async () => { csrf = (await api('/sessao')).csrf; };

  // A ATIVIDADE CLÍNICA (25/11/2026). A API só abre o prontuário e a assinatura para a sessão com uso HUMANO nos
  // últimos 15 minutos (AtendimentoTabletService.AutorizarAsync, AtividadeClinicaEm), e só o ping explícito de toque ou
  // tecla renova esse prazo (POST /api/clinico/atividade). O Consultório faz esse ping; a gestão não fazia — quem
  // entrava pelo portal e usava a gestão por mais de 15 minutos tinha TODA rota clínica recusada (401), e a tela dizia
  // "não habilitada". Aqui a gestão faz o MESMO ping, pela MESMA regra: só depois de toque ou tecla, no máximo a cada
  // 45 s. Nada afrouxa: depois de 15 minutos SEM uso o ping também é recusado e é preciso entrar de novo, como no
  // Consultório. Recusado uma vez, ele para de tentar até a página recarregar (um 401 a cada toque não ajuda ninguém).
  const PING_MS = 45000;
  let ultimoPing = 0, pingEmCurso = null, clinicoFechado = false;
  function renovarAtividade() {
    if (clinicoFechado || pingEmCurso || Date.now() - ultimoPing < PING_MS) return pingEmCurso;
    ultimoPing = Date.now();
    pingEmCurso = (async () => {
      if (!csrf) await renovarCsrf();
      try { await api('/clinico/atividade', {}); }
      catch (e) {
        if (e.status !== 400) throw e;
        await renovarCsrf(); await api('/clinico/atividade', {}); // o CSRF da aba envelheceu: uma nova tentativa
      }
    })().catch(e => { if (e.status === 401 || e.status === 403) clinicoFechado = true; })
      .finally(() => { pingEmCurso = null; });
    return pingEmCurso;
  }
  for (const tipo of ['pointerdown', 'keydown']) document.addEventListener(tipo, () => { renovarAtividade(); }, { capture: true, passive: true });

  // Por que a assinatura não abre, dito como é (nunca "não habilitada" por um erro). 401 com a sessão do portal ainda
  // válida é o prazo clínico de 15 minutos sem uso; sem ela, a sessão terminou.
  async function motivoDaRecusa(e) {
    if (e.status === 401) {
      const s = await fetch('/api/sessao', { credentials: 'same-origin', cache: 'no-store' }).then(r => r.ok ? r.json() : null).catch(() => null);
      return s?.modo === 'equipe'
        ? 'Por segurança, a assinatura só abre com uso recente: o acesso clínico se fecha depois de 15 minutos sem uso. Saia, entre de novo no portal com sua senha e assine em seguida.'
        : 'Sua sessão do portal terminou. Entre de novo no portal com sua senha e tente outra vez.';
    }
    if (e.status === 403) return 'Este aparelho está no modo paciente ou seu usuário não pode abrir o prontuário. Entre de novo no portal como profissional.';
    return e.status ? `Não foi possível consultar a assinatura agora (erro ${e.status}). Tente de novo em instantes.` : e.message;
  }
  const pdf = documento => `/api/clinico/atendimentos/0/documento/${encodeURIComponent(documento)}/pdf`;

  function janela(titulo, html) {
    const d = document.createElement('dialog');
    d.className = 'assinatura-gestao';
    d.setAttribute('aria-labelledby', 'ag-titulo');
    d.innerHTML = `<h2 id="ag-titulo">${esc(titulo)}</h2><div class="ag-corpo">${html}</div><p class="ag-erro" role="alert"></p><div class="ag-acoes"></div>`;
    document.body.append(d);
    d.addEventListener('close', () => d.remove());
    d.showModal();
    return d;
  }
  function botao(d, texto, primario, acao) {
    const b = document.createElement('button');
    b.type = 'button'; b.className = 'botao' + (primario ? ' primario' : ''); b.textContent = texto;
    b.onclick = async () => {
      const erro = d.querySelector('.ag-erro'); erro.textContent = '';
      d.querySelectorAll('.ag-acoes button').forEach(x => x.disabled = true);
      try { await renovarAtividade(); await acao(); }
      catch (e) { erro.textContent = e.status === 401 || e.status === 403 ? await motivoDaRecusa(e) : e.message; }
      finally { if (d.isConnected) d.querySelectorAll('.ag-acoes button').forEach(x => x.disabled = false); }
    };
    d.querySelector('.ag-acoes').append(b);
    return b;
  }

  /**
   * Abre a assinatura do documento já emitido. Resolve com null (desistiu), {frase, erro} (A1 ou recusa) ou
   * {saiu: true} (a página vai à Safeweb; a volta é retomar()).
   */
  async function assinar(pedido) {
    const documento = Number(pedido?.documento), paciente = Number(pedido?.paciente);
    if (!Number.isSafeInteger(documento) || documento <= 0) return { frase: 'Documento inválido para assinar.', erro: true };
    try {
      await renovarCsrf();
      await renovarAtividade(); // o toque em "Assinar" é uso: o prazo clínico se renova antes da consulta
      // Cada consulta devolve a resposta OU a falha. Só as duas RESPOSTAS dizendo "desligado" são "não habilitada";
      // qualquer falha (sessão, prazo clínico, permissão, servidor) diz o motivo de verdade.
      const consultar = caminho => api(caminho).then(dados => ({ dados }), falha => ({ falha }));
      const safeid = await consultar('/clinico/safeid');
      const a1r = await consultar('/clinico/a1');
      const comSafeId = safeid.dados?.habilitado === true;
      const a1 = a1r.dados;
      const comA1 = a1?.habilitado === true;
      if (!comSafeId && !comA1) {
        const falha = safeid.falha || a1r.falha;
        if (falha) return { frase: await motivoDaRecusa(falha), erro: true };
        return { frase: 'A assinatura digital não está habilitada neste portal.', erro: true };
      }
      let alergias = null;
      if (Number.isSafeInteger(paciente) && paciente > 0) {
        const f = await api(`/posto/pacientes/${paciente}?pagina=0`).catch(() => null);
        alergias = f ? (f.problemas || []).filter(p => p.tipo === 'Alergia' && p.situacao !== 'Descartado').map(p => p.descricao) : null;
      }
      const endereco = await api(`/clinico/atendimentos/0/documento/${documento}/assinatura/endereco`);
      return await new Promise(resolver => abrirEscolha({ documento, numero: pedido.numero, assunto: pedido.assunto, comSafeId, comA1, a1, alergias, endereco, resolver }));
    } catch (e) {
      return { frase: e.message, erro: true };
    }
  }

  function abrirEscolha(c) {
    const explicacao = c.comA1 && c.comSafeId
      ? 'Confira o PDF antes de autorizar e escolha como assinar: com seu certificado A1, informando a senha dele, ou com o SafeID, o certificado em nuvem autorizado pelo celular na página da Safeweb.'
      : c.comSafeId
        ? 'Confira o PDF antes de autorizar. A assinatura será feita com o SafeID: esta página irá à Safeweb para você autorizar no celular e, na volta, pedirá a confirmação final.'
        : 'Confira o PDF antes de autorizar. A assinatura será feita com seu certificado A1.';
    const alergias = c.alergias === null ? 'Confira as alergias na ficha do paciente.'
      : c.alergias.length ? c.alergias.map(esc).join(' · ') : 'Nenhuma cadastrada. Confira com o paciente.';
    const d = janela('Assinar documento', `<p><strong>${esc(c.assunto || ('Documento nº ' + (c.numero || c.documento)))}</strong></p>
      <p>${esc(explicacao)}</p>
      <p><a href="${pdf(c.documento)}" target="_blank" rel="noopener">Conferir o PDF</a></p>
      <p class="ag-alergias"><strong>Alergias no cadastro:</strong> ${alergias}</p>
      <div class="ag-endereco"></div>
      <label class="ag-check"><input type="checkbox" class="ag-revisei"> Conferi o PDF e as alergias deste paciente.</label>`);
    let resolvido = false;
    const responder = v => { if (!resolvido) { resolvido = true; c.resolver(v); } };
    d.addEventListener('close', () => responder(null));
    const revisei = () => { if (!d.querySelector('.ag-revisei').checked) throw new Error('Confirme a revisão antes de continuar.'); };
    const bloqueados = [];

    if (c.endereco?.precisaEndereco) {
      const area = d.querySelector('.ag-endereco');
      area.innerHTML = '<p class="ag-atencao">Falta o endereço residencial do paciente para assinar esta receita. Você pode completá-lo aqui.</p><label class="ag-campo"><span>Endereço residencial do paciente</span><input class="ag-endereco-campo" maxlength="300" autocomplete="off" placeholder="Rua, número, complemento, bairro, cidade e UF"></label>';
      const salvar = document.createElement('button');
      salvar.type = 'button'; salvar.className = 'botao'; salvar.textContent = 'Salvar endereço';
      salvar.onclick = async () => {
        const erro = d.querySelector('.ag-erro'); erro.textContent = '';
        const valor = area.querySelector('input').value.trim();
        if (valor.length < 5) { erro.textContent = 'Informe o endereço residencial completo do paciente.'; return; }
        try {
          await api(`/clinico/atendimentos/0/documento/${c.documento}/assinatura/endereco`, { endereco: valor });
          area.innerHTML = '<p role="status">Endereço salvo no cadastro. Confira o PDF atualizado antes de autorizar a assinatura.</p>';
          d.querySelector('.ag-revisei').checked = false;
          bloqueados.forEach(b => b.hidden = false);
        } catch (e) { erro.textContent = e.message; }
      };
      area.append(salvar);
    }

    if (c.comSafeId) bloqueados.push(botao(d, 'Assinar com SafeID', true, async () => {
      revisei();
      const r = await api(`/clinico/atendimentos/0/documento/${c.documento}/safeid`, { confirmouAlergia: true });
      let url;
      try { url = new URL(r?.url); } catch { throw new Error('Endereço de autorização indisponível.'); }
      if (url.protocol !== 'https:' || !SAFEWEB.includes(url.hostname)) throw new Error('Endereço de autorização indisponível.');
      try { sessionStorage.setItem(MARCA, JSON.stringify({ documento: c.documento, numero: c.numero || null, em: Date.now() })); } catch { /* sem armazenamento: a volta cai no Consultório, que conclui do mesmo jeito */ }
      responder({ saiu: true });
      // A resposta ao servidor sai antes de a página trocar; a mesma aba vai à Safeweb (sem pop-up).
      setTimeout(() => location.assign(url.href), 150);
    }));
    if (c.comA1) bloqueados.push(botao(d, c.comSafeId ? 'Assinar com A1' : 'Continuar assinatura', !c.comSafeId, async () => {
      revisei();
      if (!c.a1?.cadastrado) throw new Error('Cadastre seu A1 em Meu certificado (no Consultório do portal) antes de assinar.');
      const a = await api(`/clinico/atendimentos/0/documento/${c.documento}/a1`, { confirmouAlergia: true });
      pedirSenhaA1(d, a, responder);
    }));
    botao(d, 'Cancelar', false, async () => d.close());
    if (c.endereco?.precisaEndereco) bloqueados.forEach(b => b.hidden = true);
  }

  function pedirSenhaA1(d, a, responder) {
    d.querySelector('#ag-titulo').textContent = 'Autorizar assinatura A1';
    d.querySelector('.ag-corpo').innerHTML = `<p><strong>${esc(a.documento)}</strong></p><p>Assinar com o certificado de <strong>${esc(a.titular)}</strong>.</p>
      <label class="ag-campo"><span>Senha do certificado A1</span><input class="ag-senha" type="password" autocomplete="off" maxlength="200"></label>
      <p>A senha será usada somente nesta operação.</p>`;
    d.querySelector('.ag-acoes').replaceChildren();
    botao(d, 'Assinar', true, async () => {
      const campo = d.querySelector('.ag-senha');
      if (!campo.value) throw new Error('Informe a senha do certificado.');
      const pedido = { senha: campo.value }; campo.value = '';
      try {
        await api(`/clinico/a1/${encodeURIComponent(a.id)}/assinar`, pedido);
      } catch (e) {
        throw new Error(e.message + ' Feche esta janela e confira o documento antes de autorizar novamente.');
      } finally { pedido.senha = ''; }
      responder({ frase: 'Documento assinado com A1 e arquivado.', erro: false });
      d.close();
    });
    botao(d, 'Cancelar', false, async () => d.close());
    d.querySelector('.ag-senha').focus();
  }

  /** A volta da Safeweb: /gestao/?assinatura=<id>. Confirmação explícita e, então, concluir — como o safeid.js. */
  async function retomar() {
    const u = new URL(location.href), id = u.searchParams.get('assinatura');
    if (!id) return;
    u.searchParams.delete('assinatura');
    history.replaceState(history.state, '', u.pathname + (u.search || '') + u.hash);
    let marca = null;
    try { marca = JSON.parse(sessionStorage.getItem(MARCA) || 'null'); sessionStorage.removeItem(MARCA); } catch { /* nada */ }
    const aviso = (titulo, texto, documento) => {
      const d = janela(titulo, `<p>${esc(texto)}</p>${documento ? `<p><a href="${pdf(documento)}" target="_blank" rel="noopener">Abrir o PDF do documento</a></p>` : ''}`);
      botao(d, 'Fechar', true, async () => d.close());
    };
    try {
      await renovarCsrf();
      const a = await api('/clinico/safeid/' + encodeURIComponent(id));
      const nome = 'Documento nº ' + (marca?.documento === a.documento && marca?.numero ? marca.numero : a.documento);
      if (a.situacao === 'concluido') { aviso('Documento assinado', `${nome}: assinado com SafeID e arquivado.`, a.documento); return; }
      if (a.situacao !== 'autorizado') { aviso('Assinatura não concluída', 'A autorização do SafeID não foi concluída ou expirou. Confira o documento antes de assinar novamente.', a.documento); return; }
      const d = janela('Confirmar assinatura', `<p><strong>${esc(nome)}</strong></p><p>A autorização do SafeID foi recebida. Confirme para assinar e arquivar o documento.</p><p><a href="${pdf(a.documento)}" target="_blank" rel="noopener">Conferir o PDF</a></p>`);
      botao(d, 'Assinar e arquivar', true, async () => {
        await renovarAtividade();
        await api('/clinico/safeid/' + encodeURIComponent(id) + '/concluir', {});
        d.close();
        aviso('Documento assinado', `${nome}: assinado com SafeID e arquivado.`, a.documento);
      });
      botao(d, 'Cancelar', false, async () => d.close());
    } catch (e) {
      aviso('Assinatura não concluída', e.status === 401 || e.status === 403 ? await motivoDaRecusa(e) : e.message, null);
    }
  }

  window.assinaturaNaGestao = { assinar, retomar };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', retomar); else retomar();
})();
