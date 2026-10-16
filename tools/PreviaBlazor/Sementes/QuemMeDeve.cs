// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// "Quem me deve" e "Plano de contas": contas a receber de PACIENTE, vencidas há 3, 20, 45,
/// 80 e 200 dias (as quatro faixas do envelhecimento, com o crítico de mais de 90), mais
/// algumas já pagas e uma sem paciente — essas duas NÃO podem aparecer na tela. Um paciente
/// fica sem telefone, para o botão de cobrança mostrar o estado apagado. E duas categorias
/// a mais no plano de contas, uma delas inativa.
///
/// Roda junto com as sementes das outras telas: lê os pacientes da base pelo banco, sem Id
/// fixo, e os códigos novos de categoria levam o prefixo "QMD" para não colidir com nada.
/// </summary>
public sealed class SementeQuemMeDeve : ISemente
{
    public int Ordem => 20;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var pacientes = db.Pacientes.OrderBy(p => p.Id).Take(6).ToList();
        if (pacientes.Count < 5) return;
        var particular = db.CategoriasFinanceiras.FirstOrDefault(c => c.Nome == "Particular")?.Id;

        void Conta(Paciente p, int diasVencida, decimal valor, string descricao, bool paga = false)
        {
            var vencimento = hoje.AddDays(-diasVencida);
            db.Add(new LancamentoFinanceiro
            {
                Data = vencimento, Tipo = TipoLancamento.Entrada, Valor = valor, Descricao = descricao,
                Status = paga ? StatusLancamento.Realizado : StatusLancamento.Previsto,
                DataVencimento = vencimento,
                DataPagamento = paga ? vencimento.AddDays(2) : null,
                FormaPagamento = paga ? FormaPagamento.Pix : null,
                PacienteId = p.Id,
                CategoriaFinanceiraId = particular
            });
        }

        Conta(pacientes[0], 3, 180m, "Sessão de acupuntura — particular");
        Conta(pacientes[1], 20, 220m, "Consulta particular");
        Conta(pacientes[1], 12, 220m, "Retorno particular");
        Conta(pacientes[2], 45, 1_350m, "Pacote de 10 sessões — 2ª parcela");
        Conta(pacientes[3], 80, 480m, "Sessões de fisioterapia (2)");
        Conta(pacientes[4], 200, 2_700m, "Pacote de 20 sessões — saldo");
        Conta(pacientes[4], 150, 180m, "Sessão avulsa");
        // Já pagas: a lista é de quem DEVE — pagar tira da conta.
        Conta(pacientes[0], 30, 180m, "Sessão de acupuntura — particular", paga: true);
        Conta(pacientes[5], 60, 300m, "Consulta particular", paga: true);
        // Vencida SEM paciente: é a receber (reembolso), não inadimplência.
        db.Add(new LancamentoFinanceiro
        {
            Data = hoje.AddDays(-15), Tipo = TipoLancamento.Entrada, Valor = 900m,
            Descricao = "Reembolso do convênio — glosa revertida", Status = StatusLancamento.Previsto,
            DataVencimento = hoje.AddDays(-15)
        });

        // Sem telefone: a cobrança fica apagada e diz por quê.
        pacientes[3].Telefone = null;

        db.Add(new CategoriaFinanceira { Codigo = "QMD-E4", Nome = "Venda de produtos", Tipo = TipoLancamento.Entrada, Ordem = 9 });
        db.Add(new CategoriaFinanceira { Codigo = "QMD-S6", Nome = "Energia elétrica (antiga)", Tipo = TipoLancamento.Saida, Ordem = 10, Ativa = false });
        db.SaveChanges();
    }
}
