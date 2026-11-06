using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;
using static Clinica.Tests.WhatsAppDeTeste;

namespace Clinica.Tests;

/// <summary>O formulário do modelo do WhatsApp (PR 2): as variáveis preenchidas pelo contexto e a prévia.</summary>
public sealed class ModeloDoWhatsAppTests
{
    private static AppTemplate Modelo(string nome, string texto, int parametros, bool aceito = true, string motivo = "")
        => new(nome, "pt_BR", "APPROVED", texto, parametros, aceito, motivo, [], "UTILITY");

    private static readonly AppTemplate Lembrete = Modelo("lembrete_sessao",
        "Olá, {{1}}! Lembrando que você tem {{2}} na Clínica SemDor {{3}}. Responda SIM para confirmar ou NÃO se precisar remarcar.", 3);
    private static readonly AppTemplate Retorno = Modelo("retorno_tratamento",
        "Olá, {{1}}! Sentimos sua falta na {{2}}: já são {{3}} dias desde a última sessão. Quer que a gente veja um horário para você?", 3);
    private static readonly AppTemplate Primeiro = Modelo("primeiro_contato",
        "Olá, {{1}}! Aqui é a recepção da Clínica SemDor. Você pediu contato sobre {{2}}. Pode falar por aqui?", 2);
    private static readonly AppTemplate Documento = Modelo("documento_pronto",
        "Olá, {{1}}! O seu {{2}} está pronto. Pode retirar na recepção ou responder aqui que enviamos por mensagem.", 2);

    private static string[] Valores(AppTemplate t, ContextoDoModelo c) => ModeloDoWhatsApp.Sugestoes(t, c).Select(v => v.Valor).ToArray();

    [Fact]
    public void O_lembrete_vem_com_primeiro_nome_atendimento_e_dia_e_hora()
    {
        var c = new ContextoDoModelo("Maria das Graças Silva", Hoje, ModalidadeAtendimento.AcupunturaSimples, Hoje.ToDateTime(new TimeOnly(14, 0)));
        var variaveis = ModeloDoWhatsApp.Sugestoes(Lembrete, c);
        Assert.Equal(["Primeiro nome", "Atendimento", "Dia e hora"], variaveis.Select(v => v.Rotulo));
        Assert.Equal([1, 2, 3], variaveis.Select(v => v.Numero));
        Assert.Equal(["Maria", "acupuntura", "hoje às 14:00"], variaveis.Select(v => v.Valor));

        var bsv = c with { Atendimento = ModalidadeAtendimento.BsvApenas, Sessao = Hoje.AddDays(1).ToDateTime(new TimeOnly(9, 30)) };
        Assert.Equal(["Maria", "sessão de BSV", "amanhã às 09:30"], Valores(Lembrete, bsv));
    }

    [Fact]
    public void O_convite_de_retorno_vem_com_o_tratamento_do_caso_e_os_dias_sem_vir()
    {
        var bsv = new ContextoDoModelo("Sebastião Ramos", Hoje, Caso: Caso(dias: 32, modalidade: ModalidadeAtendimento.BsvApenas));
        Assert.Equal(["Sebastião", "sessão de BSV", "32"], Valores(Retorno, bsv));
        Assert.Equal("Olá, Sebastião! Sentimos sua falta na sessão de BSV: já são 32 dias desde a última sessão. Quer que a gente veja um horário para você?",
            ModeloDoWhatsApp.Previa(Retorno, Valores(Retorno, bsv)));

        var acupuntura = bsv with { Caso = Caso(dias: 20, modalidade: ModalidadeAtendimento.AcupunturaComEletro) };
        Assert.Equal(["Sebastião", "acupuntura", "20"], Valores(Retorno, acupuntura));
        var osDois = bsv with { Caso = Caso(dias: 21, modalidade: ModalidadeAtendimento.BsvComAcupuntura) };
        Assert.Equal("sessão de BSV e acupuntura", Valores(Retorno, osDois)[1]);
    }

    [Fact]
    public void Os_dias_so_vem_do_recall_e_o_novo_no_BSV_ganha_o_assunto_da_primeira_sessao()
    {
        var novo = new ContextoDoModelo("Felipe Arantes", Hoje, Caso: Caso(tipo: TipoAcompanhamento.NovoBsv, dias: 3));
        Assert.Equal(["Felipe", "sessão de BSV", ""], Valores(Retorno, novo)); // "dias desde a última sessão" não é a indicação
        Assert.Equal(["Felipe", "o agendamento da sua primeira sessão de BSV"], Valores(Primeiro, novo));
        Assert.Equal(["Felipe", ""], Valores(Primeiro, novo with { Caso = Caso() })); // sem assunto conhecido, a pessoa escreve
    }

    [Fact]
    public void Nada_e_inventado_sem_contexto()
    {
        var vazio = new ContextoDoModelo("(22) 99712-0045", Hoje);
        Assert.Equal(["", "", ""], Valores(Lembrete, vazio));
        Assert.Equal(["", "", ""], Valores(Retorno, vazio));
        Assert.Equal(["", ""], Valores(Documento, vazio));
        Assert.Equal(["", "atestado"], Valores(Documento, vazio with { Documento = " atestado " }));
    }

    [Fact]
    public void Modelo_desconhecido_ou_com_outra_quantidade_de_variaveis_sai_vazio_para_a_pessoa_preencher()
    {
        var desconhecido = Modelo("aviso_feriado", "Olá, {{1}}! No dia {{2}} não abrimos.", 2);
        var c = new ContextoDoModelo("Maria", Hoje, ModalidadeAtendimento.AcupunturaSimples, Hoje.ToDateTime(new TimeOnly(14, 0)));
        Assert.Equal([("Variável 1", ""), ("Variável 2", "")], ModeloDoWhatsApp.Sugestoes(desconhecido, c).Select(v => (v.Rotulo, v.Valor)));
        Assert.Null(ModeloDoWhatsApp.Rotulos(desconhecido));

        var lembreteNovo = Lembrete with { Parameters = 4 }; // a Meta aprovou outra versão: a tabela ficou velha
        Assert.Null(ModeloDoWhatsApp.Rotulos(lembreteNovo));
        Assert.All(ModeloDoWhatsApp.Sugestoes(lembreteNovo, c), v => Assert.Equal("", v.Valor));
        Assert.Empty(ModeloDoWhatsApp.Sugestoes(Modelo("sem_variavel", "Bom dia!", 0), c));
    }

    [Fact]
    public void Os_modelos_que_o_CRM_ja_tem_no_codigo_tambem_sao_conhecidos()
    {
        var c = new ContextoDoModelo("João Batista", Hoje, Sessao: Hoje.ToDateTime(new TimeOnly(15, 0)));
        Assert.Equal(["João"], Valores(Modelo("recall_semdor", "Olá, {{1}}! Aqui é da Clínica SemDor.", 1), c));
        Assert.Equal(["João", "hoje às 15:00"], Valores(Modelo("semdor_confirmacao_24h", "Olá, {{1}}! … agendada para {{2}}.", 2), c));
    }

    [Fact]
    public void A_previa_aplica_os_valores_e_mostra_o_que_falta()
    {
        Assert.Equal("Olá, Maria! O seu {{2}} está pronto. Pode retirar na recepção ou responder aqui que enviamos por mensagem.",
            ModeloDoWhatsApp.Previa(Documento, ["Maria", "  "]));
        var trechos = ModeloDoWhatsApp.Trechos(Documento, ["Maria", "atestado"]);
        Assert.Equal([("Olá, ", false), ("Maria", true), ("! O seu ", false), ("atestado", true),
            (" está pronto. Pode retirar na recepção ou responder aqui que enviamos por mensagem.", false)], trechos.Select(t => (t.Texto, t.Variavel)));
        Assert.Equal("Olá, {{1}}!", ModeloDoWhatsApp.Previa(Modelo("x", "Olá, {{1}}!", 1), []));
    }

    [Fact]
    public void O_envio_espera_o_modelo_aceito_e_todas_as_variaveis()
    {
        var c = new ContextoDoModelo("Sebastião Ramos", Hoje, Caso: Caso(dias: 32));
        var variaveis = ModeloDoWhatsApp.Sugestoes(Retorno, c);
        Assert.Null(ModeloDoWhatsApp.Impedimento(Retorno, variaveis, ["Sebastião", "sessão de BSV", "32"]));
        Assert.Equal("Preencha o campo \"Dias\".", ModeloDoWhatsApp.Impedimento(Retorno, variaveis, ["Sebastião", "sessão de BSV", " "]));
        Assert.Equal("Preencha o campo \"Dias\".", ModeloDoWhatsApp.Impedimento(Retorno, variaveis, ["Sebastião", "sessão de BSV"]));
        var recusado = Retorno with { Supported = false, Reason = "Aguardando aprovação ou indisponível" };
        Assert.Equal("Aguardando aprovação ou indisponível", ModeloDoWhatsApp.Impedimento(recusado, variaveis, ["a", "b", "c"]));
        Assert.Equal("Este modelo não pode ser enviado por aqui.",
            ModeloDoWhatsApp.Impedimento(recusado with { Reason = "" }, variaveis, ["a", "b", "c"]));
    }

    [Fact]
    public void A_selecao_que_vai_ao_CRM_leva_nome_idioma_e_valores_sem_espaco()
    {
        var s = ModeloDoWhatsApp.Selecao(Retorno, [" Sebastião ", "sessão de BSV", "32", "sobra"]);
        Assert.Equal(("retorno_tratamento", "pt_BR"), (s.Name, s.Language));
        Assert.Equal(["Sebastião", "sessão de BSV", "32"], s.Parameters);
        Assert.Null(s.ButtonPayloads);
    }

    [Fact]
    public void Os_modelos_aprovados_na_Meta_tem_rotulos_e_valores_do_contexto()
    {
        var hoje14 = Hoje.ToDateTime(new TimeOnly(14, 0));
        var c = new ContextoDoModelo("Maria das Graças Silva", Hoje, ModalidadeAtendimento.AcupunturaSimples, hoje14);
        // O lembrete do dia e o remarcado: primeiro nome e o horário por extenso.
        var lembrete = Modelo("semdor_lembrete_sessao", "Olá, {{1}}! Sua sessão é {{2}}.", 2);
        Assert.Equal(["Primeiro nome", "Dia e hora"], ModeloDoWhatsApp.Rotulos(lembrete));
        Assert.Equal("Maria", Valores(lembrete, c)[0]);
        Assert.False(string.IsNullOrEmpty(Valores(lembrete, c)[1]));
        var remarcado = Modelo("semdor_horario_remarcado", "Olá, {{1}}! Seu novo horário é {{2}}.", 2);
        Assert.Equal(Valores(lembrete, c)[1], Valores(remarcado, c)[1]);
        // O que só a recepção sabe fica vazio, com o rótulo do CRM.
        var orcamento = Modelo("semdor_orcamento", "Olá, {{1}}! {{2}}", 2);
        Assert.Equal(["Primeiro nome", "Orçamento"], ModeloDoWhatsApp.Rotulos(orcamento));
        Assert.Equal("", Valores(orcamento, c)[1]);
        // O convite de retorno aprovado tem só o primeiro nome.
        var recall = Modelo(ModeloDoWhatsApp.RecallReal, "Olá, {{1}}! Sentimos sua falta.", 1);
        Assert.Equal(["Sebastião"], Valores(recall, new ContextoDoModelo("Sebastião Ramos", Hoje)));
    }
}
