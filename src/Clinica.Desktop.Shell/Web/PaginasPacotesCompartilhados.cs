using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Desktop.Shell.Web;
using static Clinica.Desktop.Shell.Web.PaginasWebController;
using System.Collections;

namespace Clinica.Desktop.Shell.Web;

public static partial class PaginasPacotesCompartilhados
{
    private static IEnumerable<object> Lista(object vm,string p)=>(PaginasWebController.Ler(vm,p) as IEnumerable)?.Cast<object>()??[];
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
public static IEnumerable<Pagina> CriarPaginas(){return [            new("pacotes", "Pacotes e planos", typeof(PacotesViewModel), [F("FiltroPaciente","Buscar paciente"),F("SoAtivos","Somente ativos","booleano")], [],
                [S("vendidos", "Pacotes vendidos", [T("Vendidos", "Carteira de pacientes", "Paciente;Nome|Pacote;Saldo;Situacao|Situação;ValorFormatado|Valor;Compra;Pagamento", [A("Consumir","Debitar sessão",guarda:"PodeUsarSessao"),A("VerSessoes","Ver sessões"),A("CancelarPacote","Cancelar venda",Editar,"PodeCancelarVenda","perigo")])], descricao:"Resumo"),
                 S("catalogo", "Catálogo", [T("Catalogo", "Pacotes disponíveis", "Nome;Resumo;ValorFormatado|Valor;Ativo", [A("EditarDoCatalogo","Editar",guarda:"vm:PodeMexerNoCatalogo"), A("ExcluirDoCatalogo","Retirar do catálogo",guarda:"vm:PodeMexerNoCatalogo",estilo:"perigo")])], acoes:[A("NovoPacote","Novo pacote",guarda:"PodeMexerNoCatalogo")])],
                [A("Carregar","Atualizar"),A("Vender","Vender pacote",guarda:"PodeVender",estilo:"primario"),A("Orcar","Orçamento",guarda:"PodeVender"),A("AbrirCatalogo","Catálogo",guarda:"PodeMexerNoCatalogo"),A("LimparFiltro","Limpar filtros")], Permissao:Permissao.VenderPacote)];}
}
