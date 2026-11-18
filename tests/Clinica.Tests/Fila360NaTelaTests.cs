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

    [Fact]
    public void A_jornada_mostra_realizadas_faltas_e_o_que_esta_marcado_e_deixa_de_fora_o_resto()
    {
        var agora = new DateTime(2026, 10, 24, 10, 12, 0);
        var marcas = Fila360NaTela.Jornada([
            new(new DateTime(2026, 8, 10, 9, 0, 0), StatusAgendamento.Realizado),
            new(new DateTime(2026, 7, 17, 9, 0, 0), StatusAgendamento.Faltou),
            new(new DateTime(2026, 7, 20, 9, 0, 0), StatusAgendamento.Cancelado),
            new(new DateTime(2026, 9, 1, 9, 0, 0), StatusAgendamento.Substituido),
            new(new DateTime(2026, 10, 1, 9, 0, 0), StatusAgendamento.Agendado),   // passou sem baixa: é "conferir"
            new(new DateTime(2026, 10, 28, 9, 0, 0), StatusAgendamento.Agendado)], agora);

        Assert.Equal([TipoNaJornada.Falta, TipoNaJornada.Realizada, TipoNaJornada.Marcada], marcas.Select(m => m.Tipo));
        Assert.Equal("1 sessão realizada · 1 falta · 1 horário marcado.", Fila360NaTela.ResumoDaJornada(marcas));
        Assert.Equal("Nenhum horário nos últimos meses.", Fila360NaTela.ResumoDaJornada([]));
    }

    [Fact]
    public void Voltaram_por_mes_conta_cada_mes_e_mostra_o_mes_vazio_com_zero()
    {
        var linhas = new[]
        {
            Caso(1, pendente: false) with { VoltouEm = new DateOnly(2026, 10, 3) },
            Caso(2, pendente: false) with { VoltouEm = new DateOnly(2026, 10, 20) },
            Caso(3, pendente: false) with { VoltouEm = new DateOnly(2026, 8, 2) },
            Caso(4, pendente: false) with { VoltouEm = new DateOnly(2026, 3, 2) },   // fora da janela de 6 meses
            Caso(5)
        };

        var meses = Fila360NaTela.VoltaramPorMes(linhas, Hoje);

        Assert.Equal(6, meses.Count);
        Assert.Equal(new DateOnly(2026, 5, 1), meses[0].Mes);
        Assert.Equal([0, 0, 0, 1, 0, 2], meses.Select(m => m.Voltaram));
    }

    [Fact]
    public void As_conversas_da_fila_sao_as_da_ficha_de_cada_caso_aberto_com_quem_respondeu_primeiro()
    {
        var contato = new DateTime(2026, 10, 22, 15, 0, 0);
        var respondeu = Caso(1, paciente: 101, nome: "Respondeu", tentativas: 1, ultimoContato: contato);
        var esperando = Caso(2, paciente: 102, nome: "Esperando", tentativas: 1, ultimoContato: contato);
        var semConversa = Caso(3, paciente: 103, nome: "Sem conversa");
        var fechado = Caso(4, paciente: 104, nome: "Fechado", pendente: false);
        var bsvDoMesmo = Caso(5, paciente: 101, nome: "Respondeu", tipo: TipoAcompanhamento.NovoBsv, dias: 1);
        var conversas = new[]
        {
            Resumo("a", paciente: 101, atualizada: Em(23, 9, 0), ultimaRecebida: Em(23, 9, 0)),
            Resumo("a-velha", paciente: 101, atualizada: Em(20, 9, 0), ultimaRecebida: Em(20, 9, 0)),
            Resumo("b", paciente: 102, atualizada: Em(24, 8, 0), ultimaRecebida: Em(21, 9, 0)),
            Resumo("d", paciente: 104, atualizada: Em(24, 9, 0)),
            Resumo("so-telefone", paciente: null, atualizada: Em(24, 10, 0))
        };

        var fila = Fila360NaTela.ConversasDaFila([respondeu, esperando, semConversa, fechado, bsvDoMesmo], conversas, Agora);

        Assert.Equal(["a", "b"], fila.Select(c => c.Conversa.Id));
        Assert.True(fila[0].Respondeu); Assert.False(fila[1].Respondeu);
        Assert.Equal(1, fila[0].Linha.Id); // o caso de maior risco do paciente que tem dois
    }

    [Fact]
    public void O_progresso_do_dia_conta_o_que_ja_foi_feito_hoje_sobre_o_total()
    {
        var feito = Caso(1, proximo: Hoje.AddDays(2), tentativas: 1, ultimoContato: new DateTime(2026, 10, 24, 9, 0, 0));
        var pendente = Caso(2);
        var ontem = Caso(3, proximo: Hoje.AddDays(2), tentativas: 1, ultimoContato: new DateTime(2026, 10, 23, 9, 0, 0));
        var linhas = new[] { feito, pendente, ontem };

        var grupos = Fila360NaTela.Hoje(linhas, Hoje);

        Assert.Equal((1, 2), Fila360NaTela.ProgressoDeHoje(linhas, grupos, Hoje));
        Assert.Equal(1, Fila360NaTela.FeitosHoje(linhas, Hoje));
    }

    [Fact]
    public void Os_numeros_do_painel_juntam_as_duas_filas()
    {
        var alto = Caso(1, dias: 130, totalSessoes: 20) with { PacoteComSaldo = true };
        var medio = Caso(2, dias: 60);
        var bsv = Caso(3, tipo: TipoAcompanhamento.NovoBsv, dias: 2);
        var voltou = Caso(4, pendente: false, tentativas: 1, ultimoContato: new DateTime(2026, 10, 10, 9, 0, 0)) with { VoltouEm = new DateOnly(2026, 10, 20) };
        var outro = Caso(5, dias: 60) with { ResponsavelId = 99, Responsavel = "Outra Pessoa" };
        var linhas = new[] { alto, medio, bsv, voltou, outro };

        // Os grupos de Hoje vêm do MESMO recorte (minha fila): "outro" é de outra pessoa e não entra em nada.
        var minha = linhas.Where(l => AcompanhamentoNaTela.DaMinhaFila(l, 7)).ToList();
        var n = Fila360NaTela.Numeros(linhas, Fila360NaTela.Hoje(minha, Hoje), soMinhaFila: true, usuarioId: 7, Hoje);

        Assert.Equal((3, 1, 1, 3), (n.Abertos, n.RiscoAlto, n.RiscoMedio, n.ParaHoje));
        Assert.Equal((1, 1, 1, 100), (n.Voltaram, n.Contatados, n.VoltaramDosContatados, n.TaxaDeRetorno));
        Assert.Null(Fila360NaTela.Numeros([medio], [], false, 7, Hoje).TaxaDeRetorno);
    }

    [Fact]
    public void A_equipe_soma_as_duas_filas_por_responsavel_e_os_motivos()
    {
        var linhas = new[]
        {
            Caso(1), Caso(2, tipo: TipoAcompanhamento.NovoBsv),
            Caso(3, pendente: false, motivoEncerramento: "mudou", motivo: "Mudou de cidade"),
            Caso(4, pendente: false, tipo: TipoAcompanhamento.NovoBsv, motivoEncerramento: "mudou", motivo: "Mudou de cidade")
        };

        var (responsaveis, motivos) = Fila360NaTela.Equipe(linhas, Hoje);

        Assert.Equal(("Ana Paula", 2), (responsaveis.Single().Responsavel, responsaveis.Single().Pendentes));
        Assert.Equal(("Mudou de cidade", 2), (motivos.Single().Motivo, motivos.Single().Quantidade));
    }

    [Fact]
    public void A_carteira_junta_as_filas_ordena_pelo_risco_e_recorta_pela_visao()
    {
        var alto = Caso(1, nome: "Alto", dias: 130, totalSessoes: 20) with { PacoteComSaldo = true };
        var baixo = Caso(2, nome: "Baixo", dias: 31, totalSessoes: 1);
        var bsv = Caso(3, nome: "Bsv", tipo: TipoAcompanhamento.NovoBsv, dias: 20);
        var semAut = Caso(4, nome: "Sem Aut", consentimento: false, dias: 31, totalSessoes: 1);
        var fechado = Caso(5, nome: "Fechado", pendente: false, motivoEncerramento: "x", motivo: "Outro");
        var linhas = new[] { baixo, bsv, alto, semAut, fechado };
        var filtro = new FiltroAcompanhamento(TipoAcompanhamento.Recall, false);
        IEnumerable<string> Nomes(VisaoDaCarteira v) => Fila360NaTela.Carteira(linhas, filtro, v, 7, Hoje).Select(l => l.Paciente);

        Assert.Equal(["Alto", "Bsv", "Baixo", "Sem Aut"], Nomes(VisaoDaCarteira.Todos));
        Assert.Equal(["Alto"], Nomes(VisaoDaCarteira.RiscoAlto));
        Assert.Equal(["Bsv"], Nomes(VisaoDaCarteira.NovosBsv));
        Assert.Equal(["Sem Aut"], Nomes(VisaoDaCarteira.SemAutorizacao));
        Assert.Equal(["Fechado"], Nomes(VisaoDaCarteira.Encerrados));
    }

    private static LinhaAcompanhamento Caso(int id = 1, int paciente = 101, string nome = "Sebastião Ramos",
        TipoAcompanhamento tipo = TipoAcompanhamento.Recall, int dias = 32, bool pendente = true, bool consentimento = true,
        DateOnly? proximo = null, int tentativas = 0, DateTime? ultimoContato = null, int totalSessoes = 6, bool atrasado = false,
        string? motivoEncerramento = null, string? motivo = null)
        => WhatsAppDeTeste.Caso(id, paciente, nome, tipo: tipo, dias: dias, pendente: pendente, consentimento: consentimento,
            proximo: proximo, tentativas: tentativas, ultimoContato: ultimoContato, totalSessoes: totalSessoes, atrasado: atrasado,
            motivoEncerramento: motivoEncerramento, motivo: motivo);
}
