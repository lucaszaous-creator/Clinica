using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// A VISTA "RELATÓRIO PARA OS MÉDICOS" DE REPASSES (nov/2026 — o dono: "um relatório de repasse para
/// exportar aos médicos, com filtros"). A segunda resposta da mesma porta: a vista Fechamento responde
/// "o que fechar e pagar agora"; esta, "o que foi conferido para cada médico num período, e o papel de
/// cada um". Lê só FECHAMENTOS GRAVADOS (<see cref="RelatorioRepasseService"/>), nunca a prévia — o que
/// vai ao médico é o que alguém conferiu.
///
/// O padrão é o da vista "Relatório" de Lançamentos (LancamentosDoBalcao.Relatorio.cs): o filtro vai ao
/// BANCO, contador de geração próprio, as opções dos filtros lidas uma vez, e a exportação com as duas
/// barreiras (o botão desabilitado e o Exigir) — o serviço confere de novo e grava a trilha.
/// As frases e os atalhos de período moram em <see cref="RelatorioRepasseNaTela"/>.
/// </summary>
public partial class Repasses
{
    private const string VistaFechamento = "fechamento";
    private const string VistaRelatorio = "relatorio";
    private string _vista = VistaFechamento;
    private bool VendoRelatorio => _vista == VistaRelatorio;

    private sealed record Opcao(string Valor, string Rotulo);

    private FiltroRelatorioRepasse _relFiltro = RelatorioRepasseNaTela.Inicial(DateOnly.FromDateTime(DateTime.Today));
    private RelatorioRepasse? _relatorio;
    private bool _relCarregando, _relFalhou, _relOpcoesLidas, _relExportando;
    private string? _relErro, _relRecusa, _relAviso;
    private int _relGeracao;
    private IReadOnlyList<Opcao> _relMedicos = [], _relConvenios = [];
    private readonly HashSet<int> _relAbertos = [];

    private static DateOnly HojeDoRelatorio => DateOnly.FromDateTime(DateTime.Today);
    private static bool PodeExportarRelatorio => Pode(RelatorioRepasseService.PermissaoParaExportar);
    private bool RelTemLinhas => _relatorio is { PorProfissional.Count: > 0 };

    private Task AbrirVistaRelatorioAsync()
    {
        _vista = VistaRelatorio;
        return _relatorio is null && !_relCarregando ? CarregarRelatorioAsync() : Task.CompletedTask;
    }

    private void AbrirVistaFechamento() => _vista = VistaFechamento;

    private Task FiltrarRelatorioAsync(FiltroRelatorioRepasse filtro)
    {
        _relFiltro = filtro;
        _relAbertos.Clear();
        return CarregarRelatorioAsync();
    }

    private Task RelPeriodoAsync((DateOnly De, DateOnly Ate) periodo)
        => FiltrarRelatorioAsync(_relFiltro with { CompetenciaDe = periodo.De, CompetenciaAte = periodo.Ate });

    /// <summary>O campo de mês devolve "aaaa-MM"; competência é o dia 1.</summary>
    private static DateOnly? LerMes(ChangeEventArgs e)
        => DateOnly.TryParseExact($"{e.Value}-01", "yyyy-MM-dd", out var d) ? d : null;

    private Task RelDeAsync(ChangeEventArgs e)
        => LerMes(e) is { } d ? FiltrarRelatorioAsync(_relFiltro with { CompetenciaDe = d }) : Task.CompletedTask;

    private Task RelAteAsync(ChangeEventArgs e)
        => LerMes(e) is { } d ? FiltrarRelatorioAsync(_relFiltro with { CompetenciaAte = d }) : Task.CompletedTask;

    private static string? ValorDoCampo(ChangeEventArgs e) => e.Value?.ToString() is { Length: > 0 } v ? v : null;

    private Task RelMedicoAsync(ChangeEventArgs e)
        => FiltrarRelatorioAsync(_relFiltro with { ProfissionalId = int.TryParse(ValorDoCampo(e), out var id) ? id : null });

    private Task RelConvenioAsync(ChangeEventArgs e)
        => FiltrarRelatorioAsync(_relFiltro with { ConvenioCodigo = ValorDoCampo(e) });

    private Task RelSituacaoAsync(ChangeEventArgs e)
        => Enum.TryParse<SituacaoNoRelatorioRepasse>(e.Value?.ToString(), out var s)
            ? FiltrarRelatorioAsync(_relFiltro with { Situacao = s }) : Task.CompletedTask;

    private Task RelPacienteAsync(ChangeEventArgs e)
        => FiltrarRelatorioAsync(_relFiltro with { Paciente = RelatorioRepasseNaTela.Paciente(e.Value?.ToString()) });

    private Task LimparFiltrosDoRelatorioAsync() => FiltrarRelatorioAsync(RelatorioRepasseNaTela.SemFiltros(_relFiltro));

    private void AlternarMedicoDoRelatorio(int profissionalId)
    {
        if (!_relAbertos.Remove(profissionalId)) _relAbertos.Add(profissionalId);
    }

    /// <summary>
    /// Lê o recorte. Contador de geração: mudar dois filtros seguidos põe duas leituras no ar, e só a
    /// última publica. As opções (médicos e convênios) são lidas uma vez, no mesmo escopo, em sequência.
    /// </summary>
    private async Task CarregarRelatorioAsync()
    {
        var geracao = ++_relGeracao;
        _relRecusa = RelatorioRepasseNaTela.Recusa(_relFiltro);
        _relAviso = null;
        if (_relRecusa is not null) return;

        var filtro = _relFiltro;
        _relCarregando = true;
        _relFalhou = false;
        try
        {
            using var escopo = Escopo();
            var sp = escopo.ServiceProvider;
            if (!_relOpcoesLidas)
            {
                var profissionais = await sp.GetRequiredService<EquipeService>().ProfissionaisAsync();
                var convenios = await sp.GetRequiredService<ConvenioCatalogoService>().ListarAsync();
                if (geracao != _relGeracao || Descartada) return;
                _relMedicos = profissionais.OrderBy(p => p.Rotulo)
                    .Select(p => new Opcao(p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), p.Ativo ? p.Rotulo : $"{p.Rotulo} (inativo)")).ToList();
                _relConvenios = [new Opcao(FechamentoRepasseService.CodigoParticular, RelatorioRepasseNaTela.NomeParticular),
                    .. convenios.Where(c => !string.Equals(c.Codigo, ConvenioCadastro.CodigoParticular, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(c => c.Nome).Select(c => new Opcao(c.Codigo, c.Ativo ? c.Nome : $"{c.Nome} (inativo)"))];
                _relOpcoesLidas = true;
            }
            var relatorio = await sp.GetRequiredService<RelatorioRepasseService>().GerarAsync(filtro);
            if (geracao != _relGeracao || Descartada) return;
            _relatorio = relatorio;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (geracao != _relGeracao) return;
            Clinica.Application.Diagnostico.Registrar("Blazor — relatório de repasse não pôde ser lido", ex);
            _relFalhou = true;
            _relErro = ex is InvalidOperationException ? ex.Message : "Confira a conexão e tente de novo.";
        }
        finally
        {
            if (geracao == _relGeracao) _relCarregando = false;
        }
    }

    /// <summary>
    /// "PDFs para os médicos": com um médico só, salva e abre; com vários, pergunta a PASTA antes de gerar
    /// (desistir da pasta não gera nem grava trilha) e grava um arquivo por médico.
    /// </summary>
    private Task ExportarPdfsDoFiltroAsync() => ExportarPdfsAsync(_relFiltro, _relatorio?.PorProfissional.Count ?? 0);

    private Task ExportarPdfDoMedicoAsync(TotalProfissionalRelatorioRepasse medico)
        => ExportarPdfsAsync(RelatorioRepasseNaTela.DoMedico(_relFiltro, medico.ProfissionalId), 1);

    private async Task ExportarPdfsAsync(FiltroRelatorioRepasse filtro, int medicos)
    {
        if (!Exigir(RelatorioRepasseService.PermissaoParaExportar, "exportar o relatório de repasse")) return;
        if (_relExportando || medicos == 0) return;
        _relAviso = null;

        string? pasta = null;
        if (medicos > 1)
        {
            pasta = await Host.PerguntarPastaAsync("Onde gravar os PDFs dos médicos");
            if (pasta is null) return;
        }

        _relExportando = true;
        try
        {
            IReadOnlyList<ArquivoDoRelatorioRepasse> arquivos;
            using (var escopo = Escopo())
                arquivos = await escopo.ServiceProvider.GetRequiredService<RelatorioRepasseService>()
                    .ExportarPdfsAsync(filtro, SessaoUsuario.Atual);

            if (arquivos.Count == 0)
            {
                Notificacoes.Info("Nenhum médico no filtro — não há PDF para gerar.");
                return;
            }
            if (pasta is null)
            {
                // Um médico só (ou o serviço devolveu um arquivo): salva e abre, como o demonstrativo.
                var a = arquivos[0];
                var erro = await Host.SalvarEAbrirAsync(a.Conteudo, a.NomeArquivo, "Documento PDF (*.pdf)|*.pdf", ".pdf");
                if (erro is not null) _relAviso = erro;
                return;
            }

            var gravados = 0;
            foreach (var a in arquivos)
            {
                var erro = await Host.GravarArquivoAsync(System.IO.Path.Combine(pasta, a.NomeArquivo), a.Conteudo);
                if (erro is not null)
                {
                    _relAviso = $"{gravados} de {arquivos.Count} PDFs gravados. {erro}";
                    return;
                }
                gravados++;
            }
            Notificacoes.Sucesso(RelatorioRepasseNaTela.PdfsGravados(gravados, pasta));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            _relAviso = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — PDFs do relatório de repasse não puderam ser gerados", ex);
            _relAviso = "Não foi possível gerar os PDFs. Tente de novo.";
        }
        finally { _relExportando = false; }
    }

    /// <summary>A planilha .xlsx de todas as linhas do filtro — a do financeiro.</summary>
    private async Task ExportarPlanilhaDoRelatorioAsync()
    {
        if (!Exigir(RelatorioRepasseService.PermissaoParaExportar, "exportar o relatório de repasse")) return;
        if (_relExportando || !RelTemLinhas) return;
        _relExportando = true;
        _relAviso = null;
        try
        {
            ArquivoDoRelatorioRepasse planilha;
            using (var escopo = Escopo())
                planilha = await escopo.ServiceProvider.GetRequiredService<RelatorioRepasseService>()
                    .ExportarPlanilhaAsync(_relFiltro, SessaoUsuario.Atual);
            var erro = await Host.SalvarEAbrirAsync(planilha.Conteudo, planilha.NomeArquivo, "Planilha do Excel (*.xlsx)|*.xlsx", ".xlsx");
            if (erro is not null) _relAviso = erro;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            _relAviso = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — planilha do relatório de repasse não pôde ser gerada", ex);
            _relAviso = "Não foi possível gerar a planilha. Tente de novo.";
        }
        finally { _relExportando = false; }
    }
}
