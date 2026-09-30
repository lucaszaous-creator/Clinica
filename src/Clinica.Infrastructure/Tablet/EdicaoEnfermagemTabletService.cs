using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    private const long DuracaoEdicaoEnfermagem = 120_000;

    public async Task<object> ReservarEdicaoEnfermagemAsync(SessaoTablet s, int paciente,
        ReservarEdicaoEnfermagemTablet pedido, CancellationToken ct)
    {
        if (pedido.EditorId == Guid.Empty) throw ErroFormularioTablet.Criar("Abra novamente a evolução para iniciar a edição.");
        await Autorizar(s, ct, Permissao.RegistrarEvolucaoEnfermagem);
        var a = await db.Agendamentos.AsNoTracking().SingleOrDefaultAsync(a => a.Id == pedido.AgendamentoId && a.PacienteId == paciente, ct)
            ?? throw new RecursoClinicoIndisponivel();
        if (a.ModalidadePrevista is not (ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura))
            throw ErroFormularioTablet.Criar("Escolha uma sessão BSV para registrar a evolução de enfermagem.");
        // Compatibilidade com abas já abertas: validar o acesso sem reservar a sessão.
        return new { liberada = pedido.Liberar, expiraEm = pedido.Liberar ? 0 : acesso.Agora + DuracaoEdicaoEnfermagem };
    }
}
