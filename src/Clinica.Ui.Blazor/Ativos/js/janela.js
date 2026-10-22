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
    // As teclas do chat. Enter envia, Shift+Enter quebra a linha — decidido AQUI, na hora da tecla. Pelo
    // @onkeydown:preventDefault do Blazor a decisão só valia no render seguinte, e o Enter que
    // enviava deixava passar a quebra de linha. UM ouvinte na página para o campo da conversa
    // (.chat-campo), que some e volta a cada troca de conversa; o componente novo (outro login)
    // só troca a referência. Durante a composição de acento (IME) o Enter é da composição.
    _ponte: null,
    ligarCampo: function (ref) {
        var self = this;
        self._ponte = ref;
        if (self._campoLigado) return;
        self._campoLigado = true;
        function chamar(nome) {
            if (!self._ponte) return;
            var args = Array.prototype.slice.call(arguments);
            self._ponte.invokeMethodAsync.apply(self._ponte, args).catch(function () { });
        }
        document.addEventListener('keydown', function (e) {
            // Esc fecha o que o chat tem por cima (a sugestão de @, o editor, as escolhas, a busca).
            if (e.key === 'Escape') {
                if (document.querySelector('.chat-painel')) chamar('Escape');
                return;
            }
            if (e.altKey || e.isComposing) return;
            if (!e.target || !e.target.classList || !e.target.classList.contains('chat-campo')) return;
            // Com a sugestão de @ à vista, as setas andam nela e o Tab escolhe.
            if (!e.ctrlKey && document.querySelector('.chat-mencoes') && (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Tab')) {
                e.preventDefault();
                chamar('TeclaDaMencao', e.key);
                return;
            }
            if (e.key !== 'Enter' || e.shiftKey) return;
            // Chat AMPLIADO é onde se escreve o laudo: Enter quebra a linha e Ctrl+Enter envia.
            // No tamanho normal, Enter envia (e Ctrl+Enter também).
            var ampliado = !!e.target.closest('.chat-painel.ampliado');
            if (ampliado && !e.ctrlKey && !document.querySelector('.chat-mencoes')) return;
            e.preventDefault();
            chamar('EnviarPeloEnter');
        });

        // ARRASTAR a bolinha (o chat fechado) e a janelinha (pelo cabeçalho). Enquanto arrasta,
        // a página move o elemento direto; ao soltar, avisa o componente (Posicionar), que guarda
        // a posição por pessoa neste computador e passa a desenhá-la. Mexer menos de 6 px é
        // clique, não arrasto — e o clique que fecha um arrasto não abre o chat.
        var arrasto = null;
        // A área VISÍVEL (sem a barra de rolagem): right/bottom do CSS se medem a partir dela.
        function largura() { return document.documentElement.clientWidth; }
        function altura() { return document.documentElement.clientHeight; }
        document.addEventListener('pointerdown', function (e) {
            if (e.button !== 0 || !e.target.closest) return;
            var bola = e.target.closest('.chat-bola');
            var cab = bola ? null : e.target.closest('.chat-painel.janela:not(.ampliado) .chat-cab');
            if (cab && e.target.closest('button, input, a, select, textarea')) return;
            var alvo = bola ? bola.closest('.chat-flutuante') : cab ? cab.closest('.chat-painel') : null;
            if (!alvo) return;
            var r = alvo.getBoundingClientRect();
            arrasto = {
                alvo: alvo, qual: bola ? 'bola' : 'janela', id: e.pointerId, x: e.clientX, y: e.clientY,
                direita: largura() - r.right, base: altura() - r.bottom, moveu: false
            };
        });
        document.addEventListener('pointermove', function (e) {
            if (!arrasto || e.pointerId !== arrasto.id) return;
            var dx = e.clientX - arrasto.x, dy = e.clientY - arrasto.y;
            if (!arrasto.moveu && Math.abs(dx) + Math.abs(dy) < 6) return;
            arrasto.moveu = true;
            var r = arrasto.alvo.getBoundingClientRect();
            arrasto.d = Math.max(0, Math.min(arrasto.direita - dx, largura() - r.width));
            arrasto.b = Math.max(0, Math.min(arrasto.base - dy, altura() - r.height));
            arrasto.alvo.style.right = arrasto.d + 'px';
            arrasto.alvo.style.bottom = arrasto.b + 'px';
            arrasto.alvo.classList.add('arrastando');
            e.preventDefault();
        });
        function soltar(e) {
            if (!arrasto || e.pointerId !== arrasto.id) return;
            var a = arrasto;
            arrasto = null;
            a.alvo.classList.remove('arrastando');
            if (!a.moveu) return;
            var engolir = function (ev) { ev.stopPropagation(); ev.preventDefault(); };
            window.addEventListener('click', engolir, { capture: true, once: true });
            setTimeout(function () { window.removeEventListener('click', engolir, { capture: true }); }, 300);
            chamar('Posicionar', a.qual, Math.round(a.d), Math.round(a.b));
        }
        document.addEventListener('pointerup', soltar);
        document.addEventListener('pointercancel', soltar);
    },
    // A mensagem achada na busca: rola até ela, no meio da conversa.
    rolarAte: function (id) {
        var el = document.getElementById(id);
        if (el) el.scrollIntoView({ block: 'center' });
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
