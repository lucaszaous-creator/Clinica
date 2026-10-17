// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A central de Documentos da Recepção: papéis do mês com um de cada caso — receita e atestado
/// válidos, declaração de comparecimento cancelada com motivo, termo com link no ar, pedido de
/// exame com link vencido, recibo e orçamento (as folhas financeiras, que têm outro PDF e outro
/// cancelamento).
/// </summary>
public sealed class SementeDocumentosDaRecepcao : ISemente
{
    public int Ordem => 90;

    public void Semear(ClinicaDbContext db)
    {
        var helena = db.Profissionais.First(p => p.Nome == "Dra. Helena Duarte");
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Paciente P(string nome, string doc, string tel)
        {
            var p = new Paciente { Nome = nome, Documento = doc, Telefone = tel, Convenio = Convenio.UnimedPadrao, DataNascimento = new DateOnly(1978, 2, 14) };
            db.Add(p);
            db.SaveChanges();
            return p;
        }

        var lucia = P("Lúcia Ferraz Andrade", "11122233396", "(21) 98765-1201");
        var otavio = P("Otávio Rangel Prado", "22233344405", "(21) 99654-3302");
        var marina = P("Marina Sales Coutinho", "33344455514", "(21) 97543-2203");

        var n = 900;
        DocumentoClinico Doc(Paciente p, TipoDocumentoClinico tipo, int diasAtras, string titulo)
        {
            n++;
            var d = new DocumentoClinico
            {
                Numero = $"{hoje.Year}/{n:0000}", CodigoVerificacao = $"PRV{n:000}", Tipo = tipo,
                PacienteId = p.Id, ProfissionalId = helena.Id, Data = hoje.AddDays(-diasAtras), Titulo = titulo,
                Corpo = "Texto fictício da prévia."
            };
            db.Add(d);
            return d;
        }

        Doc(lucia, TipoDocumentoClinico.Receita, 0, "Receituário");
        Doc(otavio, TipoDocumentoClinico.Atestado, 1, "Atestado");
        var cancelada = Doc(marina, TipoDocumentoClinico.Comparecimento, 3, "Declaração de comparecimento");
        cancelada.CanceladoEm = DateTime.Now.AddDays(-2);
        cancelada.MotivoCancelamento = "Emitida com o horário errado — saiu outra.";
        var termo = Doc(lucia, TipoDocumentoClinico.TermoProcedimento, 5, "Termo de consentimento — BSV");
        termo.TokenPublicacao = "previa-termo-0001";
        termo.PublicadoAte = hoje.AddDays(20);
        var exame = Doc(otavio, TipoDocumentoClinico.PedidoExame, 12, "Solicitação de exames");
        exame.TokenPublicacao = "previa-exame-0002";
        exame.PublicadoAte = hoje.AddDays(-2);

        db.Add(new DocumentoFinanceiro
        {
            Numero = $"REC {hoje.Year}/0901", CodigoVerificacao = "PRVREC01", Tipo = TipoDocumentoFinanceiro.Recibo,
            PacienteId = marina.Id, Destinatario = marina.Nome, Data = hoje.AddDays(-1), Titulo = "Recibo",
            CriadoPor = "recepcao", Itens = { new ItemDocumentoFinanceiro { Ordem = 1, Descricao = "Sessão de acupuntura", ValorUnitario = 180m } }
        });
        db.Add(new DocumentoFinanceiro
        {
            Numero = $"ORC {hoje.Year}/0901", CodigoVerificacao = "PRVORC01", Tipo = TipoDocumentoFinanceiro.Orcamento,
            PacienteId = otavio.Id, Destinatario = otavio.Nome, Data = hoje.AddDays(-4), ValidoAte = hoje.AddDays(26), Titulo = "Orçamento",
            CriadoPor = "recepcao", Itens = { new ItemDocumentoFinanceiro { Ordem = 1, Descricao = "Pacote de 10 sessões", ValorUnitario = 1500m } }
        });
        db.SaveChanges();
    }
}
