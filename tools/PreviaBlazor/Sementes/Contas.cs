// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A semente da tela de Contas a pagar e a receber: contas PREVISTAS com vencimento
/// (vencidas, desta semana e futuras), algumas a receber de pacientes da base, um
/// parcelamento e as contas FIXAS (moldes), com parte das ocorrências já geradas.
///
/// Não depende de Id fixo (lê pacientes e categorias pelo que a base gravou) e não colide
/// com índice único: a origem da recorrência leva o Id do molde que acabou de nascer, e o
/// grupo do parcelamento é um Guid novo.
/// </summary>
public sealed class SementeContas : ISemente
{
    public int Ordem => 10;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var pacientes = db.Pacientes.OrderBy(p => p.Id).Take(6).ToList();
        var categorias = db.CategoriasFinanceiras.ToList();
        int? Cat(string nome) => categorias.FirstOrDefault(c => c.Nome == nome)?.Id;

        void Conta(TipoLancamento tipo, string descricao, decimal valor, int diasAteVencer,
            string? categoria = null, string? contraparte = null, string? documento = null, Paciente? paciente = null)
        {
            var venc = hoje.AddDays(diasAteVencer);
            db.Add(new LancamentoFinanceiro
            {
                Data = venc, DataVencimento = venc, Tipo = tipo, Descricao = descricao, Valor = valor,
                Status = StatusLancamento.Previsto, CategoriaFinanceiraId = categoria is null ? null : Cat(categoria),
                Contraparte = contraparte, DocumentoReferencia = documento, PacienteId = paciente?.Id,
                ValorOriginalObrigacao = valor, CriadoPor = "prévia"
            });
        }

        // ---- A pagar ----
        Conta(TipoLancamento.Saida, "Fornecedor de agulhas e descartáveis", 1_874.30m, -6, "Insumos e medicamentos", "Distribuidora Vida Médica Ltda.", "NF 48.213");
        Conta(TipoLancamento.Saida, "Manutenção do ar-condicionado", 640m, -2, null, "Clima Frio Serviços", "OS 1187");
        Conta(TipoLancamento.Saida, "Conserto da maca elétrica", 980m, 0, null, "MedTec Assistência");
        Conta(TipoLancamento.Saida, "Lavanderia — enxoval de setembro", 412.50m, 3, null, "Lavanderia Bem-Estar");
        Conta(TipoLancamento.Saida, "Seguro do consultório", 2_310m, 12, null, "Seguradora Horizonte", "Apólice 77-0912");
        Conta(TipoLancamento.Saida, "Curso de atualização em acupuntura", 1_450m, 41, "Marketing", "Instituto Oriental de Ensino");
        Conta(TipoLancamento.Saida, "Taxa do conselho regional", 735.80m, 75, "Impostos", "Conselho Regional", "Boleto anuidade");

        // ---- A receber ----
        if (pacientes.Count >= 4)
        {
            Conta(TipoLancamento.Entrada, "Sessões de acupuntura — acerto", 360m, -12, "Particular", paciente: pacientes[0]);
            Conta(TipoLancamento.Entrada, "Consulta particular", 280m, -3, "Particular", paciente: pacientes[1]);
            Conta(TipoLancamento.Entrada, "Pacote de 10 sessões — 2ª parcela", 450m, 5, "Pacotes", paciente: pacientes[2]);
            Conta(TipoLancamento.Entrada, "Avaliação fisioterapêutica", 220m, 18, "Particular", paciente: pacientes[3]);
        }
        Conta(TipoLancamento.Entrada, "Reembolso de convênio — glosa revertida", 1_120m, 9, "Convênios", "Unimed", "Recurso 2026/0412");
        Conta(TipoLancamento.Entrada, "Palestra em empresa parceira", 1_800m, 27, null, "Metalúrgica Serra Azul", "Contrato 15/2026");

        // ---- Parcelamento (3 vezes) ----
        var grupo = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            var venc = hoje.AddDays(8).AddMonths(i);
            db.Add(new LancamentoFinanceiro
            {
                Data = hoje, DataVencimento = venc, Tipo = TipoLancamento.Saida, Descricao = "Cadeiras da sala de espera",
                Valor = i == 0 ? 1_066.68m : 1_066.66m, Status = StatusLancamento.Previsto, Contraparte = "Móveis Conforto",
                DocumentoReferencia = "Pedido 3390", GrupoParcelamento = grupo, NumeroParcelaConta = i + 1, TotalParcelasConta = 3,
                ValorOriginalObrigacao = i == 0 ? 1_066.68m : 1_066.66m, CriadoPor = "prévia"
            });
        }
        db.SaveChanges();

        // ---- Contas fixas (moldes) ----
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var contador = new LancamentoRecorrente
        {
            Descricao = "Honorários do contador", Tipo = TipoLancamento.Saida, Valor = 1_200m,
            Periodicidade = PeriodicidadeRecorrencia.Mensal, PrimeiroVencimento = inicioMes.AddMonths(-5).AddDays(4), CriadoPor = "prévia"
        };
        var software = new LancamentoRecorrente
        {
            Descricao = "Mensalidade do software de agenda", Tipo = TipoLancamento.Saida, Valor = 349.90m,
            Periodicidade = PeriodicidadeRecorrencia.Mensal, PrimeiroVencimento = inicioMes.AddMonths(-2).AddDays(19), CriadoPor = "prévia"
        };
        var limpeza = new LancamentoRecorrente
        {
            Descricao = "Limpeza terceirizada", Tipo = TipoLancamento.Saida, Valor = 890m,
            Periodicidade = PeriodicidadeRecorrencia.Quinzenal, PrimeiroVencimento = hoje.AddDays(-20),
            UltimoVencimento = hoje.AddMonths(6), CategoriaFinanceiraId = Cat("Folha de pagamento"), CriadoPor = "prévia"
        };
        var sublocacao = new LancamentoRecorrente
        {
            Descricao = "Sublocação da sala 2 — fisioterapeuta parceira", Tipo = TipoLancamento.Entrada, Valor = 1_500m,
            Periodicidade = PeriodicidadeRecorrencia.Mensal, PrimeiroVencimento = inicioMes.AddMonths(-3).AddDays(9), CriadoPor = "prévia"
        };
        var alarme = new LancamentoRecorrente
        {
            Descricao = "Monitoramento do alarme", Tipo = TipoLancamento.Saida, Valor = 129m,
            Periodicidade = PeriodicidadeRecorrencia.Mensal, PrimeiroVencimento = inicioMes.AddYears(-1),
            UltimoVencimento = inicioMes.AddMonths(-1), Ativa = false, CriadoPor = "prévia"
        };
        db.AddRange(contador, software, limpeza, sublocacao, alarme);
        db.SaveChanges();

        // Parte já gerada: as ocorrências até hoje do contador e da sublocação. As futuras
        // e as das outras fixas ficam para o "Gerar fixas" — é ele que a prévia mostra.
        foreach (var r in new[] { contador, sublocacao })
            foreach (var v in r.VencimentosAte(hoje.AddDays(3)))
            {
                var pago = v < hoje.AddDays(-20);
                db.Add(new LancamentoFinanceiro
                {
                    Data = v, DataVencimento = v, Tipo = r.Tipo, Descricao = r.Descricao, Valor = r.Valor,
                    Status = pago ? StatusLancamento.Realizado : StatusLancamento.Previsto,
                    DataPagamento = pago ? v : null, FormaPagamento = pago ? FormaPagamento.Pix : null,
                    CategoriaFinanceiraId = r.CategoriaFinanceiraId, OrigemRecorrencia = r.OrigemDe(v), CriadoPor = "prévia"
                });
            }
        db.SaveChanges();
    }
}
