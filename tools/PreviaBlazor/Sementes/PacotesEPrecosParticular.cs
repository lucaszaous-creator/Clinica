// Dados FICTÍCIOS da prévia — nenhuma pessoa real, nenhum preço de tabela real.
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// PACOTES e PREÇOS DO PARTICULAR (out/2026 — as duas telas saíram do WPF). Dá às telas o que
/// elas existem para mostrar:
/// - o CATÁLOGO com os três tipos (sessões, plano livre, voucher de cortesia) e um pacote tirado
///   da venda;
/// - pacotes VENDIDOS em cada situação: ativo pago à vista, ativo a prazo com PARCELA VENCIDA,
///   esgotado, vencido e cancelado — com sessões debitadas e uma DEVOLVIDA ao saldo;
/// - a tabela do particular com o genérico, um preço por ESPECIALIDADE (vence o genérico), um
///   reajuste encerrado, um reajuste futuro e um desligado.
/// A venda passa pelo MESMO PacoteService da tela: é ele que grava os lançamentos da decisão de
/// pagamento. Sem Id fixo.
/// </summary>
public sealed class SementePacotesEPrecosParticular : ISemente
{
    public int Ordem => 98;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var pacotes = new PacoteService(new ClinicaRepositorio(db));

        PacoteCatalogo Catalogo(string nome, TipoPacote tipo, int? sessoes, decimal valor, int? dias, int ordem, bool ativo = true)
            => pacotes.SalvarCatalogoAsync(new PacoteCatalogo
            {
                Nome = nome, Tipo = tipo, SessoesIncluidas = sessoes, Valor = valor, ValidadeDias = dias,
                Ordem = ordem, Ativo = ativo, Observacoes = "Cadastro da prévia"
            }, "Prévia").GetAwaiter().GetResult();

        var dez = Catalogo("10 sessões de acupuntura", TipoPacote.Sessoes, 10, 1500m, 180, 1);
        var cinco = Catalogo("5 sessões de acupuntura", TipoPacote.Sessoes, 5, 800m, 90, 2);
        var mensal = Catalogo("Plano mensal livre", TipoPacote.Plano, null, 380m, 30, 3);
        Catalogo("Voucher de cortesia — 1 sessão", TipoPacote.Voucher, 1, 0m, 60, 4);
        Catalogo("Pacote de inverno (antigo)", TipoPacote.Sessoes, 8, 1100m, 120, 5, ativo: false);

        Paciente P(string nome, string tel)
        {
            var p = new Paciente { Nome = nome, Telefone = tel, DataNascimento = new DateOnly(1970, 5, 12) };
            db.Add(p);
            db.SaveChanges();
            return p;
        }

        var marina = P("Marina Pacote Teixeira", "(21) 98100-7701");
        var otavio = P("Otávio Pacote Ramalho", "(21) 98100-7702");
        var celia = P("Célia Pacote Andrade", "(21) 98100-7703");
        var bruno = P("Bruno Pacote Siqueira", "(21) 98100-7704");
        var renata = P("Renata Pacote Lins", "(21) 98100-7705");

        // Ativo, pago à vista no Pix, com três sessões usadas e uma devolvida ao saldo.
        var ativo = pacotes.VenderAsync(marina.Id, dez.Id, hoje.AddDays(-40), null, null, "Prévia",
            PagamentoDaVenda.AVista(FormaPagamento.Pix)).GetAwaiter().GetResult();
        for (var i = 0; i < 3; i++)
            pacotes.ConsumirAsync(ativo.Id, hoje.AddDays(-30 + i * 7), observacao: "Baixa manual pela tela de Pacotes", operador: "recepcao")
                .GetAwaiter().GetResult();
        var errada = pacotes.ConsumirAsync(ativo.Id, hoje.AddDays(-5), operador: "recepcao").GetAwaiter().GetResult();
        pacotes.CancelarConsumoAsync(errada.Id, "o paciente não veio — a baixa pegou o pacote errado", "financeiro")
            .GetAwaiter().GetResult();

        // Ativo, a prazo: entrada hoje há 60 dias + 3 parcelas, a primeira já VENCIDA.
        pacotes.VenderAsync(otavio.Id, dez.Id, hoje.AddDays(-60), 1350m, "Desconto de fidelidade", "Prévia",
            PagamentoDaVenda.APrazo(3, hoje.AddDays(-30), 300m, FormaPagamento.Dinheiro)).GetAwaiter().GetResult();

        // Esgotado: as cinco sessões usadas.
        var esgotado = pacotes.VenderAsync(celia.Id, cinco.Id, hoje.AddDays(-80), null, null, "Prévia",
            PagamentoDaVenda.AVista(FormaPagamento.Dinheiro)).GetAwaiter().GetResult();
        for (var i = 0; i < 5; i++)
            pacotes.ConsumirAsync(esgotado.Id, hoje.AddDays(-70 + i * 10), operador: "recepcao").GetAwaiter().GetResult();

        // Vencido: o plano mensal comprado há dois meses.
        pacotes.VenderAsync(bruno.Id, mensal.Id, hoje.AddDays(-65), null, null, "Prévia",
            PagamentoDaVenda.AVista(FormaPagamento.Pix)).GetAwaiter().GetResult();

        // Cancelado, com motivo — continua na lista.
        var cancelado = pacotes.VenderAsync(renata.Id, cinco.Id, hoje.AddDays(-20), null, null, "Prévia",
            PagamentoDaVenda.APrazo(2, hoje.AddDays(10))).GetAwaiter().GetResult();
        pacotes.CancelarAsync(cancelado.Id, "a paciente mudou de cidade", "financeiro").GetAwaiter().GetResult();

        // ---- Preços do particular (a semente do Lançar atendimento já pode ter gravado os genéricos).
        if (!db.Set<PrecoParticular>().Any(p => p.ModalidadeCodigo == nameof(ModalidadeAtendimento.AcupunturaSimples)))
            db.Add(new PrecoParticular { ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples), Valor = 150m, VigenteDe = hoje.AddMonths(-6) });
        db.Add(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta), Valor = 320m, VigenteDe = hoje.AddMonths(-3),
            Observacoes = "Consulta avulsa — qualquer especialidade."
        });
        db.Add(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta), EspecialidadeCodigo = nameof(Especialidade.Psiquiatria),
            Valor = 450m, VigenteDe = hoje.AddMonths(-3), Observacoes = "A psiquiatria vence o genérico da consulta."
        });
        // O reajuste: o valor antigo encerrado e o novo, já negociado, que ainda não vale.
        db.Add(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.BsvComAcupuntura), Valor = 520m,
            VigenteDe = hoje.AddYears(-1), VigenteAte = hoje.AddDays(-1)
        });
        db.Add(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.BsvComAcupuntura), Valor = 560m, VigenteDe = hoje.AddMonths(1)
        });
        db.Add(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.BsvApenas), Valor = 410m, Ativo = false
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
