// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A TRAVA DE CONSULTA DO CONVÊNIO (nov/2026). Liga a chave, marca uma gerente como quem aprova
/// (com o WhatsApp do sistema), põe uma paciente que consultou pelo convênio há três dias — é
/// com ela que a box da trava acende nas telas de marcar — e um profissional que NÃO atende o
/// convênio dela (a pré-marcação de "Particular") com preço próprio na tabela do particular.
/// </summary>
public sealed class SementeTravaDeConsulta : ISemente
{
    public int Ordem => 97;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateTime.Today;
        var helena = db.Set<Profissional>().FirstOrDefault(p => p.NomeCurto == "Dra. Helena");
        var marcos = db.Set<Profissional>().FirstOrDefault(p => p.NomeCurto == "Dr. Marcos");
        if (helena is null || marcos is null) return;

        if (db.Configuracoes.Find(ParametrosService.ChaveTravaDeConsulta) is null)
            db.Configuracoes.Add(new ConfiguracaoGlobal { Chave = ParametrosService.ChaveTravaDeConsulta, Valor = "1" });

        // Quem aprova: uma gerente que já existe na prévia, ou uma própria.
        var gerente = db.Set<UsuarioSistema>().FirstOrDefault(u => u.Perfil == PerfilAcesso.Gerente && u.Ativo);
        if (gerente is null)
        {
            gerente = new UsuarioSistema { Nome = "Glória Pires Sodré", Login = "gloria.previa", Perfil = PerfilAcesso.Gerente, Ativo = true };
            db.Add(gerente);
        }
        gerente.AprovaLiberacaoDeTrava = true;
        gerente.TelefoneWhatsApp ??= "5521982220099";
        gerente.WhatsAppLigadoEm ??= hoje.AddDays(-30);

        // A paciente que consultou há três dias (convênio padrão da prévia: a trava de 22 dias).
        var sueli = new Paciente { Nome = "Sueli Brandão Teles", Telefone = "(21) 98222-0006", DataNascimento = new DateOnly(1968, 9, 2) };
        db.Add(sueli);
        db.SaveChanges();
        db.Add(new Agendamento
        {
            PacienteId = sueli.Id, ProfissionalId = helena.Id, DataHora = hoje.AddDays(-3).AddHours(10),
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            EspecialidadeConsultaCodigo = nameof(Especialidade.ClinicaDaDor), Status = StatusAgendamento.Realizado,
            CriadoPor = "recepcao", CriadoEm = hoje.AddDays(-10).AddHours(9)
        });

        // O Dr. Marcos não atende pelo convênio dela: marcar com ele já propõe "Particular" — pelo preço DELE.
        marcos.ConveniosNaoAtendidos = sueli.ConvenioCodigo ?? sueli.Convenio.ToString();
        db.Add(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta), ProfissionalId = marcos.Id, Valor = 450m,
            VigenteDe = DateOnly.FromDateTime(hoje).AddMonths(-3)
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
