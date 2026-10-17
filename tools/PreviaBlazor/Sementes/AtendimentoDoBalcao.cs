// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// LANÇAR E MARCAR ATENDIMENTO (Recepção, out/2026). Os pacientes com horário hoje vêm das
/// sementes da agenda; esta acrescenta o que só esta tela desenha: o PREÇO do particular (a
/// linha "O pagamento" da coluna direita) e uma ficha "a definir" — o único aviso que impede.
/// </summary>
public sealed class SementeAtendimentoDoBalcao : ISemente
{
    public int Ordem => 86;

    public void Semear(ClinicaDbContext db)
    {
        if (!db.Set<PrecoParticular>().Any())
        {
            db.Add(new PrecoParticular
            {
                ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaComEletro), Valor = 180m,
                VigenteDe = DateOnly.FromDateTime(DateTime.Today).AddMonths(-6)
            });
            db.Add(new PrecoParticular
            {
                ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples), Valor = 150m,
                VigenteDe = DateOnly.FromDateTime(DateTime.Today).AddMonths(-6)
            });
        }
        db.Add(new Paciente
        {
            Nome = "Rogério Antunes Vidal", Telefone = "(21) 98111-0090", DataNascimento = new DateOnly(1959, 2, 3),
            ConvenioCodigo = ConvenioCadastro.CodigoADefinir
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
