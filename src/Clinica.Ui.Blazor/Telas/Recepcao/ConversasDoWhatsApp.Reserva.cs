using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Ui.Blazor.Formularios;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// A RESERVA SEM FICHA NA CONVERSA (nov/2026, aprovada pelo dono a partir do protótipo): quem chega pelo anúncio garante o
/// horário só com o nome — sem cadastro e sem CPF pelo WhatsApp. A tela só desenha e chama: a regra (conflito, limite de
/// reservas por telefone, motivos, a conversão em paciente) é do <see cref="IReservaDeHorarioService"/>, e as frases são de
/// <see cref="RascunhosDaConversa"/>. Cada ato deixa a mensagem pronta no campo; nada é enviado sozinho.
/// Desmarcar e "Virar paciente" abrem o MESMO <see cref="ReservaSemFichaForm"/> das abas Agenda › Dia e Visão geral.
/// </summary>
public partial class ConversasDoWhatsApp
{
    /// <summary>As reservas do telefone da conversa (só se lê sem ficha: com ficha, quem manda é o horário marcado).</summary>
    private IReadOnlyList<ReservaDeHorario> _reservasDaConversa = [];

    /// <summary>A vaga tocada numa conversa sem ficha: o resumo "Reservar o horário" no painel.</summary>
    private (VagaNaConversa Vaga, AtendimentoNaAgenda? OQue)? _vagaParaReservar;
    private string _nomeDaReserva = "";
    private bool _reservaParticular = true;
    private string? _convenioDaReserva;
    private string? _erroDaReserva;
    private bool _reservando;

    /// <summary>A reserva que está sendo REMARCADA pela conversa (a próxima vaga tocada vira o novo horário dela).</summary>
    private ReservaDeHorario? _remarcandoReserva;

    /// <summary>A reserva aberta no formulário compartilhado (Desmarcar ou Virar paciente) e por onde abriu.</summary>
    private (ReservaDeHorario Reserva, ModoDaReserva Modo)? _reservaNoFormulario;

    private IReadOnlyList<ReservaDeHorario> ReservasAbertas => _reservasDaConversa.Where(r => r.OcupaAgenda).OrderBy(r => r.DataHora).ToList();

    private static IReadOnlyList<string> ConveniosDaReserva
        => CatalogoConvenios.Ativos.Where(c => c.Ativo).Select(c => c.Nome).Distinct().OrderBy(n => n, StringComparer.Create(Formato.PtBr, true)).ToList();

    private void LimparReservaDaConversa()
    {
        _reservasDaConversa = [];
        _vagaParaReservar = null;
        _remarcandoReserva = null;
        _reservaNoFormulario = null;
        _erroDaReserva = null;
    }

    /// <summary>Lê as reservas do telefone (sem ficha). A leitura fica na trilha (o serviço registra), por isso é uma por ato.</summary>
    private async Task LerReservasDaConversaAsync(ConversaDoWhatsApp a)
    {
        if (a.Paciente is not null) { _reservasDaConversa = []; return; }
        try
        {
            using var escopo = Escopo();
            _reservasDaConversa = await escopo.ServiceProvider.GetRequiredService<IReservaDeHorarioService>()
                .DoTelefoneAsync(a.Telefone, SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — as reservas sem ficha da conversa não puderam ser lidas", ex);
            _reservasDaConversa = [];
        }
    }

    /// <summary>Sem ficha, a vaga tocada abre o resumo da reserva (ou, remarcando, vira o novo horário da reserva).</summary>
    private void EscolherVagaParaReserva(ConversaDoWhatsApp a, VagaNaConversa v)
    {
        if (!Exigir(Permissao.EditarAgenda, _remarcandoReserva is null ? "reservar horário" : "remarcar a reserva")) return;
        if (_remarcandoReserva is { } r) { _ = RemarcarReservaAsync(a, r, v); return; }
        _erroDaReserva = null;
        _vagaParaReservar = (v, _oQue);
        // O nome que a pessoa deu no WhatsApp; sem nome (o CRM nomeia pelo número), o campo começa vazio.
        _nomeDaReserva = a.Nome.Any(char.IsLetter) ? a.Nome.Trim() : "";
        _reservaParticular = true;
        _convenioDaReserva = null;
    }

    private async Task ReservarAsync(ConversaDoWhatsApp a)
    {
        if (_reservando || _vagaParaReservar is not { } escolha || escolha.OQue is not { } oque) return;
        if (!Exigir(Permissao.EditarAgenda, "reservar horário")) return;
        _reservando = true;
        _erroDaReserva = null;
        try
        {
            var nova = new NovaReserva(_nomeDaReserva, a.Telefone, a.Conversa?.Id, escolha.Vaga.ProfissionalId, null, escolha.Vaga.Inicio, null,
                oque.ModalidadeCodigo, oque.EspecialidadeCodigo, _reservaParticular, _reservaParticular ? null : _convenioDaReserva);
            ReservaDeHorario r;
            using (var escopo = Escopo())
                r = await escopo.ServiceProvider.GetRequiredService<IReservaDeHorarioService>().ReservarAsync(nova, SessaoUsuario.Atual.Operador);
            if (Descartada) return;
            var preparou = DeixarPronta(a, RascunhosDaConversa.Reserva(r.Nome, oque.Rotulo, r.DataHora, escolha.Vaga.Profissional,
                r.Particular ? null : r.ConvenioNome, Hoje));
            _vagaParaReservar = null;
            _feitoNaAgenda = "Reservado. O horário já está ocupado na agenda.";
            Notificacoes.Sucesso(preparou ? "Horário reservado. A mensagem ficou pronta no campo: revise e envie." : "Horário reservado na agenda.");
            await LerReservasDaConversaAsync(a);
            await LerVagasAsync();
        }
        catch (InvalidOperationException ex) { _erroDaReserva = ex.Message; }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — a reserva sem ficha não pôde ser gravada", ex);
            _erroDaReserva = "Não foi possível reservar agora. Tente de novo em instantes.";
        }
        finally { _reservando = false; }
        if (!Descartada) await InvokeAsync(StateHasChanged);
    }

    private void IniciarRemarcarReserva(ReservaDeHorario r)
    {
        if (!Exigir(Permissao.EditarAgenda, "remarcar a reserva")) return;
        _remarcandoReserva = r;
        _vagaParaReservar = null;
        _profissionalDaAgenda = r.ProfissionalId;
        _oQue = _opcoesDaAgenda.FirstOrDefault(o => o.ModalidadeCodigo == r.ModalidadeCodigo && o.EspecialidadeCodigo == r.EspecialidadeConsultaCodigo) ?? _oQue;
        _diaDaAgenda = null;
        _ = LerVagasAsync();
    }

    private async Task RemarcarReservaAsync(ConversaDoWhatsApp a, ReservaDeHorario r, VagaNaConversa v)
    {
        if (_reservando) return;
        _reservando = true;
        _erroDaReserva = null;
        try
        {
            ReservaDeHorario nova;
            using (var escopo = Escopo())
                nova = await escopo.ServiceProvider.GetRequiredService<IReservaDeHorarioService>().RemarcarAsync(r.Id, v.Inicio, SessaoUsuario.Atual.Operador);
            if (Descartada) return;
            var preparou = DeixarPronta(a, RascunhosDaConversa.RemarcacaoDaReserva(nova.Nome,
                AgendaDaConversa.Rotulo(nova.ModalidadeCodigo, nova.EspecialidadeConsultaCodigo), r.DataHora, nova.DataHora, v.Profissional, Hoje));
            _remarcandoReserva = null;
            _feitoNaAgenda = "Reserva remarcada na agenda.";
            Notificacoes.Sucesso(preparou ? "Reserva remarcada. A mensagem ficou pronta no campo: revise e envie." : "Reserva remarcada na agenda.");
            await LerReservasDaConversaAsync(a);
            await LerVagasAsync();
        }
        catch (InvalidOperationException ex) { _erroDaReserva = ex.Message; }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — a reserva sem ficha não pôde ser remarcada", ex);
            _erroDaReserva = "Não foi possível remarcar agora. Tente de novo em instantes.";
        }
        finally { _reservando = false; }
        if (!Descartada) await InvokeAsync(StateHasChanged);
    }

    /// <summary>Desmarcou ou virou paciente pelo formulário compartilhado: a mensagem pronta e, virando paciente, a ficha na conversa.</summary>
    private async Task ReservaDoFormularioAsync(ReservaAlterada ra)
    {
        _reservaNoFormulario = null;
        if (_aberta is not { } a) return;
        if (ra.Acao == AcaoNaReserva.Desmarcada)
        {
            var preparou = DeixarPronta(a, RascunhosDaConversa.CancelamentoDaReserva(ra.Reserva.Nome, ra.Antes, Hoje));
            _feitoNaAgenda = "Reserva desmarcada. O horário voltou a ficar livre.";
            Notificacoes.Sucesso(preparou ? "Reserva desmarcada. A mensagem ficou pronta no campo: revise e envie." : ra.Aviso);
            await LerReservasDaConversaAsync(a);
            await LerVagasAsync();
            return;
        }
        if (ra.Acao == AcaoNaReserva.VirouPaciente && ra.Paciente is { } p && a.Conversa is { } c)
        {
            // A ficha nova (ou a escolhida) passa a ser a da conversa — a mesma ligação do "É esta".
            if (await GravarNoCrmAsync(s => s.LigarFichaAsync(c, p.Id, p.Nome), ra.Aviso) is not null)
            {
                LimparReservaDaConversa();
                await ReleiaDepoisDoAtoAsync();
            }
            return;
        }
        Notificacoes.Sucesso(ra.Aviso);
        await LerReservasDaConversaAsync(a);
    }

    /// <summary>Deixa o texto no campo (nunca envia). Falso quando a janela de 24 h está fechada: aí só um modelo pode ir.</summary>
    private bool DeixarPronta(ConversaDoWhatsApp a, string texto)
    {
        if (!a.Janela.PodeEscreverLivre) return false;
        var r = Rascunho(a);
        r.Nota = false;
        r.Texto = texto;
        return true;
    }
}
