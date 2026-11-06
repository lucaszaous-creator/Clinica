using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;
using static Clinica.Tests.WhatsAppDeTeste;

namespace Clinica.Tests;

/// <summary>
/// A lista dos modelos na tela (25/10/2026, item 1 da revisão "está profissional?"): o nome que a recepção
/// lê (o técnico só na dica), as variáveis pelo nome, a ordem do que serve agora e os botões do modelo.
/// </summary>
public sealed class ModeloDoWhatsAppNaListaTests
{
    private static AppTemplate Modelo(string nome, string texto, int parametros, bool aceito = true, string[]? botoes = null)
        => new(nome, "pt_BR", "APPROVED", texto, parametros, aceito, aceito ? "" : "Modelo pausado pela Meta.", botoes ?? [], "UTILITY");

    private static readonly AppTemplate Confirmacao = Modelo("semdor_confirmacao_24h",
        "Olá, {{1}}! Sua consulta na Clínica SemDor está agendada para {{2}}. Confirme sua presença.", 2, botoes: ["Confirmar", "Remarcar", "Cancelar"]);
    private static readonly AppTemplate Recall = Modelo("semdor_recall", "Olá, {{1}}! Sentimos sua falta.", 1);
    private static readonly AppTemplate Documento = Modelo("documento_pronto", "Olá, {{1}}! O seu {{2}} está pronto.", 2);
    private static readonly AppTemplate Lembrete = Modelo("semdor_lembrete_sessao", "Olá, {{1}}! Lembrando do seu horário {{2}}.", 2);
    private static readonly AppTemplate Orcamento = Modelo("semdor_orcamento", "Olá, {{1}}! Segue o orçamento: {{2}}.", 2, aceito: false);

    [Fact]
    public void O_nome_na_tela_e_o_da_clinica_e_o_desconhecido_nunca_sai_cru()
    {
        Assert.Equal("Confirmação da consulta", ModeloDoWhatsApp.NomeNaTela("semdor_confirmacao_24h"));
        Assert.Equal("Lembrete da sessão", ModeloDoWhatsApp.NomeNaTela("SEMDOR_LEMBRETE_SESSAO"));
        Assert.Equal("Convite para voltar", ModeloDoWhatsApp.NomeNaTela(ModeloDoWhatsApp.RecallReal));
        Assert.Equal("Aviso de feriado", ModeloDoWhatsApp.NomeNaTela("semdor_aviso_de_feriado"));
        Assert.Equal("Modelo aprovado", ModeloDoWhatsApp.NomeNaTela("  "));
        // Os três convites de retorno não podem se chamar igual na mesma lista.
        var convites = new[] { ModeloDoWhatsApp.RecallReal, ModeloDoWhatsApp.RetornoTratamento, "recall_semdor" }.Select(ModeloDoWhatsApp.NomeNaTela);
        Assert.Equal(3, convites.Distinct().Count());
        // Nenhum par de modelos da mesma lista (a da prévia: os de produção e os do mockup) com o mesmo nome, nenhum com "_".
        var nomes = PreviaBlazor.CrmDeMentira.Modelos.Select(m => ModeloDoWhatsApp.NomeNaTela(m.Name)).ToList();
        Assert.Equal(nomes.Count, nomes.Distinct().Count());
        Assert.DoesNotContain(nomes, n => n.Contains('_'));
    }

    [Fact]
    public void O_texto_da_lista_mostra_as_variaveis_pelo_nome_como_o_formulario()
    {
        Assert.Equal("Olá, [nome]! Sua consulta na Clínica SemDor está agendada para [data]. Confirme sua presença.",
            ModeloDoWhatsApp.TextoNaLista(Confirmacao));
        Assert.Equal("Olá, [nome]! O seu [documento] está pronto.", ModeloDoWhatsApp.TextoNaLista(Documento));
        // Desconhecido (ou com outra quantidade de variáveis que a tabela): a variável pelo número, nunca {{n}}.
        var novo = Modelo("semdor_aviso_feriado", "Olá, {{1}}! Fechados em {{2}}.", 2);
        Assert.Equal("Olá, [variável 1]! Fechados em [variável 2].", ModeloDoWhatsApp.TextoNaLista(novo));
        Assert.DoesNotContain("{{", ModeloDoWhatsApp.TextoNaLista(Modelo("semdor_recall", "Olá, {{1}}! {{2}}", 2)));
    }

    [Fact]
    public void Com_horario_hoje_a_confirmacao_vem_primeiro()
    {
        var hoje = new ContextoDoModelo("Carlos Eduardo Mota", Hoje, ModalidadeAtendimento.BsvApenas, Hoje.ToDateTime(new TimeOnly(16, 0)));
        var ordem = ModeloDoWhatsApp.Ordenar([Documento, Orcamento, Recall, Confirmacao, Lembrete], hoje).Select(m => m.Name).ToList();
        Assert.Equal(["semdor_confirmacao_24h", "semdor_lembrete_sessao"], ordem.Take(2).Order());
        Assert.Equal("semdor_orcamento", ordem[^1]); // o que não pode ser enviado vai para o fim

        // Sem horário hoje, a ordem é a do nome que a recepção lê.
        var semHorario = hoje with { Sessao = Hoje.AddDays(3).ToDateTime(new TimeOnly(9, 0)) };
        Assert.Equal(["Confirmação da consulta", "Convite para voltar", "Documento pronto", "Lembrete da sessão", "Orçamento"],
            ModeloDoWhatsApp.Ordenar([Documento, Orcamento, Recall, Confirmacao, Lembrete], semHorario).Select(m => ModeloDoWhatsApp.NomeNaTela(m.Name)));
    }

    [Fact]
    public void Na_retencao_o_convite_vem_primeiro()
    {
        var retencao = new ContextoDoModelo("Sebastião Ramos", Hoje, Caso: Caso());
        Assert.Equal("semdor_recall", ModeloDoWhatsApp.Ordenar([Confirmacao, Documento, Recall], retencao)[0].Name);
        // O caso fechado não é retenção.
        var fechado = retencao with { Caso = Caso(pendente: false) };
        Assert.Equal("semdor_confirmacao_24h", ModeloDoWhatsApp.Ordenar([Documento, Recall, Confirmacao], fechado)[0].Name);
    }

    [Fact]
    public void Os_botoes_sao_os_do_modelo_por_que_a_mensagem_saiu()
    {
        Assert.Equal(["Confirmar", "Remarcar", "Cancelar"], ModeloDoWhatsApp.Botoes("SEMDOR_CONFIRMACAO_24H", [Recall, Confirmacao]));
        Assert.Empty(ModeloDoWhatsApp.Botoes("semdor_recall", [Recall, Confirmacao]));
        Assert.Empty(ModeloDoWhatsApp.Botoes("semdor_confirmacao_24h", null)); // a lista ainda não veio
        Assert.Empty(ModeloDoWhatsApp.Botoes(null, [Confirmacao]));
    }
}
