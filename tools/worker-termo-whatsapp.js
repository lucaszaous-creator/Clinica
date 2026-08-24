/**
 * O Worker do TERMO PELO WHATSAPP — a página onde o paciente lê, responde e assina no
 * próprio celular (parcela 81). Roteiro de instalação e a decisão inteira em
 * docs/termo-pelo-whatsapp.md; rota no Cloudflare: dominio/t/* (as receitas ficam em /r/*,
 * com o worker-validar-iti.js).
 *
 * O DESENHO EM TRÊS LINHAS
 * ------------------------
 * O desktop publica t/xx/TOKEN.json (o pedido: texto do termo + declarações, minimizado).
 * Este Worker serve a página no GET e grava t/xx/TOKEN.resposta.json no POST — WRITE-ONCE:
 * a primeira assinatura é A assinatura, e segunda gravação é recusada.
 * O desktop lê a resposta, a técnica confere e conclui — o Worker nunca sela nada.
 *
 * ⚠️ ESTE WORKER NÃO TOCA BANCO NENHUM. Ele enxerga um binding R2 (variável BALDE) e só o
 * prefixo t/. Vazamento da borda expõe no máximo os pedidos em aberto — nunca credencial
 * do Postgres da clínica. É a decisão estrutural do documento; não a "melhore" ligando o
 * banco aqui.
 *
 * INSTALAÇÃO (uma vez, no painel do Cloudflare)
 * ---------------------------------------------
 * 1. Workers & Pages → Create Worker → cole este arquivo.
 * 2. Settings → Bindings → R2 bucket: nome BALDE apontando o MESMO balde da publicação.
 * 3. Workers Routes no domínio da clínica: dominio/t/* → este worker.
 * O botão "Enviar pelo WhatsApp" do sistema só depende do endereço público já configurado
 * em Gerente → Configurações → Publicação.
 */

const CABECALHOS_PAGINA = {
  "content-type": "text/html; charset=utf-8",
  // Página de dado de saúde: nada de cache compartilhado, nada de indexação.
  "cache-control": "no-store",
  "x-robots-tag": "noindex, nofollow",
  "referrer-policy": "no-referrer",
  "content-security-policy":
    "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:",
};

const TOKEN_VALIDO = /^[A-Z2-9]{26}$/;

const caminhoPedido = (token) => `t/${token.slice(0, 2)}/${token}.json`;
const caminhoResposta = (token) => `t/${token.slice(0, 2)}/${token}.resposta.json`;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const partes = url.pathname.split("/").filter(Boolean); // ["t", TOKEN]
    const token = (partes[1] ?? "").toUpperCase();

    if (partes[0] !== "t" || !TOKEN_VALIDO.test(token))
      return pagina(404, "Endereço incompleto", "Confira o link recebido no WhatsApp.");

    const objetoPedido = await env.BALDE.get(caminhoPedido(token));
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

    const jaRespondido = await env.BALDE.head(caminhoResposta(token));

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

      await env.BALDE.put(caminhoResposta(token), resposta, {
        httpMetadata: { contentType: "application/json; charset=utf-8" },
      });

      return json(200, { ok: true });
    }

    return json(405, { erro: "Método não suportado." });
  },
};

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
      aviso.textContent = "Sem conexão — confira a internet e tente de novo.";
      botao.disabled = false; botao.textContent = "Enviar assinatura";
    }
  };
</script></body></html>`;
};
