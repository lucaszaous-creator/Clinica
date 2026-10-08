using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Web;

public sealed partial class FinanceiroPaginasController
{
    private const Permissao Editar = Permissao.EditarFinanceiro;
    private static Campo F(string p, string nome, string tipo = "texto", string? opcoes = null, string? rotulo = null, string? valor = null, Permissao permissao = Permissao.Nenhuma, string? guarda = null) => new(p, nome, tipo, opcoes, rotulo, valor, Permissao: permissao, Guarda: guarda);
    private static Acao A(string comando, string rotulo, Permissao permissao = Permissao.Nenhuma, string? guarda = null, string estilo = "secundario") => new(comando, rotulo, permissao, guarda, estilo);
    private static Tab T(string propriedade, string titulo, string colunas, Acao[]? acoes = null, Campo[]? campos = null) => new(propriedade, titulo, [propriedade], vm => Lista(vm, propriedade),
        colunas.Split(';').Select(c => { var p = c.Split('|'); return new Coluna(p[0], p.Length > 1 ? p[1] : p[0], p.Length > 2 ? p[2] : TipoColuna(p[0])); }).ToArray(), campos ?? [], acoes ?? []);
    private static string TipoColuna(string propriedade) => propriedade switch
    {
        "Valor" or "ValorFormatado" or "ValorRotulo" or "ValorBancario" or "Entradas" or "Saidas" or "Resultado" or "Previsto" or "Acumulado" or "Teto" or "Gasto" or "Custo" or "Receita" or "Esperado" or "Contado" or "Diferenca" or "Bruto" or "Taxa" or "Liquido" or "Base" => "moeda",
        "Situacao" or "Status" or "StatusRotulo" => "status",
        "TotalCodigos" or "Baixados" or "Pendentes" or "Atendimentos" or "Quantidade" => "numero",
        _ => "texto"
    };
    private static Secao S(string chave, string titulo, Tab[] tabelas, Campo[]? campos = null, Acao[]? acoes = null, string[]? indicadores = null, string? descricao = null) => new(chave, titulo, descricao, campos ?? [], indicadores ?? [], tabelas, acoes ?? []);
    private static Acao[] AtualizarExportar() => [A("Carregar", "Atualizar"), A("Exportar", "Exportar CSV")];
    private static Dictionary<string, Pagina> CriarRegistro()
    {
        Pagina[] paginas = [
            new("caixa", "Caixa", typeof(CaixaViewModel),
                [F("Mes", "Mês", "mes"), F("FiltroTexto", "Buscar lançamento"), F("FiltroSituacao", "Situação", "selecao", "OpcoesSituacaoLancamento")],
                ["Entradas", "Saidas|Saídas", "Saldo|Resultado do mês", "Previsto", "Liquido|Líquido", "Deducoes|Deduções"],
                [S("movimentos", "Movimentações", [T("Linhas", "Lançamentos", "Data;Descricao|Descrição;Categoria;StatusRotulo|Situação;ValorFormatado|Valor",
                    [A("Realizar", "Registrar pagamento", Editar, "PodeRealizar"), A("CobrarPix", "Cobrar Pix", guarda: "EhEntrada"), A("Historico", "Histórico"), A("EmitirRecibo", "Recibo", Editar, "EhEntrada"), A("Cancelar", "Cancelar", Editar, "PodeCancelar", "perigo")])], descricao: "ResumoFiltro")],
                [A("Carregar", "Atualizar"), A("NovoLancamento", "Novo lançamento", Editar, estilo: "primario"), A("CobrarPix", "Cobrar Pix"), A("LimparFiltro", "Limpar filtros"), A("Exportar", "Exportar CSV")], "UltimoMovimento"),
            new("contas", "Contas a pagar e receber", typeof(ContasViewModel),
                [F("HorizonteDias", "Próximos dias", "selecao", "Horizontes"), F("TipoDeContaEscolhido", "Tipo de conta", "selecao", "TiposDeConta", "Rotulo")],
                ["APagarVencido|A pagar vencido", "APagarAVencer|A pagar a vencer", "AReceberVencido|A receber vencido", "AReceberAVencer|A receber a vencer", "SaldoPrevisto|Saldo previsto"],
                [S("contas", "Contas", [T("Contas", "Contas previstas", "Descricao|Descrição;Categoria;Valor;Vencimento;Prazo", [A("Baixar", "Registrar pagamento", Editar), A("Adiar", "Adiar vencimento", Editar), A("Historico", "Histórico")])]),
                 S("recorrentes", "Contas fixas", [T("Recorrentes", "Recorrências", "Descricao|Descrição;Valor;Ritmo;Vigencia|Vigência;Situacao|Situação", [A("EditarRecorrente", "Editar recorrência", Editar)])], acoes:[A("NovaRecorrente", "Nova recorrência", Editar), A("Gerar", "Gerar contas do período", Editar)])],
                [.. AtualizarExportar(), A("NovaConta", "Nova conta", Editar, estilo:"primario")]),
            new("inadimplencia", "Inadimplência", typeof(InadimplenciaViewModel),
                [F("FiltroPaciente", "Buscar paciente"), F("SoCriticos", "Somente críticos", "booleano"), F("Ordem", "Ordenar por", "selecao", "Ordens")],
                ["Total|Em atraso", "QuantidadePacientes|Pacientes", "QuantidadeContas|Contas", "MedioPorPaciente|Média por paciente"],
                [S("devedores", "Pacientes", [T("Devedores", "Pacientes com atraso", "Nome|Paciente;Total;Resumo;Faixa", [A("Cobrar", "Cobrar", guarda:"TemTelefone")]),
                    new Tab("ContasAtrasadas", "Contas em atraso", ["Devedores"], vm => Lista(vm,"Devedores").SelectMany(d => Lista(d,"Contas")),
                        [new("Paciente","Paciente"),new("Descricao","Descrição"),new("Valor","Valor"),new("Vencimento","Vencimento"),new("Atraso","Atraso")], [], [A("Receber","Receber",Editar)],
                        (vm,row,c) => c == "Paciente" ? ((InadimplenciaViewModel)vm).Devedores.First(d => d.Contas.Any(conta => ReferenceEquals(conta,row))).Nome : null)]),
                 S("faixas", "Atraso por faixa", [T("Faixas", "Faixas de atraso", "Rotulo|Faixa;ValorRotulo|Valor")])],
                [..AtualizarExportar(), A("LimparFiltro", "Limpar filtros")]),
            new("plano-contas", "Plano de contas", typeof(PlanoContasViewModel), [], [],
                [S("categorias", "Categorias financeiras", [T("Categorias", "Plano de contas", "Codigo|Código;Nome;Tipo;Ativa", [A("AlternarAtiva", "Ativar / desativar", Editar)])])],
                [A("Carregar", "Atualizar"), A("NovaCategoria", "Nova categoria", Editar, estilo:"primario")]),
            new("fluxo-caixa", "Fluxo de caixa", typeof(FluxoCaixaViewModel),
                [F("MesesJanela", "Meses", "selecao", "Janelas"), F("IncluirPrevisto", "Incluir previsto", "booleano")],
                ["TotalEntradas|Entradas", "TotalSaidas|Saídas", "Resultado", "Margem"],
                [S("meses", "Comparativo mensal", [T("Meses", "Fluxo por mês", "Mes|Mês;Entradas;Saidas|Saídas;Resultado;Previsto;Acumulado")]),
                 S("categorias", "Distribuição por categoria", [T("Entradas", "Entradas", "Rotulo|Categoria;ValorRotulo|Valor"),T("Saidas", "Saídas", "Rotulo|Categoria;ValorRotulo|Valor")], descricao:"MaiorSaida")], AtualizarExportar(), "Periodo"),
            new("resultado", "Resultado e teto de despesas", typeof(ResultadoViewModel), [F("Mes","Mês","mes")],
                ["Receita", "Deducoes|Deduções", "ReceitaLiquida|Receita líquida", "Despesas", "Resultado", "Margem"],
                [S("resultado", "Resultado do mês", [T("Entradas", "Receitas", "Categoria;Valor"),T("Saidas", "Despesas", "Categoria;Valor")], descricao:"Resumo"),
                 S("tetos", "Teto por categoria", [T("Orcamentos", "Orçamento", "Categoria;Teto;Gasto;Saldo;Situacao|Situação", [A("ExcluirTeto", "Remover teto", Editar, estilo:"perigo")])], acoes:[A("DefinirTeto", "Definir teto", Editar)], descricao:"ResumoOrcamento")], AtualizarExportar(), FalhasExtras:["ResultadoNaoVerificado","OrcamentoNaoVerificado"]),
            new("fechamento-caixa", "Fechamento de caixa", typeof(FechamentoCaixaViewModel), [F("Dia","Dia","data")],
                ["EntradasEspecie|Entradas em espécie", "SaidasEspecie|Saídas em espécie", "Esperado", "Lancamentos|Lançamentos", "DiferencaPrevia|Diferença"],
                [S("conferencia", "Conferência do dia", [], [F("ValorContado","Valor contado",permissao:Editar,guarda:"PodeConferir"),F("Justificativa","Justificativa","multilinha",permissao:Editar,guarda:"PodeConferir")], [A("Conferir","Conferir caixa",Editar,"PodeConferir","primario")], descricao:"JaConferido"),
                 S("pendentes", "Dias a conferir", [T("NaoConferidos", "Pendentes", "Data;Esperado;Detalhe", [A("IrPara","Conferir este dia")])]),
                 S("historico", "Conferências anteriores", [T("Historico", "Histórico", "Data;Esperado;Contado;Diferenca|Diferença;Situacao|Situação;Detalhe", [A("Reabrir","Reabrir",Editar,"PodeReabrir")])])], [A("Carregar","Atualizar")]),
            new("recebiveis", "Recebíveis de cartão", typeof(RecebiveisViewModel),
                [F("HorizonteDias","Próximos dias","selecao","Horizontes")], ["AVencer|A vencer","Atrasado","Total","Proximo|Próximo depósito"],
                [S("depositos", "Depósitos esperados", [T("Depositos", "A confirmar", "Adquirente;Previsao|Previsão;Bruto;Taxa;Liquido|Líquido;Detalhe", [A("Confirmar","Confirmar crédito",Editar)])], [F("DataCredito","Data do crédito","data",permissao:Editar)]),
                 S("confirmados", "Créditos confirmados", [T("Confirmados", "Confirmados", "Adquirente;Creditado;Liquido|Líquido;Detalhe", [A("Desfazer","Desfazer confirmação",Editar,"vm:PodeEditar","perigo")])], [F("DiasConfirmados","Histórico em dias","numero")], descricao:"ResumoConfirmados")], [A("Carregar","Atualizar")]),
            new("conciliacao", "Conciliação de receitas", typeof(ConciliacaoViewModel),
                [F("Mes","Mês","mes"),F("FiltroConvenio","Convênio","selecao","Convenios"),F("FiltroPaciente","Paciente"),F("FiltroGuia","Guia"),F("SomenteSemValor","Somente sem valor","booleano")], [],
                [S("guias", "Guias a lançar", [T("Linhas", "Guias", "DataBaixa|Data;Paciente;Convenio|Convênio;NumeroGuia|Guia;Tipo;Procedencia|Origem do valor;Retencao|Retenção;AvisoGlosa|Glosa",
                    [A("Lancar","Registrar receita",Editar),A("Prever","Prever receita",Editar)], [F("Valor","Valor",permissao:Editar)])], descricao:"Resumo"),
                 S("glosadas", "Receitas glosadas", [T("Glosadas", "Glosas", "Paciente;Convenio|Convênio;NumeroGuia|Guia;DataGlosa|Data;Valor;Situacao|Situação;Motivo;Prazo;Orientacao|Orientação", [A("CancelarReceita","Cancelar receita prevista",Editar,"PodeCancelar","perigo")])], descricao:"ResumoGlosadas"),
                 S("particulares", "Sessões particulares", [T("Particulares", "Sessões sem receita", "Data;Paciente;Modalidade;Convenio|Convênio;Procedencia|Origem do valor",
                    [A("LancarSessao","Registrar receita",Editar)], [F("Valor","Valor",permissao:Editar),F("Forma","Forma de pagamento","selecao","Formas",permissao:Editar),F("AReceber","A receber","booleano",permissao:Editar),F("Vencimento","Vencimento","data",permissao:Editar)])], descricao:"ResumoParticulares")],
                [A("Carregar","Atualizar"),A("LimparFiltro","Limpar filtros")], FalhasExtras:["GlosadasNaoVerificadas","ParticularesNaoVerificadas"]),
            new("extrato-banco", "Conciliação bancária", typeof(ExtratoBancoViewModel), [], [],
                [S("extrato", "Transações do extrato", [T("Linhas", "Extrato importado", "Data;Descricao|Descrição;Valor;Situacao|Situação",
                    [A("Conciliar","Conciliar",Editar,"PodeConciliar"),A("Desfazer","Desfazer",Editar,"JaConciliada","perigo")],
                    [F("Escolhido","Lançamento correspondente","selecao","Candidatos","Descricao",permissao:Editar,guarda:"Escolher")])], descricao:"Arquivo"),
                 S("sistema", "Somente no sistema", [T("SoNoSistema", "Sem correspondência bancária", "Data;Descricao|Descrição;ValorBancario|Valor;Status|Situação")])],
                [A("AbrirArquivo","Importar extrato",estilo:"primario"),A("Cruzar","Cruzar novamente",guarda:"TemExtrato")]),
            new("producao", "Produção", typeof(ProducaoViewModel),
                [new Campo("JanelaMeses","Meses","selecao",Fixas:[6,12])], ["TotalCodigos|Códigos","TotalBaixados|Efetivados","TotalPendentes|Pendentes","TaxaBaixaFormatada|Taxa de baixa"],
                [S("meses", "Produção mensal", [T("Meses", "Comparativo", "Rotulo|Mês;TotalCodigos|Códigos;Baixados|Efetivados;Pendentes;TaxaBaixa|Taxa de baixa|percentual")])], [A("Carregar","Atualizar")], null),
            new("pacotes", "Pacotes e planos", typeof(PacotesViewModel), [F("FiltroPaciente","Buscar paciente"),F("SoAtivos","Somente ativos","booleano")], [],
                [S("vendidos", "Pacotes vendidos", [T("Vendidos", "Carteira de pacientes", "Paciente;Nome|Pacote;Saldo;Situacao|Situação;ValorFormatado|Valor;Compra;Pagamento", [A("Consumir","Debitar sessão",guarda:"PodeUsarSessao"),A("VerSessoes","Ver sessões"),A("CancelarPacote","Cancelar venda",Editar,"PodeCancelarVenda","perigo")])], descricao:"Resumo"),
                 S("catalogo", "Catálogo", [T("Catalogo", "Pacotes disponíveis", "Nome;Resumo;ValorFormatado|Valor;Ativo", [A("EditarDoCatalogo","Editar",guarda:"vm:PodeMexerNoCatalogo"), A("ExcluirDoCatalogo","Retirar do catálogo",guarda:"vm:PodeMexerNoCatalogo",estilo:"perigo")])], acoes:[A("NovoPacote","Novo pacote",guarda:"PodeMexerNoCatalogo")])],
                [A("Carregar","Atualizar"),A("Vender","Vender pacote",guarda:"PodeVender",estilo:"primario"),A("Orcar","Orçamento",guarda:"PodeVender"),A("LimparFiltro","Limpar filtros")], Permissao:Permissao.VenderPacote),
            new("estoque", "Estoque e materiais", typeof(EstoqueViewModel), [F("Busca","Buscar item")], [],
                [S("itens", "Estoque", [T("Itens", "Itens", "Nome;Cadastro;Saldo;Minimo|Mínimo;Custo;Ativo", [A("EditarItem","Editar",Editar),A("Movimentar","Movimentar",Editar),A("Inventariar","Conferir saldo",Editar),A("Extrato","Extrato"),A("ExcluirItem","Excluir",Editar,estilo:"perigo")])], acoes:[A("NovoItem","Novo item",Editar),A("ListaDeCompras","Lista de compras")], descricao:"Resumo"),
                 S("validades", "Validades", [T("Validades", "Lotes a acompanhar", "Item;Validade;Quantidade;Situacao|Situação")]),
                 S("custos", "Custo por sessão", [T("CustosSessao", "Sessões", "Data;Paciente;Custo;Itens")], [F("CustoInicio","De","data"),F("CustoFim","Até","data")], [A("CarregarCustos","Atualizar custos")], ["CustoTotal|Total","CustoMedio|Média por sessão","CustoSessoes|Sessões"], "CustoResumo"),
                 S("materiais", "Materiais dos atendimentos", [T("AtendimentosMateriais", "Atendimentos", "Data;Paciente;Situacao|Situação;Pendencia|Pendência", [A("RegistrarMateriais","Conferir materiais",Editar)])],
                    [F("MateriaisInicio","De","data"),F("MateriaisFim","Até","data"),F("ModoMateriaisSelecionado","Registro de materiais","selecao","ModosMateriais","Nome","Modo",Editar,"PodeConfigurarMateriais")],
                    [A("CarregarMateriais","Atualizar materiais"),A("SalvarModoMateriais","Salvar configuração",Editar,"PodeConfigurarMateriais")], descricao:"ResumoMateriais")], [A("Carregar","Atualizar")]),
            new("repasses", "Repasses profissionais", typeof(RepassesViewModel), [F("Mes","Mês","mes")], [],
                [S("calculados", "Cálculo do período", [T("Calculados", "Profissionais", "Profissional;Atendimentos;Receita;Valor;Regra;Situacao|Situação", [A("Apurar","Apurar repasse",Editar,"PodeApurar")])]),
                 S("apurados", "Apurações", [T("Apuracoes", "Apurações registradas", "Profissional;Periodo|Período;Valor;Situacao|Situação", [A("CancelarApuracao","Cancelar apuração",Editar,"PodeCancelar","perigo")])]),
                 S("regras", "Regras de repasse", [T("Regras", "Regras", "Profissional;Regra;Vigencia|Vigência;Ativa", [A("ExcluirRegra","Excluir regra",Editar,estilo:"perigo")])], acoes:[A("NovaRegra","Nova regra",Editar)])], AtualizarExportar()),
            new("taxas", "Taxas e tributos", typeof(TaxasViewModel), [], ["CargaHoje|Carga tributária hoje"],
                [S("taxas", "Taxas de cartão", [T("Taxas", "Taxas cadastradas", "Descricao|Descrição;Prazo;Vigencia|Vigência;Situacao|Situação", [A("Editar","Editar",Editar),A("Excluir","Excluir",Editar,estilo:"perigo")])], acoes:[A("NovaTaxa","Nova taxa",Editar)]),
                 S("tributos", "Regime tributário", [T("Tributos", "Tributos", "Descricao|Descrição;Nome;Vigencia|Vigência;Situacao|Situação;Origem;ValendoAgora|Vigente hoje", [A("EditarTributo","Editar",Editar),A("ExcluirTributo","Excluir",Editar,estilo:"perigo")])],
                    [F("AliquotaImposto","Alíquota única (%)",permissao:Editar)], [A("NovoTributo","Novo tributo",Editar),A("SalvarImposto","Salvar alíquota única",Editar)], descricao:"OrigemDaCarga"),
                 S("simulador", "Simulador de recebimento", [], [F("SimValor","Valor bruto"),F("SimForma","Forma de pagamento","selecao","FormasSimulacao"),F("SimAdquirente","Adquirente"),F("SimBandeira","Bandeira"),F("SimParcelas","Parcelas"),F("SimReterImposto","Reter imposto","booleano")], [A("Simular","Simular")], descricao:"SimResultado"),
                 S("apuracao", "Apuração mensal", [T("Apuracao", "Tributos apurados", "Sigla;Nome;Aliquota|Alíquota;Base;Valor;Natureza")], [F("MesApuracao","Mês","mes")], [A("Apurar","Apurar"),A("ExportarApuracao","Exportar CSV")], ["ResumoApuracao|Apuração","DivergenciaApuracao|Divergência"], "ResumoApuracao")], [A("Carregar","Atualizar")], "OrigemDaCarga", FalhasExtras:["ApuracaoNaoVerificada"])
        ];
        return paginas.ToDictionary(p => p.Chave, StringComparer.Ordinal);
    }
}
