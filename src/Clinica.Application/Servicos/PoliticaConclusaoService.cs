using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public sealed record PoliticaConclusao(string[] ModalidadesEnfermagem, bool Automatica = false,
    int Horas = 24, DateTime? AtivadaEm = null)
{
    public static PoliticaConclusao Padrao => new([nameof(ModalidadeAtendimento.BsvApenas), nameof(ModalidadeAtendimento.BsvComAcupuntura)]);
    public bool ExigeEnfermagem(Agendamento horario) => ModalidadesEnfermagem.Contains(
        string.IsNullOrWhiteSpace(horario.ModalidadeCodigo) ? horario.ModalidadePrevista.ToString() : horario.ModalidadeCodigo,
        StringComparer.OrdinalIgnoreCase);
}

public sealed class PoliticaConclusaoService(IClinicaRepositorio repo)
{
    public const string Chave = "PoliticaConclusaoClinicaV1";
    public async Task<PoliticaConclusao> ObterAsync(CancellationToken ct = default)
    {
        var json = await repo.ObterConfiguracaoAsync(Chave, ct);
        var regra = json is null ? PoliticaConclusao.Padrao : JsonSerializer.Deserialize<PoliticaConclusao>(json)
            ?? throw new InvalidOperationException("Confira as regras de conclusão no Gerente Geral.");
        if (regra.ModalidadesEnfermagem is null || regra.Horas is < 1 or > 720 || (regra.Automatica && regra.AtivadaEm is null))
            throw new InvalidOperationException("Configuração de conclusão inválida. Confira no Gerente Geral.");
        return regra;
    }

    public async Task SalvarAsync(string[] modalidades, bool automatica, int horas, int usuarioId, CancellationToken ct = default)
    {
        var usuario = await repo.ObterUsuarioAsync(usuarioId, ct);
        if (usuario is null || !usuario.Ativo || usuario.Perfil != PerfilAcesso.Gerente || !usuario.Pode(Permissao.GerenciarUsuarios))
            throw new UnauthorizedAccessException("Somente o Gerente Geral pode configurar a conclusão clínica.");
        if (horas is < 1 or > 720) throw new InvalidOperationException("Informe um prazo entre 1 e 720 horas.");
        var catalogo = await new ModalidadeCatalogoService(repo).ListarAsync(ct);
        if (modalidades is null || modalidades.Any(c => !catalogo.Any(m => m.Codigo == c)))
            throw new InvalidOperationException("Selecione modalidades cadastradas.");
        var anterior = await ObterAsync(ct);
        var agora = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime;
        var regra = new PoliticaConclusao(modalidades.Distinct().Order().ToArray(), automatica, horas,
            automatica ? anterior.Automatica ? anterior.AtivadaEm : agora : null);
        await repo.SalvarConfiguracaoAsync(Chave, JsonSerializer.Serialize(regra), ct);
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = usuario.Login, Acao = "PoliticaConclusaoAlterada",
            Detalhe = $"Antes: {JsonSerializer.Serialize(anterior)}; depois: {JsonSerializer.Serialize(regra)}." }, ct);
        await repo.SalvarAsync(ct);
    }
}
