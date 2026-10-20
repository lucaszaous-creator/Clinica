// O EDITOR DE TEXTO CLÍNICO (out/2026) — a metade do navegador do componente
// EditorTextoClinico.razor. O contrato é o MESMO do editor WPF (Clinica.Desktop.Controls.
// EditorTextoClinico): só texto, negrito e itálico. O texto simples continua canônico; quem
// decide a formatação gravada é o C# (TextoFormatado.Guardar), a partir dos trechos que este
// arquivo lê do campo. Nada de HTML vai para o banco: o campo é desenhado a partir de
// trechos, e lido de volta em trechos.
//
// Módulo ES carregado sob demanda (import()), para não exigir nada da página-hospedeira:
// funciona igual no BlazorWebView do app e na prévia.

const avisando = new WeakMap();

function empurrar(saida, texto, negrito, italico) {
    if (!texto) return;
    const ultimo = saida[saida.length - 1];
    if (ultimo && ultimo.negrito === negrito && ultimo.italico === italico) ultimo.texto += texto;
    else saida.push({ texto, negrito, italico });
}

function temTexto(saida) {
    return saida.some(t => t.texto.length > 0);
}

function andar(no, negrito, italico, saida) {
    for (const filho of no.childNodes) {
        if (filho.nodeType === Node.TEXT_NODE) {
            // O contenteditable troca espaços repetidos por NBSP: devolvê-los a espaço comum
            // é o que mantém o texto simples idêntico ao que a pessoa digitou.
            empurrar(saida, filho.nodeValue.replace(/ /g, ' '), negrito, italico);
            continue;
        }
        if (filho.nodeType !== Node.ELEMENT_NODE) continue;
        const tag = filho.tagName;
        if (tag === 'BR') { empurrar(saida, '\n', negrito, italico); continue; }
        const peso = filho.style && filho.style.fontWeight;
        const n = negrito || tag === 'B' || tag === 'STRONG' || peso === 'bold' || parseInt(peso, 10) >= 600;
        const i = italico || tag === 'I' || tag === 'EM' || (filho.style && filho.style.fontStyle === 'italic');
        if ((tag === 'DIV' || tag === 'P') && temTexto(saida)) empurrar(saida, '\n', negrito, italico);
        andar(filho, n, i, saida);
    }
}

/** Lê o campo em trechos {texto, negrito, italico}. */
export function ler(campo) {
    const saida = [];
    andar(campo, false, false, saida);
    // A quebra no FIM precisa de um <br> a mais para aparecer na tela (o navegador não
    // desenha linha vazia final): esse <br> extra não é texto da pessoa.
    if (campo.lastChild && campo.lastChild.nodeName === 'BR' && saida.length > 0) {
        const ultimo = saida[saida.length - 1];
        if (ultimo.texto.endsWith('\n')) {
            ultimo.texto = ultimo.texto.slice(0, -1);
            if (!ultimo.texto) saida.pop();
        }
    }
    return saida;
}

/** Desenha os trechos no campo (texto nunca vira HTML: só nós de texto, <b>, <i> e <br>). */
export function definir(campo, trechos) {
    const fragmento = document.createDocumentFragment();
    let tudo = '';
    for (const t of trechos || []) {
        const partes = (t.texto || '').split('\n');
        partes.forEach((parte, k) => {
            if (k > 0) fragmento.appendChild(document.createElement('br'));
            if (!parte) return;
            let no = document.createTextNode(parte);
            if (t.italico) { const el = document.createElement('i'); el.appendChild(no); no = el; }
            if (t.negrito) { const el = document.createElement('b'); el.appendChild(no); no = el; }
            fragmento.appendChild(no);
        });
        tudo += t.texto || '';
    }
    if (tudo.endsWith('\n')) fragmento.appendChild(document.createElement('br'));
    campo.replaceChildren(fragmento);
}

function avisar(campo) {
    const ponte = avisando.get(campo);
    if (ponte) ponte.invokeMethodAsync('Mudou', ler(campo));
}

/** Liga o campo: desenha o conteúdo e passa a avisar o C# a cada mudança. */
export function ligar(campo, ponte, trechos, somenteLeitura) {
    avisando.set(campo, ponte);
    definir(campo, trechos);
    campo.contentEditable = somenteLeitura ? 'false' : 'true';
    if (campo.dataset.ligado) return;
    campo.dataset.ligado = '1';

    campo.addEventListener('input', () => avisar(campo));
    campo.addEventListener('keydown', e => {
        const k = (e.key || '').toLowerCase();
        if (e.ctrlKey && !e.altKey && !e.shiftKey && (k === 'b' || k === 'i')) {
            e.preventDefault();
            e.stopPropagation();
            document.execCommand(k === 'b' ? 'bold' : 'italic');
            avisar(campo);
            return;
        }
        // O contrato não tem sublinhado, lista nem alinhamento: o atalho não faz nada.
        if (e.ctrlKey && !e.altKey && (k === 'u' || k === 'e' || k === 'j' || k === 'l' || k === 'r')) {
            e.preventDefault();
            return;
        }
        // Enter vira quebra de linha (<br>), nunca um bloco novo: é o "\n" do texto simples.
        if (e.key === 'Enter') {
            e.preventDefault();
            document.execCommand('insertLineBreak');
            avisar(campo);
        }
    });
    // Colar entra como TEXTO SIMPLES — o formato de outra fonte não atravessa.
    campo.addEventListener('paste', e => {
        e.preventDefault();
        const texto = (e.clipboardData || window.clipboardData).getData('text/plain') || '';
        document.execCommand('insertText', false, texto.replace(/\r\n?/g, '\n'));
        avisar(campo);
    });
    campo.addEventListener('drop', e => e.preventDefault());
}

/** Os botões N / I da barra: aplicam ao trecho selecionado do campo. */
export function formatar(campo, comando) {
    campo.focus();
    document.execCommand(comando === 'negrito' ? 'bold' : 'italic');
    avisar(campo);
}

export function somenteLeitura(campo, valor) {
    campo.contentEditable = valor ? 'false' : 'true';
}

export function desligar(campo) {
    avisando.delete(campo);
}
