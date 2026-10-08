using Clinica.Desktop.Shell.Web;
using P = Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Recepcao.Web;
public static partial class RecepcaoWebRegistro
{
    public static P.Secao[] SecoesAdministrativas(string prefixo = "Administrativo.DadosWeb")
    {
        var raiz = string.IsNullOrEmpty(prefixo) ? "" : prefixo + ".";
        P.Campo C(P.Campo c) => c with { Propriedade = raiz + c.Propriedade, Opcoes = c.Opcoes is null ? null : raiz + c.Opcoes, Guarda = Prefixar(c.Guarda), Visivel = Prefixar(c.Visivel) };
        string? Prefixar(string? v) => v is null ? null : string.Join("&", v.Split('&').Select(p => (p.StartsWith("!") ? "!" : "") + raiz + p.TrimStart('!')));
        P.Acao A(P.Acao a) => a with { Comando = raiz + a.Comando, Guarda = Prefixar(a.Guarda), Visivel = Prefixar(a.Visivel) };
        P.Acao Linha(P.Acao a) => a with { Comando = raiz + a.Comando };
        return [
            new P.Secao("ResumoAdministrativoPaciente", "Resumo administrativo", null, [], [], [
                PT(raiz + "Alertas", [new P.Coluna("Descricao", "Descricao")], [], [Linha(new P.Acao("ReceberDivida", "Receber…", Guarda: null, SemParametro: true, Visivel: "PodeReceber", Parametro: null))], Prefixar(null)),
                PT(raiz + "ProximosHorarios", [new P.Coluna("Rotulo", "Rotulo"), new P.Coluna("Contexto", "Contexto")], [], [Linha(new P.Acao("AbrirNaAgenda", "Abrir na agenda", Guarda: null, SemParametro: false, Visivel: null, Parametro: null))], Prefixar("PodeVerAgenda")),
            ], []),
            new P.Secao("ConvenioPaciente", "Convênio e autorizações", null, [C(new P.Campo("ValidadeConsulta", "Validade Consulta", "leitura", Visivel: null))], [], [
                PT(raiz + "Autorizacoes", [new P.Coluna("Autorizacao.Numero", "Numero"), new P.Coluna("Resumo", "Resumo"), new P.Coluna("Autorizacao.DataEmissao", "Data Emissao"), new P.Coluna("Autorizacao.DataValidade", "Data Validade")], [], [Linha(new P.Acao("EditarAutorizacao", "Editar", Guarda: null, SemParametro: false, Visivel: null, Parametro: null)), Linha(new P.Acao("ExcluirAutorizacao", "Excluir", Guarda: null, SemParametro: false, Visivel: null, Parametro: null))], Prefixar(null)),
            ], [A(new P.Acao("RenovarConsulta", "Renovar validade da consulta", Guarda: "PodeRenovarConsulta", SemParametro: false, Visivel: "UsaConsultaRenovavel", Parametro: null)), A(new P.Acao("NovaAutorizacao", "Nova autorização", Guarda: "PodeEditarCadastro", SemParametro: false, Visivel: null, Parametro: null))]),
            new P.Secao("RelacionamentoPaciente", "Relacionamento", null, [C(new P.Campo("Origem", "ORIGEM", "leitura", Visivel: null))], [], [
                PT(raiz + "Contatos", [new P.Coluna("Data", "Data"), new P.Coluna("Tipo", "Tipo"), new P.Coluna("Detalhe", "Detalhe"), new P.Coluna("Situacao", "Situacao")], [], [], Prefixar(null)),
            ], []),
            new P.Secao("PrivacidadePaciente", "Privacidade", null, [C(new P.Campo("TermoLgpdSituacao", "Termo de consentimento assinado pelo paciente", "leitura", Visivel: null))], [], [
                PT(raiz + "Consentimentos", [new P.Coluna("Rotulo", "Rotulo"), new P.Coluna("Situacao", "Situacao")], [], [Linha(new P.Acao("Revogar", "Revogar", Guarda: null, SemParametro: false, Visivel: null, Parametro: null))], Prefixar(null)),
            ], [A(new P.Acao("ColherTermoLgpd", "Colher assinatura…", Guarda: "PodeColherTermoLgpd", SemParametro: false, Visivel: null, Parametro: null)), A(new P.Acao("ExportarDados", "Exportar meus dados", Guarda: "PodeVerProntuario", SemParametro: false, Visivel: null, Parametro: null)), A(new P.Acao("Anonimizar", "Anonimizar cadastro", Guarda: "PodeAnonimizar", SemParametro: false, Visivel: null, Parametro: null))]),
            new P.Secao("TermosPaciente", "Termos", null, [C(new P.Campo("ResumoTermos", "Resumo Termos", "leitura", Visivel: "TemTermoDoDia"))], [], [
                PT(raiz + "Termos", [new P.Coluna("Nome", "Nome"), new P.Coluna("Procedimento", "Procedimento"), new P.Coluna("Situacao", "Situacao")], [], [Linha(new P.Acao("ColherTermo", "Colher assinatura…", Guarda: "PodeColher", SemParametro: false, Visivel: null, Parametro: null))], Prefixar("TemTermoDoDia")),
            ], [A(new P.Acao("ColherTermoAvulso", "Colher um termo…", Guarda: null, SemParametro: false, Visivel: "TemTermoDoDia", Parametro: null))]),
        ];
    }
}
