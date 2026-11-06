// A GRAVAÇÃO DE ÁUDIO DO WHATSAPP (24/10/2026) — a metade do navegador do GravadorDeAudio.razor.
// O mesmo caminho do CRM web (semdor-crm, wwwroot/reception-media.js): MediaRecorder no formato
// que o navegador oferece (WebM/Opus no WebView2 e no Chromium), em pedaços de 250 ms, até 16 MB e
// até o limite de tempo; quem SOBE o arquivo e o ENVIA é o C# (o CRM o converte para OGG/Opus, a
// nota de voz do WhatsApp). O hospedeiro WPF libera o microfone só para a origem do app.
//
// Uma gravação por vez. Nada sai daqui sem a pessoa clicar Enviar: parar() devolve os bytes e
// cancelar() os descarta; os dois desligam o microfone na hora.
//
// Módulo ES carregado sob demanda (import()): funciona igual no BlazorWebView e na prévia.

const FORMATOS = [
    ["audio/webm;codecs=opus", "audio/webm"],
    ["audio/ogg;codecs=opus", "audio/ogg"],
    ["audio/mp4", "audio/mp4"],
];
const TETO_BYTES = 16 * 1024 * 1024;

let atual = null;

function formato() {
    const R = window.MediaRecorder;
    if (!R || typeof R.isTypeSupported !== "function") return null;
    for (const [pedido, mime] of FORMATOS) if (R.isTypeSupported(pedido)) return { pedido, mime };
    return null;
}

function classificar(erro) {
    const nome = erro && erro.name ? erro.name : "";
    if (nome === "NotAllowedError" || nome === "SecurityError") return "negado";
    if (nome === "NotFoundError" || nome === "OverconstrainedError" || nome === "DevicesNotFoundError") return "sem-microfone";
    if (nome === "NotReadableError" || nome === "AbortError" || nome === "TrackStartError") return "ocupado";
    return "erro";
}

function desligar(g) {
    if (g.vigia) clearInterval(g.vigia);
    if (g.fluxo) for (const t of g.fluxo.getTracks()) t.stop();
    g.fluxo = null;
}

/** Liga o microfone e começa a gravar. Devolve { estado, mime, mensagem }. */
export async function iniciar(limiteSegundos) {
    cancelar();
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return { estado: "sem-suporte", mime: null, mensagem: null };
    const f = formato();
    if (!f) return { estado: "sem-suporte", mime: null, mensagem: null };
    const g = { pedacos: [], bytes: 0, inicio: 0, fim: null, interrompida: false, excedeu: false, parado: null, fluxo: null, gravador: null, vigia: null };
    atual = g;
    try {
        g.fluxo = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch (erro) {
        if (atual === g) atual = null;
        return { estado: classificar(erro), mime: null, mensagem: String((erro && erro.message) || erro) };
    }
    if (atual !== g) { desligar(g); return { estado: "cancelada", mime: null, mensagem: null }; }
    try {
        g.gravador = new MediaRecorder(g.fluxo, { mimeType: f.pedido });
    } catch (erro) {
        desligar(g); atual = null;
        return { estado: "sem-suporte", mime: null, mensagem: String((erro && erro.message) || erro) };
    }
    g.parado = new Promise(resolver => { g.gravador.onstop = () => resolver(); });
    g.gravador.ondataavailable = e => {
        if (!e.data || !e.data.size) return;
        g.pedacos.push(e.data); g.bytes += e.data.size;
        if (g.bytes > TETO_BYTES) { g.excedeu = true; g.fim ??= performance.now(); try { g.gravador.stop(); } catch { } desligar(g); }
    };
    g.gravador.onerror = () => { g.interrompida = true; g.fim ??= performance.now(); try { g.gravador.stop(); } catch { } desligar(g); };
    for (const t of g.fluxo.getTracks()) t.addEventListener("ended", () => { if (!g.fim) { g.interrompida = true; g.fim = performance.now(); try { g.gravador.stop(); } catch { } desligar(g); } });
    g.gravador.start(250);
    g.inicio = performance.now();
    // O limite de tempo para a gravação (fica guardada, à espera do Enviar), como o CRM web.
    g.vigia = setInterval(() => {
        if ((performance.now() - g.inicio) / 1000 >= limiteSegundos && g.gravador.state === "recording") {
            g.fim = performance.now(); try { g.gravador.stop(); } catch { } desligar(g);
        }
    }, 250);
    return { estado: "gravando", mime: f.mime, mensagem: null };
}

/** Como está a gravação: segundos, se ainda grava, e se parou sozinha (microfone saiu, 16 MB, limite). */
export function situacao() {
    const g = atual;
    if (!g) return { segundos: 0, gravando: false, interrompida: false, excedeu: false };
    const fim = g.fim ?? performance.now();
    return {
        segundos: Math.max(0, (fim - g.inicio) / 1000),
        gravando: !!g.gravador && g.gravador.state === "recording",
        interrompida: g.interrompida,
        excedeu: g.excedeu,
    };
}

/**
 * Para e devolve os bytes (Uint8Array — o C# os lê como IJSStreamReference, em pedaços), ou null quando
 * não há gravação, ficou vazia ou passou de 16 MB.
 */
export async function parar() {
    const g = atual;
    if (!g || !g.gravador) return null;
    if (!g.fim) g.fim = performance.now();
    if (g.gravador.state !== "inactive") { try { g.gravador.stop(); } catch { } }
    await g.parado;
    desligar(g);
    if (atual === g) atual = null;
    if (g.excedeu || g.interrompida) return null;
    const blob = new Blob(g.pedacos, { type: g.gravador.mimeType || "audio/webm" });
    if (!blob.size || blob.size > TETO_BYTES) return null;
    return new Uint8Array(await blob.arrayBuffer());
}

/** Descarta a gravação e desliga o microfone. */
export function cancelar() {
    const g = atual;
    atual = null;
    if (!g) return;
    g.pedacos = [];
    if (g.gravador && g.gravador.state !== "inactive") { try { g.gravador.stop(); } catch { } }
    desligar(g);
}
