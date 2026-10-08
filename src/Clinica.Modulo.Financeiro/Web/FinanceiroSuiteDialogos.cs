using Clinica.Desktop.Shell.Componentes;
using Clinica.Financeiro.ViewModels;
using Clinica.Desktop.Shell.Web;
using static Clinica.Desktop.Shell.Web.DialogosWebController;
using Clinica.Domain.Entities;

namespace Clinica.Financeiro.Web;

public static class FinanceiroSuiteDialogos
{
    public static IEnumerable<RegistroDialogo> CriarDialogos() {
        (string Chave, Type Tipo)[] formularios =
        [
            ("Lancamento", typeof(LancamentoEdicaoViewModel)),
            ("Recebimento", typeof(BaixarLancamentoViewModel)),
            ("CobrancaPix", typeof(CobrancaPixViewModel)),
            ("Conta", typeof(ContaEdicaoViewModel)),
            ("Recorrente", typeof(RecorrenteEdicaoViewModel)),
            ("Categoria", typeof(CategoriaEdicaoViewModel)),
            ("Orcamento", typeof(OrcamentoEdicaoViewModel)),
            ("RegraRepasse", typeof(RegraRepasseViewModel)),
            ("Taxa", typeof(TaxaEdicaoViewModel)),
            ("Tributo", typeof(TributoEdicaoViewModel)),
            ("ItemEstoque", typeof(ItemEstoqueEdicaoViewModel)),
            ("MovimentoEstoque", typeof(MovimentoEstoqueViewModel)),
            ("PacoteVenda", typeof(PacoteVendaViewModel)),
            ("PacoteCatalogo", typeof(PacoteCatalogoEdicaoViewModel)),
            ("ContasFixas", typeof(ContasViewModel)),
            ("RegrasRepasse", typeof(RepassesViewModel)),
            ("ValidadesEstoque", typeof(EstoqueViewModel)),
            ("ExtratoEstoque", typeof(ExtratoEstoqueViewModel)),
            ("CatalogoPacotes", typeof(PacotesViewModel)),
            ("ConsumosPacote", typeof(ConsumosPacoteViewModel)),
            ("MateriaisProcedimento", typeof(MateriaisProcedimentoViewModel)),
        ];
        return formularios.Select(f=>new RegistroDialogo(f.Chave,f.Tipo,Definir(f.Chave,f.Tipo),PermissaoDoDialogo(f.Chave), f.Chave=="ConsumosPacote" ? vm=>((ConsumosPacoteViewModel)vm).CarregarAsync():null, Autorizado:f.Chave is "PacoteVenda" or "PacoteCatalogo" ? ()=>SessaoUsuario.Atual.PodeAlgum(Permissao.VenderPacote|Permissao.EditarFinanceiro):null));
    }
    private static Permissao PermissaoDoDialogo(string chave) => chave switch
    {
        "PacoteVenda" or "PacoteCatalogo" => Permissao.Nenhuma,
        "Lancamento" or "Recebimento" or "Conta" or "Recorrente" or "Categoria" or "Orcamento" or "RegraRepasse" or "Taxa" or "Tributo" or "ItemEstoque" or "MovimentoEstoque" or "MateriaisProcedimento" => Permissao.EditarFinanceiro,
        _ => Permissao.VerFinanceiro
    };
    private static Campo C(string caminho, string rotulo, string tipo = "texto", string? opcoes = null,
        string? visivel = null, string? habilitado = null, int maximo = 4000, string? ajuda = null, bool obrigatorio = false) =>
        new(caminho, caminho, rotulo, tipo, opcoes, Visivel: visivel, Habilitado: habilitado, Maximo: maximo, Ajuda: ajuda, Obrigatorio: obrigatorio);
    private static Campo L(string caminho, string rotulo = "") => C(caminho, rotulo, "leitura");
    private static Campo S(string caminho, string rotulo, string opcoes, string? visivel = null, string? habilitado = null) => C(caminho, rotulo, "selecao", opcoes, visivel, habilitado);
    private static Campo D(string caminho, string rotulo, string? visivel = null, string? habilitado = null) => C(caminho, rotulo, "data", visivel: visivel, habilitado: habilitado);
    private static Campo B(string caminho, string rotulo, string? visivel = null, string? habilitado = null, string? ajuda = null) => C(caminho, rotulo, "booleano", visivel: visivel, habilitado: habilitado, ajuda: ajuda);
    private static Acao Salvar(string rotulo = "Salvar") => new("salvar", rotulo, "SalvarCommand", "primario");
    private static Acao A(string chave, string rotulo, string comando, string? habilitado = null, string? linha = null, string estilo = "secundario") => new(chave, rotulo, comando, estilo, habilitado, linha);
    private static Definicao F(string titulo, Campo[] campos, Acao[]? acoes = null, string? descricao = null, Tabela[]? tabelas = null) => new(titulo, descricao, campos, acoes ?? [Salvar()], tabelas ?? []);
    private static Campo[] Paciente(string? visivel = null) =>
    [
        C("Seletor.Termo", "Buscar paciente", visivel: visivel, maximo: 80, ajuda: "Digite o nome e selecione a pessoa nos resultados."),
        S("Seletor.Selecionado", "Paciente", "Seletor.Resultados", visivel),
        L("Seletor.Erro", "Busca de paciente")
    ];

    private static Definicao Definir(string tipo, Type tipoVm) => (tipo, tipoVm) switch
    {
        ("Lancamento", var t) when t == typeof(LancamentoEdicaoViewModel) => F("Novo lançamento",
        [
            S("Tipo", "Tipo", "Tipos"), D("Data", "Data"), C("Descricao", "Descrição", maximo: 200),
            C("Valor", "Valor (R$)", maximo: 20, ajuda: "Informe sempre um valor positivo. O tipo define entrada ou saída."),
            S("Situacao", "Situação", "Situacoes"), S("Categoria", "Categoria", "Categorias"), S("FormaPagamento", "Forma de pagamento", "Formas"),
            ..Paciente("MostrarPaciente"), S("Sessao", "Sessão particular vinculada", "Sessoes", "MostrarPaciente"), L("AvisoSessoes"),
            C("Adquirente", "Maquininha / adquirente", visivel: "EhCartao", maximo: 60), C("Bandeira", "Bandeira", visivel: "EhCartao", maximo: 40),
            C("Parcelas", "Parcelas do cartão", visivel: "EhCartao", maximo: 2), B("ReterImposto", "Reter imposto neste recebimento"),
            L("ResumoDeducoes", "Deduções calculadas"), C("Observacoes", "Observações", "textarea")
        ], [Salvar("Salvar lançamento")], "Previsto não registra pagamento. Receitas de guia devem ser registradas pela conciliação."),
        ("Recebimento", var t) when t == typeof(BaixarLancamentoViewModel) => F("Confirmar pagamento ou recebimento",
        [
            L("Descricao", "Conta"), L("Valor", "Saldo"), C("ValorInformado", "Valor pago ou recebido agora", maximo: 15),
            D("VencimentoSaldo", "Vencimento do saldo restante"), D("Data", "Data do pagamento"), S("Forma", "Forma de pagamento", "Formas"),
            C("Adquirente", "Maquininha / adquirente", visivel: "EhCartao", maximo: 60), C("Bandeira", "Bandeira", visivel: "EhCartao", maximo: 40),
            C("Parcelas", "Parcelas no crédito", visivel: "EhCartao", maximo: 2)
        ], [A("confirmar", "Confirmar", "ConfirmarCommand", "PodeConfirmar", estilo: "primario")],
            "Na baixa parcial, somente o valor confirmado entra no caixa. O restante continua em aberto."),
        ("CobrancaPix", var t) when t == typeof(CobrancaPixViewModel) => F("Cobrar por Pix",
        [C("Valor", "Valor (R$)", maximo: 20), C("Referencia", "Referência", maximo: 25), L("CopiaECola", "Pix copia e cola"), L("Procedencia", "Recebedor")],
            [A("gerar", "Gerar código", "GerarCommand", estilo: "primario"), A("copiar", "Copiar código", "CopiarCommand", "TemCodigo")],
            "Gerar o código não confirma pagamento. Confira o crédito no extrato antes de realizar o lançamento."),
        ("Conta", var t) when t == typeof(ContaEdicaoViewModel) => F("Nova conta",
        [
            C("Descricao", "Descrição", maximo: 200), C("Valor", "Valor total da obrigação", maximo: 14), D("Vencimento", "Primeiro vencimento"),
            C("Contraparte", "Fornecedor / cliente", maximo: 200), C("DocumentoReferencia", "Documento de referência", maximo: 100),
            D("Competencia", "Competência"), C("QuantidadeParcelas", "Quantidade de parcelas mensais", maximo: 3),
            B("EhSaida", "Conta a pagar", ajuda: "Desmarque para registrar uma conta a receber."), S("Categoria", "Categoria", "Categorias"), ..Paciente("EhEntrada")
        ], [Salvar("Registrar conta")], "A conta nasce prevista. Parcelar divide o total sem registrar pagamento."),
        ("Recorrente", var t) when t == typeof(RecorrenteEdicaoViewModel) => F("Conta fixa",
        [
            C("Descricao", "Descrição", maximo: 200), C("Valor", "Valor", maximo: 14),
            S("Periodicidade", "Periodicidade", "Periodicidades", habilitado: "EhNova"), D("PrimeiroVencimento", "Primeiro vencimento", habilitado: "EhNova"),
            D("UltimoVencimento", "Último vencimento (opcional)"), B("EhSaida", "Conta a pagar", habilitado: "EhNova", ajuda: "Desmarque para conta a receber."),
            S("Categoria", "Categoria", "Categorias"), B("Ativa", "Ativa")
        ], [Salvar("Salvar conta fixa")], "Alterar o molde não modifica contas que já foram geradas."),
        ("Categoria", var t) when t == typeof(CategoriaEdicaoViewModel) => F("Nova categoria",
            [C("Codigo", "Código", maximo: 40), C("Nome", "Nome", maximo: 80), S("Tipo", "Tipo", "Tipos")],
            [Salvar("Criar categoria")], "O código identifica a categoria e não muda após a criação."),
        ("Orcamento", var t) when t == typeof(OrcamentoEdicaoViewModel) => F("Teto de gasto",
            [S("Categoria", "Categoria de saída", "Categorias"), C("Teto", "Teto do mês (R$)", maximo: 12)],
            [Salvar("Salvar teto")], "O teto vale para o mês selecionado. Zero significa que nenhum gasto foi autorizado nesta categoria."),
        ("RegraRepasse", var t) when t == typeof(RegraRepasseViewModel) => F("Regra de repasse",
        [
            S("Profissional", "Profissional", "Profissionais"), S("Base", "Base do cálculo", "Bases"),
            C("Percentual", "Percentual da receita (%)", visivel: "EhPercentual", maximo: 6),
            C("ValorPorAtendimento", "Valor por atendimento", visivel: "!EhPercentual", maximo: 12),
            D("VigenteDe", "Vigente de"), D("VigenteAte", "Vigente até (opcional)"), C("Observacoes", "Observações", "textarea", maximo: 500)
        ], [Salvar("Salvar regra")], "O percentual incide sobre a receita recebida. Uma regra nova não reescreve apurações anteriores."),
        ("Taxa", var t) when t == typeof(TaxaEdicaoViewModel) => F("Taxa da maquininha",
        [
            C("Adquirente", "Maquininha / contrato", maximo: 60), C("Bandeira", "Bandeira (opcional)", maximo: 40), S("Modalidade", "Modalidade", "Modalidades"),
            C("ParcelasDe", "Parcelas de", visivel: "EhParcelado", maximo: 2), C("ParcelasAte", "Parcelas até", visivel: "EhParcelado", maximo: 2),
            C("Percentual", "Percentual (%)", maximo: 8), C("DiasParaReceber", "Primeiro crédito (dias)", maximo: 4),
            D("VigenteDe", "Vigente de"), D("VigenteAte", "Vigente até (opcional)"), B("LiquidacaoMensal", "Receber uma parcela por mês", "EhParcelado"), B("Ativa", "Ativa")
        ], [Salvar("Salvar taxa")], "Use as condições do contrato, inclusive quando a taxa é zero. Alterações não reescrevem vendas já recebidas."),
        ("Tributo", var t) when t == typeof(TributoEdicaoViewModel) => F("Tributo",
        [
            C("Sigla", "Sigla", maximo: 12), C("Nome", "Nome", maximo: 80), C("Percentual", "Alíquota (%)", maximo: 8),
            C("Base", "Base de cálculo (%)", maximo: 8, ajuda: "Em branco equivale a 100%."), C("Convenio", "Convênio que retém na fonte (opcional)", maximo: 40),
            D("VigenteDe", "Vigente de"), D("VigenteAte", "Vigente até (opcional)"), B("Ativo", "Ativo")
        ], [Salvar("Salvar tributo")], "A retenção da operadora substitui os tributos gerais do recebimento; ela não é somada novamente."),
        ("ItemEstoque", var t) when t == typeof(ItemEstoqueEdicaoViewModel) => F("Item de estoque",
        [
            C("Nome", "Nome", maximo: 120), C("CodigoInterno", "Código interno", maximo: 40), C("CodigoBarras", "Código de barras", maximo: 60),
            S("Grupo", "Grupo", "Grupos"), S("Uso", "Uso permitido", "Usos"), C("Fabricante", "Fabricante", maximo: 120),
            C("Apresentacao", "Apresentação / especificação", maximo: 120), C("Unidade", "Unidade de consumo", maximo: 10),
            C("Minimo", "Estoque mínimo", maximo: 10), C("Maximo", "Estoque máximo", maximo: 14), C("UnidadeCompra", "Unidade de compra", maximo: 10),
            C("FatorCompra", "Unidades de consumo por embalagem", maximo: 14), C("LocalArmazenamento", "Local de armazenamento", maximo: 100),
            B("ExigirLote", "Exigir lote nas entradas"), B("ExigirValidade", "Exigir validade nas entradas"), C("Observacoes", "Observações", "textarea", maximo: 500), B("Ativo", "Ativo")
        ], [Salvar("Salvar item")], "Medicamentos exigem lote e validade. Produtos com movimentação preservam o histórico."),
        ("MovimentoEstoque", var t) when t == typeof(MovimentoEstoqueViewModel) => F("Movimentar estoque",
        [
            L("Item", "Item"), S("Tipo", "Tipo", "Tipos"), C("Quantidade", "Quantidade", maximo: 10), D("Data", "Data"),
            B("EmUnidadeCompra", "Quantidade e custo na embalagem de compra", "EhEntrada"), C("Fornecedor", "Fornecedor / origem", visivel: "EhEntrada", maximo: 100),
            C("DocumentoEntrada", "Documento da entrada", visivel: "EhEntrada", maximo: 80), C("CustoUnitario", "Custo unitário", visivel: "EhEntrada", maximo: 12),
            D("Validade", "Validade do lote", "EhEntrada"), C("Lote", "Lote", visivel: "EhEntrada", maximo: 60), B("GerarContaCompra", "Registrar compra no financeiro", "EhEntrada"),
            D("VencimentoCompra", "Vencimento da compra", "EhEntrada&GerarContaCompra"), B("CompraPaga", "Compra já paga", "EhEntrada&GerarContaCompra"),
            S("FormaCompra", "Forma de pagamento da compra", "FormasCompra", "EhEntrada&GerarContaCompra&CompraPaga"),
            C("SetorDestino", "Setor de destino", visivel: "EhSaida", maximo: 100), C("Observacao", "Observação / motivo", "textarea", maximo: 300)
        ], [A("salvar", "Registrar movimento", "SalvarCommand", "PodeSalvar", estilo: "primario")], "Perdas exigem motivo. Estoque e conta de compra são gravados juntos quando solicitado."),
        ("PacoteVenda", var t) when t == typeof(PacoteVendaViewModel) => F("Vender pacote",
        [
            ..Paciente(), S("PacoteSelecionado", "Pacote", "Opcoes"), D("DataCompra", "Data da compra"), C("ValorCobrado", "Valor cobrado", maximo: 12),
            B("APrazo", "Cobrar depois", ajuda: "Desmarcado: pagar agora. Parcelamento do cartão é diferente de cobrar parcelas futuras do paciente."),
            S("Forma", "Forma de pagamento (ou da entrada)", "Formas"), C("Entrada", "Entrada hoje", visivel: "APrazo", maximo: 12),
            C("Parcelas", "Parcelas futuras", visivel: "APrazo", maximo: 2), D("PrimeiroVencimento", "Primeiro vencimento", "APrazo"),
            C("Adquirente", "Maquininha / contrato", visivel: "EhCartao", maximo: 60), C("Bandeira", "Bandeira", visivel: "EhCartao", maximo: 40),
            C("ParcelasCartao", "Parcelas na maquininha", visivel: "EhCartao&EhCredito", maximo: 2), L("PreviaDasParcelas", "Prévia da venda"),
            C("Observacoes", "Observações", "textarea", maximo: 500)
        ], [A("cadastrar-pacote", "Cadastrar pacote no catálogo", "CadastrarNoCatalogoCommand", "PodeMexerNoCatalogo"), Salvar("Vender pacote")],
            "O preço vem do catálogo e pode ser ajustado. Confira a prévia antes de confirmar a venda."),
        ("PacoteCatalogo", var t) when t == typeof(PacoteCatalogoEdicaoViewModel) => F("Pacote do catálogo",
        [C("Nome", "Nome", maximo: 120), S("Tipo", "Tipo", "Tipos"), C("Sessoes", "Sessões", maximo: 4), C("Valor", "Valor", maximo: 12), C("ValidadeDias", "Validade (dias)", maximo: 4)],
            [Salvar("Salvar pacote")], "Sem limite de sessões, o plano é livre durante a validade. Alterar o catálogo não modifica vendas anteriores."),
        ("ContasFixas", var t) when t == typeof(ContasViewModel) => F("Contas fixas", [], [A("nova", "Nova conta fixa", "NovaRecorrenteCommand", "PodeEditar")],
            "Modelos de contas recorrentes. Editar o modelo preserva as ocorrências já geradas.",
            [new("recorrentes", "Contas cadastradas", "Recorrentes", [new("Descricao", "Descrição"), new("Valor", "Valor"), new("Ritmo", "Periodicidade"), new("Vigencia", "Vigência"), new("Situacao", "Situação")],
                [A("editar", "Editar", "EditarRecorrenteCommand", "PodeEditar")])]),
        ("RegrasRepasse", var t) when t == typeof(RepassesViewModel) => F("Regras e apurações", [], [A("nova", "Nova regra", "NovaRegraCommand", "PodeEditarFinanceiro")], null,
        [
            new("regras", "Regras", "Regras", [new("Profissional", "Profissional"), new("Regra", "Regra"), new("Vigencia", "Vigência")], [A("excluir", "Excluir", "ExcluirRegraCommand", "PodeEditarFinanceiro", estilo: "perigo")]),
            new("apuracoes", "Apurações", "Apuracoes", [new("Profissional", "Profissional"), new("Periodo", "Período"), new("Valor", "Valor"), new("Situacao", "Situação")], [A("cancelar", "Cancelar", "CancelarApuracaoCommand", "PodeEditarFinanceiro", "PodeCancelar", "perigo")])
        ]),
        ("ValidadesEstoque", var t) when t == typeof(EstoqueViewModel) => F("Validades e mínimos", [L("Resumo", "Resumo")], [],
            "Lotes vencidos ou a vencer em 60 dias que ainda têm saldo.",
            [new("validades", "Validades", "Validades", [new("Item", "Item"), new("Validade", "Validade"), new("Situacao", "Situação")], [])]),
        ("ExtratoEstoque", var t) when t == typeof(ExtratoEstoqueViewModel) => F("Extrato do item", [L("Resumo", "Resumo")], [], null,
            [new("movimentos", "Movimentos", "Movimentos", [new("Data", "Data"), new("Tipo", "Tipo"), new("Movimento", "Movimento"), new("SaldoApos", "Saldo após"), new("Detalhe", "Detalhe"), new("Quem", "Quem")], [])]),
        ("CatalogoPacotes", var t) when t == typeof(PacotesViewModel) => F("Catálogo de pacotes", [], [A("novo", "Novo pacote", "NovoPacoteCommand", "PodeMexerNoCatalogo")], null,
            [new("catalogo", "Pacotes à venda", "Catalogo", [new("Nome", "Nome"), new("Resumo", "Condições"), new("ValorFormatado", "Valor")],
                [A("editar", "Editar", "EditarDoCatalogoCommand", "PodeMexerNoCatalogo"), A("excluir", "Excluir", "ExcluirDoCatalogoCommand", "PodeMexerNoCatalogo", estilo: "perigo")])]),
        ("ConsumosPacote", var t) when t == typeof(ConsumosPacoteViewModel) => F("Sessões do pacote", [L("Saldo", "Saldo hoje"), L("Resumo", "Resumo")], [],
            "Devolver uma sessão exige motivo e preserva o consumo cancelado no histórico.",
            [new("consumos", "Sessões", "Consumos", [new("Data", "Data"), new("Origem", "Origem"), new("Situacao", "Situação")],
                [A("devolver", "Devolver ao saldo", "DevolverCommand", "PodeEditarFinanceiro", "PodeDevolver", "perigo")])]),
        ("MateriaisProcedimento", var t) when t == typeof(MateriaisProcedimentoViewModel) => F("Materiais do procedimento",
            [L("Contexto", "Atendimento"), C("Busca", "Buscar produto por nome ou código", maximo: 120), L("Situacao", "Situação"),
                B("SemConsumo", "Confirmo que não houve consumo de materiais nesta sessão", habilitado: "PodeEditar")],
            [A("confirmar", "Registrar materiais / tentar baixa pendente", "ConfirmarCommand", "PodeConfirmar", estilo: "primario")],
            "Informe quantidades na unidade de consumo e os lotes. O filtro mantém os valores preenchidos; use Outro lote para mais de um lote do mesmo produto.",
            [new("materiais", "Materiais utilizados", "Itens", [new("Nome", "Material"), new("Saldo", "Disponível")],
                [A("outro-lote", "Outro lote", "OutroLoteCommand", "PodeEditar")],
                [C("Quantidade", "Quantidade utilizada", maximo: 14), C("Lote", "Lote utilizado", maximo: 60)], "PodeEditar")]),
        _ => throw new InvalidOperationException("Formulário não cadastrado para a interface web: " + tipo)
    };
}
