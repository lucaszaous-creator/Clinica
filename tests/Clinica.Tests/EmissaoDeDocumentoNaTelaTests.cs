using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A emissão de documento escrito (receita, atestado, laudo, declaração e pedido de exame) — as
/// regras que eram do <c>DocumentoEdicaoViewModel</c> WPF e passaram para
/// <see cref="RascunhoDeDocumento"/> / <see cref="EmissaoDeDocumentoNaTela"/> quando a janela
/// virou o <c>DocumentoForm</c> Blazor (out/2026). A janela WPF não era testável (o projeto de
/// teste não compila WPF); agora a conferência de alergia, a regra do CID, a herança da sessão e
/// a montagem campo a campo têm teste — e é por isso que moram aqui.
/// </summary>
public sealed class EmissaoDeDocumentoNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 20);

    private static Profissional Medica() => new() { Id = 7, Nome = "Dra. Ana Lima", RegistroConselho = "CRM-SP 12345", Ativo = true };

    private static RascunhoDeDocumento Receita(HerancaDaSessao? heranca = null)
        => new(10, TipoDocumentoClinico.Receita, heranca, hoje: Hoje);

    // ==================== abrir ====================

    [Fact]
    public void Termo_do_procedimento_nao_se_emite_por_aqui_e_a_frase_nomeia_a_porta()
        => EmissaoDeDocumentoNaTela.RecusaDeAbertura(10, TipoDocumentoClinico.TermoProcedimento, null)
            .Should().Contain("assinado pelo PACIENTE").And.Contain("aba Documentos, no dia do procedimento");

    [Fact]
    public void Termo_LGPD_manda_para_a_aba_LGPD_e_nao_para_o_dia_do_procedimento()
        => EmissaoDeDocumentoNaTela.RecusaDeAbertura(10, TipoDocumentoClinico.Consentimento, null)
            .Should().Contain("aba LGPD").And.NotContain("dia do procedimento");

    [Fact]
    public void Os_papeis_escritos_abrem()
    {
        foreach (var tipo in EmissaoDeDocumentoNaTela.Tipos)
            EmissaoDeDocumentoNaTela.RecusaDeAbertura(10, tipo, null).Should().BeNull();
    }

    [Fact]
    public void Nova_versao_so_do_laudo_e_so_do_mesmo_paciente()
    {
        var receita = new DocumentoClinico { Id = 1, Tipo = TipoDocumentoClinico.Receita, PacienteId = 10, Numero = "2026/0001" };
        EmissaoDeDocumentoNaTela.RecusaDeAbertura(10, TipoDocumentoClinico.Receita, receita)
            .Should().Contain("não se altera por nova versão");

        var laudo = new DocumentoClinico { Id = 2, Tipo = TipoDocumentoClinico.LaudoMedico, PacienteId = 99, Numero = "2026/0002" };
        EmissaoDeDocumentoNaTela.RecusaDeAbertura(10, TipoDocumentoClinico.LaudoMedico, laudo)
            .Should().Be("O documento 2026/0002 é de outro paciente.");

        var act = () => new RascunhoDeDocumento(10, TipoDocumentoClinico.LaudoMedico, versaoDe: laudo);
        act.Should().Throw<InvalidOperationException>().WithMessage("*de outro paciente*");
    }

    [Fact]
    public void As_recusas_da_nova_versao_na_ordem_da_janela_WPF()
    {
        EmissaoDeDocumentoNaTela.RecusaDaNovaVersao(TipoDocumentoClinico.Atestado, "Atestado", "2026/0001", false, 10)
            .Should().Contain("cancele com motivo e emita outro");
        EmissaoDeDocumentoNaTela.RecusaDaNovaVersao(TipoDocumentoClinico.LaudoMedico, "Laudo", "2026/0001", true, 10)
            .Should().Contain("está cancelado");
        EmissaoDeDocumentoNaTela.RecusaDaNovaVersao(TipoDocumentoClinico.LaudoMedico, "Laudo", "2026/0001", false, null)
            .Should().Contain("não é de um paciente");
        EmissaoDeDocumentoNaTela.RecusaDaNovaVersao(TipoDocumentoClinico.LaudoMedico, "Laudo", "2026/0001", false, 10)
            .Should().BeNull();
    }

    [Fact]
    public void Nova_versao_comeca_do_que_o_laudo_diz_e_trava_o_tipo()
    {
        var anterior = new DocumentoClinico
        {
            Id = 2, Tipo = TipoDocumentoClinico.LaudoMedico, PacienteId = 10, Numero = "2026/0002",
            Titulo = "Laudo pericial", Corpo = "Quadro clínico estável.", Observacoes = "Reavaliar.", Cid = "M54.5", CidAutorizado = true
        };
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.Receita, versaoDe: anterior, hoje: Hoje);

        r.Tipo.Should().Be(TipoDocumentoClinico.LaudoMedico);
        r.Corpo.Should().Be("Quadro clínico estável.");
        r.Cid.Should().Be("M54.5");
        r.CidAutorizado.Should().BeTrue();
        r.Titulo.Should().Be("Laudo pericial");
        r.PodeTrocarTipo.Should().BeFalse();
        r.TrocarTipo(TipoDocumentoClinico.Receita).Should().BeFalse();
        r.NumeroDaFolha.Should().Be("Nº gerado ao emitir · substitui o 2026/0002");
        r.Contexto.Should().Contain("nova versão do 2026/0002 — ele será cancelado ao emitir");
    }

    // ==================== a conferência de alergia (parcela 40) ====================

    private static ConferenciaPrescricao Colide() => new(
        [new AlertaPrescricao(GravidadePrescricao.Alergia, "Dipirona 500mg", "alergia registrada a dipirona")],
        [new ProblemaPaciente { Natureza = NaturezaProblema.Alergia, Descricao = "Dipirona" }],
        [new ProblemaPaciente { Natureza = NaturezaProblema.MedicacaoContinua, Descricao = "Losartana 50mg" }]);

    [Fact]
    public void A_conferencia_mostra_a_alergia_a_medicacao_continua_e_a_colisao()
    {
        var r = Receita();
        r.AplicarConferencia(Colide());

        r.AlertasClinicos.Should().Contain(a => a.Contains("Dipirona")).And.Contain("Em uso contínuo: Losartana 50mg");
        r.ColisoesAlergia.Should().ContainSingle().Which.Should().Be("“Dipirona 500mg” — alergia registrada a dipirona");
        r.ColideComAlergia.Should().BeTrue();
    }

    [Fact]
    public void Com_colisao_sem_o_eu_vi_nao_emite_e_a_recusa_diz_por_que()
    {
        var r = Receita();
        r.DefinirAssinante([Medica()], 7);
        r.AplicarConferencia(Colide());

        r.PodeEmitir(ocupado: false).Should().BeFalse();
        r.RecusaDaAlergia().Should().Be(EmissaoDeDocumentoNaTela.RecusaAlergia);

        r.AlergiaConferida = true;
        r.PodeEmitir(ocupado: false).Should().BeTrue();
        r.RecusaDaAlergia().Should().BeNull();
    }

    [Fact]
    public void Reconferir_e_achar_a_mesma_colisao_nao_desfaz_o_eu_vi()
    {
        // O clique de emitir RECONFERE: quem já marcou "eu vi" não pode perder a marca por isso.
        var r = Receita();
        r.AplicarConferencia(Colide());
        r.AlergiaConferida = true;

        r.AplicarConferencia(Colide());

        r.AlergiaConferida.Should().BeTrue();
    }

    [Fact]
    public void A_colisao_que_some_leva_junto_a_confirmacao()
    {
        var r = Receita();
        r.AplicarConferencia(Colide());
        r.AlergiaConferida = true;

        r.AplicarConferencia(ConferenciaPrescricao.Vazia);

        r.ColideComAlergia.Should().BeFalse();
        r.AlergiaConferida.Should().BeFalse("um \"eu vi\" de uma conferência anterior não vale para a próxima receita");
    }

    [Fact]
    public void Trocar_o_texto_desfaz_a_confirmacao_e_as_colisoes()
    {
        var r = Receita();
        r.AplicarConferencia(Colide());
        r.AlergiaConferida = true;

        r.Corpo = "Dipirona 1g, de 6/6h";

        r.AlergiaConferida.Should().BeFalse();
        r.ColideComAlergia.Should().BeFalse();
        r.ColisoesAlergia.Should().BeEmpty();
    }

    [Fact]
    public void Conferencia_que_falha_avisa_em_vez_de_calar_e_nao_bloqueia()
    {
        var r = Receita();
        r.AplicarConferencia(Colide());
        r.ConferenciaClinicaFalhou();

        r.AlertasClinicos.Should().ContainSingle().Which.Should().Be(EmissaoDeDocumentoNaTela.AlergiasNaoConferidas);
        r.ColideComAlergia.Should().BeFalse();
    }

    [Fact]
    public void O_que_vai_a_conferencia_e_o_texto_da_receita_e_as_linhas_do_pedido()
    {
        var r = Receita();
        r.Corpo = "Dipirona 500mg";
        r.Escritos().Should().Equal("Dipirona 500mg");

        var pedido = new RascunhoDeDocumento(10, TipoDocumentoClinico.PedidoExame, hoje: Hoje);
        pedido.Itens[0].Descricao = "Hemograma";
        pedido.Itens[0].Detalhe = "Anemia";
        pedido.Escritos().Should().Equal("Hemograma Anemia");
    }

    // ==================== quem assina ====================

    [Fact]
    public void Quem_assina_e_quem_esta_logado_e_sem_vinculo_a_folha_diz_por_que()
    {
        var r = Receita();
        r.DefinirAssinante([Medica(), new Profissional { Id = 8, Nome = "Dr. Bruno" }], 7);
        r.Profissional!.Id.Should().Be(7);
        r.MotivoSemAssinante.Should().BeNull();
        r.RegistroDoAssinante.Should().Be("CRM-SP 12345");

        var semVinculo = Receita();
        semVinculo.DefinirAssinante([Medica()], null);
        semVinculo.Profissional.Should().BeNull("o primeiro da lista NÃO é proposto");
        semVinculo.MotivoSemAssinante.Should().Be(AssinanteDoDocumento.AvisoSemVinculo);
        semVinculo.PodeEmitir(false).Should().BeFalse();
        semVinculo.FaltaAssinante.Should().Be(AssinanteDoDocumento.AvisoSemVinculo);
    }

    [Fact]
    public void A_declaracao_de_comparecimento_nao_exige_quem_assina()
    {
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.Comparecimento, hoje: Hoje);
        r.DefinirAssinante([], null);
        r.MotivoSemAssinante.Should().BeNull();
        r.PodeEmitir(false).Should().BeTrue();
    }

    [Fact]
    public void Ocupado_nao_emite()
    {
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.Comparecimento, hoje: Hoje);
        r.PodeEmitir(ocupado: true).Should().BeFalse();
    }

    // ==================== o texto obrigatório ====================

    [Fact]
    public void Receita_e_laudo_nao_saem_em_branco()
    {
        Receita().RecusaDoTexto().Should().Be("Escreva a prescrição antes de emitir a receita.");
        new RascunhoDeDocumento(10, TipoDocumentoClinico.LaudoMedico, hoje: Hoje).RecusaDoTexto()
            .Should().Be("Escreva o laudo antes de emiti-lo.");
        new RascunhoDeDocumento(10, TipoDocumentoClinico.Atestado, hoje: Hoje).RecusaDoTexto().Should().BeNull();
    }

    // ==================== a herança da sessão ====================

    private static HerancaDaSessao Sessao() => new(10, Cid: "M54.5", Hipotese: "Lombalgia mecânica",
        Chegada: new TimeOnly(14, 0), Saida: new TimeOnly(14, 50), AgendamentoId: 33, EvolucaoId: 44,
        DataHoraDaSessao: new DateTime(2026, 10, 20, 14, 0, 0));

    [Fact]
    public void O_atestado_herda_o_CID_da_hipotese_e_diz_de_onde_veio()
    {
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.Atestado, Sessao(), hoje: Hoje);
        r.Cid.Should().Be("M54.5");
        r.CidVeioDaSessao.Should().BeTrue();
        r.ExplicacaoDoCid.Should().Be("Veio da sua hipótese: Lombalgia mecânica. Trocar aqui não muda o prontuário.");
    }

    [Fact]
    public void Digitar_outro_CID_desliga_a_marca_e_o_mesmo_em_outra_caixa_nao()
    {
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.Atestado, Sessao(), hoje: Hoje);
        r.Cid = "m54.5 ";
        r.CidVeioDaSessao.Should().BeTrue();
        r.Cid = "M54.4";
        r.CidVeioDaSessao.Should().BeFalse();
    }

    [Fact]
    public void A_declaracao_herda_os_horarios_e_o_pedido_a_indicacao()
    {
        var declaracao = new RascunhoDeDocumento(10, TipoDocumentoClinico.Comparecimento, Sessao(), hoje: Hoje);
        declaracao.HoraChegadaTexto.Should().Be("14:00");
        declaracao.HoraSaidaTexto.Should().Be("14:50");
        declaracao.HorasVieramDaSessao.Should().BeTrue();

        var pedido = new RascunhoDeDocumento(10, TipoDocumentoClinico.PedidoExame, Sessao(), hoje: Hoje);
        pedido.Itens[0].Detalhe.Should().Be("Lombalgia mecânica");
        pedido.IndicacaoVeioDaSessao.Should().BeTrue();
    }

    [Fact]
    public void A_receita_abre_em_branco_mesmo_com_sessao()
    {
        var r = Receita(Sessao());
        r.Corpo.Should().BeNull("extrair medicamento da conduta poria no papel o que ninguém escreveu como prescrição");
        r.Cid.Should().BeNull();
    }

    [Fact]
    public void Trocar_o_tipo_reaplica_a_heranca_sem_sobrescrever_o_que_foi_digitado()
    {
        var r = Receita(Sessao());
        r.TrocarTipo(TipoDocumentoClinico.Atestado).Should().BeTrue();
        r.Cid.Should().Be("M54.5");
        r.CidVeioDaSessao.Should().BeTrue();

        r.TrocarTipo(TipoDocumentoClinico.Comparecimento);
        r.CidVeioDaSessao.Should().BeFalse("a marca do papel anterior mentiria sobre este");
        r.HoraChegadaTexto = "09:10";
        r.TrocarTipo(TipoDocumentoClinico.Receita);
        r.TrocarTipo(TipoDocumentoClinico.Comparecimento);
        r.HoraChegadaTexto.Should().Be("09:10");
    }

    [Fact]
    public void O_contexto_diz_a_sessao_de_onde_o_papel_vem()
        => Receita(Sessao()).Contexto.Should().Contain("da sessão de hoje, 14h00");

    // ==================== a montagem ====================

    [Fact]
    public void A_montagem_leva_a_procedencia_da_sessao()
    {
        var r = Receita(Sessao());
        r.Corpo = "Paracetamol";
        var d = r.MontarDocumento();
        d.AgendamentoId.Should().Be(33);
        d.EvolucaoId.Should().Be(44);
        d.Data.Should().Be(Hoje);
        d.PacienteId.Should().Be(10);
    }

    [Fact]
    public void Dias_de_afastamento_que_nao_sao_numero_recusam()
    {
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.Atestado, hoje: Hoje) { DiasAfastamentoTexto = "três" };
        r.Invoking(x => x.MontarDocumento()).Should().Throw<InvalidOperationException>()
            .WithMessage("Os dias de afastamento devem ser um número.");
        r.DiasAfastamentoTexto = "3";
        r.MontarDocumento().DiasAfastamento.Should().Be(3);
    }

    [Fact]
    public void O_CID_so_vai_no_atestado_e_no_laudo_e_o_periodo_so_onde_se_le()
    {
        var receita = Receita();
        receita.Cid = "M54.5";
        receita.PeriodoInicio = Hoje;
        var d = receita.MontarDocumento();
        d.Cid.Should().BeNull();
        d.PeriodoInicio.Should().BeNull();

        var atestado = new RascunhoDeDocumento(10, TipoDocumentoClinico.Atestado, hoje: Hoje) { Cid = "M54.5", CidAutorizado = true, PeriodoInicio = Hoje };
        var a = atestado.MontarDocumento();
        a.Cid.Should().Be("M54.5");
        a.CidAutorizado.Should().BeTrue();
        a.PeriodoInicio.Should().Be(Hoje);
    }

    [Fact]
    public void Hora_que_nao_e_hora_recusa_na_declaracao()
    {
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.Comparecimento, hoje: Hoje) { HoraChegadaTexto = "meio-dia" };
        r.Invoking(x => x.MontarDocumento()).Should().Throw<InvalidOperationException>().WithMessage("*chegada*14:30*");
        r.HoraChegadaTexto = "14:30";
        r.MontarDocumento().HoraChegada.Should().Be(new TimeOnly(14, 30));
    }

    [Fact]
    public void So_o_pedido_de_exame_leva_as_linhas_e_sem_as_vazias()
    {
        var pedido = new RascunhoDeDocumento(10, TipoDocumentoClinico.PedidoExame, hoje: Hoje);
        pedido.Itens[0].Descricao = "Hemograma";
        pedido.Itens[0].Quantidade = "1";
        pedido.AdicionarItem();
        pedido.MontarDocumento().Itens.Should().ContainSingle().Which.Descricao.Should().Be("Hemograma");

        var receita = Receita();
        receita.Itens[0].Descricao = "Linha antiga";
        receita.MontarDocumento().Itens.Should().BeEmpty();
    }

    [Fact]
    public void A_lista_de_exames_nunca_fica_sem_nenhuma_linha()
    {
        var pedido = new RascunhoDeDocumento(10, TipoDocumentoClinico.PedidoExame, hoje: Hoje);
        pedido.RemoverItem(pedido.Itens[0]);
        pedido.Itens.Should().ContainSingle();
    }

    [Fact]
    public void Editar_a_linha_descarta_a_formatacao_antiga()
    {
        var linha = new LinhaDoDocumento { Descricao = "Hemograma", DescricaoFormatada = "x", Detalhe = "A", DetalheFormatado = "y" };
        linha.Descricao = "Hemograma completo";
        linha.Detalhe = "B";
        linha.DescricaoFormatada.Should().BeNull();
        linha.DetalheFormatado.Should().BeNull();
    }

    // ==================== CID ====================

    [Fact]
    public void O_CID_sem_autorizacao_avisa_que_sera_omitido_e_o_fora_da_lista_e_dito()
    {
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.LaudoMedico, hoje: Hoje) { Cid = "Z99.999" };
        r.AvisaCidOmitido.Should().BeTrue();
        r.CidForaDaLista.Should().BeTrue();
        r.CidAutorizado = true;
        r.AvisaCidOmitido.Should().BeFalse();

        var vazio = new RascunhoDeDocumento(10, TipoDocumentoClinico.LaudoMedico, hoje: Hoje);
        vazio.CidForaDaLista.Should().BeFalse("com o campo vazio não há o que conferir");
    }

    // ==================== a conformidade legal (parcela 43) ====================

    [Fact]
    public void A_folha_nao_repete_o_art_13_nem_o_endereco_e_o_texto_da_receita()
    {
        var faltas = new[]
        {
            new ExigenciaLegal("Documento em arquivo exige assinatura qualificada", ConformidadeDocumentoClinico.Art13, PesoExigencia.Impeditiva),
            new ExigenciaLegal("Informe o endereço residencial do paciente", ConformidadeDocumentoClinico.Art35, PesoExigencia.Impeditiva),
            new ExigenciaLegal("Escreva a prescrição com o modo de usar", ConformidadeDocumentoClinico.Art35, PesoExigencia.Impeditiva),
            new ExigenciaLegal("Cadastre o registro do profissional", "outra", PesoExigencia.Impeditiva)
        };
        EmissaoDeDocumentoNaTela.ExigenciasDaFolha(faltas, receita: true).Should().ContainSingle()
            .Which.Should().StartWith("Cadastre o registro");
        EmissaoDeDocumentoNaTela.ExigenciasDaFolha(faltas, receita: false).Should().HaveCount(3);
    }

    [Fact]
    public void A_conferencia_legal_so_roda_depois_da_carga_e_reconfere_ao_escrever()
    {
        var r = Receita();
        r.Corpo = "Algo";
        r.ExigenciasLegais.Should().BeEmpty("a janela WPF não conferia enquanto carregava");
        r.LigarConferenciaLegal();
        r.Corpo = null;
        r.Corpo = "Paracetamol 750mg, 1 comprimido de 8/8h";
        r.ExigenciasLegais.Should().NotContain(e => e.Contains("endereço residencial"));
    }

    // ==================== modelos ====================

    [Fact]
    public void Na_receita_o_modelo_acrescenta_sem_perguntar()
    {
        var r = Receita();
        r.Corpo = "Texto do profissional";
        r.AplicarModeloPedeConfirmacao.Should().BeFalse();
        var frase = r.AplicarModelo(new ModeloDocumento { Nome = "Orientações", Tipo = TipoDocumentoClinico.Receita, Corpo = "Repouso" });
        r.Corpo.Should().StartWith("Texto do profissional").And.Contain("Repouso");
        frase.Should().Be("Modelo Orientações acrescentado. Revise antes de emitir.");
    }

    [Fact]
    public void Fora_da_receita_o_modelo_substitui_e_pergunta_quando_ha_o_que_perder()
    {
        var pedido = new RascunhoDeDocumento(10, TipoDocumentoClinico.PedidoExame, hoje: Hoje);
        pedido.AplicarModeloPedeConfirmacao.Should().BeFalse();
        pedido.Itens[0].Descricao = "Glicemia";
        pedido.AplicarModeloPedeConfirmacao.Should().BeTrue();

        var modelo = new ModeloDocumento { Nome = "Check-up", Titulo = "Exames de rotina", Corpo = "Em jejum" };
        modelo.Itens.Add(new ItemModelo { Descricao = "TSH", Ordem = 2, Id = 1 });
        modelo.Itens.Add(new ItemModelo { Descricao = "Hemograma", Ordem = 1, Id = 2 });
        pedido.AplicarModelo(modelo).Should().Be("Modelo \"Check-up\" aplicado.");
        pedido.Itens.Select(i => i.Descricao).Should().Equal("Hemograma", "TSH");
        pedido.Titulo.Should().Be("Exames de rotina");
        pedido.TituloFolha.Should().Be("EXAMES DE ROTINA");
    }

    [Fact]
    public void Guardar_como_modelo_leva_as_linhas_so_do_pedido()
    {
        var pedido = new RascunhoDeDocumento(10, TipoDocumentoClinico.PedidoExame, hoje: Hoje);
        pedido.Itens[0].Descricao = "Hemograma";
        pedido.MontarModelo("Rotina", 0).Itens.Should().ContainSingle();

        var receita = Receita();
        receita.Itens[0].Descricao = "Ignorada";
        receita.Corpo = "Texto";
        var m = receita.MontarModelo("Rx", 5);
        m.Itens.Should().BeEmpty();
        m.Id.Should().Be(5);
        m.Tipo.Should().Be(TipoDocumentoClinico.Receita);
    }

    [Fact]
    public void A_busca_de_modelos_ignora_caixa_e_acento()
    {
        var modelos = new[] { new ModeloDocumento { Nome = "Orientação pós-sessão" }, new ModeloDocumento { Nome = "Atestado padrão" } };
        EmissaoDeDocumentoNaTela.FiltrarModelos(modelos, "orientacao").Should().ContainSingle();
        EmissaoDeDocumentoNaTela.FiltrarModelos(modelos, "  ").Should().HaveCount(2);
    }

    // ==================== frases e nomes ====================

    [Fact]
    public void O_numero_so_nasce_na_emissao_e_o_titulo_segue_o_papel()
    {
        Receita().NumeroDaFolha.Should().Be("Nº gerado ao emitir");
        Receita().TituloFolha.Should().Be("RECEITA MÉDICA");
        EmissaoDeDocumentoNaTela.TituloDocumento(TipoDocumentoClinico.PedidoExame).Should().Be("Pedido de exames");
        EmissaoDeDocumentoNaTela.RotuloDoTexto(TipoDocumentoClinico.Atestado).Should().Be("Texto complementar · opcional");
    }

    [Fact]
    public void A_identificacao_pula_o_que_a_ficha_nao_tem()
    {
        var paciente = new Paciente { Nome = "Maria", Documento = "52998224725" };
        var ident = EmissaoDeDocumentoNaTela.Identificacao(paciente, Hoje);
        ident.Should().Contain("CPF/doc.").And.NotContain("anos");
        ident.Should().NotContain("·   ·");
        EmissaoDeDocumentoNaTela.Identificacao(null, Hoje).Should().BeEmpty();
    }

    [Fact]
    public void O_endereco_da_receita_diz_que_sera_pedido_quando_falta()
    {
        EmissaoDeDocumentoNaTela.EnderecoDaReceita(null).Should().Be("Endereço: será solicitado ao emitir.");
        EmissaoDeDocumentoNaTela.EnderecoDaReceita("Rua A, 1").Should().Be("Endereço: Rua A, 1");
    }

    [Fact]
    public void O_arquivo_nao_leva_barra_nem_caractere_proibido()
    {
        EmissaoDeDocumentoNaTela.NomeDoArquivo(TipoDocumentoClinico.LaudoMedico, "2026/0007")
            .Should().NotContain("/").And.EndWith("-2026-0007.pdf");
        EmissaoDeDocumentoNaTela.NomeDaPrevia(TipoDocumentoClinico.Receita).Should().StartWith("Previa-");
    }

    [Fact]
    public void Enviar_ao_paciente_so_e_oferecido_a_quem_tem_o_bit_e_ao_papel_que_sai_como_arquivo()
    {
        EmissaoDeDocumentoNaTela.OferecerEnvio(TipoDocumentoClinico.Receita, false, false, podeMexerNoTipo: false).Should().BeFalse();
        EmissaoDeDocumentoNaTela.OferecerEnvio(TipoDocumentoClinico.Receita, false, true, podeMexerNoTipo: true).Should().BeFalse();
        EmissaoDeDocumentoNaTela.OferecerEnvio(TipoDocumentoClinico.Receita, false, false, podeMexerNoTipo: true)
            .Should().Be(EnvioAoPacienteService.PodeEnviar(TipoDocumentoClinico.Receita, false, false));
    }

    [Fact]
    public void A_falha_de_impressao_diz_que_o_documento_ja_existe()
        => EmissaoDeDocumentoNaTela.FalhaDeImpressao("Não abriu.", "2026/0009")
            .Should().Be("Não abriu. O documento 2026/0009 foi emitido e está na ficha do paciente.");
}
