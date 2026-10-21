// As pontes da página com as telas (out/2026): atalhos de teclado, a moldura e o vídeo do
// Treinamento. Num arquivo próprio para a página do app (index.html) e a prévia
// (tools/PreviaBlazor) carregarem o MESMO código — uma cópia na prévia divergiria na primeira
// mudança, e a gravação das videoaulas mostraria um atalho que o app não tem.
// Atalhos de teclado das telas (Ctrl+S salvar, Ctrl+P imprimir/exportar, F5 atualizar):
// com o navegador em foco o WPF não recebe a tecla, então a página a repassa à tela à
// vista (PalcoDeTelas.Atalho). preventDefault impede o navegador de recarregar (F5) ou
// abrir a impressão da página (Ctrl+P).
window.clinicaAtalhos = {
    ligar: function (ref) {
        document.addEventListener('keydown', function (e) {
            var k = (e.key || '').toLowerCase(), nome = null;
            if (e.ctrlKey && !e.altKey && !e.shiftKey && k === 's') nome = 'Salvar';
            else if (e.ctrlKey && !e.altKey && !e.shiftKey && k === 'p') nome = 'Imprimir';
            else if (e.key === 'F5' && !e.ctrlKey && !e.altKey) nome = 'Atualizar';
            if (!nome) return;
            e.preventDefault();
            ref.invokeMethodAsync('Atalho', nome);
        });
    }
};
// A MOLDURA (out/2026): Ctrl+F põe o cursor na busca de telas, Ctrl+B leva o foco ao
// menu do topo e Esc fecha o que estiver sobreposto (menu, sino, conta, busca). Os
// mesmos atalhos que a janela WPF tinha — com o navegador em foco, só a página os ouve.
window.clinicaMoldura = {
    ligar: function (ref) {
        document.addEventListener('keydown', function (e) {
            var k = (e.key || '').toLowerCase();
            if (e.ctrlKey && !e.altKey && !e.shiftKey && k === 'f') {
                var campo = document.getElementById('pesquisa-global');
                if (campo) { e.preventDefault(); campo.focus(); campo.select(); }
            } else if (e.ctrlKey && !e.altKey && !e.shiftKey && k === 'b') {
                var menu = document.querySelector('.moldura-nav button');
                if (menu) { e.preventDefault(); menu.focus(); }
            } else if (e.key === 'Escape') {
                ref.invokeMethodAsync('Escape');
            }
        });
    }
};
// O vídeo do Treinamento: começa de onde a pessoa parou, avisa a posição a cada 5 s e
// ao pausar (é o que guarda o progresso), e avisa o fim e a falha.
window.clinicaVideo = {
    ligar: function (el, ref, inicio) {
        if (!el) return;
        var ultimo = -1;
        el.addEventListener('loadedmetadata', function () {
            if (inicio > 0 && isFinite(el.duration)) el.currentTime = Math.min(inicio, Math.max(0, el.duration - 1));
        }, { once: true });
        el.addEventListener('timeupdate', function () {
            var bloco = Math.floor(el.currentTime / 5);
            if (bloco !== ultimo) { ultimo = bloco; ref.invokeMethodAsync('Posicao', el.currentTime); }
        });
        el.addEventListener('pause', function () { if (!el.ended) ref.invokeMethodAsync('Posicao', el.currentTime); });
        el.addEventListener('ended', function () { ref.invokeMethodAsync('Terminou'); });
        el.addEventListener('error', function () { ref.invokeMethodAsync('Falhou'); });
    },
    ir: function (el, segundos) { if (el) { el.currentTime = segundos; el.play(); } },
    pausar: function (el) { if (el && !el.paused) el.pause(); }
};
// A altura da janela, para o menu "⋯" da Agenda do dia abrir para CIMA quando não cabe
// embaixo (out/2026). Sem ela o menu cobria o próprio botão na última linha.
window.clinicaJanela = {
    altura: function () { return window.innerHeight; }
};
// O CHAT DA EQUIPE (out/2026): o modo (bolinha / janela / encostado), a conversa aberta e
// as preferências de som moram NESTE computador, por pessoa — "só para você, neste
// computador" é o que a tela promete. O som é sintetizado (WebAudio): não há arquivo de
// áudio para empacotar, e o navegador da janela já está acordado.
window.clinicaChat = {
    ler: function (usuario) {
        try { return localStorage.getItem('clinica.chat.' + usuario); } catch (e) { return null; }
    },
    gravar: function (usuario, texto) {
        try { localStorage.setItem('clinica.chat.' + usuario, texto); } catch (e) { }
    },
    rolarAoFim: function (el) {
        if (el) el.scrollTop = el.scrollHeight;
    },
    _ctx: null,
    tocar: function (tipo) {
        try {
            var Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return;
            var ctx = this._ctx || (this._ctx = new Ctx());
            if (ctx.state === 'suspended') ctx.resume();
            // Direta: dois toques curtos. Menção: três, mais agudos. Pedido: um grave e longo.
            var notas = tipo === 'mencao' ? [880, 1046, 1318]
                      : tipo === 'pedido' ? [523, 392]
                      : [784, 988];
            var dur = tipo === 'pedido' ? 0.22 : 0.11;
            var t = ctx.currentTime;
            notas.forEach(function (f, i) {
                var o = ctx.createOscillator(), g = ctx.createGain();
                o.type = 'sine'; o.frequency.value = f;
                var ini = t + i * (dur + 0.04);
                g.gain.setValueAtTime(0.0001, ini);
                g.gain.exponentialRampToValueAtTime(0.18, ini + 0.015);
                g.gain.exponentialRampToValueAtTime(0.0001, ini + dur);
                o.connect(g); g.connect(ctx.destination);
                o.start(ini); o.stop(ini + dur + 0.02);
            });
        } catch (e) { }
    }
};
