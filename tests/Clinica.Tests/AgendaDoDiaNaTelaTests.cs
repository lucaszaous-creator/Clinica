using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a AGENDA DO DIA do balcão AFIRMA (out/2026 — a tela virou Blazor e a regra que morava
/// no <c>FilaViewModel</c>/<c>FiltroListaAgenda</c> WPF desceu para a Application). Cada teste
/// fixa um comportamento que o WPF tinha e que a migração não pode perder: o passo de cada
/// linha, o "só hoje" do check-in, a pendência de fechamento, o selo de confirmação (com o
/// "Não vem" em vermelho), o menu "⋯" por estado e permissão, e o recorte com as contagens
/// ANTES do clique.
/// </summary>
public class AgendaDoDiaNaTelaTests
{
    private static readonly DateTime Agora = new(2026, 10, 15, 11, 0, 0);
    private static readonly DateOnly Dia = DateOnly.FromDateTime(Agora);

    private static readonly PermissoesDaAgendaDoDia Tudo = new(true, true, true, true, true, true);
    private static readonly PermissoesDaAgendaDoDia SoLeitura = new(false, false, false, false, false, false);

    private static int _id;

    private static Agendamento Ag(int hora, StatusAgendamento status = StatusAgendamento.Agendado,
        DateTime? chegada = null, DateTime? inicio = null, DateTime? fim = null, int? atendimentoId = null,
        Paciente? paciente = null, int? profissionalId = 1, string profissional = "Dra. Helena",
        ModalidadeAtendimento modalidade = ModalidadeAtendimento.AcupunturaSimples, DateTime? dataHora = null)
    {
        var id = Interlocked.Increment(ref _id);
        paciente ??= new Paciente { Id = 1000 + id, Nome = $"Paciente {id}" };
        return new Agendamento
        {
            Id = id, PacienteId = paciente.Id, Paciente = paciente,
            DataHora = dataHora ?? Dia.ToDateTime(new TimeOnly(hora, 0)), DuracaoMinutos = 30,
            ModalidadePrevista = modalidade, Status = status,
            ChegadaEm = chegada, InicioAtendimentoEm = inicio, FimAtendimentoEm = fim, AtendimentoId = atendimentoId,
            ProfissionalId = profissionalId,
            Profissional = profissionalId is { } pid ? new Profissional { Id = pid, Nome = profissional } : null
        };
    }

    private static DadosDaAgendaDoDia Dados(IEnumerable<Agendamento> horarios,
        Dictionary<int, RespostaDeConfirmacao>? confirmacoes = null,
        Dictionary<int, (int, int)>? pacotes = null, HashSet<int>? jaFechados = null, HashSet<int>? guias = null)
        => new(Dia, horarios.ToList(), guias ?? [], new Dictionary<int, IReadOnlyList<SituacaoTermo>>(),
            confirmacoes ?? [], pacotes ?? [], jaFechados ?? []);

    private static LinhaAgendaDoDia Uma(Agendamento a, DadosDaAgendaDoDia? d = null)
        => AgendaDoDiaNaTela.Montar(d ?? Dados([a]), Agora).Single();

    [Fact]
    public void A_lista_e_na_ordem_da_hora_e_mantem_cancelado_e_falta_apagados()
    {
        var linhas = AgendaDoDiaNaTela.Montar(Dados([
            Ag(15), Ag(9, StatusAgendamento.Cancelado), Ag(10, StatusAgendamento.Faltou), Ag(8)]), Agora);

        Assert.Equal([8, 9, 10, 15], linhas.Select(l => l.DataHora.Hour).ToArray());
        Assert.True(linhas[1].ForaDaFila);
        Assert.Equal("Cancelado", linhas[1].Status);
        Assert.Equal("Faltou", linhas[2].Status);
        Assert.Equal(PassoDaAgenda.Nenhum, linhas[1].Passo);
    }

    [Fact]
    public void Chegou_e_o_passo_de_quem_ainda_vem_e_so_no_dia_da_sessao()
    {
        Assert.Equal("Chegou", Uma(Ag(14)).RotuloDoPasso);

        // Conclusão pendente de outro dia que entrou na lista de hoje: não há check-in.
        var ontem = Ag(14, dataHora: Agora.AddDays(-1).Date.AddHours(14), inicio: Agora.AddDays(-1).Date.AddHours(14));
        var linha = Uma(ontem);
        Assert.False(linha.EhHoje);
        Assert.Equal("Conclusão pendente", linha.Status);
        Assert.Equal(PassoDaAgenda.Concluir, linha.Passo);
        Assert.StartsWith(ontem.DataHora.ToString("dd'/'MM"), linha.Horario);
    }

    [Fact]
    public void No_local_nao_tem_passo_do_balcao()
    {
        // De "No local" quem age é quem atende — um "Concluir" aqui pularia o registro clínico.
        var linha = Uma(Ag(10, chegada: Agora.AddMinutes(-12)));
        Assert.Equal("No local", linha.Status);
        Assert.Equal(PassoDaAgenda.Nenhum, linha.Passo);
        Assert.Contains("espera 12 min", linha.StatusDetalhe);
    }

    [Fact]
    public void Em_atendimento_concluir_exige_editar_agenda_E_lancar_atendimento()
    {
        var linha = Uma(Ag(10, chegada: Agora.AddMinutes(-40), inicio: Agora.AddMinutes(-30)));
        Assert.Equal(PassoDaAgenda.Concluir, linha.Passo);
        Assert.True(AgendaDoDiaNaTela.PodeDarOPasso(linha, Tudo));
        Assert.False(AgendaDoDiaNaTela.PodeDarOPasso(linha, Tudo with { ConcluirSessao = false }));
    }

    [Fact]
    public void Concluida_com_pacote_pede_debitar_ate_o_fechamento_acontecer()
    {
        var a = Ag(8, StatusAgendamento.Realizado, atendimentoId: 77);
        var pendente = Uma(a, Dados([a], pacotes: new() { [a.PacienteId] = (9, 10) }));
        Assert.True(pendente.FechamentoPendente);
        Assert.Equal("Debitar pacote", pendente.RotuloDoPasso);

        var fechado = Uma(a, Dados([a], pacotes: new() { [a.PacienteId] = (9, 10) }, jaFechados: [77]));
        Assert.False(fechado.FechamentoPendente);
        Assert.Equal(PassoDaAgenda.Nenhum, fechado.Passo);

        // Convênio sem pacote: o dinheiro vem pela guia — nada a fechar.
        Assert.False(Uma(a, Dados([a])).FechamentoPendente);
    }

    [Fact]
    public void Particular_sem_pacote_tambem_pede_o_fechamento_e_o_rotulo_nao_fala_de_pacote()
    {
        const string codigo = "ParticularAgendaDoDia";
        var paciente = new Paciente { Id = 5555, Nome = "Particular", ConvenioCodigo = codigo };
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(codigo, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica(), FormatoNumeroGuia.SemValidacao, GeraGuia: false)
        ]);
        try
        {
            var linha = Uma(Ag(8, StatusAgendamento.Realizado, atendimentoId: 88, paciente: paciente));
            Assert.True(linha.FechamentoPendente);
            Assert.Equal("Fechar sessão", linha.RotuloDoPasso);
        }
        finally
        {
            CatalogoConvenios.Atualizar([]);
        }
    }

    [Theory]
    [InlineData(StatusContato.Respondido, null, "Confirmou", "sucesso")]
    [InlineData(StatusContato.Enviado, null, "Não confirmou", "aviso")]
    [InlineData(StatusContato.Respondido, RodadaDeConfirmacao.RespostaRemarcar, RodadaDeConfirmacao.SeloNaoVem, "perigo")]
    [InlineData(StatusContato.Respondido, RodadaDeConfirmacao.RespostaCancelar, RodadaDeConfirmacao.SeloPediuCancelar, "perigo")]
    public void O_selo_de_confirmacao_vem_da_rodada_e_o_nao_vem_e_vermelho(StatusContato status, string? comentario, string selo, string tom)
    {
        var a = Ag(14);
        var linha = Uma(a, Dados([a], confirmacoes: new()
        {
            [a.Id] = new RespostaDeConfirmacao(status, Agora.AddDays(-1), comentario, CanalContato.WhatsApp)
        }));
        Assert.Equal(selo, linha.Confirmacao);
        Assert.Equal(tom, AgendaDoDiaNaTela.TomDaConfirmacao(linha.Confirmacao));
    }

    // Out/2026: o menu "⋯" é FIXO na janela (a tabela rola por dentro e o cortava). Abre abaixo
    // do clique quando cabe; no pé da janela abre para CIMA, sem cobrir o botão de onde saiu.
    [Fact]
    public void Menu_da_linha_abre_abaixo_quando_cabe_e_acima_no_pe_da_janela()
    {
        var a = Ag(14);
        var acoes = AgendaDoDiaNaTela.Menu(Uma(a), Tudo);
        Assert.Contains("top:314px", AgendaDoDiaNaTela.EstiloDoMenu(1300, 300, acoes, 768));
        var embaixo = AgendaDoDiaNaTela.EstiloDoMenu(1300, 740, acoes, 768);
        var topo = int.Parse(embaixo[(embaixo.IndexOf("top:") + 4)..embaixo.IndexOf("px", embaixo.IndexOf("top:"))]);
        Assert.True(topo < 740 - 14, "no pé da janela o menu abre acima do clique");
        Assert.Contains("100vh", AgendaDoDiaNaTela.EstiloDoMenu(1300, 740, acoes));
    }

    // Out/2026: o pedido de cancelamento pelo WhatsApp se aceita NA LINHA — botão e item do "⋯".
    [Fact]
    public void Pedido_de_cancelamento_se_aceita_na_propria_linha()
    {
        var a = Ag(14);
        var linha = Uma(a, Dados([a], confirmacoes: new()
        {
            [a.Id] = new RespostaDeConfirmacao(StatusContato.Respondido, Agora.AddHours(-1), RodadaDeConfirmacao.RespostaCancelar, CanalContato.WhatsApp)
        }));
        Assert.True(linha.PediuCancelar);
        Assert.Contains(AcaoDaLinhaAgenda.Cancelar, AgendaDoDiaNaTela.Menu(linha, Tudo));
        Assert.Equal("Aceitar o cancelamento pedido", AgendaDoDiaNaTela.Rotulo(AcaoDaLinhaAgenda.Cancelar, linha));
        Assert.False(Uma(a).PediuCancelar);
    }

    [Fact]
    public void Depois_do_check_in_a_confirmacao_e_ruido_e_some()
    {
        var a = Ag(10, chegada: Agora.AddMinutes(-5));
        var linha = Uma(a, Dados([a], confirmacoes: new()
        {
            [a.Id] = new RespostaDeConfirmacao(StatusContato.Respondido, Agora.AddDays(-1), null, CanalContato.WhatsApp)
        }));
        Assert.Equal(string.Empty, linha.Confirmacao);
    }

    [Fact]
    public void Linha_cancelada_nao_carrega_selo_do_paciente()
    {
        var a = Ag(9, StatusAgendamento.Cancelado);
        var linha = Uma(a, Dados([a], guias: [a.PacienteId], pacotes: new() { [a.PacienteId] = (10, 10) }));
        Assert.Empty(linha.Selos);
        Assert.False(linha.TemGuiaPendente);
        Assert.False(linha.TemPacote);
    }

    [Fact]
    public void O_atraso_corre_com_o_relogio_e_vira_selo_so_para_quem_nao_chegou()
    {
        var linha = Uma(Ag(10)); // marcado às 10:00, agora são 11:00
        Assert.Contains(linha.Selos, s => s.Texto == "Atrasado 1h");
    }

    [Fact]
    public void O_menu_segue_estado_e_permissao_sem_separador_orfao()
    {
        var aberta = Uma(Ag(14));
        Assert.Equal(
            [AcaoDaLinhaAgenda.Ficha, AcaoDaLinhaAgenda.ConferirConvenio, AcaoDaLinhaAgenda.Separador,
             AcaoDaLinhaAgenda.MarcarFalta, AcaoDaLinhaAgenda.Cancelar, AcaoDaLinhaAgenda.Separador,
             AcaoDaLinhaAgenda.AbrirHorario],
            AgendaDoDiaNaTela.Menu(aberta, Tudo).ToArray());

        // Quem só lê fica só com abrir o horário (leitura), sem separador sobrando.
        Assert.Equal([AcaoDaLinhaAgenda.AbrirHorario], AgendaDoDiaNaTela.Menu(aberta, SoLeitura).ToArray());

        // A cancelada: a janela do horário é a única ação — e o rótulo diz que ali se reabre.
        var cancelada = Uma(Ag(9, StatusAgendamento.Cancelado));
        Assert.Equal([AcaoDaLinhaAgenda.Ficha, AcaoDaLinhaAgenda.Separador, AcaoDaLinhaAgenda.AbrirHorario],
            AgendaDoDiaNaTela.Menu(cancelada, Tudo).ToArray());
        Assert.Contains("reabrir", AgendaDoDiaNaTela.Rotulo(AcaoDaLinhaAgenda.AbrirHorario, cancelada));

        // A concluída: fechar a sessão no menu; falta e cancelamento não.
        var concluida = Uma(Ag(8, StatusAgendamento.Realizado, atendimentoId: 9));
        Assert.Contains(AcaoDaLinhaAgenda.FecharSessao, AgendaDoDiaNaTela.Menu(concluida, Tudo));
        Assert.DoesNotContain(AcaoDaLinhaAgenda.MarcarFalta, AgendaDoDiaNaTela.Menu(concluida, Tudo));
    }

    [Fact]
    public void As_respostas_ao_lembrete_aparecem_no_topo_e_filtram_a_lista()
    {
        RespostaDeConfirmacao R(StatusContato s, string? c = null) => new(s, Agora.AddDays(-1), c, CanalContato.Sms);
        var naoVem = Ag(9); var semResposta = Ag(10); var confirmou = Ag(11); var semContato = Ag(15);
        var jaChegou = Ag(8, chegada: Agora.AddMinutes(-5));
        var linhas = AgendaDoDiaNaTela.Montar(Dados([naoVem, semResposta, confirmou, semContato, jaChegou], confirmacoes: new()
        {
            [naoVem.Id] = R(StatusContato.Respondido, RodadaDeConfirmacao.RespostaRemarcar),
            [semResposta.Id] = R(StatusContato.Enviado),
            [confirmou.Id] = R(StatusContato.Respondido),
            [jaChegou.Id] = R(StatusContato.Enviado) // chegou: a resposta deixou de importar
        }), Agora);

        var tudo = AgendaDoDiaNaTela.Recortar(linhas, new FiltroAgendaDoDia());
        Assert.Equal(1, tudo.Respostas!["nao-vem"]);
        Assert.Equal(1, tudo.Respostas["sem-resposta"]);
        Assert.Equal(1, tudo.Respostas["confirmou"]);

        var soNaoVem = AgendaDoDiaNaTela.Recortar(linhas, new FiltroAgendaDoDia(Resposta: "nao-vem"));
        Assert.Equal(naoVem.Id, Assert.Single(soNaoVem.Visiveis).AgendamentoId);
        // A distribuição continua visível com o filtro ligado.
        Assert.Equal(1, soNaoVem.Respostas!["confirmou"]);
        Assert.True(new FiltroAgendaDoDia(Resposta: "nao-vem").TemFiltros);
    }

    [Fact]
    public void As_contagens_das_pilulas_sao_antes_do_clique_e_o_resumo_diz_o_recorte()
    {
        var linhas = AgendaDoDiaNaTela.Montar(Dados([
            Ag(8, StatusAgendamento.Realizado, atendimentoId: 1),
            Ag(9, StatusAgendamento.Cancelado),
            Ag(10, chegada: Agora.AddMinutes(-3)),
            Ag(14), Ag(15)]), Agora);

        var recorte = AgendaDoDiaNaTela.Recortar(linhas, new FiltroAgendaDoDia(Situacao: "pendente"));
        Assert.Equal(2, recorte.Visiveis.Count);
        Assert.Equal(5, recorte.Contagens["todos"]);
        Assert.Equal(1, recorte.Contagens["atendido"]);
        Assert.Equal(1, recorte.Contagens["cancelado"]);
        Assert.Equal(1, recorte.Contagens["no-local"]);
        Assert.Equal("2 de 5 horários no dia", recorte.Resumo);
        Assert.False(recorte.NenhumCorresponde);
    }

    [Fact]
    public void A_busca_ignora_acento_e_caixa_e_o_vazio_do_filtro_e_outra_pergunta()
    {
        var joao = new Paciente { Id = 7001, Nome = "João Antônio" };
        var linhas = AgendaDoDiaNaTela.Montar(Dados([Ag(9, paciente: joao), Ag(10)]), Agora);

        Assert.Single(AgendaDoDiaNaTela.Recortar(linhas, new FiltroAgendaDoDia(Busca: "joao antonio")).Visiveis);

        var nada = AgendaDoDiaNaTela.Recortar(linhas, new FiltroAgendaDoDia(Busca: "zzz"));
        Assert.True(nada.NenhumCorresponde);
        Assert.Equal(2, nada.Total);
    }

    [Fact]
    public void O_profissional_filtra_e_some_do_filtro_quando_sai_do_dia()
    {
        var linhas = AgendaDoDiaNaTela.Montar(Dados([
            Ag(9, profissionalId: 1, profissional: "Dra. Helena"),
            Ag(10, profissionalId: 2, profissional: "Dr. Marcos"),
            Ag(11, profissionalId: null)]), Agora);

        var profissionais = AgendaDoDiaNaTela.Profissionais(linhas);
        Assert.Equal(["Todos os profissionais · 3", "Dr. Marcos · 1", "Dra. Helena · 1"], profissionais.Select(p => p.Rotulo).ToArray());

        // Horário sem profissional só aparece em "Todos": ele não é de ninguém.
        Assert.Single(AgendaDoDiaNaTela.Recortar(linhas, new FiltroAgendaDoDia(ProfissionalId: 2)).Visiveis);

        var ajustado = AgendaDoDiaNaTela.Ajustar(new FiltroAgendaDoDia(ProfissionalId: 99), profissionais);
        Assert.Null(ajustado.ProfissionalId);
    }

    [Fact]
    public void A_frase_do_fechamento_so_conta_guia_quando_ela_nasceu_neste_clique()
    {
        var resultado = new ResultadoFechamento(new Atendimento(), new ConsumoPacote(), null, [], []);
        Assert.Equal("Sessão de Ana fechada — 1 sessão do pacote.", AgendaDoDiaNaTela.FraseDoFechamento("Ana", resultado));
        Assert.Equal("Sessão de Ana fechada — 2 guia(s) · 1 sessão do pacote.", AgendaDoDiaNaTela.FraseDoFechamento("Ana", resultado, 2));
        Assert.Equal("Sessão de Ana fechada — nada a debitar nem a lançar.",
            AgendaDoDiaNaTela.FraseDoFechamento("Ana", new ResultadoFechamento(new Atendimento(), null, null, [], [])));
    }

    [Fact]
    public void A_conferencia_de_convenio_nunca_diz_tudo_certo_quando_nao_conferiu()
    {
        Assert.StartsWith("Nada a resolver", AgendaDoDiaNaTela.FraseDaElegibilidade(false, [], falhou: false));
        Assert.DoesNotContain("Nada a resolver", AgendaDoDiaNaTela.FraseDaElegibilidade(false, [], falhou: true));
        Assert.Contains("GUIA PENDENTE", AgendaDoDiaNaTela.FraseDaElegibilidade(true, [], falhou: false));
    }

    [Fact]
    public void Sem_autoria_a_dica_assume_a_lacuna()
    {
        Assert.StartsWith("Marcado antes", AgendaDoDiaNaTela.DescreverLancamento(new Agendamento()));
        Assert.Equal("Marcado por ana em 08/08/2026 14:32",
            AgendaDoDiaNaTela.DescreverLancamento(new Agendamento { CriadoPor = "ana", CriadoEm = new DateTime(2026, 8, 8, 14, 32, 0) }));
    }
}
