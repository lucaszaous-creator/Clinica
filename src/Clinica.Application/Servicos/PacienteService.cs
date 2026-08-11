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

    /// <summary>
    /// Busca por nome ou CPF. <paramref name="limite"/> corta no banco (ver o repositório);
    /// null traz todos. Quem escolhe o corte é a tela, por um único ponto (o seletor de paciente).
    /// </summary>
    public Task<IReadOnlyList<Paciente>> BuscarAsync(string? termo, int? limite = null, CancellationToken ct = default)
        => _repo.BuscarPacientesAsync(termo, limite, ct);

    public Task<Paciente?> ObterComHistoricoAsync(int pacienteId, CancellationToken ct = default)
        => _repo.ObterPacienteComHistoricoAsync(pacienteId, ct);

    /// <summary>Consultas autorizadas do paciente (ciclo de renovação), da mais recente para a mais antiga.</summary>
    public async Task<IReadOnlyList<Consulta>> ConsultasAsync(int pacienteId, CancellationToken ct = default)
        => (await _repo.ConsultasDoPacienteAsync(pacienteId, ct))
            .OrderByDescending(c => c.DataEmissao)
            .ToList();

    public async Task<Paciente> SalvarNovoAsync(Paciente paciente, bool categoriaManual = false, CancellationToken ct = default)
    {
        await CriticarAsync(paciente, ct);
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
        await CriticarAsync(paciente, ct);
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

    /// <summary>
    /// Valida nome e CPF, normaliza o CPF (só dígitos) e RECUSA CPF já cadastrado.
    ///
    /// Por que a recusa mora aqui, e não num índice único do banco
    /// -----------------------------------------------------------
    /// É a mesma decisão do CPF do profissional (parcela 45), pela mesma razão: a
    /// migration roda no <c>MigrateAsync</c> da ABERTURA do app — inclusive do
    /// faturamento, que está em produção. Um índice único falharia na criação se a base
    /// da clínica já tivesse duplicata, e o que não abriria seria o sistema que fatura.
    /// A base atual pode muito bem ter duas fichas do mesmo CPF: até aqui nada as impedia.
    ///
    /// Na escrita a regra não só impede como EXPLICA — e explicar é metade do valor, porque
    /// CPF repetido quase nunca é fraude: é a mesma pessoa cadastrada de novo por quem não
    /// achou a ficha antiga. Dizer o nome de quem já tem aquele CPF transforma um erro
    /// numa instrução ("é este aqui, abra a ficha dele").
    ///
    /// Ponto único de propósito: as duas telas de cadastro (Recepção e faturamento)
    /// passam por aqui, e validar em cada uma cobriria uma e deixaria a outra passando —
    /// o defeito recorrente do projeto vestido de validação.
    /// </summary>
    private async Task CriticarAsync(Paciente paciente, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(paciente.Nome))
            throw new ArgumentException("Informe o nome do paciente.");

        // CPF em branco é o caso NORMAL e continua passando: criança, paciente de
        // convênio cadastrado pela carteirinha, quem chegou sem documento. Exigi-lo aqui
        // travaria o cadastro no balcão com o paciente na frente — e o pedido foi impedir
        // DUPLICATA, não tornar o CPF obrigatório.
        if (string.IsNullOrWhiteSpace(paciente.Documento))
        {
            // Vazio vira nulo: dois pacientes com documento "" são iguais para qualquer
            // comparação futura (e para um índice único, se algum dia houver um).
            paciente.Documento = null;
            return;
        }

        if (!Cpf.Valido(paciente.Documento))
            throw new ArgumentException("CPF inválido. Verifique os dígitos.");

        var cpf = Cpf.Normalizar(paciente.Documento);
        paciente.Documento = cpf;

        // A regra é simples de propósito: CPF que já é de OUTRA ficha é recusado, na
        // criação e na edição.
        //
        // Houve uma versão que abria exceção para a ficha antiga já duplicada — para não
        // travar a correção do telefone dela enquanto a duplicata não fosse resolvida. A
        // direção dispensou: as duplicatas que já existem serão apagadas direto no banco,
        // e daí em diante a única coisa que precisa existir é o impedimento. Regra com
        // exceção que ninguém vai exercer é código a mais para manter e mais uma resposta
        // possível para a mesma pergunta.
        //
        // Id 0 = ficha nova; ao EDITAR, a própria ficha não conta como duplicata dela mesma.
        var repetido = (await _repo.PacientesPorCpfAsync(cpf, ct))
            .FirstOrDefault(p => p.Id != paciente.Id);

        if (repetido is not null)
            throw new InvalidOperationException(
                $"O CPF {Cpf.Formatar(cpf)} já está cadastrado para {repetido.Nome}. "
                + "Abra a ficha dela em vez de criar outra — duas fichas da mesma pessoa "
                + "partem o histórico em dois.");
    }
}
