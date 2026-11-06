using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Formularios;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// A CONVERSA RESOLVE (25/10 — docs/whatsapp-nativo-implementacao.md §16): o que a secretária fazia saindo da
/// aba e agora faz dentro dela. Num arquivo à parte, para as outras frentes da tela não esbarrarem; no Razor
/// ficam só os pontos de ligação (a seção Horários, o balão recebido, o filtro Minhas e a camada dos formulários).
/// <list type="bullet">
/// <item><b>Marcar horário</b> (R5): o formulário da AGENDA com a ficha conferida travada; na volta, compara os
/// horários como a remarcação faz e deixa "Marquei a sua …" no campo. Cancelar também deixa o rascunho.</item>
/// <item><b>Guardar na ficha</b> (R9): a foto ou o PDF recebidos vão aos arquivos da ficha conferida, e a nota
/// interna "Guardado na ficha por …" fica na conversa (quando a conversa é de quem guardou).</item>
/// <item><b>Perguntar ao profissional</b> (R10): a mensagem do paciente vai a quem trata pelo chat da equipe.</item>
/// <item><b>Passar o turno</b> (D12): as conversas abertas de quem sai voltam à fila, com uma nota.</item>
/// </list>
/// Tudo o que a tela afirma vem da Application (<see cref="RascunhosDaConversa"/>, <see cref="GuardarNaFichaService"/>,
/// <see cref="PerguntaAoProfissionalService"/>, <see cref="TurnoNoWhatsApp"/>); aqui, as duas barreiras, o escopo
/// por operação e o desenho. Nada vai ao paciente sem o Enviar.
/// </summary>
public partial class ConversasDoWhatsApp
{
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>A camada dos quatro formulários desta parte (no fim do Razor, ao lado da foto ampliada).</summary>
    private RenderFragment FormulariosQueResolvem() => b =>
    {
        var seq = 0;
        if (_marcando is { } m && _aberta?.Paciente?.Id == m.PacienteId)
        {
            b.OpenComponent<AgendamentoForm>(seq++);
            b.AddAttribute(seq++, nameof(AgendamentoForm.PacienteId), (int?)m.PacienteId);
            b.AddAttribute(seq++, nameof(AgendamentoForm.ProfissionalPreferidoId), m.ProfissionalId);
            b.AddAttribute(seq++, nameof(AgendamentoForm.Titulo), $"Marcar horário para {m.Nome}");
            b.AddAttribute(seq++, nameof(AgendamentoForm.AoFechar), EventCallback.Factory.Create(this, () => _marcando = null));
            b.AddAttribute(seq++, nameof(AgendamentoForm.AoConcluir), EventCallback.Factory.Create<IReadOnlyList<string>>(this, MarcouAsync));
            b.CloseComponent();
        }
        seq = 20;
        if (_guardando is { } g && _aberta?.Conversa?.Id == g.ConversaId && _aberta.Paciente is { } paciente)
        {
            b.OpenComponent<GuardarNaFichaForm>(seq++);
            b.AddAttribute(seq++, nameof(GuardarNaFichaForm.Paciente), paciente.Nome);
            b.AddAttribute(seq++, nameof(GuardarNaFichaForm.Rotulo), g.Rotulo);
            b.AddAttribute(seq++, nameof(GuardarNaFichaForm.NomeDoArquivo), g.Nome);
            b.AddAttribute(seq++, nameof(GuardarNaFichaForm.Endereco), g.Tipo == TipoDeMidia.Imagem ? _memoriaDeMidia.Ler(g.Chave)?.Endereco : null);
            b.AddAttribute(seq++, nameof(GuardarNaFichaForm.AoFechar), EventCallback.Factory.Create(this, () => _guardando = null));
            b.AddAttribute(seq++, nameof(GuardarNaFichaForm.AoConfirmar), (Func<string, DateOnly, Task<string?>>)((t, d) => Redesenhando(GuardarNaFichaAsync(t, d))));
            b.CloseComponent();
        }
        seq = 40;
        if (_perguntando is { } p && _aberta?.Conversa?.Id == p.ConversaId && _aberta is { } aberta)
        {
            b.OpenComponent<PerguntarAoProfissionalForm>(seq++);
            b.AddAttribute(seq++, nameof(PerguntarAoProfissionalForm.Paciente), aberta.Paciente?.Nome ?? aberta.Nome);
            b.AddAttribute(seq++, nameof(PerguntarAoProfissionalForm.PacienteAnexado), AnexaPaciente(aberta));
            b.AddAttribute(seq++, nameof(PerguntarAoProfissionalForm.Trecho), Trecho(p.Item));
            b.AddAttribute(seq++, nameof(PerguntarAoProfissionalForm.Profissionais), _profissionaisDoChat);
            b.AddAttribute(seq++, nameof(PerguntarAoProfissionalForm.Sugerido),
                PerguntaAoProfissionalService.Sugestao(_profissionaisDoChat, aberta.ProximoHorario?.ProfissionalId, aberta.Caso?.ProfissionalId));
            b.AddAttribute(seq++, nameof(PerguntarAoProfissionalForm.AoFechar), EventCallback.Factory.Create(this, () => _perguntando = null));
            b.AddAttribute(seq++, nameof(PerguntarAoProfissionalForm.AoConfirmar), (Func<ProfissionalDoChat, string?, Task<string?>>)((q, x) => Redesenhando(PerguntarAsync(q, x))));
            b.CloseComponent();
        }
        seq = 60;
        if (_passandoTurno && _painel is not null)
        {
            b.OpenComponent<PassarOTurnoForm>(seq++);
            b.AddAttribute(seq++, nameof(PassarOTurnoForm.Conversas), MinhasDoTurno);
            b.AddAttribute(seq++, nameof(PassarOTurnoForm.Relato), _relatoDoTurno);
            b.AddAttribute(seq++, nameof(PassarOTurnoForm.AoFechar), EventCallback.Factory.Create(this, () => _passandoTurno = false));
            b.AddAttribute(seq++, nameof(PassarOTurnoForm.AoConfirmar), (Func<string, Task>)(n => Redesenhando(PassarOTurnoAsync(n))));
            b.CloseComponent();
        }
    };

    /// <summary>
    /// Os formulários chamam a tela por <c>Func</c> (precisam da frase da recusa), e <c>Func</c> não redesenha a tela
    /// sozinha (lição de 24/10, o gravador): sem isto o formulário ficava aberto e a lista velha até a releitura
    /// de 7 s — com a notificação de sucesso já na tela (achado na foto 79).
    /// </summary>
    private async Task<T> Redesenhando<T>(Task<T> ato)
    {
        try { return await ato; }
        finally { if (!Descartada) StateHasChanged(); }
    }

    private async Task Redesenhando(Task ato)
    {
        try { await ato; }
        finally { if (!Descartada) StateHasChanged(); }
    }

    // ==================================================================== R5 — marcar horário

    private (int PacienteId, string Nome, int? ProfissionalId)? _marcando;
    private IReadOnlyList<HorarioDoPaciente> _horariosAntesDeMarcar = [];

    /// <summary>O "Marcar horário" da seção Horários (que agora aparece sempre que a ficha está conferida).</summary>
    private RenderFragment MarcarHorario(ConversaDoWhatsApp a) => b =>
    {
        b.OpenComponent<MarcarHorarioNaConversa>(0);
        b.AddAttribute(1, nameof(MarcarHorarioNaConversa.SemHorarios), a.Dados.Horarios.Count == 0);
        b.AddAttribute(2, nameof(MarcarHorarioNaConversa.Desabilitado), !PodeEscrever || _ocupado || a.Paciente is null);
        b.AddAttribute(3, nameof(MarcarHorarioNaConversa.Dica), PodeEscrever ? null : "Seu acesso não marca horário na agenda.");
        b.AddAttribute(4, nameof(MarcarHorarioNaConversa.AoMarcar), EventCallback.Factory.Create(this, () => AbrirMarcacao(a)));
        b.CloseComponent();
    };

    private void AbrirMarcacao(ConversaDoWhatsApp a)
    {
        if (!Exigir(Permissao.EditarAgenda, "marcar horário") || a.Paciente is not { } p) return;
        _horariosAntesDeMarcar = a.Dados.Horarios;
        // O profissional sugerido: o da próxima sessão; sem ela, o do caso do Acompanhamento. A secretária troca no formulário.
        _marcando = (p.Id, p.Nome, a.ProximoHorario?.ProfissionalId ?? a.Caso?.ProfissionalId);
    }

    /// <summary>Gravou na agenda: relê a coluna e, com a janela aberta e o campo vazio, deixa a mensagem do que a clínica FEZ.</summary>
    private async Task MarcouAsync(IReadOnlyList<string> avisos)
    {
        var antes = _horariosAntesDeMarcar;
        var paciente = _marcando?.PacienteId;
        _marcando = null;
        await ReleiaDepoisDoAtoAsync();
        bool preparou = false, janelaFechada = false;
        if (_aberta is { Conversa: not null, Encerrada: false } a && a.Paciente?.Id == paciente
            && RascunhosDaConversa.HorarioNovo(antes, a.Dados.Horarios) is { } novo)
        {
            if (!a.Janela.PodeEscreverLivre) janelaFechada = true;
            else
            {
                var r = Rascunho(a);
                if (string.IsNullOrWhiteSpace(r.Texto))
                {
                    r.Nota = false;
                    r.Texto = RascunhosDaConversa.Marcacao(a.Paciente?.Nome ?? a.Nome, novo, Hoje);
                    preparou = true;
                }
            }
        }
        Notificacoes.Sucesso(RascunhosDaConversa.DepoisDeMarcar(preparou, janelaFechada));
        if (avisos.Count > 0) await Dialogos.AvisoAsync("Atenção — guias deste horário", string.Join("\n\n", avisos));
    }

    /// <summary>Depois de cancelar: o rascunho "Cancelei o seu horário de …", se a janela está aberta e o campo vazio. Verdadeiro = preparou.</summary>
    private bool PrepararRascunhoDoCancelamento(HorarioDoPaciente h)
    {
        if (_aberta is not { Conversa: not null, Encerrada: false } a || !a.Janela.PodeEscreverLivre) return false;
        var r = Rascunho(a);
        if (!string.IsNullOrWhiteSpace(r.Texto)) return false;
        r.Nota = false;
        r.Texto = RascunhosDaConversa.Cancelamento(a.Paciente?.Nome ?? a.Nome, h, Hoje);
        return true;
    }

    // ==================================================================== R9 — guardar na ficha

    private MidiaDaMensagem? _guardando;

    /// <summary>Por que o "Guardar na ficha" está apagado (nulo = vale): a aba, a permissão de anexar e a ficha conferida.</summary>
    private string? MotivoSemGuardar()
        => !PodeEscrever ? "Seu acesso não inclui o WhatsApp da clínica."
            : !PodeAlgum(GuardarNaFichaService.Permissoes) ? GuardarNaFichaService.SemPermissao
            : _aberta?.Conversa?.PatientId is null ? GuardarNaFichaService.SemFicha
            : null;

    private void AbrirGuardar(MidiaDaMensagem m)
    {
        if (!Exigir(Permissao.EditarAgenda, "guardar arquivo na ficha")
            || !ExigirAlgum(GuardarNaFichaService.Permissoes, "guardar arquivo na ficha do paciente")) return;
        if (_aberta?.Conversa?.PatientId is null) { Recado(GuardarNaFichaService.SemFicha, "info"); return; }
        _guardando = m;
    }

    /// <summary>Guarda (o arquivo vem da memória da tela, ou do CRM) e deixa a nota interna. Devolve a frase da recusa, ou nulo.</summary>
    private async Task<string?> GuardarNaFichaAsync(string titulo, DateOnly data)
    {
        if (_guardando is not { } m || _aberta is not { Conversa: { } conversa } a || conversa.Id != m.ConversaId) return "A conversa não está mais aberta.";
        if (!ExigirAlgum(GuardarNaFichaService.Permissoes, "guardar arquivo na ficha do paciente")) return GuardarNaFichaService.SemPermissao;
        if (await TrazerMidiaAsync(m) is not { } carregada)
            return _midiaFalhou.GetValueOrDefault(m.Chave) ?? "O arquivo ainda está chegando. Tente de novo em instantes.";
        try
        {
            using var escopo = Escopo();
            await escopo.ServiceProvider.GetRequiredService<GuardarNaFichaService>().GuardarAsync(conversa.PatientId, m, carregada.Arquivo, titulo, data);
        }
        catch (InvalidOperationException ex) { return ex.Message; }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — arquivo do WhatsApp não foi guardado na ficha", ex);
            return "Não foi possível guardar agora. Nada foi gravado na ficha; tente de novo.";
        }
        if (Descartada) return null;
        _guardando = null;
        var paciente = a.Paciente?.Nome ?? a.Nome;
        // A nota é de quem está com a conversa (regra do CRM): para os outros ela seria recusada, e a ficha já tem o arquivo.
        var notaNaConversa = false;
        if (a.Minha && _aberta?.Conversa is { } atual && atual.Id == conversa.Id)
        {
            var nota = GuardarNaFichaService.Nota(SessaoUsuario.Atual.Nome, titulo, data);
            notaNaConversa = await GravarNoCrmAsync(s => s.AnotarAsync(atual, nota, Guid.NewGuid().ToString("D")), null, "deixar a nota na conversa") is not null;
        }
        Notificacoes.Sucesso(GuardarNaFichaService.Feito(paciente, notaNaConversa));
        return null;
    }

    // ==================================================================== R10 — perguntar ao profissional

    private (string ConversaId, ItemDaConversa Item)? _perguntando;
    private IReadOnlyList<ProfissionalDoChat> _profissionaisDoChat = [];

    /// <summary>A ação da mensagem recebida (no balão do paciente).</summary>
    private RenderFragment AcoesDaRecebida(ConversaDoWhatsApp c, ItemDaConversa item) => b =>
    {
        b.OpenComponent<AcoesDaMensagemRecebida>(0);
        b.AddAttribute(1, nameof(AcoesDaMensagemRecebida.Desabilitado), !PodeEscrever || string.IsNullOrWhiteSpace(Trecho(item)));
        b.AddAttribute(2, nameof(AcoesDaMensagemRecebida.Dica), PodeEscrever ? null : "Seu acesso não inclui o WhatsApp da clínica.");
        b.AddAttribute(3, nameof(AcoesDaMensagemRecebida.AoPerguntar), EventCallback.Factory.Create(this, () => AbrirPerguntaAsync(c, item)));
        b.CloseComponent();
    };

    /// <summary>O que o paciente escreveu; sem texto, o que ele mandou ("Foto recebida").</summary>
    private static string Trecho(ItemDaConversa item) => !string.IsNullOrWhiteSpace(item.Texto) ? item.Texto : item.Midia?.Rotulo ?? "";

    /// <summary>O paciente vai anexado quando a ficha está conferida E quem pergunta abre a ficha (a barreira do chat).</summary>
    private static bool AnexaPaciente(ConversaDoWhatsApp a) => a.Paciente is not null && Pode(Permissao.VerFichaPaciente);

    private async Task AbrirPerguntaAsync(ConversaDoWhatsApp c, ItemDaConversa item)
    {
        if (!Exigir(Permissao.EditarAgenda, "perguntar ao profissional") || c.Conversa is not { } conversa) return;
        try
        {
            using var escopo = Escopo();
            _profissionaisDoChat = await escopo.ServiceProvider.GetRequiredService<PerguntaAoProfissionalService>().QuemPodeResponderAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — a equipe não pôde ser lida para perguntar ao profissional", ex);
            Notificacoes.Erro("Não foi possível ler a equipe agora. Tente de novo em instantes.");
            return;
        }
        if (Descartada) return;
        _perguntando = (conversa.Id, item);
    }

    private async Task<string?> PerguntarAsync(ProfissionalDoChat para, string? pergunta)
    {
        if (_perguntando is not { } p || _aberta is not { } a || a.Conversa?.Id != p.ConversaId) return "A conversa não está mais aberta.";
        if (!Exigir(Permissao.EditarAgenda, "perguntar ao profissional")) return "Seu acesso não inclui o WhatsApp da clínica.";
        var anexa = AnexaPaciente(a);
        var texto = PerguntaAoProfissionalService.Texto(a.Paciente?.Nome ?? a.Nome, a.Paciente is not null, Trecho(p.Item), pergunta);
        try
        {
            using var escopo = Escopo();
            await escopo.ServiceProvider.GetRequiredService<PerguntaAoProfissionalService>().PerguntarAsync(para, anexa ? a.Paciente!.Id : null, texto);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException) { return ex.Message; }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — a pergunta ao profissional não saiu pelo chat da equipe", ex);
            return "Não foi possível enviar agora. Nada saiu; tente de novo.";
        }
        if (Descartada) return null;
        _perguntando = null;
        Notificacoes.Sucesso(PerguntaAoProfissionalService.Enviada(para.Nome));
        return null;
    }

    // ==================================================================== D12 — passar o turno

    private bool _passandoTurno;
    private string? _relatoDoTurno;

    /// <summary>As conversas que passam: as abertas de quem está logado (o filtro "Minhas").</summary>
    private IReadOnlyList<AppConversationSummary> MinhasDoTurno
        => _painel is { } p ? TurnoNoWhatsApp.DoTurno(p.Conversas, p.Sessao.AgentId) : [];

    /// <summary>A faixa do filtro "Minhas" com o "Passar o turno" (só quando há conversa com a pessoa).</summary>
    private RenderFragment BarraDoTurno() => b =>
    {
        var quantas = MinhasDoTurno.Count;
        if (quantas == 0) return;
        b.OpenComponent<BarraDoTurno>(0);
        b.AddAttribute(1, nameof(Componentes.BarraDoTurno.Quantas), quantas);
        b.AddAttribute(2, nameof(Componentes.BarraDoTurno.Desabilitado), !PodeEscrever || _ocupado);
        b.AddAttribute(3, nameof(Componentes.BarraDoTurno.Dica), PodeEscrever ? null : "Seu acesso não inclui o WhatsApp da clínica.");
        b.AddAttribute(4, nameof(Componentes.BarraDoTurno.AoPassar), EventCallback.Factory.Create(this, AbrirPassagemDoTurno));
        b.CloseComponent();
    };

    private void AbrirPassagemDoTurno()
    {
        if (!Exigir(Permissao.EditarAgenda, "passar o turno")) return;
        _relatoDoTurno = null;
        _passandoTurno = true;
    }

    /// <summary>
    /// Devolve as conversas, uma a uma (o serviço não para no primeiro erro), relê e relata. Todas passaram:
    /// fecha com a confirmação passageira. Alguma ficou: o formulário fica aberto com o relato e só as que ficaram.
    /// </summary>
    private async Task PassarOTurnoAsync(string nota)
    {
        if (!Exigir(Permissao.EditarAgenda, "passar o turno") || _painel is not { } painel || _ocupado) return;
        var minhas = MinhasDoTurno;
        ResultadoDaPassagem resultado;
        _ocupado = true;
        try
        {
            using var escopo = Escopo();
            resultado = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().PassarOTurnoAsync(minhas, nota, painel.Sessao.AgentId);
        }
        catch (InvalidOperationException ex) { _relatoDoTurno = ex.Message; return; }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — passar o turno no WhatsApp", ex);
            _relatoDoTurno = "Não foi possível passar o turno agora. Confira em \"Minhas\" o que ficou com você e tente de novo.";
            return;
        }
        finally { _ocupado = false; }
        if (Descartada) return;
        await ReleiaDepoisDoAtoAsync();
        if (resultado.Falharam.Count == 0)
        {
            _passandoTurno = false;
            Notificacoes.Sucesso(TurnoNoWhatsApp.Relato(resultado));
        }
        else _relatoDoTurno = TurnoNoWhatsApp.Relato(resultado);
    }
}
