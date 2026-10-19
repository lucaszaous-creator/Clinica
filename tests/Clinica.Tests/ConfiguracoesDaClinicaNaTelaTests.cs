using Clinica.Application.Abstracoes;
using Clinica.Application.Email;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de CONFIGURAÇÕES do Gerente decide (Blazor desde out/2026, etapa 6) — antes no
/// <c>ConfiguracoesViewModel</c>, no <c>CamposPersonalizadosViewModel</c> e no
/// <c>ModelosTermoViewModel</c> do WPF. As frases de recusa são as MESMAS do WPF; a regra nova
/// é a dos SEGREDOS (vazio no Salvar = manter o gravado), que o WPF não tinha.
/// </summary>
public class ConfiguracoesDaClinicaNaTelaTests
{
    // ============================ Operação ============================

    [Fact]
    public void Operacao_valida_le_os_tres_campos_e_carimbadora_vazia_e_o_caso_normal()
    {
        var op = ConfiguracoesDaClinicaNaTela.LerOperacao(" 480 ", "60", "  ", out var erro);
        erro.Should().BeNull();
        op.Should().Be(new OperacaoDaClinica(480, 60, null));
    }

    [Theory]
    [InlineData("0", "60", null, "A jornada diária vai de 1 a 1440 minutos (8 h = 480).")]
    [InlineData("1441", "60", null, "A jornada diária vai de 1 a 1440 minutos (8 h = 480).")]
    [InlineData("480", "0", null, "Os dias de inatividade do recall vão de 1 a 3650.")]
    [InlineData("480", "abc", null, "Os dias de inatividade do recall vão de 1 a 3650.")]
    public void Operacao_fora_da_faixa_e_recusada_com_a_frase_do_WPF(string jornada, string recall, string? url, string esperado)
    {
        ConfiguracoesDaClinicaNaTela.LerOperacao(jornada, recall, url, out var erro).Should().BeNull();
        erro.Should().Be(esperado);
    }

    [Fact]
    public void Carimbadora_escrita_errada_e_recusada_antes_de_gravar()
    {
        ConfiguracoesDaClinicaNaTela.LerOperacao("480", "60", "act.exemplo/tsa", out var erro).Should().BeNull();
        erro.Should().StartWith("O endereço da carimbadora de tempo precisa ser uma URL completa");
    }

    [Fact]
    public void A_guia_no_agendamento_so_e_regravada_quando_muda_e_so_ligar_pede_confirmacao()
    {
        ConfiguracoesDaClinicaNaTela.GravarGuiaNoAgendamento(true, true).Should().BeFalse("ligar de novo repetiria o backfill");
        ConfiguracoesDaClinicaNaTela.GravarGuiaNoAgendamento(false, true).Should().BeTrue();
        ConfiguracoesDaClinicaNaTela.PedeConfirmacaoAoLigarGuia(true, false).Should().BeTrue();
        ConfiguracoesDaClinicaNaTela.PedeConfirmacaoAoLigarGuia(false, true).Should().BeFalse("desligar não depende da ordem de atualização");
        ConfiguracoesDaClinicaNaTela.PedeConfirmacaoAoLigarGuia(true, true).Should().BeFalse();
    }

    // ============================ Publicação ============================

    private const string Endpoint = "https://s3.exemplo.com.br";

    [Fact]
    public void Publicacao_os_tres_estados_sao_frases_diferentes()
    {
        ConfiguracoesDaClinicaNaTela.DescreverPublicacao(null, true, 30).Should().StartWith("Publicação DESLIGADA");
        ConfiguracoesDaClinicaNaTela.DescreverPublicacao("https://doc.clinica.com.br", false, 30)
            .Should().StartWith("Domínio cadastrado, mas SEM armazenamento");
        ConfiguracoesDaClinicaNaTela.DescreverPublicacao("https://doc.clinica.com.br", true, 45)
            .Should().Be("Publicação LIGADA em https://doc.clinica.com.br. O documento assinado fica no ar por 45 dias e o farmacêutico o abre escaneando o QR.");
    }

    [Fact]
    public void Armazenamento_pela_metade_e_o_mesmo_que_nenhum()
    {
        ConfiguracoesDaClinicaNaTela.TemArmazenamento(false, Endpoint, null, "balde", "chave", null).Should().BeFalse();
        ConfiguracoesDaClinicaNaTela.TemArmazenamento(false, Endpoint, null, "balde", "chave", "segredo").Should().BeTrue();
        ConfiguracoesDaClinicaNaTela.TemArmazenamento(true, null, null, null, null, null).Should().BeTrue("vindo do ambiente já está provado");
    }

    [Fact]
    public void Dominio_sem_https_e_recusado()
    {
        ConfiguracoesDaClinicaNaTela.LerPublicacao("http://doc.clinica.com.br", "30", null, null, null, null, null, null, false, out var erro)
            .Should().BeNull();
        erro.Should().StartWith("O domínio de publicação precisa ser um endereço https completo");
    }

    [Fact]
    public void Dias_no_ar_fora_da_faixa_sao_recusados()
    {
        ConfiguracoesDaClinicaNaTela.LerPublicacao(null, "366", null, null, null, null, null, null, false, out var erro).Should().BeNull();
        erro.Should().Be("Os dias no ar vão de 1 a 365.");
    }

    [Fact]
    public void Chave_secreta_vazia_MANTEM_a_gravada()
    {
        var pub = ConfiguracoesDaClinicaNaTela.LerPublicacao("https://doc.clinica.com.br", "30", Endpoint, null, "balde", "chave",
            segredoDigitado: "", segredoGravado: "GRAVADO", vemDoAmbiente: false, out var erro);
        erro.Should().BeNull();
        pub!.Segredo.Should().Be("GRAVADO");
        pub.GravarCredenciais.Should().BeTrue();
    }

    [Fact]
    public void Chave_secreta_digitada_troca_a_gravada()
    {
        var pub = ConfiguracoesDaClinicaNaTela.LerPublicacao(null, "30", Endpoint, "sa-east-1", "balde", "chave",
            segredoDigitado: " NOVA ", segredoGravado: "GRAVADO", vemDoAmbiente: false, out _);
        pub!.Segredo.Should().Be("NOVA");
        pub.Regiao.Should().Be("sa-east-1");
    }

    [Fact]
    public void Os_quatro_em_branco_DESLIGAM_e_levam_a_chave_secreta_gravada_junto()
    {
        var pub = ConfiguracoesDaClinicaNaTela.LerPublicacao(null, "30", " ", null, "", null,
            segredoDigitado: "", segredoGravado: "GRAVADO", vemDoAmbiente: false, out var erro);
        erro.Should().BeNull();
        pub!.Segredo.Should().BeNull("deixar os quatro em branco é desligar — manter o segredo ali seria meia credencial");
        pub.Endpoint.Should().BeNull();
    }

    [Fact]
    public void Credencial_pela_metade_e_recusada()
    {
        ConfiguracoesDaClinicaNaTela.LerPublicacao(null, "30", Endpoint, null, "balde", null,
            segredoDigitado: "", segredoGravado: null, vemDoAmbiente: false, out var erro).Should().BeNull();
        erro.Should().Be("O armazenamento precisa de endpoint, balde, chave e segredo. Preencha os quatro, ou deixe os quatro em branco.");
    }

    [Fact]
    public void Credencial_vinda_do_ambiente_nao_e_tocada()
    {
        var pub = ConfiguracoesDaClinicaNaTela.LerPublicacao(null, "30", null, null, null, null, null, null, vemDoAmbiente: true, out _);
        pub!.GravarCredenciais.Should().BeFalse();
        ConfiguracoesDaClinicaNaTela.FrasePublicacaoSalva(true).Should().Contain("variável de ambiente");
    }

    // ============================ E-mail ============================

    [Fact]
    public void Email_desligado_e_ligado_sao_frases_diferentes_e_a_ligada_diz_de_onde_sai()
    {
        ConfiguracoesDaClinicaNaTela.DescreverEmail(null).Should().StartWith("Lembrete por e-mail DESLIGADO");
        var opcoes = OpcoesEmail.De("smtp.exemplo.com.br", "587", "u", "s", "contato@clinica.com.br", null, true)!;
        ConfiguracoesDaClinicaNaTela.DescreverEmail(opcoes).Should().EndWith("Sai de contato@clinica.com.br por smtp.exemplo.com.br:587 (TLS).");
    }

    [Fact]
    public void Senha_vazia_MANTEM_a_gravada_e_sem_usuario_ela_SAI()
    {
        var manter = ConfiguracoesDaClinicaNaTela.LerEmail("smtp.exemplo.com.br", "587", "usuario", "", "GRAVADA",
            "contato@clinica.com.br", null, true, out var erro, out var desligando);
        erro.Should().BeNull();
        desligando.Should().BeFalse();
        manter!.Senha.Should().Be("GRAVADA");

        var trocar = ConfiguracoesDaClinicaNaTela.LerEmail("smtp.exemplo.com.br", "587", "usuario", " nova ", "GRAVADA",
            "contato@clinica.com.br", null, true, out _, out _);
        trocar!.Senha.Should().Be(" nova ", "espaço no fim de uma senha é parte dela");

        var semUsuario = ConfiguracoesDaClinicaNaTela.LerEmail("relay.interno", null, " ", "", "GRAVADA",
            "contato@clinica.com.br", null, false, out _, out _);
        semUsuario!.Senha.Should().BeNull("servidor sem autenticação não usa senha — é assim que a gravada se apaga");
    }

    [Fact]
    public void Servidor_e_remetente_em_branco_desligam_o_lembrete()
    {
        var c = ConfiguracoesDaClinicaNaTela.LerEmail(null, "587", null, null, null, "", "Clínica", true, out var erro, out var desligando);
        erro.Should().BeNull();
        desligando.Should().BeTrue();
        c!.Host.Should().BeNull();
        c.Remetente.Should().BeNull();
        ConfiguracoesDaClinicaNaTela.FraseEmailSalvo(true).Should().StartWith("Lembrete por e-mail desligado");
    }

    [Theory]
    [InlineData("smtp.exemplo.com.br", null, "587", "Informe o servidor de saída E o remetente — ou deixe os dois em branco para desligar o lembrete.")]
    [InlineData("smtp.exemplo.com.br", "nao-e-email", "587", "O remetente precisa ser um e-mail válido (ex.: contato@suaclinica.com.br).")]
    [InlineData("smtp.exemplo.com.br", "contato@clinica.com.br", "70000", "A porta precisa ser um número de 1 a 65535 — 587 é a mais comum; 465 em alguns provedores.")]
    public void Email_recusas_com_a_frase_do_WPF(string host, string? remetente, string porta, string esperado)
    {
        ConfiguracoesDaClinicaNaTela.LerEmail(host, porta, null, null, null, remetente, null, true, out var erro, out _).Should().BeNull();
        erro.Should().Be(esperado);
    }

    // ============================ Conclusão ============================

    [Theory]
    [InlineData("24", 24)]
    [InlineData(" 720 ", 720)]
    public void Prazo_de_conclusao_valido(string texto, int horas)
        => ConfiguracoesDaClinicaNaTela.LerPrazoConclusao(texto, out _).Should().Be(horas);

    [Theory]
    [InlineData("0")]
    [InlineData("721")]
    [InlineData("1,5")]
    public void Prazo_de_conclusao_fora_da_faixa_e_recusado_antes_do_servico(string texto)
    {
        ConfiguracoesDaClinicaNaTela.LerPrazoConclusao(texto, out var erro).Should().BeNull();
        erro.Should().Be("Informe o prazo em horas inteiras, de 1 a 720.");
    }

    [Fact]
    public void Resumo_das_pendencias_de_conclusao()
    {
        ConfiguracoesDaClinicaNaTela.ResumoPendenciasConclusao(0).Should().Be("Nenhuma sessão com prazo vencido pendente de conclusão.");
        ConfiguracoesDaClinicaNaTela.ResumoPendenciasConclusao(3).Should().StartWith("3 sessão(ões) com prazo vencido.");
        ConfiguracoesDaClinicaNaTela.NomeDaModalidadeDeEnfermagem("BSV", false).Should().Be("BSV (inativa)");
    }

    // ============================ Backup ============================

    [Fact]
    public void Politica_de_backup_le_inteiros_de_1_em_diante()
    {
        ConfiguracoesDaClinicaNaTela.LerPoliticaBackup("7", "8", out var erro).Should().Be((7, 8));
        erro.Should().BeNull();
        ConfiguracoesDaClinicaNaTela.LerPoliticaBackup("0", "8", out erro).Should().BeNull();
        erro.Should().NotBeNull();
        ConfiguracoesDaClinicaNaTela.LerPoliticaBackup("7", "x", out _).Should().BeNull();
    }

    [Fact]
    public void Backup_vazio_e_escrito_como_inutil_e_o_cheio_conta_o_que_tem()
    {
        var vazio = new ResumoDoBackup(new DateTime(2026, 10, 1, 9, 0, 0), 60, 0, true, 0, 0, 0);
        ConfiguracoesDaClinicaNaTela.ResumoDaConferencia(vazio).Should().StartWith("ATENÇÃO: o arquivo abriu, tem 60 tabela(s) e NENHUM registro.");

        var cheio = new ResumoDoBackup(new DateTime(2026, 10, 1, 9, 5, 0), 60, 12345, false, 2238, 4287, 900);
        ConfiguracoesDaClinicaNaTela.ResumoDaConferencia(cheio)
            .Should().Be("Backup de 01/10/2026 09:05 — 12.345 registro(s) em 60 tabela(s). Pacientes: 2.238 · Evoluções: 4.287 · Lançamentos: 900.");
    }

    [Fact]
    public void Copia_agora_diz_onde_gravou_e_quantas_antigas_sairam_ou_por_que_falhou()
    {
        var ok = new ResultadoDaCopiaAgora(@"\\servidor\backup\b.json", new ResumoDoBackup(DateTime.Now, 50, 1500, false, 10, 20, 30), 2, null);
        ConfiguracoesDaClinicaNaTela.ResumoDaCopiaAgora(ok)
            .Should().Be(@"Cópia gravada em \\servidor\backup\b.json — 1.500 registro(s) em 50 tabela(s). 2 cópia(s) antiga(s) saíram da pasta.");
        ConfiguracoesDaClinicaNaTela.ResumoDaCopiaAgora(new ResultadoDaCopiaAgora(null, null, 0, "pasta de rede fora do ar"))
            .Should().Be("Não foi possível copiar: pasta de rede fora do ar");
        ConfiguracoesDaClinicaNaTela.NomeDoArquivoDeBackup(new DateOnly(2026, 10, 18)).Should().Be("backup-clinica-2026-10-18.json");
    }

    // ============================ Campos e termos ============================

    [Fact]
    public void Campo_sem_modalidade_aparece_em_todas_e_a_situacao_tem_nome()
    {
        ConfiguracoesDaClinicaNaTela.OndeApareceOCampo(null).Should().Be("todas as modalidades");
        ConfiguracoesDaClinicaNaTela.OndeApareceOCampo(" ").Should().Be("todas as modalidades");
        ConfiguracoesDaClinicaNaTela.SituacaoDoCampo(false).Should().Be("Desativado");
        foreach (var t in Enum.GetValues<Clinica.Domain.Entities.TipoCampoPersonalizado>())
            ConfiguracoesDaClinicaNaTela.RotuloDoTipoDeCampo(t).Should().NotBe(t.ToString().Length > 5 ? t.ToString() : "?", "nome de enum cru na tela é o defeito da parcela 41");
        ConfiguracoesDaClinicaNaTela.RotuloDoTipoDeCampo(Clinica.Domain.Entities.TipoCampoPersonalizado.Numero).Should().Be("Número");
        ConfiguracoesDaClinicaNaTela.LimiteDeCampos.Should().StartWith($"Até {CampoPersonalizadoService.MaximoAtivos} campos ativos");
    }

    [Fact]
    public void Termo_sem_nome_ou_sem_texto_e_recusado_e_a_exigencia_diz_a_validade()
    {
        ConfiguracoesDaClinicaNaTela.ConferirTermo(" ", "texto").Should().Be("Dê um nome ao termo (ex.: \"Termo do BSV\").");
        ConfiguracoesDaClinicaNaTela.ConferirTermo("Termo", " ").Should().Be("Escreva o texto do termo — é o que o paciente vai ler e assinar.");
        ConfiguracoesDaClinicaNaTela.ConferirTermo("Termo", "texto").Should().BeNull();

        ConfiguracoesDaClinicaNaTela.SituacaoDaExigencia(false, true).Should().Be("Desligado");
        ConfiguracoesDaClinicaNaTela.SituacaoDaExigencia(true, true).Should().Be("Exigido a cada sessão");
        ConfiguracoesDaClinicaNaTela.SituacaoDaExigencia(true, false).Should().Be("Exigido — vale a partir da assinatura");
        ConfiguracoesDaClinicaNaTela.AcaoDaExigencia(true).Should().Be("Desligar");
        ConfiguracoesDaClinicaNaTela.ResumoDasDeclaracoes(1).Should().Be("1 declaração");
        ConfiguracoesDaClinicaNaTela.ResumoDasDeclaracoes(3).Should().Be("3 declarações");
    }

    [Fact]
    public void Substituir_os_termos_do_BSV_lista_cada_termo_em_uso_uma_vez()
        => ConfiguracoesDaClinicaNaTela.PerguntaSubstituirTermosDoBsv(["TCLE", "TCLE", null])
            .Should().StartWith("O BSV já exige termo: \"TCLE\"; \"termo\".");

    [Fact]
    public void A_chave_pix_diz_como_foi_reconhecida()
    {
        ConfiguracoesDaClinicaNaTela.TipoDaChavePix(null).Should().StartWith("Sem chave cadastrada");
        ConfiguracoesDaClinicaNaTela.TipoDaChavePix("contato@clinica.com.br").Should().Be("Reconhecida como e-mail.");
    }
}
