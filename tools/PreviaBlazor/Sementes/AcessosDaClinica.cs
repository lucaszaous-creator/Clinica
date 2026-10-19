// Dados FICTÍCIOS da prévia — nenhuma pessoa real. Senhas não existem aqui: o hash é "-".
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A tela de Acessos do Gerente (etapa 6, out/2026). As outras sementes já criam alguns usuários
/// (Ana Lima, Bruno, Joana…), todos no padrão do perfil e sem nenhum acesso registrado; esta
/// acrescenta o que a tela existe para mostrar:
/// - alguém com permissões ajustadas À MÃO (liberada E tirada), para a procedência aparecer;
/// - um usuário que JÁ ENTROU (não se exclui — desativa) e um inativo;
/// - um vinculado a um profissional, para o perfil "Médico · especialidade" e o CPF de assinatura;
/// - uma senha provisória pendente de troca.
/// O primeiro da lista em ordem alfabética ("Aarão") nunca entrou e não é usado por nenhuma outra
/// semente: é nele que o teste das telas clica "Excluir" — excluir um usuário que outra semente
/// referencia quebraria a tela vizinha. Sem Id fixo: o profissional é achado pelo que já existe.
/// </summary>
public sealed class SementeAcessosDaClinica : ISemente
{
    public int Ordem => 97;

    public void Semear(ClinicaDbContext db)
    {
        if (db.Set<UsuarioSistema>().Any(u => u.Login == "aarao.previa")) return;
        var agora = DateTime.Now;

        var vinculados = db.Set<UsuarioSistema>().Where(u => u.ProfissionalId != null).Select(u => u.ProfissionalId!.Value).ToList();
        var medico = db.Set<Profissional>().Where(p => p.Ativo && !vinculados.Contains(p.Id))
            .OrderBy(p => p.Id).FirstOrDefault();

        db.AddRange(
            new UsuarioSistema
            {
                Nome = "Aarão Prates (fictício)", Login = "aarao.previa", Perfil = PerfilAcesso.Recepcao,
                SenhaHash = "-", SenhaSalt = "-", Ativo = true, DeveTrocarSenha = true
            },
            new UsuarioSistema
            {
                Nome = "Carla Menezes (fictícia)", Login = "carla.previa", Perfil = PerfilAcesso.Recepcao,
                // "A recepcionista também lança o caixa" — e não cadastra paciente: as duas exceções à mão.
                PermissoesExtras = Permissao.VerFinanceiro | Permissao.EditarFinanceiro,
                PermissoesNegadas = Permissao.EditarPaciente,
                SenhaHash = "-", SenhaSalt = "-", Ativo = true, UltimoAcessoEm = agora.Date.AddDays(-1).AddHours(9).AddMinutes(12)
            },
            new UsuarioSistema
            {
                Nome = "Marta Sousa (fictícia)", Login = "marta.previa", Perfil = PerfilAcesso.Faturista,
                SenhaHash = "-", SenhaSalt = "-", Ativo = false, UltimoAcessoEm = agora.Date.AddMonths(-4).AddHours(17).AddMinutes(40)
            },
            new UsuarioSistema
            {
                Nome = "Otávio Diretor (fictício)", Login = "otavio.previa", Perfil = PerfilAcesso.Gerente,
                SenhaHash = "-", SenhaSalt = "-", Ativo = true, UltimoAcessoEm = agora.Date.AddHours(7).AddMinutes(55)
            });

        if (medico is not null)
            db.Add(new UsuarioSistema
            {
                Nome = medico.Nome, Login = "medico.previa", Perfil = PerfilAcesso.Profissional, ProfissionalId = medico.Id,
                SenhaHash = "-", SenhaSalt = "-", Ativo = true, UltimoAcessoEm = agora.Date.AddHours(8).AddMinutes(3)
            });

        db.SaveChanges();
    }
}
