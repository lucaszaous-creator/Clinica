using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Web;
using static Clinica.Desktop.Shell.Web.DialogosWebController;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.Web;

public static class DialogosPacotesCompartilhados
{
    public static IEnumerable<RegistroDialogo> CriarDialogos() {
        (string Chave, Type Tipo)[] formularios =
        [
            ("PacoteVenda", typeof(PacoteVendaViewModel)),
            ("PacoteCatalogo", typeof(PacoteCatalogoEdicaoViewModel)),
            ("CatalogoPacotes", typeof(PacotesViewModel)),
            ("ConsumosPacote", typeof(ConsumosPacoteViewModel)),
            ("MateriaisProcedimento", typeof(MateriaisProcedimentoViewModel)),
        ];
        return formularios.Select(f=>new RegistroDialogo(f.Chave,f.Tipo,Definir(f.Chave,f.Tipo),Permissao.Nenhuma, f.Chave=="ConsumosPacote" ? vm=>((ConsumosPacoteViewModel)vm).CarregarAsync():null));
    }
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
