using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O desenho A do Relacionamento (out/2026): o que a fila afirma — o botão de cada linha, a
/// faixa do funil, o degrau do progresso e o que cada "Como foi?" grava.
/// </summary>
public sealed class AcompanhamentoNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 16); // sexta-feira

    private static LinhaAcompanhamento L(int id = 1, int? responsavel = 7, bool pendente = true, string? telefone = "21999990000",
        bool consentimento = true, EtapaAcompanhamento etapa = EtapaAcompanhamento.AContatar, int tentativas = 0,
        DateOnly? proximo = null, bool atrasado = false, string situacao = "A contatar", DateTime? agendado = null,
        DateOnly? voltou = null, DateTime? ultimoContato = null, string? motivoEncerramento = null,
        TipoAcompanhamento tipo = TipoAcompanhamento.Recall, string paciente = "Maria Souza", bool cancelou = false)
        => new(id, 10 + id, paciente, telefone, "Unimed", tipo, ModalidadeAtendimento.AcupunturaComEletro, "Dr. Gustavo", 3,
            responsavel, responsavel is null ? "A assumir" : "Ana", new DateTime(2026, 8, 2), 75, situacao, pendente, atrasado,
            proximo ?? Hoje, etapa, tentativas, ultimoContato, null, consentimento, false, agendado, cancelou, false, Guid.NewGuid(),
            motivoEncerramento, null, 0, voltou);

    [Fact]
    public void O_botao_da_linha_pergunta_o_dono_depois_o_canal_e_so_entao_a_etapa()
    {
        Assert.Equal(PassoAcompanhamento.Assumir, AcompanhamentoNaTela.Passo(L(responsavel: null, telefone: null)));
        Assert.Equal(PassoAcompanhamento.AtualizarFicha, AcompanhamentoNaTela.Passo(L(telefone: " ")));
        Assert.Equal(PassoAcompanhamento.ColherAutorizacao, AcompanhamentoNaTela.Passo(L(consentimento: false)));
        Assert.Equal(PassoAcompanhamento.Contatar, AcompanhamentoNaTela.Passo(L()));
        Assert.Equal(PassoAcompanhamento.TentarDeNovo, AcompanhamentoNaTela.Passo(L(tentativas: 2, etapa: EtapaAcompanhamento.SemResposta)));
        Assert.Equal(PassoAcompanhamento.TentarDeNovo, AcompanhamentoNaTela.Passo(L(cancelou: true)));
        Assert.Equal(PassoAcompanhamento.MarcarHorario, AcompanhamentoNaTela.Passo(L(etapa: EtapaAcompanhamento.ProntoParaAgendar)));
        Assert.Equal(PassoAcompanhamento.AcompanharPlano, AcompanhamentoNaTela.Passo(L(etapa: EtapaAcompanhamento.AguardandoPlano)));
        Assert.Equal(PassoAcompanhamento.ConferirAgenda, AcompanhamentoNaTela.Passo(L(situacao: "Conferir comparecimento")));
        Assert.Equal(PassoAcompanhamento.Ver, AcompanhamentoNaTela.Passo(L(pendente: false)));
    }

    [Fact]
    public void Nao_respondeu_numa_sexta_volta_na_terca_e_nao_no_domingo()
    {
        var (g, imp) = AcompanhamentoNaTela.Gravar(DesfechoContato.NaoRespondeu, Hoje, null);
        Assert.Null(imp);
        Assert.Equal(EtapaAcompanhamento.SemResposta, g!.Etapa);
        Assert.Equal(new DateOnly(2026, 10, 20), g.ProximoContato);
        Assert.False(g.Encerrar);
    }

    [Fact]
    public void Pediu_outra_data_sem_data_e_recusado_e_data_no_passado_tambem()
    {
        Assert.NotNull(AcompanhamentoNaTela.Gravar(DesfechoContato.PediuOutraData, Hoje, null).Impedimento);
        Assert.NotNull(AcompanhamentoNaTela.Gravar(DesfechoContato.PediuOutraData, Hoje, Hoje.AddDays(-1)).Impedimento);
        var (g, _) = AcompanhamentoNaTela.Gravar(DesfechoContato.PediuOutraData, Hoje, new DateOnly(2026, 11, 3));
        Assert.Equal(EtapaAcompanhamento.RetornarNaData, g!.Etapa);
        Assert.Equal(new DateOnly(2026, 11, 3), g.ProximoContato);
    }

    [Fact]
    public void Quer_marcar_e_pronto_para_agendar_hoje_e_nao_quer_voltar_encerra()
    {
        var (marcar, _) = AcompanhamentoNaTela.Gravar(DesfechoContato.QuerMarcar, Hoje, null);
        Assert.Equal(EtapaAcompanhamento.ProntoParaAgendar, marcar!.Etapa);
        Assert.Equal(Hoje, marcar.ProximoContato);
        var (encerra, _) = AcompanhamentoNaTela.Gravar(DesfechoContato.NaoQuerVoltar, Hoje, null);
        Assert.True(encerra!.Encerrar);
        Assert.Null(encerra.ProximoContato);
    }

    [Fact]
    public void O_funil_separa_as_faixas_e_a_taxa_so_conta_quem_foi_contatado()
    {
        var linhas = new[]
        {
            L(1, responsavel: null),                                              // a assumir
            L(2, atrasado: true, proximo: Hoje.AddDays(-2)),                       // contatar hoje (atrasado)
            L(3, proximo: Hoje.AddDays(3), tentativas: 1, ultimoContato: new DateTime(2026, 10, 14)), // em conversa
            L(4, pendente: false, agendado: new DateTime(2026, 10, 23, 14, 0, 0), ultimoContato: new DateTime(2026, 10, 10)),
            L(5, pendente: false, voltou: new DateOnly(2026, 10, 12), ultimoContato: new DateTime(2026, 10, 5)),  // voltou com contato
            L(6, pendente: false, voltou: new DateOnly(2026, 10, 8)),             // voltou sozinho
            L(7, pendente: false, motivoEncerramento: "desistiu", ultimoContato: new DateTime(2026, 10, 1)),
            L(8, tipo: TipoAcompanhamento.NovoBsv)                                 // outra fila
        };
        var f = AcompanhamentoNaTela.Funil(linhas, TipoAcompanhamento.Recall, soMinhaFila: false, usuarioId: 7, Hoje);
        Assert.Equal(1, f.AAssumir);
        Assert.Equal(1, f.ContatarHoje);
        Assert.Equal(1, f.Atrasados);
        Assert.Equal(1, f.EmConversa);
        Assert.Equal(1, f.Agendados);
        Assert.Equal(2, f.Voltaram);
        Assert.Equal(4, f.ContatadosNoPeriodo);
        Assert.Equal(25, f.TaxaDeRetorno); // 1 dos 4 contatados — quem voltou sozinho não conta
    }

    [Fact]
    public void Taxa_sem_contato_no_periodo_e_nula_e_nao_zero()
        => Assert.Null(AcompanhamentoNaTela.Funil([L()], TipoAcompanhamento.Recall, false, 7, Hoje).TaxaDeRetorno);

    [Fact]
    public void Minha_fila_traz_o_meu_e_o_que_ninguem_assumiu_e_esconde_o_dos_colegas()
    {
        var linhas = new[] { L(1, responsavel: 7), L(2, responsavel: null), L(3, responsavel: 9) };
        var minha = AcompanhamentoNaTela.Filtrar(linhas, new(TipoAcompanhamento.Recall, SoMinhaFila: true), 7, Hoje);
        Assert.Equal(new[] { 2, 1 }, minha.Select(x => x.Id)); // sem dono vem antes no mesmo dia
        Assert.Equal(3, AcompanhamentoNaTela.Filtrar(linhas, new(TipoAcompanhamento.Recall, false), 7, Hoje).Count);
    }

    [Fact]
    public void Sem_faixa_a_fila_mostra_so_o_que_pede_acao_e_o_atrasado_vem_primeiro()
    {
        var linhas = new[] { L(1, proximo: Hoje), L(2, atrasado: true, proximo: Hoje.AddDays(-3)), L(3, pendente: false, voltou: Hoje) };
        var fila = AcompanhamentoNaTela.Filtrar(linhas, new(TipoAcompanhamento.Recall, false), 7, Hoje);
        Assert.Equal(new[] { 2, 1 }, fila.Select(x => x.Id));
        var voltaram = AcompanhamentoNaTela.Filtrar(linhas, new(TipoAcompanhamento.Recall, false, FaixaAcompanhamento.Voltou), 7, Hoje);
        Assert.Equal(3, Assert.Single(voltaram).Id);
    }

    [Fact]
    public void A_busca_acha_pelo_telefone_sem_mascara()
    {
        var linhas = new[] { L(1, telefone: "(21) 99876-5432"), L(2, telefone: "21911112222", paciente: "João") };
        var achou = AcompanhamentoNaTela.Filtrar(linhas, new(TipoAcompanhamento.Recall, false, Busca: "98765"), 7, Hoje);
        Assert.Equal(1, Assert.Single(achou).Id);
    }

    [Fact]
    public void Degrau_e_selo_contam_o_caminho_do_caso()
    {
        Assert.Equal(0, AcompanhamentoNaTela.Degrau(L(responsavel: null)));
        Assert.Equal(1, AcompanhamentoNaTela.Degrau(L()));
        Assert.Equal(2, AcompanhamentoNaTela.Degrau(L(tentativas: 1)));
        Assert.Equal(3, AcompanhamentoNaTela.Degrau(L(pendente: false, agendado: new DateTime(2026, 10, 20, 9, 0, 0))));
        Assert.Equal(4, AcompanhamentoNaTela.Degrau(L(pendente: false, voltou: Hoje)));
        Assert.Equal(("Atrasado 2 dias", "perigo"), AcompanhamentoNaTela.Selo(L(atrasado: true, proximo: Hoje.AddDays(-2)), Hoje));
        Assert.Equal("Sem resposta · 2ª tentativa", AcompanhamentoNaTela.Selo(L(etapa: EtapaAcompanhamento.SemResposta, tentativas: 2), Hoje).Texto);
        Assert.Equal("Agendado 20/10 às 09:00", AcompanhamentoNaTela.Selo(L(pendente: false, agendado: new DateTime(2026, 10, 20, 9, 0, 0)), Hoje).Texto);
        Assert.Equal("Fez a 1ª sessão em 16/10", AcompanhamentoNaTela.Selo(L(pendente: false, voltou: Hoje, tipo: TipoAcompanhamento.NovoBsv), Hoje).Texto);
    }

    [Fact]
    public void Mais_filtros_conta_quantos_estao_ligados()
        => Assert.Equal(3, new FiltroAcompanhamento(TipoAcompanhamento.Recall, false, Convenio: "Unimed",
            SoPacoteComSaldo: true, ResponsavelId: 4).Extras);
}
