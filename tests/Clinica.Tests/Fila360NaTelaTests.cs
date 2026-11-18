using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Xunit;
using static Clinica.Tests.WhatsAppDeTeste;

namespace Clinica.Tests;

/// <summary>A Fila 360 (nov/2026): o risco explicável e a aba Hoje — por onde a gerente começa o dia.</summary>
public sealed class Fila360NaTelaTests
{
    [Fact]
    public void O_risco_soma_os_sinais_e_cada_ponto_tem_o_seu_motivo()
    {
        var l = Caso(dias: 99, totalSessoes: 33, tentativas: 2, atrasado: true) with { PacoteComSaldo = true, Faltou = true };

        var r = Fila360NaTela.Risco(l);

        Assert.Equal(25 + 15 + 15 + 15 + 10 + 10, r.Pontos);
        Assert.Equal(FaixaDeRisco.Alto, r.Faixa);
        Assert.Equal(("Alto", "perigo"), (r.Rotulo, r.Tom));
        Assert.Equal("Sem vir há 99 dias", r.Principal);
        Assert.Equal(r.Pontos, r.Motivos.Sum(m => m.Pontos));
        Assert.Contains(r.Motivos, m => m.Texto == "Tratamento longo (33 sessões)" && m.Pontos == 15);
        Assert.Contains(r.Motivos, m => m.Texto == "2 tentativas sem resposta");
    }

    [Theory]
    [InlineData(20, 0, FaixaDeRisco.Baixo)]
    [InlineData(32, 10, FaixaDeRisco.Baixo)]
    [InlineData(60, 20, FaixaDeRisco.Baixo)]
    [InlineData(90, 25, FaixaDeRisco.Baixo)]
    [InlineData(150, 30, FaixaDeRisco.Baixo)]
    public void Os_dias_sem_vir_pesam_em_degraus(int dias, int pontos, FaixaDeRisco faixa)
    {
        var r = Fila360NaTela.Risco(Caso(dias: dias, totalSessoes: 1));
        Assert.Equal((pontos, faixa), (r.Pontos, r.Faixa));
    }

    [Fact]
    public void Quem_quer_marcar_ou_ja_marcou_tem_o_risco_reduzido_e_nunca_negativo()
    {
        var querMarcar = Caso(dias: 32, totalSessoes: 1) with { Etapa = EtapaAcompanhamento.ProntoParaAgendar };
        var marcado = Caso(dias: 32, totalSessoes: 1) with { AgendadoPara = new DateTime(2026, 10, 28, 9, 0, 0) };

        Assert.Equal(0, Fila360NaTela.Risco(querMarcar).Pontos);
        Assert.Equal(0, Fila360NaTela.Risco(marcado).Pontos);
        Assert.Equal("Disse que quer marcar", Fila360NaTela.Risco(querMarcar).Motivos.Last().Texto);
    }

    [Fact]
    public void Indicacao_de_BSV_pesa_pelos_dias_sem_a_primeira_sessao_e_caso_fechado_e_zero()
    {
        var bsv = Caso(tipo: TipoAcompanhamento.NovoBsv, dias: 15);
        Assert.Equal("Indicado há 15 dias sem a 1ª sessão", Fila360NaTela.Risco(bsv).Principal);
        Assert.Equal(25, Fila360NaTela.Risco(bsv).Pontos);
        Assert.Equal("Indicado há 1 dia sem a 1ª sessão", Fila360NaTela.Risco(bsv with { Dias = 1 }).Principal);

        var fechado = Fila360NaTela.Risco(Caso(dias: 200, pendente: false));
        Assert.Equal((0, FaixaDeRisco.Baixo), (fechado.Pontos, fechado.Faixa));
        Assert.Empty(fechado.Motivos);
    }

    [Fact]
    public void Hoje_agrupa_pela_acao_do_maior_risco_para_o_menor_e_deixa_de_fora_o_que_espera()
    {
        var baixo = Caso(1, nome: "Baixo Risco", dias: 31, totalSessoes: 1);
        var alto = Caso(2, nome: "Alto Risco", dias: 130, totalSessoes: 20) with { PacoteComSaldo = true };
        var semDono = Caso(3, nome: "Sem Dono") with { ResponsavelId = null, Responsavel = "" };
        var semAutorizacao = Caso(4, nome: "Sem Autorização", consentimento: false);
        var querMarcar = Caso(5, nome: "Quer Marcar") with { Etapa = EtapaAcompanhamento.ProntoParaAgendar };
        var depois = Caso(6, nome: "Depois", proximo: Hoje.AddDays(3));
        var fechado = Caso(7, nome: "Fechado", pendente: false);

        var grupos = Fila360NaTela.Hoje([baixo, alto, semDono, semAutorizacao, querMarcar, depois, fechado], Hoje);

        Assert.Equal([GrupoDeHoje.Pegar, GrupoDeHoje.Mensagem, GrupoDeHoje.Marcar, GrupoDeHoje.Balcao], grupos.Select(g => g.Grupo));
        Assert.Equal(["Alto Risco", "Baixo Risco"], grupos.Single(g => g.Grupo == GrupoDeHoje.Mensagem).Linhas.Select(l => l.Paciente));
        Assert.DoesNotContain(grupos.SelectMany(g => g.Linhas), l => l.Paciente is "Depois" or "Fechado");
        Assert.Equal("Pedir autorização no balcão", grupos.Last().Titulo);
        Assert.Equal("5 pacientes pedem ação hoje · 1 com risco alto.", Fila360NaTela.Resumo(grupos));
    }

    [Fact]
    public void Quem_respondeu_vem_antes_de_tudo_mesmo_com_o_proximo_contato_mais_adiante()
    {
        var respondeu = Caso(1, nome: "Respondeu", proximo: Hoje.AddDays(5), tentativas: 1, ultimoContato: new DateTime(2026, 10, 22, 15, 0, 0));
        var semDono = Caso(2, nome: "Sem Dono") with { ResponsavelId = null };

        var grupos = Fila360NaTela.Hoje([respondeu, semDono], Hoje, new HashSet<int> { 1 });

        Assert.Equal(GrupoDeHoje.Responder, grupos[0].Grupo);
        Assert.Equal("Respondeu", grupos[0].Linhas.Single().Paciente);
    }

    [Fact]
    public void Quem_respondeu_e_a_mesma_regra_da_conversa_do_WhatsApp()
    {
        var contatado = Caso(1, paciente: 101, tentativas: 1, ultimoContato: new DateTime(2026, 10, 22, 15, 0, 0));
        var semContato = Caso(2, paciente: 102);
        var antigo = Caso(3, paciente: 103, tentativas: 1, ultimoContato: new DateTime(2026, 10, 23, 9, 0, 0));
        var ultimas = new Dictionary<int, DateTimeOffset>
        {
            [101] = Em(23, 8, 30),   // escreveu depois do contato de 22/10
            [102] = Em(23, 8, 30),   // nunca foi contatado: não é "respondeu"
            [103] = Em(22, 18, 0)    // escreveu ANTES do último contato
        };

        Assert.Equal(new HashSet<int> { 1 }, Fila360NaTela.QueResponderam([contatado, semContato, antigo], ultimas, Agora));
    }

    [Fact]
    public void Sem_nada_para_hoje_a_frase_diz_isso()
        => Assert.Equal("Nada pede ação hoje.", Fila360NaTela.Resumo(Fila360NaTela.Hoje([Caso(proximo: Hoje.AddDays(2))], Hoje)));

    private static LinhaAcompanhamento Caso(int id = 1, int paciente = 101, string nome = "Sebastião Ramos",
        TipoAcompanhamento tipo = TipoAcompanhamento.Recall, int dias = 32, bool pendente = true, bool consentimento = true,
        DateOnly? proximo = null, int tentativas = 0, DateTime? ultimoContato = null, int totalSessoes = 6, bool atrasado = false)
        => WhatsAppDeTeste.Caso(id, paciente, nome, tipo: tipo, dias: dias, pendente: pendente, consentimento: consentimento,
            proximo: proximo, tentativas: tentativas, ultimoContato: ultimoContato, totalSessoes: totalSessoes, atrasado: atrasado);
}
