using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Registra a BAIXA de uma guia (faturamento efetivado). Não trata de recebimento/recebíveis.</summary>
public sealed class FaturamentoService
{
    private readonly IClinicaRepositorio _repo;

    public FaturamentoService(IClinicaRepositorio repo) => _repo = repo;

    /// <param name="guiaAns">
    /// A guia de consulta no padrão ANS conferida no formulário de baixa. OBRIGATÓRIA quando a
    /// guia a exige (<see cref="GuiaAnsNaBaixa.Exige(CodigoFaturamento)"/>: consulta da Saúde
    /// Petrobras) e ignorada nas demais. Fica por último, depois do <paramref name="ct"/>, para
    /// não mudar a chamada de nenhuma porta que já existia.
    /// </param>
    public async Task DarBaixaAsync(
        int codigoId, DateOnly dataBaixa, string? numeroGuia, string? usuario, string? observacao,
        CancellationToken ct = default, DadosGuiaConsultaAns? guiaAns = null)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        // A crítica de formato mora AQUI, e não em cada tela, porque a baixa tem quatro
        // portas e três caminhos até este método: a tela de baixa (uma guia), a baixa em lote
        // do painel e a rodada de pendências — esta com duas telas, o formulário do painel e
        // a janela bloqueante da abertura do app. O Gerente dá baixa por essas mesmas telas.
        // Validar na tela cobriria uma; validar no caminho único cobre as quatro.
        if (RegraNumeroGuia.Criticar(numeroGuia, FormatoDaGuia(codigo)) is { } critica)
            throw new InvalidOperationException(critica);

        // A guia ANS (nov/2026) segue a mesma lógica: a exigência mora no caminho ÚNICO. A tela de
        // baixa traz a guia conferida; o lote e a rodada não têm formulário por guia, e a recusa
        // daqui é a rede para o que escapar das conferências deles (que avisam ANTES de gravar).
        var exigeGuiaAns = GuiaAnsNaBaixa.Exige(codigo);
        if (exigeGuiaAns)
        {
            if (guiaAns is null)
                throw new InvalidOperationException(GuiaAnsNaBaixa.RecusaSemGuia);
            var erros = GuiaAnsNaBaixa.Validar(guiaAns);
            if (erros.Count > 0)
                throw new InvalidOperationException("A guia ANS não pode ser gravada: " + string.Join(" ", erros.Values));
            GuiaAnsNaBaixa.Aplicar(codigo, guiaAns, usuario, DateTime.Now);
        }

        codigo.DarBaixa(dataBaixa, numeroGuia, usuario, observacao);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = "BaixaGuia",
            Detalhe = $"Baixa em {dataBaixa:dd/MM/yyyy}" +
                      (string.IsNullOrWhiteSpace(numeroGuia) ? "" : $", guia {numeroGuia}") +
                      (exigeGuiaAns ? ", guia ANS de consulta conferida" : ""),
            CodigoId = codigo.Id,
            PacienteId = codigo.Atendimento?.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Anota (ou atualiza/limpa) por que a guia ainda não foi baixada — a explicação fica
    /// visível na pendência para consulta futura. Não dá baixa: só registra a situação.
    /// </summary>
    public async Task RegistrarObservacaoPendenciaAsync(
        int codigoId, string? observacao, string? usuario, CancellationToken ct = default)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        var limpando = string.IsNullOrWhiteSpace(observacao);
        codigo.RegistrarObservacaoPendencia(observacao);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = limpando ? "ObservacaoPendenciaRemovida" : "ObservacaoPendencia",
            Detalhe = limpando ? "Observação da pendência removida" : observacao!.Trim(),
            CodigoId = codigo.Id,
            PacienteId = codigo.Atendimento?.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Estorna a baixa de uma guia (reabre a pendência), quando foi baixada por engano.</summary>
    public async Task EstornarBaixaAsync(int codigoId, string? motivo, string? usuario, CancellationToken ct = default)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        var guiaAnterior = codigo.NumeroGuiaReal;
        codigo.EstornarBaixa(motivo, usuario);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = "EstornoBaixa",
            Detalhe = (string.IsNullOrWhiteSpace(motivo) ? "Estorno" : motivo) +
                      (string.IsNullOrWhiteSpace(guiaAnterior) ? "" : $" (guia anterior: {guiaAnterior})"),
            CodigoId = codigo.Id,
            PacienteId = codigo.Atendimento?.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Formato do número da guia que vale para ESTA guia — o do convênio do paciente.
    ///
    /// Sem paciente carregado devolve <see cref="FormatoNumeroGuia.SemValidacao"/>: não dá
    /// para afirmar o formato de um convênio que não se sabe qual é, e recusar a baixa por
    /// isso travaria o faturamento por causa de um dado que falta no NOSSO lado.
    /// </summary>
    private static FormatoNumeroGuia FormatoDaGuia(CodigoFaturamento codigo)
    {
        var paciente = codigo.Atendimento?.Paciente;
        if (paciente is null) return FormatoNumeroGuia.SemValidacao;

        // ConvenioCodigo nulo significa convênio embutido, e aí o código é o nome da
        // família — a mesma convenção que o catálogo usa em todo o resto do sistema.
        return CatalogoConvenios.FormatoDoNumeroDaGuia(
            paciente.ConvenioCodigo ?? paciente.Convenio.ToString());
    }
}
