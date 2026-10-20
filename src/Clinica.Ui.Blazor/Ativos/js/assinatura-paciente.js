// A ÁREA ONDE O PACIENTE ASSINA (out/2026) — a metade do navegador do AssinaturaDoPacienteForm,
// que substituiu o InkCanvas da AssinaturaPacienteWindow WPF. O que sai daqui tem de ser o MESMO
// que o WPF gravava, porque é o que vai para o PDF do termo:
//   · PNG de fundo TRANSPARENTE (sem moldura nem cor de fundo — o traço cai em cima da linha de
//     assinatura do papel, não dentro de uma caixinha cinza);
//   · rasterizado em resolução DOBRADA (o WPF usava 192 dpi, o dobro dos 96 de tela): o PDF
//     encolhe a imagem para uma faixa de ~38 pt, e na resolução de tela o traço sai serrilhado;
//   · caneta PRETA de 2,2 unidades de tela, cantos redondos e curva suavizada (FitToCurve);
//   · largura e altura devolvidas em UNIDADES DE TELA (o ActualWidth/ActualHeight do WPF), não em
//     pixels — é o que o serviço grava ao lado do PNG.
// Ponteiro (pointer events): dedo, caneta e mouse pelo mesmo caminho. Módulo ES carregado sob
// demanda (import()), como o editor de texto clínico: funciona no app e na prévia.

const ESCALA = 2;          // 192 / 96
const CANETA = 2.2;        // a espessura do DrawingAttributes do WPF, em unidades de tela
const estados = new WeakMap();

function contexto(canvas) {
    const ctx = canvas.getContext('2d');
    ctx.setTransform(ESCALA, 0, 0, ESCALA, 0, 0);
    ctx.lineWidth = CANETA;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.strokeStyle = '#000000';
    ctx.fillStyle = '#000000';
    return ctx;
}

function desenharTraco(ctx, pontos) {
    if (pontos.length === 0) return;
    if (pontos.length === 1) {
        // Um toque é um ponto: sem isso o "i" da assinatura some.
        ctx.beginPath();
        ctx.arc(pontos[0].x, pontos[0].y, CANETA / 2, 0, Math.PI * 2);
        ctx.fill();
        return;
    }
    ctx.beginPath();
    ctx.moveTo(pontos[0].x, pontos[0].y);
    // Curva pelos pontos médios — a suavização do FitToCurve: o traço não sai poligonal.
    for (let i = 1; i < pontos.length - 1; i++) {
        const mx = (pontos[i].x + pontos[i + 1].x) / 2;
        const my = (pontos[i].y + pontos[i + 1].y) / 2;
        ctx.quadraticCurveTo(pontos[i].x, pontos[i].y, mx, my);
    }
    const ultimo = pontos[pontos.length - 1];
    ctx.lineTo(ultimo.x, ultimo.y);
    ctx.stroke();
}

function redesenhar(canvas, estado) {
    const ctx = contexto(canvas);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    for (const t of estado.tracos) desenharTraco(ctx, t);
}

function dimensionar(canvas, estado) {
    const r = canvas.getBoundingClientRect();
    const w = Math.max(1, Math.round(r.width));
    const h = Math.max(1, Math.round(r.height));
    if (estado.largura === w && estado.altura === h) return;
    estado.largura = w;
    estado.altura = h;
    canvas.width = Math.ceil(w * ESCALA);
    canvas.height = Math.ceil(h * ESCALA);
    redesenhar(canvas, estado);
}

function ponto(canvas, e) {
    const r = canvas.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
}

/** Liga a área: o .NET recebe "Desenhou" quando um traço termina. */
export function ligar(canvas, ref) {
    if (estados.has(canvas)) return;
    const estado = { tracos: [], atual: null, largura: 0, altura: 0, ref };

    estado.down = e => {
        if (e.button !== undefined && e.button !== 0 && e.pointerType === 'mouse') return;
        e.preventDefault();
        canvas.setPointerCapture(e.pointerId);
        dimensionar(canvas, estado);
        estado.atual = [ponto(canvas, e)];
        estado.tracos.push(estado.atual);
        redesenhar(canvas, estado);
    };
    estado.move = e => {
        if (!estado.atual) return;
        e.preventDefault();
        estado.atual.push(ponto(canvas, e));
        redesenhar(canvas, estado);
    };
    estado.up = e => {
        if (!estado.atual) return;
        estado.atual = null;
        try { canvas.releasePointerCapture(e.pointerId); } catch { /* já solto */ }
        estado.ref.invokeMethodAsync('Desenhou');
    };
    estado.redimensionar = () => dimensionar(canvas, estado);

    canvas.addEventListener('pointerdown', estado.down);
    canvas.addEventListener('pointermove', estado.move);
    canvas.addEventListener('pointerup', estado.up);
    canvas.addEventListener('pointercancel', estado.up);
    window.addEventListener('resize', estado.redimensionar);
    estados.set(canvas, estado);
    dimensionar(canvas, estado);
}

/** Apaga o traço (o "Limpar assinatura"). */
export function limpar(canvas) {
    const estado = estados.get(canvas);
    if (!estado) return;
    estado.tracos = [];
    estado.atual = null;
    redesenhar(canvas, estado);
}

/** Quantos traços há — é o que decide se o Confirmar pode exportar. */
export function tracos(canvas) {
    const estado = estados.get(canvas);
    return estado ? estado.tracos.length : 0;
}

/**
 * O PNG (base64, sem o prefixo "data:") e a largura e a altura da área em unidades de tela.
 * Exporta só o traço, com fundo transparente — a moldura em volta é CSS e não entra.
 */
export function exportar(canvas) {
    const estado = estados.get(canvas);
    if (!estado) return null;
    dimensionar(canvas, estado);
    const url = canvas.toDataURL('image/png');
    return { png: url.substring(url.indexOf(',') + 1), largura: estado.largura, altura: estado.altura };
}

export function desligar(canvas) {
    const estado = estados.get(canvas);
    if (!estado) return;
    canvas.removeEventListener('pointerdown', estado.down);
    canvas.removeEventListener('pointermove', estado.move);
    canvas.removeEventListener('pointerup', estado.up);
    canvas.removeEventListener('pointercancel', estado.up);
    window.removeEventListener('resize', estado.redimensionar);
    estados.delete(canvas);
}
