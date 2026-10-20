// A WEBCAM DO BALCÃO (out/2026) — a metade do navegador do CapturaFotoForm.razor. Era a
// CapturaFotoWindow do shell WPF (DirectShow via AForge); no Blazor a câmera é a do próprio
// navegador (getUserMedia), e o hospedeiro WPF libera a permissão só para a origem do app.
//
// O recorte é o MESMO do Retrato.cs do WPF, e as constantes têm de continuar iguais às de lá
// (os dois gravam na MESMA tabela): quadrado CENTRAL, foto cheia de até 640 px (JPEG 0,88) e
// miniatura de até 160 px (JPEG 0,82), sem nunca ampliar. Quem decide e grava é o C#; aqui só
// se liga a câmera, se congela o quadro e se codifica.
//
// Módulo ES carregado sob demanda (import()): funciona igual no BlazorWebView e na prévia.

const LADO_CHEIO = 640;
const LADO_MINIATURA = 160;
const ESPERA_PRIMEIRO_QUADRO_MS = 6000;

const fluxos = new WeakMap();

function parar(video) {
    const fluxo = fluxos.get(video);
    if (fluxo) {
        for (const t of fluxo.getTracks()) t.stop();
        fluxos.delete(video);
    }
    if (video) video.srcObject = null;
}

function classificar(erro) {
    const nome = erro && erro.name ? erro.name : "";
    if (nome === "NotAllowedError" || nome === "SecurityError") return "negado";
    if (nome === "NotFoundError" || nome === "OverconstrainedError" || nome === "DevicesNotFoundError") return "sem-camera";
    if (nome === "NotReadableError" || nome === "AbortError" || nome === "TrackStartError") return "ocupada";
    return "erro";
}

async function listarCameras() {
    try {
        const dispositivos = await navigator.mediaDevices.enumerateDevices();
        return dispositivos
            .filter(d => d.kind === "videoinput")
            .map((d, i) => ({ id: d.deviceId, nome: d.label || `Câmera ${i + 1}` }));
    } catch {
        return [];
    }
}

// Quando a câmera é bloqueada pela privacidade do Windows (ou está presa em outro programa)
// ela costuma abrir sem erro e simplesmente não entregar quadro nenhum. A espera transforma
// esse silêncio num estado que a tela sabe explicar.
function esperarQuadro(video) {
    return new Promise(resolve => {
        if (video.videoWidth > 0 && video.readyState >= 2) { resolve(true); return; }
        let feito = false;
        const terminar = ok => { if (feito) return; feito = true; clearInterval(vigia); clearTimeout(prazo); resolve(ok); };
        const vigia = setInterval(() => { if (video.videoWidth > 0 && video.readyState >= 2) terminar(true); }, 100);
        const prazo = setTimeout(() => terminar(false), ESPERA_PRIMEIRO_QUADRO_MS);
    });
}

/** Liga a câmera (a escolhida, ou a padrão) no <video>. Devolve o estado, as câmeras e a ligada. */
export async function ligar(video, cameraId) {
    parar(video);
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        return { estado: "sem-suporte", cameras: [], cameraId: null, mensagem: null };
    }
    let fluxo;
    try {
        fluxo = await navigator.mediaDevices.getUserMedia({
            video: cameraId ? { deviceId: { exact: cameraId } } : true,
            audio: false
        });
    } catch (erro) {
        return { estado: classificar(erro), cameras: await listarCameras(), cameraId: null, mensagem: String(erro && erro.message || erro) };
    }
    fluxos.set(video, fluxo);
    video.muted = true;
    video.playsInline = true;
    video.srcObject = fluxo;
    try { await video.play(); } catch { /* o quadro chega mesmo assim; a espera abaixo decide */ }

    const recebeu = await esperarQuadro(video);
    const trilha = fluxo.getVideoTracks()[0];
    const ligada = trilha && trilha.getSettings ? trilha.getSettings().deviceId || cameraId : cameraId;
    return { estado: recebeu ? "ao-vivo" : "sem-imagem", cameras: await listarCameras(), cameraId: ligada || null, mensagem: null };
}

export function desligar(video) {
    parar(video);
}

function codificar(fonte, largura, altura, ladoMaximo, qualidade) {
    const lado = Math.min(largura, altura);
    const sx = Math.floor((largura - lado) / 2);
    const sy = Math.floor((altura - lado) / 2);
    const saida = Math.min(lado, ladoMaximo); // reduz, nunca amplia
    const tela = document.createElement("canvas");
    tela.width = saida;
    tela.height = saida;
    const ctx = tela.getContext("2d");
    ctx.imageSmoothingQuality = "high";
    ctx.drawImage(fonte, sx, sy, lado, lado, 0, 0, saida, saida);
    const url = tela.toDataURL("image/jpeg", qualidade);
    return url.substring(url.indexOf(",") + 1);
}

function preparar(fonte, largura, altura) {
    if (!largura || !altura) return null;
    return {
        cheia: codificar(fonte, largura, altura, LADO_CHEIO, 0.88),
        miniatura: codificar(fonte, largura, altura, LADO_MINIATURA, 0.82)
    };
}

/** Congela o quadro atual do <video>. Nulo = ainda não há imagem no visor. */
export function capturar(video) {
    if (!video || !video.videoWidth || !video.videoHeight) return null;
    return preparar(video, video.videoWidth, video.videoHeight);
}

/** Prepara um arquivo escolhido (JPG, PNG, BMP). Nulo = não é uma imagem que o navegador saiba abrir. */
export async function prepararArquivo(bytes) {
    try {
        const imagem = await createImageBitmap(new Blob([bytes]));
        try { return preparar(imagem, imagem.width, imagem.height); }
        finally { imagem.close(); }
    } catch {
        return null;
    }
}
