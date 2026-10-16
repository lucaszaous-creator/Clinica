using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A CONVERSA DO PACIENTE (out/2026, tela Blazor do Acompanhamento): o que ela recusa antes de
/// chamar o serviço, o pedido que ela monta, o trilho, o vazio que separa "fila em dia" de
/// "filtro escondendo gente", os filtros de "Mais filtros" que vieram da tela WPF e a visão
/// por responsável.
/// </summary>
public sealed class ConversaDoAcompanhamentoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 16); // sexta-feira

    private static LinhaAcompanhamento L(int? responsavel = 7, bool pendente = true, string? telefone = "21999990000",
        bool consentimento = true, int tentativas = 0, DateTime? agendado = null, DateOnly? voltou = null,
        string? motivoEncerramento = null, string? motivo = null, int dias = 75, DateTime? ultimoContato = null,
        EtapaAcompanhamento etapa = EtapaAcompanhamento.AContatar, string responsavelNome = "Ana Lima", bool atrasado = false,
        TipoAcompanhamento tipo = TipoAcompanhamento.Recall)
        => new(1, 11, "Maria Souza", telefone, "Unimed", tipo, ModalidadeAtendimento.AcupunturaComEletro, "Dr. Gustavo", 3,
            responsavel, responsavel is null ? "A assumir" : responsavelNome, new DateTime(2026, 8, 2), dias, "A contatar", pendente, atrasado,
            Hoje, etapa, tentativas, ultimoContato, null, consentimento, false, agendado, false, false, Guid.NewGuid(),
            motivoEncerramento, motivo, 12, voltou);

    [Fact]
    public void Nao_respondeu_sem_canal_e_recusado_porque_a_tentativa_nao_contaria()
    {
        var (pedido, impedimento) = ConversaDoAcompanhamento.Pedido(L(), DesfechoContato.NaoRespondeu, Hoje, null, null, null, null, 7, Guid.NewGuid());
        Assert.Null(pedido);
        Assert.Contains("por onde", impedimento);
    }

    [Fact]
    public void Nao_respondeu_com_canal_grava_sem_resposta_em_dois_dias_uteis_e_observacao_opcional()
    {
        var idem = Guid.NewGuid();
        var linha = L();
        var (pedido, impedimento) = ConversaDoAcompanhamento.Pedido(linha, DesfechoContato.NaoRespondeu, Hoje, null, CanalContato.WhatsApp, "  ", null, 7, idem);
        Assert.Null(impedimento);
        Assert.NotNull(pedido);
        Assert.Equal(EtapaAcompanhamento.SemResposta, pedido!.Etapa);
        Assert.Equal(new DateOnly(2026, 10, 20), pedido.ProximoContato); // sexta + 2 úteis = terça
        Assert.Equal(CanalContato.WhatsApp, pedido.Canal);
        Assert.Equal(string.Empty, pedido.Observacao);
        Assert.Equal(idem, pedido.Idempotencia);
        Assert.Equal(linha.Versao, pedido.Versao);
        Assert.False(pedido.Encerrar);
    }

    [Fact]
    public void Aguardando_o_plano_pode_ser_sem_canal()
    {
        var (pedido, _) = ConversaDoAcompanhamento.Pedido(L(), DesfechoContato.AguardandoPlano, Hoje, null, null, null, null, 7, Guid.NewGuid());
        Assert.Equal(EtapaAcompanhamento.AguardandoPlano, pedido!.Etapa);
        Assert.Null(pedido.Canal);
    }

    [Fact]
    public void Pediu_outra_data_sem_data_nao_inventa_uma()
    {
        var (pedido, impedimento) = ConversaDoAcompanhamento.Pedido(L(), DesfechoContato.PediuOutraData, Hoje, null, CanalContato.Telefone, null, null, 7, Guid.NewGuid());
        Assert.Null(pedido);
        Assert.Contains("dia do próximo contato", impedimento);
    }

    [Fact]
    public void Nao_quer_voltar_exige_motivo_e_justificativa_e_encerra()
    {
        Assert.Contains("motivo", ConversaDoAcompanhamento.Pedido(L(), DesfechoContato.NaoQuerVoltar, Hoje, null, null, "Mudou de cidade", null, 7, Guid.NewGuid()).Impedimento);
        Assert.Contains("justificativa", ConversaDoAcompanhamento.Pedido(L(), DesfechoContato.NaoQuerVoltar, Hoje, null, null, "não", 4, 7, Guid.NewGuid()).Impedimento);
        var (pedido, _) = ConversaDoAcompanhamento.Pedido(L(), DesfechoContato.NaoQuerVoltar, Hoje, null, null, "Mudou de cidade", 4, 7, Guid.NewGuid());
        Assert.True(pedido!.Encerrar);
        Assert.Null(pedido.ProximoContato);
        Assert.Equal(4, pedido.MotivoId);
    }

    [Fact]
    public void A_legenda_que_o_caso_ja_tem_vai_no_pedido_e_nao_some_num_nao_respondeu()
    {
        var (pedido, _) = ConversaDoAcompanhamento.Pedido(L(motivo: "Pediu adiamento"), DesfechoContato.NaoRespondeu, Hoje, null, CanalContato.Telefone, null, 9, 7, Guid.NewGuid());
        Assert.Equal(9, pedido!.MotivoId);
    }

    [Fact]
    public void Caso_encerrado_so_se_reabre_com_justificativa()
    {
        var encerrado = L(pendente: false, motivoEncerramento: "Mudou-se");
        Assert.Contains("reabra", ConversaDoAcompanhamento.Pedido(encerrado, DesfechoContato.NaoRespondeu, Hoje, null, CanalContato.WhatsApp, null, null, 7, Guid.NewGuid()).Impedimento);
        Assert.NotNull(ConversaDoAcompanhamento.Reabertura(encerrado, Hoje, null, "oi", null, 7, Guid.NewGuid()).Impedimento);
        Assert.NotNull(ConversaDoAcompanhamento.Reabertura(encerrado, Hoje, Hoje.AddDays(-1), "Ligou pedindo para voltar", null, 7, Guid.NewGuid()).Impedimento);
        var (pedido, _) = ConversaDoAcompanhamento.Reabertura(encerrado, Hoje, null, "Ligou pedindo para voltar", null, 7, Guid.NewGuid());
        Assert.True(pedido!.Reabrir);
        Assert.Equal(Hoje, pedido.ProximoContato);
        Assert.Null(pedido.Canal);
    }

    [Fact]
    public void O_trilho_diz_quem_assumiu_quantas_tentativas_e_qual_e_o_proximo_degrau()
    {
        var t = ConversaDoAcompanhamento.Trilho(L(responsavel: 7, tentativas: 1), usuarioId: 7);
        Assert.Equal(["Sumiu", "Assumido", "Contato", "Agendou", "Voltou"], t.Select(d => d.Rotulo));
        Assert.Equal("02/08", t[0].Detalhe);
        Assert.Equal("por você", t[1].Detalhe);
        Assert.Equal("1ª tentativa", t[2].Detalhe);
        Assert.True(t[3].Agora);
        Assert.False(t[3].Feito);

        var outro = ConversaDoAcompanhamento.Trilho(L(responsavel: 9, responsavelNome: "Bruno Castro"), usuarioId: 7);
        Assert.Equal("por Bruno", outro[1].Detalhe);
        Assert.True(outro[2].Agora);

        var voltou = ConversaDoAcompanhamento.Trilho(L(pendente: false, voltou: new DateOnly(2026, 10, 10)), 7);
        Assert.All(voltou, d => Assert.True(d.Feito));
        Assert.DoesNotContain(voltou, d => d.Agora);

        var bsv = ConversaDoAcompanhamento.Trilho(L(responsavel: null, tipo: TipoAcompanhamento.NovoBsv), 7);
        Assert.Equal("Indicado", bsv[0].Rotulo);
        Assert.True(bsv[1].Agora);
    }

    [Fact]
    public void O_vazio_separa_fila_em_dia_de_filtro_escondendo_gente()
    {
        Assert.Equal("Fila em dia", ConversaDoAcompanhamento.Vazio(0, false, null).Titulo);
        Assert.Equal("Ninguém bate com o filtro", ConversaDoAcompanhamento.Vazio(12, true, null).Titulo);
        Assert.Equal("Nenhum encerrado", ConversaDoAcompanhamento.Vazio(0, false, FaixaAcompanhamento.Encerrado).Titulo);
        Assert.Equal("3 de 12 na fila · filtrada", ConversaDoAcompanhamento.Resumo(3, 12, true));
        Assert.Equal("1 paciente na fila", ConversaDoAcompanhamento.Resumo(1, 1, false));
    }

    [Fact]
    public void Os_filtros_que_vieram_da_tela_WPF_recortam_e_dizem_quantos_estao_ligados()
    {
        var extra = new FiltroExtraAcompanhamento(TentativasMinimas: 2, DiasMinimos: 60, DiasMaximos: 90, Etapa: EtapaAcompanhamento.SemResposta);
        Assert.Equal(3, extra.Contagem);
        Assert.True(extra.Aceita(L(tentativas: 2, dias: 75, etapa: EtapaAcompanhamento.SemResposta)));
        Assert.False(extra.Aceita(L(tentativas: 1, dias: 75, etapa: EtapaAcompanhamento.SemResposta)));
        Assert.False(extra.Aceita(L(tentativas: 2, dias: 95, etapa: EtapaAcompanhamento.SemResposta)));
        Assert.False(extra.Aceita(L(tentativas: 2, dias: 75)));

        var periodo = new FiltroExtraAcompanhamento(ContatoDesde: new DateOnly(2026, 10, 1), ContatoAte: new DateOnly(2026, 10, 10));
        Assert.True(periodo.Aceita(L(ultimoContato: new DateTime(2026, 10, 10, 18, 0, 0))));
        Assert.False(periodo.Aceita(L(ultimoContato: null)));
        Assert.False(periodo.Aceita(L(ultimoContato: new DateTime(2026, 9, 30))));

        Assert.NotNull(new FiltroExtraAcompanhamento(DiasMinimos: 90, DiasMaximos: 60).Impedimento());
        Assert.Null(FiltroExtraAcompanhamento.Nenhum.Impedimento());
        Assert.Equal(0, FiltroExtraAcompanhamento.Nenhum.Contagem);
    }

    [Fact]
    public void Quem_nao_pode_receber_mensagem_vai_dito_no_CSV_e_na_ficha()
    {
        Assert.Equal("não — sem telefone", ConversaDoAcompanhamento.PodeReceberMensagem(L(telefone: null)));
        Assert.Equal("não — sem consentimento (LGPD)", ConversaDoAcompanhamento.PodeReceberMensagem(L(consentimento: false)));
        Assert.Equal("sim", ConversaDoAcompanhamento.PodeReceberMensagem(L()));
        Assert.Equal("não autorizada (LGPD)", ConversaDoAcompanhamento.Autorizacao(L(consentimento: false)));
    }

    [Fact]
    public void A_visao_da_equipe_conta_por_responsavel_e_os_encerramentos_por_motivo()
    {
        var linhas = new[]
        {
            L(responsavel: 7, atrasado: true),
            L(responsavel: 7),
            L(responsavel: 9, responsavelNome: "Bruno Castro", pendente: false, motivoEncerramento: "Mudou-se", motivo: "Mudou de cidade"),
            L(responsavel: 9, responsavelNome: "Bruno Castro", pendente: false, agendado: new DateTime(2026, 10, 20, 9, 0, 0))
        };
        var (resp, enc) = ConversaDoAcompanhamento.VisaoDaEquipe(linhas, TipoAcompanhamento.Recall, Hoje);
        var ana = resp.Single(r => r.Responsavel == "Ana Lima");
        Assert.Equal((2, 1), (ana.Pendentes, ana.Atrasados));
        Assert.Equal(1, resp.Single(r => r.Responsavel == "Bruno Castro").Agendados);
        Assert.Equal(new EncerramentosPorMotivo("Mudou de cidade", 1), Assert.Single(enc));
    }

    [Fact]
    public void Iniciais_e_dono_da_linha()
    {
        Assert.Equal("AL", ConversaDoAcompanhamento.Iniciais("Ana Lima"));
        Assert.Equal("MS", ConversaDoAcompanhamento.Iniciais("Maria da Silva"));
        Assert.Equal("BC", ConversaDoAcompanhamento.Iniciais("Bruno Castro (inativo)"));
        Assert.Equal("Você", ConversaDoAcompanhamento.Dono(L(responsavel: 7), 7));
        Assert.Equal("ninguém", ConversaDoAcompanhamento.Dono(L(responsavel: null), 7));
        Assert.Equal("Ana", ConversaDoAcompanhamento.Dono(L(responsavel: 9), 7));
    }
}
