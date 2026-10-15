using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;

namespace Clinica.Application.Servicos;

/// <summary>Lê apenas as naturezas que a tela exibe, em um único escopo de carga.</summary>
public sealed class LeituraLinhaDoTempoService(IClinicaRepositorio repo)
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

        // Sequenciais: o repositório compartilha o mesmo DbContext.
        IReadOnlyList<Evolucao> sessoes = Ler(NaturezaRegistroClinico.SessaoMedica)
            ? await repo.EvolucoesDoPacienteAsync(pacienteId, ct) : [];
        IReadOnlyDictionary<int, int> anexos = sessoes.Count > 0
            ? await repo.ContagemDeAnexosAsync(sessoes.Select(e => e.Id).ToArray(), ct)
            : new Dictionary<int, int>();
        IReadOnlyList<EvolucaoEnfermagem> enfermagem = Ler(NaturezaRegistroClinico.EvolucaoEnfermagem)
            ? await repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, 200, ct) : [];
        IReadOnlyList<PrescricaoInterna> infusoes = Ler(NaturezaRegistroClinico.PrescricaoInterna)
            ? await repo.PrescricoesInternasDoPacienteAsync(pacienteId, 200, ct) : [];
        IReadOnlyList<DocumentoClinico> documentos = Ler(NaturezaRegistroClinico.DocumentoClinico)
            ? await repo.DocumentosDoPacienteAsync(pacienteId, ct) : [];
        // Cancelados permanecem visíveis e marcados na ficha.
        IReadOnlyList<AnexoPaciente> arquivos = Ler(NaturezaRegistroClinico.ArquivoDaFicha)
            ? await repo.AnexosDaFichaAsync(pacienteId, incluirCancelados: true, ct: ct) : [];

        return LinhaDoTempoClinica.Montar(acessos, sessoes, anexos, enfermagem, infusoes, documentos, arquivos);
    }
}
