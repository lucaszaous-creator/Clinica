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

// 3) Colar print e arrastar arquivo (F3, nov/2026): o print colado no campo (Ctrl+V) ou o arquivo solto na conversa vira o
//    anexo do rascunho. O arquivo vai à tela como fluxo (DotNet.createJSStreamReference), sem passar pelo limite de mensagem;
//    quem confere tamanho, canal e permissão é a tela (ColarArquivo). Só o PRIMEIRO arquivo; texto colado segue normal.
let ligadoArquivos = false;

export function ligarArquivos(ref) {
    if (ligadoArquivos) return;
    ligadoArquivos = true;
    const mandar = function (arquivo) {
        if (!arquivo) return false;
        arquivo.arrayBuffer().then(function (b) {
            ref.invokeMethodAsync('ColarArquivo', arquivo.name || 'print.png', arquivo.type || '', DotNet.createJSStreamReference(b));
        });
        return true;
    };
    document.addEventListener('paste', function (e) {
        const alvo = e.target;
        if (!alvo || !alvo.matches || !alvo.matches('.wa-escrever textarea')) return;
        const itens = (e.clipboardData && e.clipboardData.files) || [];
        if (itens.length > 0 && mandar(itens[0])) e.preventDefault();
    });
    document.addEventListener('dragover', function (e) {
        if (e.target && e.target.closest && e.target.closest('.wa-conversa') && e.dataTransfer && e.dataTransfer.types.indexOf('Files') >= 0) {
            e.preventDefault();
        }
    });
    document.addEventListener('drop', function (e) {
        if (!e.target || !e.target.closest || !e.target.closest('.wa-conversa')) return;
        const arquivos = (e.dataTransfer && e.dataTransfer.files) || [];
        if (arquivos.length > 0 && mandar(arquivos[0])) e.preventDefault();
    });
}

// 4) A busca na conversa com ↑↓ (F3): leva o achado ATUAL (o que a tela marcou) ao meio da conversa, depois do desenho.
export function irAoAchado() {
    setTimeout(function () {
        const el = document.querySelector('.wa-mensagens mark.wa-achado.atual');
        if (el) el.scrollIntoView({ block: 'center' });
    }, 60);
}

// 5) A citação leva à original (nov/2026, como no WhatsApp): rola até ela e a destaca por um instante.
export function irParaMensagem(id) {
    setTimeout(function () {
        const el = document.querySelector('.wa-mensagens [data-mensagem="' + (id || '').replace(/"/g, '') + '"]');
        if (!el) return;
        el.scrollIntoView({ block: 'center' });
        el.classList.remove('destacada'); void el.offsetWidth; el.classList.add('destacada');
        setTimeout(function () { el.classList.remove('destacada'); }, 1700);
    }, 60);
}

// 6) O histórico aos poucos: chegar ao topo pede as anteriores à tela, e a rolagem fica onde estava (nada "pula").
let ligadoHistorico = false;
export function ligarHistorico(ref) {
    if (ligadoHistorico) return;
    ligadoHistorico = true;
    let pedindo = false;
    document.addEventListener('scroll', function (e) {
        const el = e.target;
        if (pedindo || !el || !el.classList || !el.classList.contains('wa-mensagens') || el.scrollTop > 40) return;
        if (!el.querySelector('.wa-anteriores')) return;
        pedindo = true;
        const antes = el.scrollHeight;
        ref.invokeMethodAsync('CarregarAnteriores').then(function () {
            requestAnimationFrame(function () { el.scrollTop += el.scrollHeight - antes; pedindo = false; });
        }, function () { pedindo = false; });
    }, true);
}

// 6) A Agenda da conversa (nov/2026): tocou num dia, os horários dele aparecem — rolando o MÍNIMO (o título do dia
//    entra pela borda de baixo), sem tirar o calendário de vista.
export function mostrarHorariosDoDia() {
    setTimeout(function () {
        const el = document.querySelector('.wa-ag-do-dia');
        if (el) el.scrollIntoView({ block: 'nearest' });
    }, 60);
}
