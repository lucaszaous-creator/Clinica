using Clinica.Application.Abstracoes;
using Clinica.Application.Formatacao;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O arquivo exportado: o conteúdo, o nome sugerido e quantas linhas levou.</summary>
public sealed record PlanilhaExportada(byte[] Conteudo, string NomeArquivo, int Linhas);

/// <summary>
/// O RELATÓRIO DE ATENDIMENTOS (nov/2026 — a cliente: "se ela quiser saber quais pacientes são de BSV do
/// mês e quais são os convênios deles, a gente não tem essa possibilidade"). Mora na vista "Relatório" de
/// Lançamentos (a porta de "atendimentos no período"), não numa tela nova.
///
/// <list type="bullet">
/// <item>Filtra no BANCO (<see cref="IClinicaRepositorio.RelatorioDeAtendimentosAsync"/>): período,
/// situação, profissional, modalidade, especialidade, convênio e nome. Só o "Particular" — que depende do
/// catálogo carregado — é conferido aqui, sobre o recorte já pequeno.</item>
/// <item>Exportar é SAÍDA DE DADO PESSOAL (lista de pacientes com convênio e carteirinha): exige
/// <see cref="Permissao.VerFichaPaciente"/>, o mesmo bit da exportação da Fila 360, e grava a trilha no
/// mesmo SaveChanges — quem, quando, o filtro e quantas linhas. Nada clínico vai na planilha.</item>
/// </list>
/// </summary>
public sealed class RelatorioDeAtendimentosService(IClinicaRepositorio repo)
{
    /// <summary>O bit que libera a exportação (as duas barreiras: a tela desabilita, o serviço recusa).</summary>
    public const Permissao PermissaoParaExportar = Permissao.VerFichaPaciente;

    public const string AcaoDaTrilha = "RelatorioAtendimentosExportado";

    public async Task<RelatorioDeAtendimentos> GerarAsync(FiltroRelatorioAtendimentos filtro, CancellationToken ct = default)
    {
        if (RelatorioDeAtendimentosRegras.Recusa(filtro) is { } recusa) throw new InvalidOperationException(recusa);
        var linhas = (await repo.RelatorioDeAtendimentosAsync(filtro, ct))
            .Select(RelatorioDeAtendimentosRegras.Montar)
            .Where(l => RelatorioDeAtendimentosRegras.BateConvenio(l, filtro.Convenio))
            .ToList();
        return new RelatorioDeAtendimentos(filtro, linhas);
    }

    /// <summary>
    /// Gera o .xlsx do que o filtro mostra e grava a trilha. Recusa sem login ou sem o bit, ANTES de ler.
    /// <paramref name="profissional"/> é só o nome para a trilha (o filtro guarda o id).
    /// </summary>
    public async Task<PlanilhaExportada> ExportarAsync(FiltroRelatorioAtendimentos filtro, SessaoUsuario? sessao,
        string? profissional = null, CancellationToken ct = default)
    {
        if (sessao is not { Autenticado: true } || !sessao.Pode(PermissaoParaExportar))
            throw new UnauthorizedAccessException("Seu acesso não permite exportar a lista de pacientes (é preciso ver a ficha do paciente).");

        var relatorio = await GerarAsync(filtro, ct);
        var conteudo = PlanilhaXlsx.Gerar("Atendimentos", RelatorioDeAtendimentosRegras.Colunas,
            relatorio.Linhas.Select(RelatorioDeAtendimentosRegras.Celulas));
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(sessao.Operador) ? "?" : sessao.Operador,
            Acao = AcaoDaTrilha,
            Detalhe = RelatorioDeAtendimentosRegras.DetalheDaTrilha(filtro, relatorio.Atendimentos, profissional)
        }, ct);
        await repo.SalvarAsync(ct);
        return new PlanilhaExportada(conteudo, RelatorioDeAtendimentosRegras.NomeArquivo(filtro), relatorio.Atendimentos);
    }
}
