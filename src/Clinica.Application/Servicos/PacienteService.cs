using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Cadastro e busca de pacientes. Valida e normaliza o CPF.</summary>
public sealed class PacienteService
{
    private readonly IClinicaRepositorio _repo;

    public PacienteService(IClinicaRepositorio repo) => _repo = repo;

    public Task<IReadOnlyList<Paciente>> BuscarAsync(string? termo, CancellationToken ct = default)
        => _repo.BuscarPacientesAsync(termo, ct);

    public Task<Paciente?> ObterComHistoricoAsync(int pacienteId, CancellationToken ct = default)
        => _repo.ObterPacienteComHistoricoAsync(pacienteId, ct);

    public async Task<Paciente> SalvarNovoAsync(Paciente paciente, bool categoriaManual = false, CancellationToken ct = default)
    {
        Validar(paciente);
        if (!categoriaManual)
            paciente.Categoria = CategoriaConvenio.Base(paciente.Convenio, paciente.PossuiApp);
        await _repo.AdicionarPacienteAsync(paciente, ct);
        await _repo.SalvarAsync(ct);
        return paciente;
    }

    /// <summary>
    /// Salva alterações de um paciente já rastreado pelo mesmo contexto.
    /// Por padrão a categoria é derivada do convênio + app; passe <paramref name="categoriaManual"/>
    /// = true para preservar uma categoria definida manualmente na ficha.
    /// </summary>
    public async Task AtualizarAsync(Paciente paciente, bool categoriaManual = false, CancellationToken ct = default)
    {
        Validar(paciente);
        if (!categoriaManual)
            paciente.Categoria = CategoriaConvenio.Base(paciente.Convenio, paciente.PossuiApp);
        await _repo.SalvarAsync(ct);
    }

    public async Task RemoverAsync(int pacienteId, CancellationToken ct = default)
    {
        await _repo.RemoverPacienteAsync(pacienteId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Retrato em tamanho cheio (JPEG) do paciente; null quando não há foto.</summary>
    public async Task<byte[]?> ObterFotoAsync(int pacienteId, CancellationToken ct = default)
        => (await _repo.ObterFotoPacienteAsync(pacienteId, ct))?.Conteudo;

    /// <summary>
    /// Grava o retrato capturado na recepção: a foto cheia e a miniatura usada nos avatares.
    /// Ambas já chegam em JPEG, recortadas e redimensionadas pela camada visual.
    /// </summary>
    public async Task DefinirFotoAsync(int pacienteId, byte[] conteudo, byte[] miniatura, CancellationToken ct = default)
    {
        if (conteudo.Length == 0 || miniatura.Length == 0)
            throw new ArgumentException("Foto vazia — repita a captura.");

        await _repo.DefinirFotoPacienteAsync(pacienteId, conteudo, miniatura, ct);
        await _repo.SalvarAsync(ct);
    }

    public async Task RemoverFotoAsync(int pacienteId, CancellationToken ct = default)
    {
        await _repo.RemoverFotoPacienteAsync(pacienteId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Valida nome e CPF, normalizando o CPF (só dígitos) quando informado.</summary>
    private static void Validar(Paciente paciente)
    {
        if (string.IsNullOrWhiteSpace(paciente.Nome))
            throw new ArgumentException("Informe o nome do paciente.");

        if (!string.IsNullOrWhiteSpace(paciente.Documento))
        {
            if (!Cpf.Valido(paciente.Documento))
                throw new ArgumentException("CPF inválido. Verifique os dígitos.");
            paciente.Documento = Cpf.Normalizar(paciente.Documento);
        }
    }
}
