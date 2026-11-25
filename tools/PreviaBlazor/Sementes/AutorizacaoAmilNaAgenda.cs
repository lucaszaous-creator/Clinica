// Dados FICTÍCIOS da prévia — nenhuma pessoa real, nenhuma carteira, senha ou token reais.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A AUTORIZAÇÃO PELO SISTEMA (nov/2026, Amil), para fotografar a aba Dia e a baixa. Liga a chave de
/// autorização da Amil SÓ no banco da prévia (no app ela nasce desligada) e põe na agenda de HOJE três
/// pacientes da Amil: um que chegou, autorizado e ainda sem token (o campo aparece na linha), um com o
/// token já digitado ("token ok") e — da semente da elegibilidade — um ainda sem pedido (o botão
/// "Pedir autorização"). A guia da Amil da semente da baixa ganha a autorização com o token: a baixa
/// abre com a senha, a data, a validade e o token travados.
/// </summary>
public sealed class SementeAutorizacaoAmilNaAgenda : ISemente
{
    public int Ordem => 103;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateTime.Today;
        var dia = DateOnly.FromDateTime(hoje);
        var tulio = db.Set<Profissional>().FirstOrDefault(p => p.Nome == "Dr. Túlio Prévia Lins");
        if (tulio is null) return;

        db.Configuracoes.Add(new ConfiguracaoGlobal { Chave = CredenciaisTissService.ChaveAutorizacao("Amil"), Valor = "true" });

        Paciente P(string nome, string carteira) => new()
        {
            Nome = nome, Convenio = Convenio.Amil, ConvenioCodigo = "Amil", Carteirinha = carteira,
            ValidadeCarteirinha = dia.AddDays(300), Telefone = "(21) 98222-0910", DataNascimento = new DateOnly(1969, 9, 21)
        };
        var gilda = P("Gilda Ferraz Montes", "0007770003");
        var heitor = P("Ivo Barreto Viana", "0007770004");
        db.AddRange(gilda, heitor);
        db.SaveChanges();

        Agendamento H(Paciente p, int hora) => new()
        {
            PacienteId = p.Id, ProfissionalId = tulio.Id, DataHora = hoje.AddHours(hora), DuracaoMinutos = 30,
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            CriadoPor = "recepcao", CriadoEm = hoje.AddDays(-4).AddHours(9), ChegadaEm = hoje.AddHours(hora).AddMinutes(-10)
        };
        var hGilda = H(gilda, 9);
        var hHeitor = H(heitor, 10);
        db.AddRange(hGilda, hHeitor);
        db.SaveChanges();

        AutorizacaoTiss A(int agendamentoId, int pacienteId, string senha, DateOnly data, string? token) => new()
        {
            AgendamentoId = agendamentoId, PacienteId = pacienteId, ConvenioCodigo = "Amil", Situacao = SituacaoAutorizacaoTiss.Autorizada,
            NumeroCarteira = "0007770000", CodigoProcedimento = "10101012", DescricaoProcedimento = "CONSULTA EM CONSULTORIO",
            ProfissionalNome = tulio.Nome, ConselhoProfissional = "06", NumeroConselho = "520001", UfConselho = "33", Cbos = "225105",
            NumeroGuiaPrestador = "A" + agendamentoId, NumeroGuiaOperadora = "PRV" + agendamentoId, Senha = senha,
            DataAutorizacao = data, ValidadeSenha = data.AddDays(60), PedidaPor = "recepcao", PedidaEm = data.ToDateTime(new TimeOnly(8, 50)),
            Token = token, TokenRegistradoPor = token is null ? null : "recepcao", TokenRegistradoEm = token is null ? null : hoje.AddHours(9).AddMinutes(52)
        };
        db.Add(A(hGilda.Id, gilda.Id, "PRV0000001", dia, null));
        db.Add(A(hHeitor.Id, heitor.Id, "PRV0000002", dia, "000013"));

        // A guia da Amil da semente da baixa: autorizada no dia do atendimento, com o token da recepção.
        var lucia = db.Set<Agendamento>().FirstOrDefault(a => a.Paciente!.Nome == "Lúcia Amaral Pinto");
        if (lucia is not null)
        {
            var data = DateOnly.FromDateTime(lucia.DataHora);
            db.Add(A(lucia.Id, lucia.PacienteId, "PRV0000003", data, "000027"));
        }
        db.SaveChanges();
    }
}
