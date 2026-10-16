/**
 * O WORKER DA CLÍNICA — as DUAS funções do domínio público num arquivo só:
 *
 *   /t/TOKEN   → o TERMO PELO WHATSAPP (parcela 81): a página onde o paciente lê,
 *                responde e assina no celular. Decisão em docs/termo-pelo-whatsapp.md.
 *   /c/TOKEN   → a CONFIRMAÇÃO DA SESSÃO pelo SMS (out/2026): "Confirmo" ou "Não vou".
 *                Decisão em docs/canal-sms.md.
 *   (resto)    → as RECEITAS: o PDF no navegador e o contrato de QR do validador
 *                gov.br (parcela 68). Roteiro em docs/validar-pelo-qr-code.md.
 *
 * ⚠️ POR QUE UM WORKER SÓ (parcela 82 — descoberto no primeiro teste da clínica):
 * o endereço público configurado no sistema é o hostname deste worker no workers.dev
 * (ex.: divine-bush-1075.….workers.dev), e no workers.dev O HOSTNAME É O WORKER —
 * não existe rota por caminho entre dois workers. Dois arquivos separados só
 * funcionariam com domínio PRÓPRIO (o CNAME da parcela 53). Enquanto o domínio for o
 * workers.dev, é este arquivo, colado NO WORKER CUJO HOSTNAME ESTÁ CONFIGURADO em
 * Gerente → Configurações → Publicação — e mesmo com domínio próprio ele continua
 * valendo, com uma única rota dominio/*.
 *
 * INSTALAÇÃO
 * ----------
 * 1. Workers & Pages → o worker do endereço configurado (o das receitas) → Edit code →
 *    substitua TUDO por este arquivo → Save and deploy.
 * 2. Bindings: o R2 do balde da publicação com o nome BUCKET (o que já existe lá).
 * 3. Nada mais: /t/* passa a responder no mesmo hostname que o app já usa.
 *
 * ⚠️ ESTE WORKER NÃO TOCA BANCO NENHUM — só o balde. Vazamento da borda expõe no
 * máximo o que está publicado; nunca uma credencial do Postgres da clínica.
 */

// ==================== Receitas + validador gov.br ====================

const FORMATO_VALIDADOR = "application/validador-iti+json";

/** Compara códigos ignorando caixa, hífen e espaço — "ab3k7 9xq2m" confere com "AB3K7-9XQ2M". */
const normalizar = (texto) => (texto ?? "").replace(/[^0-9a-z]/gi, "").toUpperCase();


// ==================== O termo pelo WhatsApp ====================

const CABECALHOS_PAGINA = {
  "content-type": "text/html; charset=utf-8",
  // Página de dado de saúde: nada de cache compartilhado, nada de indexação.
  "cache-control": "no-store",
  "x-robots-tag": "noindex, nofollow",
  "referrer-policy": "no-referrer",
  // ⚠️ `connect-src 'self'` é o que deixa a página fazer o PRÓPRIO POST da assinatura:
  // sem ele, `default-src 'none'` bloqueia o fetch da própria origem, e o envio morria
  // com "sem conexão" — o CSP escrito à mão tem de listar as chamadas que a página faz,
  // e esta página faz exatamente UMA (parcela 82, achado no teste da clínica).
  "content-security-policy":
    "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; "
    + "img-src data:; connect-src 'self'",
};

const TOKEN_VALIDO = /^[A-Z2-9]{26}$/;

const caminhoPedido = (token) => `t/${token.slice(0, 2)}/${token}.json`;
const caminhoResposta = (token) => `t/${token.slice(0, 2)}/${token}.resposta.json`;


// ==================== O despacho ====================

export default {
  async fetch(request, env) {
    const partes = new URL(request.url).pathname.split("/").filter(Boolean);
    if (partes[0] === "t") return atenderTermo(request, env);
    if (partes[0] === "c") return atenderConfirmacao(request, env);
    return atenderReceita(request, env);
  },
};

async function atenderReceita(request, env) {
    const url = new URL(request.url);

    // O validador acrescenta "/?" ao endereço do QR, então o caminho chega com uma barra
    // sobrando no fim: /r/AB/TOKEN.pdf/ . Sem esta limpeza, nada seria encontrado.
    const chave = url.pathname.replace(/\/+$/, "").replace(/^\/+/, "");

    if (request.method === "OPTIONS")
      return new Response(null, { headers: cors() });

    // ---- O contrato do validador ----
    //
    // ⚠️ O "+" de "validador-iti+json" vira ESPAÇO quando a query é decodificada — é o
    // padrão de URL (application/x-www-form-urlencoded), não um defeito do Cloudflare.
    // Sem a volta do replace, o formato chegaria como "application/validador-iti json",
    // nunca casaria, e o Worker devolveria o PDF onde o validador espera o JSON — o
    // fluxo inteiro falharia em silêncio, com tudo configurado certo.
    const formato = (url.searchParams.get("_format") ?? "").replace(" ", "+");
    if (formato === FORMATO_VALIDADOR) {
      const objeto = await env.BUCKET.head(chave);
      if (!objeto) return new Response(null, { status: 404, headers: cors() });

      const esperado = normalizar(objeto.customMetadata?.codigo);
      const informado = normalizar(url.searchParams.get("_secretCode"));

      // Código informado e errado: 401, como o guia manda. Vazio passa (ver cabeçalho).
      // Objeto antigo sem metadado também passa: ele foi publicado antes de o app gravar
      // o código, e recusá-lo deixaria toda receita já impressa fora do fluxo.
      if (informado.length > 0 && esperado.length > 0 && informado !== esperado)
        return new Response(null, { status: 401, headers: cors() });

      return new Response(
        JSON.stringify({
          version: "1.0.0",
          prescription: { signatureFiles: [{ url: `${url.origin}/${chave}` }] },
        }),
        { headers: { "content-type": FORMATO_VALIDADOR, ...cors() } });
    }

    // ---- Fora do contrato: entrega o PDF, como o domínio sempre entregou ----
    const objeto = await env.BUCKET.get(chave);
    if (!objeto)
      return new Response("Documento não encontrado ou fora do ar.", {
        status: 404, headers: cors() });

    return new Response(objeto.body, {
      headers: {
        "content-type": objeto.httpMetadata?.contentType ?? "application/pdf",
        "content-disposition": 'inline; filename="documento.pdf"',
        // O validador busca o PDF a partir da página dele (outra origem): sem CORS a
        // busca falha DEPOIS do código certo, que é o pior lugar para falhar.
        ...cors(),
      },
    });
}

async function atenderTermo(request, env) {
    const url = new URL(request.url);
    const partes = url.pathname.split("/").filter(Boolean); // ["t", TOKEN]
    const token = (partes[1] ?? "").toUpperCase();

    if (partes[0] !== "t" || !TOKEN_VALIDO.test(token))
      return pagina(404, "Endereço incompleto", "Confira o link recebido no WhatsApp.");

    const objetoPedido = await env.BUCKET.get(caminhoPedido(token));
    if (!objetoPedido)
      return pagina(
        404, "Este link não existe mais",
        "O termo pode já ter sido assinado no balcão, ou o envio foi cancelado. Fale com a recepção.");

    const pedido = await objetoPedido.json();

    // A expiração vem DE DENTRO do pedido (o desktop a escreveu ao publicar): o Worker
    // recusa servir pedido vencido mesmo que a limpeza ainda não tenha apagado o objeto.
    if (Date.now() > (pedido.expiraEmUnixMs ?? 0))
      return pagina(410, "Este link venceu",
        "O link vale por 24 horas. Peça um novo na recepção — leva um minuto.");

    const jaRespondido = await env.BUCKET.head(caminhoResposta(token));

    if (request.method === "GET") {
      if (jaRespondido)
        return pagina(200, "Termo já assinado",
          "A sua assinatura já foi recebida. Pode devolver a atenção à equipe — obrigado!");
      return new Response(paginaDoTermo(pedido, token), { headers: CABECALHOS_PAGINA });
    }

    if (request.method === "POST") {
      // WRITE-ONCE: a primeira assinatura é A assinatura. Sem isto, quem tivesse o link
      // depois poderia sobrescrever o traço de quem assinou primeiro.
      if (jaRespondido)
        return json(409, { erro: "Este termo já foi assinado." });

      let corpo;
      try { corpo = await request.json(); } catch { return json(400, { erro: "Envio ilegível." }); }

      const traco = typeof corpo.traco === "string" ? corpo.traco : "";
      // ~256 bytes de PNG é o piso de um traço de verdade (o mesmo piso do desktop);
      // o teto barra abuso do endpoint aberto.
      if (traco.length < 400 || traco.length > 3_000_000 || !traco.startsWith("data:image/png;base64,"))
        return json(400, { erro: "Assine na área indicada antes de enviar." });

      const respostas = {};
      for (const d of pedido.declaracoes ?? []) {
        const v = corpo.respostas?.[String(d.ordem)];
        if (v !== "Sim" && v !== "Não")
          return json(400, { erro: "Responda todas as perguntas antes de enviar." });
        respostas[String(d.ordem)] = v;
      }

      const resposta = JSON.stringify({
        versao: 1,
        respostas,
        traco,
        tracoLargura: Number(corpo.tracoLargura) || 0,
        tracoAltura: Number(corpo.tracoAltura) || 0,
        respondidoEmUnixMs: Date.now(),
        // Evidência do canal — vai para a linha ColetaRemotaTermo no banco da clínica.
        ip: request.headers.get("cf-connecting-ip") ?? "",
        aparelho: (request.headers.get("user-agent") ?? "").slice(0, 200),
      });

      await env.BUCKET.put(caminhoResposta(token), resposta, {
        httpMetadata: { contentType: "application/json; charset=utf-8" },
      });

      return json(200, { ok: true });
    }

    return json(405, { erro: "Método não suportado." });
}

// ==================== A confirmação da sessão pelo SMS ====================

// 12 caracteres do mesmo alfabeto das receitas — ConfirmacaoPorLink.TamanhoToken no app.
const TOKEN_CONFIRMACAO = /^[A-Z2-9]{12}$/;
const caminhoPedidoConf = (token) => `c/${token.slice(0, 2)}/${token}.json`;
const caminhoRespostaConf = (token) => `c/${token.slice(0, 2)}/${token}.resposta.json`;

async function atenderConfirmacao(request, env) {
    const url = new URL(request.url);
    const partes = url.pathname.split("/").filter(Boolean); // ["c", TOKEN]
    const token = (partes[1] ?? "").toUpperCase();

    if (!TOKEN_CONFIRMACAO.test(token))
      return pagina(404, "Endereço incompleto", "Confira o link recebido por SMS.");

    const objetoPedido = await env.BUCKET.get(caminhoPedidoConf(token));
    if (!objetoPedido)
      return pagina(404, "Este link não vale mais",
        "A sessão pode já ter passado ou ter sido remarcada. Em caso de dúvida, ligue para a clínica.");

    const pedido = await objetoPedido.json();
    // A sessão passou: o link morre junto (a expiração vem DE DENTRO do pedido).
    if (Date.now() > (pedido.expiraEmUnixMs ?? 0))
      return pagina(410, "Este horário já passou", "Para marcar outra sessão, fale com a clínica.");

    const jaRespondido = await env.BUCKET.get(caminhoRespostaConf(token));

    if (request.method === "GET") {
      if (jaRespondido) {
        const r = await jaRespondido.json();
        return pagina(200, "Resposta recebida",
          r.resposta === "confirma"
            ? "Sua presença está confirmada. Até lá!"
            : "Avisamos a clínica que você não poderá ir. A recepção vai entrar em contato para remarcar.");
      }
      return new Response(paginaDaConfirmacao(pedido), { headers: CABECALHOS_PAGINA });
    }

    if (request.method === "POST") {
      // WRITE-ONCE: a primeira resposta é A resposta. Mudou de ideia? Liga para a clínica —
      // a recepção pode já ter oferecido o horário a outra pessoa.
      if (jaRespondido) return json(409, { erro: "Esta sessão já foi respondida." });

      let corpo;
      try { corpo = await request.json(); } catch { return json(400, { erro: "Envio ilegível." }); }
      const resposta = corpo?.resposta;
      if (resposta !== "confirma" && resposta !== "nao-vai")
        return json(400, { erro: "Escolha uma das duas opções." });

      await env.BUCKET.put(caminhoRespostaConf(token), JSON.stringify({
        versao: 1,
        resposta,
        respondidoEmUnixMs: Date.now(),
        ip: request.headers.get("cf-connecting-ip") ?? "",
        aparelho: (request.headers.get("user-agent") ?? "").slice(0, 200),
      }), { httpMetadata: { contentType: "application/json; charset=utf-8" } });

      return json(200, { ok: true });
    }

    return json(405, { erro: "Método não suportado." });
}

/** A página: quem, quando, e dois botões grandes. Nada de dado clínico — só o que estava no SMS. */
const paginaDaConfirmacao = (pedido) => `<!doctype html><html lang="pt-BR"><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Confirmar sessão</title>
<style>
  body{font-family:system-ui,sans-serif;margin:0;background:#f4f6f8;color:#1c2430}
  .caixa{max-width:480px;margin:0 auto;padding:28px 16px}
  .cartao{background:#fff;border:1px solid #dde3ea;border-radius:14px;padding:24px}
  .clinica{color:#66788c;font-size:.85rem;text-transform:uppercase;letter-spacing:.06em;margin:0 0 10px}
  h1{font-size:1.25rem;margin:0 0 6px}
  .quando{font-size:1.1rem;margin:0 0 22px;line-height:1.4}
  button{display:block;width:100%;padding:16px 0;border-radius:10px;font-size:1.05rem;margin-top:10px;
    border:1.5px solid #b9c4d0;background:#fff;color:#1c2430}
  #sim{background:#12467b;border-color:#12467b;color:#fff;font-weight:600}
  #aviso{color:#a3272b;font-weight:600;min-height:1.2em;margin:12px 0 0}
  .rodape{color:#66788c;font-size:.8rem;line-height:1.45;margin-top:16px}
</style>
<body><div class="caixa"><div class="cartao">
  ${pedido.clinica ? `<p class="clinica">${escapar(pedido.clinica)}</p>` : ""}
  <h1>Olá, ${escapar(pedido.paciente ?? "")}!</h1>
  <p class="quando">Sua sessão é <strong>${escapar(pedido.quando ?? "")}</strong>.</p>
  <button type="button" id="sim">Confirmo, estarei lá</button>
  <button type="button" id="nao">Não poderei ir</button>
  <p id="aviso"></p>
  <p class="rodape">Sua resposta chega direto à recepção. Se não puder ir, eles vão falar com você para remarcar.</p>
</div></div>
<script>
  const aviso = document.getElementById("aviso");
  const enviar = async (resposta) => {
    document.querySelectorAll("button").forEach((b) => (b.disabled = true));
    aviso.textContent = "";
    try {
      const r = await fetch(location.pathname, {
        method: "POST", headers: { "content-type": "application/json" },
        body: JSON.stringify({ resposta }),
      });
      const j = await r.json().catch(() => ({}));
      if (!r.ok) { aviso.textContent = j.erro || "A resposta não saiu — tente de novo."; document.querySelectorAll("button").forEach((b) => (b.disabled = false)); return; }
      document.querySelector(".cartao").innerHTML = resposta === "confirma"
        ? "<h1>Presença confirmada ✓</h1><p class='quando'>Obrigado! Até lá.</p>"
        : "<h1>Avisamos a clínica</h1><p class='quando'>A recepção vai entrar em contato para remarcar.</p>";
    } catch {
      aviso.textContent = "A resposta não saiu — confira a internet e tente de novo.";
      document.querySelectorAll("button").forEach((b) => (b.disabled = false));
    }
  };
  document.getElementById("sim").onclick = () => enviar("confirma");
  document.getElementById("nao").onclick = () => enviar("nao-vai");
</script></body></html>`;

const cors = () => ({
  "access-control-allow-origin": "*",
  "access-control-allow-methods": "GET, HEAD, OPTIONS",
});

const json = (status, corpo) =>
  new Response(JSON.stringify(corpo), {
    status,
    headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" },
  });

const pagina = (status, titulo, texto) =>
  new Response(
    `<!doctype html><html lang="pt-BR"><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escapar(titulo)}</title>
<body style="font-family:system-ui,sans-serif;margin:0;padding:24px;background:#f4f6f8">
<div style="max-width:560px;margin:40px auto;background:#fff;border-radius:12px;padding:28px;border:1px solid #dde3ea">
<h1 style="font-size:1.2rem;margin:0 0 10px">${escapar(titulo)}</h1>
<p style="color:#445;line-height:1.5;margin:0">${escapar(texto)}</p></div></body></html>`,
    { status, headers: CABECALHOS_PAGINA });

const escapar = (t) =>
  String(t).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

/** A página do termo: ler → responder Sim/Não → assinar com o dedo → enviar. */
const paginaDoTermo = (pedido, token) => {
  const declaracoes = (pedido.declaracoes ?? [])
    .map(
      (d) => `
  <div class="declaracao">
    <p class="pergunta">${escapar(d.texto)}</p>
    ${d.detalhe ? `<p class="detalhe">${escapar(d.detalhe)}</p>` : ""}
    <div class="opcoes" data-ordem="${Number(d.ordem)}">
      <button type="button" data-v="Sim">Sim</button>
      <button type="button" data-v="Não">Não</button>
    </div>
  </div>`)
    .join("");

  return `<!doctype html><html lang="pt-BR"><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escapar(pedido.titulo ?? "Termo")}</title>
<style>
  body{font-family:system-ui,sans-serif;margin:0;background:#f4f6f8;color:#1c2430}
  .caixa{max-width:640px;margin:0 auto;padding:20px 16px 40px}
  .cartao{background:#fff;border:1px solid #dde3ea;border-radius:12px;padding:20px;margin-bottom:14px}
  h1{font-size:1.15rem;margin:0 0 2px}
  .num{color:#66788c;font-size:.85rem;margin:0 0 14px}
  .corpo{white-space:pre-wrap;line-height:1.55;font-size:.97rem}
  .pergunta{font-weight:600;margin:0 0 4px}
  .detalhe{color:#66788c;font-size:.88rem;margin:0 0 8px}
  .declaracao{border-top:1px solid #eef1f5;padding:12px 0 4px}
  .opcoes button{min-width:84px;padding:10px 0;margin-right:8px;border-radius:8px;
    border:1.5px solid #b9c4d0;background:#fff;font-size:1rem}
  .opcoes button.marcado{background:#12467b;border-color:#12467b;color:#fff;font-weight:600}
  canvas{width:100%;height:200px;border:1.5px dashed #b9c4d0;border-radius:8px;
    touch-action:none;background:#fff}
  .acoes{display:flex;gap:10px;margin-top:10px}
  .acoes button{flex:1;padding:14px 0;border-radius:8px;font-size:1.05rem;border:1.5px solid #b9c4d0;background:#fff}
  #enviar{background:#12467b;border-color:#12467b;color:#fff;font-weight:600}
  #aviso{color:#a3272b;font-weight:600;min-height:1.2em;margin:8px 0 0}
  .rodape{color:#66788c;font-size:.8rem;line-height:1.45}
</style>
<body><div class="caixa">
  <div class="cartao">
    <h1>${escapar(pedido.titulo ?? "Termo")}</h1>
    <p class="num">Nº ${escapar(pedido.numero ?? "")} · ${escapar(pedido.paciente ?? "")}</p>
    <div class="corpo">${escapar(pedido.corpo ?? "")}</div>
  </div>
  <div class="cartao">${declaracoes ||
    '<p class="detalhe" style="margin:0">Este termo não tem perguntas — leia e assine abaixo.</p>'}</div>
  <div class="cartao">
    <p class="pergunta" style="margin-bottom:8px">Assine com o dedo, dentro da área:</p>
    <canvas id="area" width="600" height="200"></canvas>
    <div class="acoes">
      <button type="button" id="limpar">Limpar</button>
      <button type="button" id="enviar">Enviar assinatura</button>
    </div>
    <p id="aviso"></p>
    <p class="rodape">Ao enviar, o traço, as respostas, a hora e o aparelho ficam registrados
    junto do termo na clínica (assinatura eletrônica simples — MP 2.200-2/2001, art. 10, §2º).
    Em caso de dúvida, fale com a equipe antes de assinar.</p>
  </div>
</div>
<script>
  const respostas = {};
  document.querySelectorAll(".opcoes").forEach((g) =>
    g.querySelectorAll("button").forEach((b) =>
      b.addEventListener("click", () => {
        respostas[g.dataset.ordem] = b.dataset.v;
        g.querySelectorAll("button").forEach((x) => x.classList.toggle("marcado", x === b));
      })));

  const area = document.getElementById("area");
  const tinta = area.getContext("2d");
  tinta.lineWidth = 2.5; tinta.lineCap = "round"; tinta.strokeStyle = "#1c2430";
  let desenhando = false, assinou = false;
  const ponto = (e) => {
    const r = area.getBoundingClientRect();
    const t = e.touches ? e.touches[0] : e;
    return { x: (t.clientX - r.left) * (area.width / r.width),
             y: (t.clientY - r.top) * (area.height / r.height) };
  };
  const comeca = (e) => { desenhando = true; const p = ponto(e); tinta.beginPath(); tinta.moveTo(p.x, p.y); e.preventDefault(); };
  const move = (e) => { if (!desenhando) return; const p = ponto(e); tinta.lineTo(p.x, p.y); tinta.stroke(); assinou = true; e.preventDefault(); };
  const para = () => { desenhando = false; };
  area.addEventListener("pointerdown", comeca); area.addEventListener("pointermove", move);
  addEventListener("pointerup", para);

  document.getElementById("limpar").onclick = () => {
    tinta.clearRect(0, 0, area.width, area.height); assinou = false; };

  const aviso = document.getElementById("aviso");
  document.getElementById("enviar").onclick = async () => {
    const total = document.querySelectorAll(".opcoes").length;
    if (Object.keys(respostas).length < total) { aviso.textContent = "Responda todas as perguntas acima."; return; }
    if (!assinou) { aviso.textContent = "Assine na área antes de enviar."; return; }
    aviso.textContent = "";
    const botao = document.getElementById("enviar");
    botao.disabled = true; botao.textContent = "Enviando…";
    try {
      const r = await fetch(location.pathname, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          respostas,
          traco: area.toDataURL("image/png"),
          tracoLargura: area.width, tracoAltura: area.height,
        }),
      });
      const j = await r.json().catch(() => ({}));
      if (!r.ok) { aviso.textContent = j.erro || "Não foi possível enviar — tente de novo."; botao.disabled = false; botao.textContent = "Enviar assinatura"; return; }
      document.body.innerHTML = '<div class="caixa"><div class="cartao"><h1>Assinatura enviada ✓</h1>' +
        '<p class="corpo">Obrigado! A equipe já recebeu — pode devolver a atenção a ela.</p></div></div>';
    } catch {
      aviso.textContent =
        "O envio não saiu — confira a internet e tente de novo. Se persistir, avise a recepção.";
      botao.disabled = false; botao.textContent = "Enviar assinatura";
    }
  };
</script></body></html>`;
};
