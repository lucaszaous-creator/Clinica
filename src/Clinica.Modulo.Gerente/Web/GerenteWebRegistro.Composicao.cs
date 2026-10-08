using System.Globalization;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Web;
using Clinica.Gerente.ViewModels;
using static Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Gerente.Web;

public static partial class GerenteWebRegistro
{
    public static IEnumerable<Pagina> CriarPaginas()=>CriarPaginasBase().Select(Organizar);
    private static Pagina Organizar(Pagina p)
    {
        if (p.Chave is "auditoria" or "guarda-prontuario")
        {
            // A extração do ListBox preservou a busca, mas perdeu seu SelectedItem.
            // A mesma seleção aciona os eventos originais de filtro/guarda no C#.
            var campos = p.Campos.Select(c => c.Propriedade == "Paciente.Termo"
                ? c with { Rotulo = "Buscar paciente por nome ou CPF" } : c).ToList();
            var indice = campos.FindIndex(c => c.Propriedade == "Paciente.Termo");
            campos.Insert(indice + 1, new Campo("Paciente.Selecionado", "Paciente", "selecao", "Paciente.Resultados", "Nome"));
            p = p with { Campos = campos.ToArray(), Secoes = p.Secoes.Select(s => s with
            {
                Tabelas = s.Tabelas.Select(t => t.Chave == "Paciente.Resultados"
                    ? t with { Titulo = "Pacientes encontrados", Colunas = [new("Nome", "Paciente"), new("Documento", "CPF"), new("ConvenioNome", "Convênio")] }
                    : t).ToArray()
            }).ToArray() };
        }
        string[] destaques=p.Chave switch
        {
            "painel-direcao"=>["EntradasMes|Entradas no mês|VariacaoEntradas","SaidasMes|Saídas no mês","SaldoMes|Saldo do mês|DeducoesMes","ContasVencidas|Contas vencidas|ContasVencidasDetalhe","PendenciasFaturamento|Pendências de faturamento|PendenciasDetalhe","AReceberPrevisto|A receber|AReceberDetalhe","DepositoAtrasado|Depósitos atrasados|DepositoAtrasadoDetalhe"],
            "indicadores"=>["Atendidos|Atendimentos|VariacaoAtendidos","NoShowFormatado|Faltas|VariacaoNoShow","OcupacaoFormatada|Ocupação","NpsFormatado|NPS|NpsDetalhe"],
            "custo-transacao"=>["Bruto|Receita bruta","Taxa|Taxas","Imposto|Impostos","Liquido|Receita líquida"],
            "documentos-emitidos"=>["Emitidos|Emitidos","Assinados|Assinados","Cancelados|Cancelados","NoAr|Publicados"],
            _=>[]
        };
        var nomes=destaques.SelectMany(i=>i.Split('|').Where((_,n)=>n!=1)).ToHashSet();
        p=p with {Campos=p.Campos.Where(c=>!nomes.Contains(c.Propriedade)&&c.Propriedade!="Mensagem").ToArray(),Indicadores=destaques,
            Graficos=(vm,secao)=>secao=="principal"?GraficosGerente(vm):[]};
        if(p.Chave=="configuracoes")p=ConfiguracaoOrganizada(p);
        if(p.Chave=="painel-direcao")p=p with{Subtitulo="DataFormatada",Campos=p.Campos.Where(c=>c.Propriedade is not ("DataFormatada" or "Saudacao")).ToArray()};
        return p with{Campos=p.Campos.Select(Rotular).ToArray(),Acoes=p.Acoes.Select(a=>a with{Rotulo=Humanizar(a.Rotulo)}).ToArray(),Secoes=p.Secoes.Select(s=>s with{
            Campos=s.Campos.Select(Rotular).ToArray(),Tabelas=s.Tabelas.Select(t=>t with{Titulo=Humanizar(t.Titulo),Colunas=t.Colunas.Select(c=>c with{Rotulo=Humanizar(c.Rotulo)}).ToArray()}).ToArray()}).ToArray()};
    }
    private static Campo Rotular(Campo c)=>c with{Rotulo=Humanizar(c.Rotulo)};
    private static string Humanizar(string s)
    {
        var palavras=new Dictionary<string,string>{{"Periodo","Período"},{"Situacao","Situação"},{"Codigo","Código"},{"Conferencia","Conferência"},{"Rotulo","Descrição"},{"Saudacao","Saudação"},{"Titulo","Título"},{"Deposito","Depósito"},{"Deducoes","Deduções"},{"Observacoes","Observações"},{"Pendencias","Pendências"},{"Previa","Prévia"},{"Convenio","Convênio"},{"Convenios","Convênios"},{"Descricao","Descrição"},{"Modalidade","Modalidade"},{"Mes","Mês"},{"Agendamento Id","Sessão"},{"Numero","Número"},{"Importacao","Importação"},{"Nao Verificados","Dados não verificados"}};
        foreach(var par in palavras)s=s.Replace(par.Key,par.Value,StringComparison.Ordinal);
        return s.Replace(" Formatado","").Replace(" Formatada","").Replace(" Selecionado","").Replace(" Selecionada","").Trim();
    }
    private static Pagina ConfiguracaoOrganizada(Pagina p)
    {
        string GrupoCampo(string campo)=>campo.StartsWith("DadosClinica.")?"clinica":campo.StartsWith("RegrasFaturamento.")?"faturamento":
            campo.Contains("Backup")?"backup":campo is "PrazoConclusaoHoras" or "ResumoPendenciasConclusao"?"atendimento":
            campo.StartsWith("Armazenamento")||campo.StartsWith("Email")||campo.Contains("Publicacao")||campo is "CarimbadoraDeTempo" or "UrlDoExemplo"?"integracoes":"operacao";
        string GrupoAcao(string acao)=>acao.StartsWith("DadosClinica.")?"clinica":acao.StartsWith("RegrasFaturamento.")?"faturamento":
            acao.Contains("Backup")||acao=="CopiarAgora"?"backup":acao.Contains("Conclusao")?"atendimento":
            acao.Contains("Publicacao")||acao.Contains("Email")||acao is "TestarConexao" or "EnviarExemplo"?"integracoes":"operacao";
        var grupos=new[]{("clinica","Dados da clínica"),("atendimento","Conclusão dos atendimentos"),("operacao","Operação e personalização"),("faturamento","Regras de faturamento"),("integracoes","Integrações e publicação"),("backup","Cópias de segurança")};
        return p with {Campos=[],Acoes=[],Secoes=grupos.Select(g=>new Secao(g.Item1,g.Item2,null,
            p.Campos.Where(c=>GrupoCampo(c.Propriedade)==g.Item1).Select(c=>c.Propriedade.StartsWith("Armazenamento")&&c.Tipo!="leitura"?c with{Guarda="ArmazenamentoEditavel"}:c).ToArray(),[],
            g.Item1=="atendimento"?p.Secoes.SelectMany(s=>s.Tabelas).ToArray():[],p.Acoes.Where(a=>GrupoAcao(a.Comando)==g.Item1).ToArray())).ToArray()};
    }
    private static GraficoWebDto Linha(string chave,string titulo,IEnumerable<PontoGrafico> pontos,string unidade="")
        =>new(chave,titulo,"linha",unidade,pontos.Select(p=>new PontoWebDto(p.Rotulo,p.Valor,p.Valor is {} v?v.ToString(unidade=="R$"?"C":"0.#",CultureInfo.GetCultureInfo("pt-BR"))+(unidade=="%"?"%":""):"—")).ToArray());
    private static IReadOnlyList<GraficoWebDto> GraficosGerente(object vm)=>vm switch
    {
        IndicadoresViewModel v=>[Linha("atendidos","Atendimentos por mês",v.SerieAtendidos),Linha("faltas","Faltas por mês",v.SerieNoShow,"%")],
        FaturamentoGerencialViewModel v=>[Linha("baixa","Taxa de baixa",v.SerieTaxaBaixa,"%")],
        CustoTransacaoViewModel v=>[Linha("percentual","Custo sobre a receita",v.SeriePercentual,"%"),Linha("deducoes","Deduções mensais",v.SerieDeducoes,"R$")],
        _=>[]
    };
}
