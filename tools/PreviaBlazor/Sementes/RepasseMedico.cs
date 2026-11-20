// Dados FICTÍCIOS da prévia — nenhum paciente, médico ou valor de contrato real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O repasse por GUIA RECEBIDA (nov/2026) — as telas "Repasses" e "Repasse médico". Reproduz o
/// caso que a clínica fazia à mão no "Demonstrativo de Repasse Médico":
/// - regras por consulta, por convênio: Amil R$ 65, Petrobras R$ 80, Sul América R$ 72 (Dra.
///   Helena e Dr. Marcos); a Bianca só tem regra para a acupuntura com eletro — a consulta de
///   convênio dela cai em "sem regra", o atalho "Cadastrar regra" tem o que mostrar;
/// - guias PAGAS e conciliadas neste mês (receita de entrada REALIZADA ligada à guia, com a
///   referência do pagamento: "GRD 004471", "Conta 88213", "Lote 2026-77"), de atendimentos do
///   mês passado — a competência é a do PAGAMENTO;
/// - guias faturadas e ainda sem pagamento, e uma com a receita lançada como A RECEBER
///   (previsto): "aguardando o convênio", informadas e nunca somadas;
/// - um fechamento do mês passado já PAGO (Dra. Helena) e outro só CONFERIDO (Dr. Marcos), com
///   os itens gravados — as guias deles não voltam à prévia (a chave G:{guia} está viva).
/// Roda depois de Recebimentos (30: Sul América no catálogo, preços) e de RepassesResultado
/// (60: as regras por percentual e as sessões particulares). Sem Id fixo.
/// </summary>
public sealed class SementeRepasseMedico : ISemente
{
    public int Ordem => 104;

    public void Semear(ClinicaDbContext db)
    {
        var profissionais = db.Set<Profissional>().OrderBy(p => p.Id).ToList();
        if (profissionais.Count < 3) return;
        var helena = profissionais[0];
        var marcos = profissionais[1];
        var bianca = profissionais[2];

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesPassado = inicioMes.AddMonths(-1);
        // Dias do mês corrente que já passaram: o pagamento nunca é no futuro.
        var diasPassados = Math.Max(1, hoje.Day);
        DateOnly NoMes(int dia) => inicioMes.AddDays(Math.Min(dia, diasPassados) - 1);

        if (!db.Set<ConvenioCadastro>().Any(c => c.Codigo == "SulAmericaPrevia"))
        {
            db.Add(new ConvenioCadastro { Codigo = "SulAmericaPrevia", Nome = "Sul América Saúde", Familia = Convenio.Personalizado, Ativo = true });
            db.SaveChanges();
            new ConvenioCatalogoService(new ClinicaRepositorio(db)).RecarregarCacheAsync().GetAwaiter().GetResult();
        }
        var categoriaConvenios = db.Set<CategoriaFinanceira>().FirstOrDefault(c => c.Nome == "Convênios")?.Id;
        var categoriaFolha = db.Set<CategoriaFinanceira>().FirstOrDefault(c => c.Nome == "Folha de pagamento")?.Id;

        // ===== Regras (a tabela que a clínica escrevia no rodapé do demonstrativo) =====
        var vigencia = inicioMes.AddMonths(-6);
        RegraRepasse Regra(Profissional p, string convenio, decimal valor, DateOnly? de = null, DateOnly? ate = null, string? obs = null)
            => db.Add(new RegraRepasse
            {
                ProfissionalId = p.Id, Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = valor,
                ConvenioCodigo = convenio, TipoGuia = TipoCodigo.Consulta, VigenteDe = de ?? vigencia, VigenteAte = ate,
                Observacoes = obs, CriadoPor = "Prévia"
            }).Entity;
        // O reajuste que a tabela mostra no histórico: a Amil da Dra. Helena foi de 60 para 65.
        Regra(helena, "Amil", 60m, vigencia.AddMonths(-6), vigencia.AddDays(-1));
        Regra(helena, "Amil", 65m, obs: "Reajuste negociado em maio.");
        Regra(helena, "Petrobras", 80m);
        Regra(helena, "SulAmericaPrevia", 72m);
        Regra(marcos, "Amil", 65m);
        Regra(marcos, "Petrobras", 80m);
        // A Bianca: só a acupuntura com eletro (as sessões particulares da semente de Repasses).
        db.Add(new RegraRepasse
        {
            ProfissionalId = bianca.Id, Base = BaseRepasse.PercentualDaReceita, Percentual = 35m,
            ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaComEletro), VigenteDe = vigencia, CriadoPor = "Prévia"
        });
        db.SaveChanges();

        // ===== Pacientes desta semente (nomes inventados) =====
        Paciente P(string nome, Convenio familia, string codigo) =>
            db.Add(new Paciente { Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Telefone = "(21) 97777-0000" }).Entity;
        var amil = new[]
        {
            P("Renata Queiroz Abreu", Convenio.Amil, "Amil"), P("Fábio Nogueira Lins", Convenio.Amil, "Amil"),
            P("Silvana Prates", Convenio.Amil, "Amil"), P("Caio Medeiros Tavares", Convenio.Amil, "Amil")
        };
        var petrobras = new[] { P("Ivone Carvalhal", Convenio.Petrobras, "Petrobras"), P("Wagner Siqueira", Convenio.Petrobras, "Petrobras") };
        var sul = new[] { P("Lúcia Bittencourt", Convenio.Personalizado, "SulAmericaPrevia"), P("Otávio Fontes", Convenio.Personalizado, "SulAmericaPrevia") };
        db.SaveChanges();

        var numero = 7300;
        decimal Preco(string convenio) => convenio switch { "Amil" => 210m, "Petrobras" => 180m, _ => 132.4m };

        // Uma consulta de convênio REALIZADA, com o médico no horário e a guia baixada (faturada).
        CodigoFaturamento Consulta(Paciente p, Profissional prof, DateOnly atendidoEm)
        {
            var quando = atendidoEm.ToDateTime(new TimeOnly(9 + numero % 8, 0));
            var atendimento = db.Add(new Atendimento
            {
                PacienteId = p.Id, Data = atendidoEm, Modalidade = ModalidadeAtendimento.Consulta,
                ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta), EspecialidadeConsulta = Especialidade.Acupuntura,
                Categoria = Clinica.Domain.Categoria.Verde, LancadoPor = "Prévia", LancadoEm = quando, RealizadoEm = quando
            }).Entity;
            db.Add(new Agendamento
            {
                PacienteId = p.Id, DataHora = quando, ModalidadePrevista = ModalidadeAtendimento.Consulta,
                ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta), Status = StatusAgendamento.Realizado,
                Atendimento = atendimento, ProfissionalId = prof.Id, DuracaoMinutos = 30
            });
            db.SaveChanges();
            var guia = db.Add(new CodigoFaturamento
            {
                AtendimentoId = atendimento.Id, Tipo = TipoCodigo.Consulta, Ordem = OrdemCodigo.Primeiro,
                FormaObtencao = FormaObtencao.NaoAplica, DataPrevistaFaturamento = atendidoEm, Status = StatusCodigo.Baixado,
                DataBaixa = atendidoEm.AddDays(1) > hoje ? hoje : atendidoEm.AddDays(1),
                NumeroGuiaReal = $"{++numero:D8}", UsuarioBaixa = "Prévia"
            }).Entity;
            db.SaveChanges();
            return guia;
        }

        // A receita da guia — REALIZADA (o convênio pagou e foi conciliado) ou PREVISTA (a receber).
        LancamentoFinanceiro Receita(CodigoFaturamento guia, Paciente p, DateOnly? pagoEm, string? referencia)
            => db.Add(new LancamentoFinanceiro
            {
                Data = guia.DataBaixa!.Value, Tipo = TipoLancamento.Entrada, Valor = Preco(p.ConvenioCodigo!),
                Status = pagoEm is null ? StatusLancamento.Previsto : StatusLancamento.Realizado,
                DataPagamento = pagoEm, DocumentoReferencia = referencia,
                Descricao = $"{NomeDoConvenio(p)} — guia {guia.NumeroGuiaReal} ({p.Nome})",
                FormaPagamento = FormaPagamento.Convenio, CategoriaFinanceiraId = categoriaConvenios,
                PacienteId = p.Id, AtendimentoId = guia.AtendimentoId, CodigoFaturamentoId = guia.Id,
                Convenio = p.Convenio, ConvenioCodigo = p.ConvenioCodigo, CriadoPor = "Prévia"
            }).Entity;

        // ===== Pagas e conciliadas NESTE mês (atendimentos do mês passado) =====
        var grd = "GRD 004471";
        var conta = "Conta 88213";
        var lote = "Lote 2026-77";
        void Paga(Paciente p, Profissional prof, int diaAtendido, int diaPago, string referencia)
        {
            var guia = Consulta(p, prof, mesPassado.AddDays(diaAtendido - 1));
            Receita(guia, p, NoMes(diaPago), referencia);
        }
        Paga(amil[0], helena, 6, 10, grd);
        Paga(amil[1], helena, 6, 10, grd);
        Paga(amil[2], helena, 13, 10, grd);
        Paga(amil[3], helena, 20, 10, grd);
        Paga(amil[0], marcos, 8, 10, grd);
        Paga(amil[2], marcos, 21, 10, grd);
        Paga(petrobras[0], helena, 7, 14, conta);
        Paga(petrobras[1], helena, 15, 14, conta);
        Paga(petrobras[1], marcos, 9, 14, conta);
        // A competência é a do PAGAMENTO: a Sul América deste mês trouxe consultas de dias 6 e 7.
        Paga(sul[0], helena, 6, 16, lote);
        Paga(sul[1], helena, 7, 16, lote);
        Paga(sul[0], helena, 22, 16, lote);
        // A consulta de convênio da Bianca: paga, mas sem regra que a alcance.
        Paga(sul[1], bianca, 23, 16, lote);
        db.SaveChanges();

        // ===== Aguardando o convênio =====
        Consulta(amil[1], helena, NoMes(3));                                   // faturada, sem receita
        Consulta(amil[3], helena, NoMes(4));
        var prevista = Consulta(petrobras[0], marcos, mesPassado.AddDays(26)); // receita A RECEBER
        Receita(prevista, petrobras[0], null, null);
        db.SaveChanges();

        // ===== Fechamentos do mês passado: um PAGO e um só CONFERIDO =====
        RepasseFechamento Fechamento(Profissional prof, IReadOnlyList<(Paciente P, int DiaAtendido)> guias, decimal valorPorGuia,
            string referencia, bool pago)
        {
            var itens = new List<ItemRepasse>();
            foreach (var (p, dia) in guias)
            {
                var atendidoEm = mesPassado.AddDays(-30 + dia);
                var guia = Consulta(p, prof, atendidoEm);
                var pagoEm = mesPassado.AddDays(9);
                var receita = Receita(guia, p, pagoEm, referencia);
                db.SaveChanges();
                itens.Add(new ItemRepasse
                {
                    Origem = OrigemItemRepasse.Convenio, Natureza = NaturezaItemRepasse.Credito, Chave = $"G:{guia.Id}",
                    AtendimentoId = guia.AtendimentoId, CodigoFaturamentoId = guia.Id, LancamentoReceitaId = receita.Id,
                    PacienteId = p.Id, PacienteNome = p.Nome, DataAtendimento = atendidoEm,
                    ConvenioCodigo = p.ConvenioCodigo!, ConvenioNome = NomeDoConvenio(p), TipoGuia = TipoCodigo.Consulta,
                    Descricao = "Consulta", NumeroGuia = guia.NumeroGuiaReal, ReferenciaPagamento = referencia,
                    DataRecebimento = pagoEm, ValorRecebido = receita.Valor,
                    RegraDescricao = $"{NomeDoConvenio(p)} · Consulta · {valorPorGuia.ToString("C", PtBr)} por guia",
                    Valor = valorPorGuia
                });
            }
            var total = itens.Sum(i => i.Valor);
            var conferidoEm = inicioMes.AddDays(1).ToDateTime(new TimeOnly(14, 10));
            var saida = new LancamentoFinanceiro
            {
                Data = inicioMes.AddDays(1), Tipo = TipoLancamento.Saida, Valor = total,
                Descricao = $"Repasse {prof.Rotulo} — competência {mesPassado:MM/yyyy}",
                Status = pago ? StatusLancamento.Realizado : StatusLancamento.Previsto,
                DataPagamento = pago ? NoMes(5) : null, DataVencimento = NoMes(10),
                FormaPagamento = pago ? FormaPagamento.Pix : null, CategoriaFinanceiraId = categoriaFolha, CriadoPor = "Prévia"
            };
            return db.Add(new RepasseFechamento
            {
                ProfissionalId = prof.Id, Competencia = mesPassado, RecebidosAte = inicioMes.AddDays(-1),
                ProfissionalNome = prof.Nome, ProfissionalRegistro = prof.RegistroConselho,
                Quantidade = itens.Count, Valor = total, ConferidoPor = "Marta Ribeiro (financeiro)", ConferidoEm = conferidoEm,
                Lancamento = saida, Itens = itens,
                Observacoes = pago ? "Conferido com o demonstrativo da Amil." : null
            }).Entity;
        }
        Fechamento(helena, [(amil[0], 3), (amil[1], 10), (amil[2], 17)], 65m, "GRD 004102", pago: true);
        Fechamento(marcos, [(petrobras[0], 12), (petrobras[1], 19)], 80m, "Conta 87650", pago: false);

        // O marco da virada (exigido para fechar): a clínica fictícia passou a fechar pelo
        // sistema no mês passado — o que foi recebido antes já tinha sido repassado à mão.
        db.Add(new ConfiguracaoGlobal
        {
            Chave = Clinica.Application.Servicos.FechamentoRepasseService.ChaveRecebidosDesde,
            Valor = mesPassado.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        });
        db.SaveChanges();
    }

    private static readonly System.Globalization.CultureInfo PtBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    private static string NomeDoConvenio(Paciente p) => Clinica.Domain.Regras.CatalogoConvenios.Nome(p.ConvenioCodigo, p.Convenio);
}
