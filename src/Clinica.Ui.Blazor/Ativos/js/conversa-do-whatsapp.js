// A CONVERSA DO WHATSAPP (25/10/2026, docs/whatsapp-nativo-implementacao.md §15): o que o Blazor não
// alcança sozinho. Módulo carregado pela tela (ConversasDoWhatsApp) e pelo balão do áudio (MidiaNoBalao).
//
// 1) Enter envia e Shift+Enter quebra a linha, como no WhatsApp Web. QUEM ENVIA é a tela (TeclaAsync, com
//    teste); aqui só se impede a quebra de linha que o Enter faria. A decisão é na hora da tecla — pelo
//    @onkeydown:preventDefault do Blazor ela só valeria no render seguinte (a lição do chat da equipe, em
//    janela.js). A regra é a MESMA do C# (EnviaPeloEnter): Enter sem Shift e sem Alt; durante a composição de
//    acento (IME) o Enter é da composição.
// 2) A velocidade do áudio: playbackRate é propriedade do elemento, não atributo.

let ligado = false;

export function ligar() {
    if (ligado) return;
    ligado = true;
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' || e.shiftKey || e.altKey || e.isComposing) return;
        const alvo = e.target;
        if (alvo && alvo.matches && alvo.matches('.wa-escrever textarea')) e.preventDefault();
    }, true);
}

export function velocidade(el, taxa) {
    if (!el) return;
    el.defaultPlaybackRate = taxa;
    el.playbackRate = taxa;
}
