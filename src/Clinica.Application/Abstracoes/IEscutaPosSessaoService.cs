using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Abstracoes;

/// <summary>
/// A ESCUTA PÓS-SESSÃO (nov/2026, Fila 360). As barreiras moram aqui, no serviço, e não só no botão:
/// <list type="bullet">
/// <item><b>Ver a fila</b> (quem teve sessão e pede escuta — dado de agenda): o acesso do acompanhamento.</item>
/// <item><b>Ler e registrar como o paciente está</b> (dado de saúde): o acesso do acompanhamento E
/// <see cref="Permissao.VerProntuario"/>.</item>
/// <item><b>O aviso de piora e o "Ciente"</b>: o profissional da sessão (o login ligado a ele), com
/// <see cref="Permissao.VerProntuario"/>.</item>
/// </list>
/// Registro clínico não se apaga: errou, CANCELA com motivo e registra de novo.
/// </summary>
public interface IEscutaPosSessaoService
{
    /// <summary>As sessões dos últimos <see cref="EscutaNaTela.DiasParaEscutar"/> dias que pedem escuta e ainda não têm.</summary>
    Task<IReadOnlyList<SessaoParaEscutar>> ParaEscutarAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>As escutas do paciente, canceladas incluídas. Dado de saúde.</summary>
    Task<IReadOnlyList<EscutaPosSessao>> DoPacienteAsync(int usuarioId, int pacienteId, CancellationToken ct = default);

    /// <summary>Registra a resposta. Idempotente pela sessão: a escuta vigente da mesma sessão não se duplica.</summary>
    Task<EscutaPosSessao> RegistrarAsync(int usuarioId, RegistrarEscuta pedido, CancellationToken ct = default);

    /// <summary>Cancela com motivo (5 letras ou mais). A linha fica.</summary>
    Task CancelarAsync(int usuarioId, int escutaId, string motivo, CancellationToken ct = default);

    /// <summary>As pioras sem "Ciente" das sessões do profissional ligado ao login.</summary>
    Task<IReadOnlyList<AvisoDePiora>> AvisosDoProfissionalAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>O profissional leu o aviso de piora. Só o da sessão (ou a gerência).</summary>
    Task CienteAsync(int usuarioId, int escutaId, CancellationToken ct = default);
}
