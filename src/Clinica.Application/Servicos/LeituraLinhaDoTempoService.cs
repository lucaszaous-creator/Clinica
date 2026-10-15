using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;

namespace Clinica.Application.Servicos;

/// <summary>
/// Lê apenas as naturezas que a tela exibe. Com <see cref="ILeituraParalela"/> (o app), cada natureza
/// sai num escopo próprio e as seis leituras correm juntas: o tempo da linha do tempo passa a ser o
/// da leitura mais lenta, não a soma das seis. Sem ela (os testes), continua em fila no mesmo escopo.
/// </summary>
public sealed class LeituraLinhaDoTempoService(IClinicaRepositorio repo, ILeituraParalela? paralela = null)
{
    public async Task<IReadOnlyDictionary<NaturezaRegistroClinico, IReadOnlyList<RegistroClinicoPaciente>>> LerAsync(
        int pacienteId, Permissao acessos, IReadOnlyCollection<NaturezaRegistroClinico> secoesVisiveis,
        bool mostrarDocumentos = true, CancellationToken ct = default)
    {
        // O componente clínico exige esta permissão antes de QUALQUER leitura.
        if (pacienteId <= 0 || !acessos.HasFlag(Permissao.VerProntuario))
            return LinhaDoTempoClinica.Montar(acessos);

        bool Ler(NaturezaRegistroClinico natureza) =>
            (secoesVisiveis.Count == 0 || secoesVisiveis.Contains(natureza))
            && acessos.HasFlag(CatalogoRegistroClinico.Obter(natureza).PermissaoVer)
            && (mostrarDocumentos || natureza != NaturezaRegistroClinico.DocumentoClinico);

        Task<IReadOnlyList<TItem>> Natureza<TItem>(NaturezaRegistroClinico natureza,
            Func<IClinicaRepositorio, Task<IReadOnlyList<TItem>>> leitura)
            => !Ler(natureza) ? Task.FromResult<IReadOnlyList<TItem>>([])
                : paralela is null ? leitura(repo) : paralela.LerAsync(leitura);

        if (paralela is null)
        {
            // Em fila: o mesmo repositório compartilha o mesmo DbContext.
            var s1 = await Natureza(NaturezaRegistroClinico.SessaoMedica, r => r.EvolucoesDoPacienteAsync(pacienteId, ct));
            var a1 = s1.Count > 0 ? await repo.ContagemDeAnexosAsync(s1.Select(e => e.Id).ToArray(), ct) : new Dictionary<int, int>();
            var e1 = await Natureza(NaturezaRegistroClinico.EvolucaoEnfermagem, r => r.EvolucoesEnfermagemDoPacienteAsync(pacienteId, 200, ct));
            var i1 = await Natureza(NaturezaRegistroClinico.PrescricaoInterna, r => r.PrescricoesInternasDoPacienteAsync(pacienteId, 200, ct));
            var d1 = await Natureza(NaturezaRegistroClinico.DocumentoClinico, r => r.DocumentosDoPacienteAsync(pacienteId, ct));
            // Cancelados permanecem visíveis e marcados na ficha.
            var f1 = await Natureza(NaturezaRegistroClinico.ArquivoDaFicha, r => r.AnexosDaFichaAsync(pacienteId, incluirCancelados: true, ct: ct));
            return LinhaDoTempoClinica.Montar(acessos, s1, a1, e1, i1, d1, f1);
        }

        // Em paralelo, um escopo por natureza. A contagem de anexos depende das sessões e vai junto delas.
        var tSessoes = Ler(NaturezaRegistroClinico.SessaoMedica)
            ? paralela.LerAsync<(IReadOnlyList<Evolucao>, IReadOnlyDictionary<int, int>)>(async r =>
            {
                var lidas = await r.EvolucoesDoPacienteAsync(pacienteId, ct);
                IReadOnlyDictionary<int, int> cont = lidas.Count > 0
                    ? await r.ContagemDeAnexosAsync(lidas.Select(e => e.Id).ToArray(), ct) : new Dictionary<int, int>();
                return (lidas, cont);
            })
            : Task.FromResult<(IReadOnlyList<Evolucao>, IReadOnlyDictionary<int, int>)>(([], new Dictionary<int, int>()));
        var tEnfermagem = Natureza(NaturezaRegistroClinico.EvolucaoEnfermagem, r => r.EvolucoesEnfermagemDoPacienteAsync(pacienteId, 200, ct));
        var tInfusoes = Natureza(NaturezaRegistroClinico.PrescricaoInterna, r => r.PrescricoesInternasDoPacienteAsync(pacienteId, 200, ct));
        var tDocumentos = Natureza(NaturezaRegistroClinico.DocumentoClinico, r => r.DocumentosDoPacienteAsync(pacienteId, ct));
        var tArquivos = Natureza(NaturezaRegistroClinico.ArquivoDaFicha, r => r.AnexosDaFichaAsync(pacienteId, incluirCancelados: true, ct: ct));
        await Task.WhenAll(tSessoes, tEnfermagem, tInfusoes, tDocumentos, tArquivos);
        var (sessoes, anexos) = tSessoes.Result;
        var enfermagem = tEnfermagem.Result; var infusoes = tInfusoes.Result;
        var documentos = tDocumentos.Result; var arquivos = tArquivos.Result;

        return LinhaDoTempoClinica.Montar(acessos, sessoes, anexos, enfermagem, infusoes, documentos, arquivos);
    }
}
