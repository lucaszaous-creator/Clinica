// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// Documentos emitidos (a leitura AGREGADA da direção, etapa 6 do Gerente). A central da Recepção
/// (<see cref="SementeDocumentosDaRecepcao"/>) já deixa sete papéis do mês, todos de UMA
/// profissional e nenhum selado. Esta acrescenta o que a tela da direção existe para mostrar:
/// - papéis SELADOS com e-CPF (o cartão "Assinados c/ e-CPF" e o selo da linha);
/// - outros DOIS profissionais e um papel do BALCÃO (sem profissional) — é o que dá barras
///   diferentes em "Por quem assina";
/// - mais duas CANCELADAS, uma com motivo longo (a lista com teto e rolagem) e uma de outro
///   profissional (o contexto "quem · quando");
/// - um link NO AR selado (o cartão "Publicados no ar").
/// Sem Id fixo: profissionais e pacientes se acham pelo que as outras sementes gravaram. O
/// código de conferência "PRVDE01" serve para testar o "Conferir" na prévia.
/// </summary>
public sealed class SementeDocumentosEmitidos : ISemente
{
    public int Ordem => 96;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var profissionais = db.Profissionais.OrderBy(p => p.Id).Take(3).ToList();
        var pacientes = db.Set<Paciente>().OrderBy(p => p.Id).Select(p => p.Id).Take(6).ToList();
        if (profissionais.Count < 3 || pacientes.Count < 6) return;

        var n = 950;
        DocumentoClinico Doc(int paciente, Profissional? quem, TipoDocumentoClinico tipo, int diaDoMes, string titulo, bool selado = false)
        {
            n++;
            // Dentro do mês corrente — o período padrão da tela é "Este mês".
            var data = inicioDoMes.AddDays(Math.Min(diaDoMes, hoje.Day - 1));
            var d = new DocumentoClinico
            {
                Numero = $"{hoje.Year}/{n:0000}", CodigoVerificacao = $"PRVDE{n - 950:00}", Tipo = tipo,
                PacienteId = paciente, ProfissionalId = quem?.Id, Data = data, Titulo = titulo,
                Corpo = "Texto fictício da prévia.", CriadoPor = quem is null ? "recepcao" : "consultorio"
            };
            if (selado)
            {
                d.AssinaturaTipo = TipoAssinatura.IcpBrasil;
                d.AssinanteNome = quem?.Nome;
                d.AssinadoEm = data.ToDateTime(new TimeOnly(10, 30));
            }
            db.Add(d);
            return d;
        }

        var (helena, marcos, bianca) = (profissionais[0], profissionais[1], profissionais[2]);

        var receitaNoAr = Doc(pacientes[0], marcos, TipoDocumentoClinico.Receita, 1, "Receituário", selado: true);
        receitaNoAr.TokenPublicacao = "previa-de-0001";
        receitaNoAr.PublicadoAte = hoje.AddDays(25);
        Doc(pacientes[1], marcos, TipoDocumentoClinico.Receita, 2, "Receituário", selado: true);
        Doc(pacientes[2], marcos, TipoDocumentoClinico.Atestado, 4, "Atestado", selado: true);
        Doc(pacientes[3], helena, TipoDocumentoClinico.RelatorioEvolucao, 6, "Relatório de evolução", selado: true);
        Doc(pacientes[4], bianca, TipoDocumentoClinico.PedidoExame, 7, "Solicitação de exames");
        Doc(pacientes[5], null, TipoDocumentoClinico.Comparecimento, 8, "Declaração de comparecimento");

        var canceladaLonga = Doc(pacientes[2], marcos, TipoDocumentoClinico.Receita, 3, "Receituário");
        canceladaLonga.CanceladoEm = DateTime.Now.AddDays(-1);
        canceladaLonga.MotivoCancelamento =
            "A posologia saiu com a dose da apresentação errada (500 mg no lugar de 250 mg); emitida outra receita no mesmo dia, já selada.";
        var canceladaBianca = Doc(pacientes[4], bianca, TipoDocumentoClinico.PedidoExame, 5, "Solicitação de exames");
        canceladaBianca.CanceladoEm = DateTime.Now.AddDays(-3);
        canceladaBianca.MotivoCancelamento = "Paciente já tinha o exame recente.";

        db.SaveChanges();
    }
}
