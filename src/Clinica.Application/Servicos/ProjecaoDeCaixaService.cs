using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// A projeção de caixa dos próximos dias (out/2026, etapa "Dinheiro" do Gerente).
///
/// Não calcula nada que outro serviço já calcule: as contas em aberto são as do
/// <see cref="ContasService"/>, os depósitos de cartão são os do <see cref="RecebiveisService"/>
/// (líquido, por adquirente e dia) e as contas fixas saem do próprio molde
/// (<c>VencimentosAte</c> + <c>OrigemDe</c>, os mesmos que a geração usa). A montagem e as
/// frases são do <see cref="DinheiroNaTela"/>, puro.
///
/// As leituras vão EM FILA, no mesmo repositório: é o mesmo DbContext, e ele não aceita duas
/// operações ao mesmo tempo.
/// </summary>
public sealed class ProjecaoDeCaixaService
{
    private readonly IClinicaRepositorio _repo;

    public ProjecaoDeCaixaService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<ProjecaoDeCaixa> ProjetarAsync(
        DateOnly hoje, int dias = DinheiroNaTela.HorizontePadrao, CancellationToken ct = default)
    {
        if (dias < 1) dias = 1;
        var ate = hoje.AddDays(dias - 1);

        var contas = await new ContasService(_repo).EmAbertoAsync(ate, null, ct);

        // As ocorrências que o molde promete dentro do horizonte e que ainda não viraram
        // lançamento. As que já viraram (inclusive canceladas e pagas) estão nas contas — ou
        // não vão acontecer —, e contá-las de novo dobraria o aluguel.
        var recorrentes = await _repo.RecorrentesAsync(somenteAtivas: true, ct);
        var candidatas = recorrentes
            .SelectMany(r => r.VencimentosAte(ate).Where(v => v >= hoje).Select(v => (Molde: r, Vencimento: v)))
            .ToList();
        var existentes = candidatas.Count == 0
            ? new HashSet<string>()
            : (await _repo.OrigensRecorrenciaExistentesAsync(
                candidatas.Select(c => c.Molde.OrigemDe(c.Vencimento)).Distinct().ToList(), ct)).ToHashSet();
        var fixas = candidatas.Where(c => !existentes.Contains(c.Molde.OrigemDe(c.Vencimento))).ToList();

        var depositos = await new RecebiveisService(_repo).EsperadosAsync(ate, ct);
        var semData = await _repo.PrevistosSemVencimentoAsync(ct);

        return DinheiroNaTela.Projetar(hoje, dias, contas, fixas, depositos, semData);
    }
}
