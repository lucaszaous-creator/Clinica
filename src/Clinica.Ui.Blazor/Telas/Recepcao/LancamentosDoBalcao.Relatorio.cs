using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// A VISTA "RELATÓRIO" DE LANÇAMENTOS (nov/2026 — a cliente: "quais pacientes são de BSV do mês e quais são
/// os convênios deles"). A terceira resposta da mesma porta: Horários marcados (quem marcou), Atendimentos
/// (o que foi lançado) e Relatório (os horários do período por modalidade, especialidade, convênio,
/// profissional e situação, com a exportação .xlsx que se filtra como planilha).
///
/// Diferente das outras duas, o filtro aqui vai ao BANCO (<see cref="RelatorioDeAtendimentosService"/>): um
/// mês inteiro da clínica é a pergunta normal, e cada mudança de filtro relê só o recorte. O período é o
/// do relatório (começa no mês corrente), separado do "hoje" da conferência do balcão.
/// </summary>
public partial class LancamentosDoBalcao
{
    private const string VistaRelatorio = "relatorio";
    private bool VendoRelatorio => _vista == VistaRelatorio;

    private sealed record Opcao(string Valor, string Rotulo);

    private FiltroRelatorioAtendimentos _relFiltro = NovoFiltroDoMes();
    private RelatorioDeAtendimentos? _relatorio;
    private bool _relCarregando, _relFalhou, _relOpcoesLidas, _relExportando;
    private string? _relErro, _relRecusa, _relAviso;
    private int _relGeracao;
    private IReadOnlyList<Opcao> _relModalidades = [], _relEspecialidades = [], _relConvenios = [], _relProfissionais = [];

    private static FiltroRelatorioAtendimentos NovoFiltroDoMes()
    {
        var (inicio, fim) = RelatorioDeAtendimentosRegras.EsteMes(Hoje);
        return new FiltroRelatorioAtendimentos(inicio, fim);
    }

    private static bool PodeExportarRelatorio => Pode(RelatorioDeAtendimentosService.PermissaoParaExportar);

    private bool RelFiltroAtivo => _relFiltro.Modalidade is not null || _relFiltro.Especialidade is not null
        || _relFiltro.Convenio is not null || _relFiltro.ProfissionalId is not null
        || !string.IsNullOrWhiteSpace(_relFiltro.Paciente) || _relFiltro.Situacoes is { Count: > 0 };

    private Task AbrirRelatorioAsync()
    {
        _vista = VistaRelatorio;
        return _relatorio is null && !_relCarregando ? CarregarRelatorioAsync() : Task.CompletedTask;
    }

    private Task FiltrarRelatorioAsync(FiltroRelatorioAtendimentos filtro)
    {
        _relFiltro = filtro;
        return CarregarRelatorioAsync();
    }

    private Task RelPeriodoAsync((DateOnly Inicio, DateOnly Fim) periodo)
        => FiltrarRelatorioAsync(_relFiltro with { Inicio = periodo.Inicio, Fim = periodo.Fim });

    private Task RelDeAsync(ChangeEventArgs e)
        => DateOnly.TryParseExact(e.Value?.ToString(), "yyyy-MM-dd", out var d) ? FiltrarRelatorioAsync(_relFiltro with { Inicio = d }) : Task.CompletedTask;

    private Task RelAteAsync(ChangeEventArgs e)
        => DateOnly.TryParseExact(e.Value?.ToString(), "yyyy-MM-dd", out var d) ? FiltrarRelatorioAsync(_relFiltro with { Fim = d }) : Task.CompletedTask;

    private static string? Valor(ChangeEventArgs e) => e.Value?.ToString() is { Length: > 0 } v ? v : null;

    private bool RelSituacaoMarcada(StatusAgendamento s) => _relFiltro.SituacoesEfetivas.Contains(s);

    private Task RelAlternarSituacaoAsync(StatusAgendamento s, bool marcar)
    {
        var atuais = _relFiltro.SituacoesEfetivas.ToList();
        if (marcar && !atuais.Contains(s)) atuais.Add(s);
        if (!marcar) atuais.Remove(s);
        // Desmarcar tudo volta ao padrão (realizado, falta, cancelado): lista vazia não é "nada".
        var padrao = atuais.Count == 0 || (atuais.Count == RelatorioDeAtendimentosRegras.SituacoesPadrao.Count
                                           && RelatorioDeAtendimentosRegras.SituacoesPadrao.All(atuais.Contains));
        return FiltrarRelatorioAsync(_relFiltro with { Situacoes = padrao ? null : atuais });
    }

    private Task LimparFiltroDoRelatorioAsync()
        => FiltrarRelatorioAsync(new FiltroRelatorioAtendimentos(_relFiltro.Inicio, _relFiltro.Fim));

    /// <summary>
    /// Lê o recorte. Contador de geração: mudar dois filtros seguidos põe duas leituras no ar, e só a última
    /// publica. As opções dos filtros (catálogos e equipe) são lidas uma vez, no mesmo escopo, em sequência.
    /// </summary>
    private async Task CarregarRelatorioAsync()
    {
        var geracao = ++_relGeracao;
        _relRecusa = RelatorioDeAtendimentosRegras.Recusa(_relFiltro);
        _relAviso = null;
        if (_relRecusa is not null) return;

        _relCarregando = true;
        _relFalhou = false;
        try
        {
            using var escopo = Escopo();
            var sp = escopo.ServiceProvider;
            if (!_relOpcoesLidas)
            {
                var modalidades = await sp.GetRequiredService<ModalidadeCatalogoService>().ListarAsync();
                var especialidades = await sp.GetRequiredService<EspecialidadeCatalogoService>().ListarAsync();
                var convenios = await sp.GetRequiredService<ConvenioCatalogoService>().ListarAsync();
                var profissionais = await sp.GetRequiredService<EquipeService>().ProfissionaisAsync();
                if (geracao != _relGeracao || Descartada) return;
                _relModalidades = modalidades.OrderBy(m => m.Nome).Select(m => new Opcao(m.Codigo, m.Ativo ? m.Nome : $"{m.Nome} (inativa)")).ToList();
                _relEspecialidades = especialidades.OrderBy(e => e.Nome).Select(e => new Opcao(e.Codigo, e.Ativo ? e.Nome : $"{e.Nome} (inativa)")).ToList();
                _relConvenios = [new Opcao(RelatorioDeAtendimentosRegras.Particular, "Particular"),
                    .. convenios.OrderBy(c => c.Nome).Select(c => new Opcao(c.Codigo, c.Ativo ? c.Nome : $"{c.Nome} (inativo)"))];
                _relProfissionais = profissionais.OrderBy(p => p.Rotulo).Select(p => new Opcao(p.Id.ToString(), p.Ativo ? p.Rotulo : $"{p.Rotulo} (inativo)")).ToList();
                _relOpcoesLidas = true;
            }
            var relatorio = await sp.GetRequiredService<RelatorioDeAtendimentosService>().GerarAsync(_relFiltro);
            if (geracao != _relGeracao || Descartada) return;
            _relatorio = relatorio;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (geracao != _relGeracao) return;
            Clinica.Application.Diagnostico.Registrar("Lançamentos — relatório de atendimentos", ex);
            _relFalhou = true;
            _relErro = ex is InvalidOperationException ? ex.Message : "Confira a conexão e tente de novo.";
        }
        finally
        {
            if (geracao == _relGeracao) _relCarregando = false;
        }
    }

    /// <summary>Exporta o que o filtro mostra: as duas barreiras (o botão desabilitado e o Exigir), e o serviço confere de novo.</summary>
    private async Task ExportarRelatorioAsync()
    {
        if (!Exigir(RelatorioDeAtendimentosService.PermissaoParaExportar, "exportar a lista de pacientes")) return;
        if (_relExportando) return;
        _relExportando = true;
        _relAviso = null;
        try
        {
            PlanilhaExportada planilha;
            using (var escopo = Escopo())
            {
                var profissional = _relFiltro.ProfissionalId is { } id ? _relProfissionais.FirstOrDefault(p => p.Valor == id.ToString())?.Rotulo : null;
                planilha = await escopo.ServiceProvider.GetRequiredService<RelatorioDeAtendimentosService>()
                    .ExportarAsync(_relFiltro, SessaoUsuario.Atual, profissional);
            }
            var erro = await Host.SalvarEAbrirAsync(planilha.Conteudo, planilha.NomeArquivo, "Planilha do Excel (*.xlsx)|*.xlsx", ".xlsx");
            _relAviso = erro;
        }
        catch (UnauthorizedAccessException ex) { _relAviso = ex.Message; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Clinica.Application.Diagnostico.Registrar("Lançamentos — exportar o relatório de atendimentos", ex);
            _relAviso = ex is InvalidOperationException ? ex.Message : "Não foi possível gerar a planilha. Tente de novo.";
        }
        finally { _relExportando = false; }
    }
}
