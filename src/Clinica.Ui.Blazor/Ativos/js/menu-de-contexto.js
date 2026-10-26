// O MENU DE CONTEXTO (o botão direito, out/2026 — componente MenuDeContexto): o que o Blazor não alcança sozinho.
//
// 1) A posição: o menu nasce no ponto do clique e, se passar da borda da janela, é puxado para dentro (para a
//    esquerda e para cima) — perto da borda direita ou de baixo ele abriria cortado. Medir o menu só dá depois
//    de desenhado, então é aqui, não no C#.
// 2) O foco: o primeiro item habilitado recebe o foco, e as setas, Home e End andam entre os habilitados (o Enter
//    é o clique nativo do botão). A decisão é na hora da tecla, como no conversa-do-whatsapp.js.
// 3) A seleção: o texto selecionado DENTRO do balão no instante do clique ("Copiar a seleção").

const MARGEM = 8;

export function posicionar(menu, x, y) {
    if (!menu) return;
    const largura = menu.offsetWidth, altura = menu.offsetHeight;
    const esquerda = Math.max(MARGEM, Math.min(x, window.innerWidth - largura - MARGEM));
    const topo = Math.max(MARGEM, Math.min(y, window.innerHeight - altura - MARGEM));
    menu.style.left = esquerda + 'px';
    menu.style.top = topo + 'px';
    const primeiro = itens(menu)[0];
    (primeiro || menu).focus();
    if (!menu.dataset.ligado) {
        menu.dataset.ligado = '1';
        menu.addEventListener('keydown', andar);
    }
}

function itens(menu) {
    return Array.from(menu.querySelectorAll('[role=menuitem]:not([aria-disabled=true])'));
}

function andar(e) {
    const lista = itens(e.currentTarget);
    if (lista.length === 0) return;
    const atual = lista.indexOf(document.activeElement);
    let proximo = -1;
    if (e.key === 'ArrowDown') proximo = (atual + 1) % lista.length;
    else if (e.key === 'ArrowUp') proximo = (atual - 1 + lista.length) % lista.length;
    else if (e.key === 'Home') proximo = 0;
    else if (e.key === 'End') proximo = lista.length - 1;
    if (proximo < 0) return;
    e.preventDefault();
    lista[proximo].focus();
}

export function selecaoEm(seletor) {
    const el = document.querySelector(seletor);
    const s = window.getSelection();
    if (!el || !s || s.isCollapsed || s.rangeCount === 0) return '';
    const r = s.getRangeAt(0);
    return el.contains(r.commonAncestorContainer) ? s.toString().trim() : '';
}
