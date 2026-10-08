using System.Reflection;
using System.Windows.Input;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Web;

public sealed partial class DialogosFinanceiroController
{
    /// <summary>Confere a lista declarativa sem criar serviços, janelas ou consultar dados.</summary>
    public static IReadOnlyList<string> ValidarRegistro()
    {
        (string Chave, Type Tipo)[] formularios =
        [
            ("Pergunta", typeof(Pergunta)),
            ("Confirmacao", typeof(Pergunta)),
            ("Aviso", typeof(Pergunta)),
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
            ("TrocaSenha", typeof(DialogosTrocaSenhaViewModel)),
        ];
        return formularios.SelectMany(f => ValidarDefinicao(f.Chave, f.Tipo, Definir(f.Chave, f.Tipo))).ToArray();
    }

    private static IReadOnlyList<string> ValidarDefinicao(string chave, Type tipoVm, Definicao definicao)
    {
        var erros = new List<string>();
        PropertyInfo? Exigir(Type tipo, string caminho)
        {
            PropertyInfo? propriedade = null;
            foreach (var parte in caminho.Split('.'))
            {
                propriedade = tipo.GetProperty(parte);
                if (propriedade is null) { erros.Add($"{chave}: {tipo.Name}.{parte} não existe"); return null; }
                tipo = propriedade.PropertyType;
            }
            return propriedade;
        }
        void CondicaoValida(Type tipo, string? expressao)
        {
            if (expressao is null) return;
            foreach (var parte in expressao.Split('&'))
            {
                var p = Exigir(tipo, parte.TrimStart('!'));
                if (p is not null && p.PropertyType != typeof(bool)) erros.Add($"{chave}: condição {parte} não é booleana");
            }
        }
        void CampoValido(Type tipo, Campo campo)
        {
            var p = Exigir(tipo, campo.Caminho);
            if (campo.Tipo != "leitura" && p?.SetMethod?.IsPublic != true) erros.Add($"{chave}: campo {campo.Caminho} não editável");
            if (campo.Opcoes is not null) Exigir(tipoVm, campo.Opcoes);
            CondicaoValida(tipo, campo.Habilitado); CondicaoValida(tipo, campo.Visivel);
        }
        void AcaoValida(Acao acao, Type tipoLinha)
        {
            if (tipoVm != typeof(Pergunta))
            {
                var p = Exigir(tipoVm, acao.Comando);
                if (p is not null && !typeof(ICommand).IsAssignableFrom(p.PropertyType)) erros.Add($"{chave}: {acao.Comando} não é um comando");
            }
            CondicaoValida(tipoVm, acao.Habilitado); CondicaoValida(tipoLinha, acao.HabilitadoLinha);
        }
        foreach (var campo in definicao.Campos) CampoValido(tipoVm, campo);
        foreach (var acao in definicao.Acoes) AcaoValida(acao, tipoVm);
        foreach (var tabela in definicao.Tabelas)
        {
            var origem = Exigir(tipoVm, tabela.Colecao);
            var tipo = origem?.PropertyType.GetInterfaces().Append(origem.PropertyType)
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
            if (tipo is null) { erros.Add($"{chave}: coleção {tabela.Colecao} não é tipada"); continue; }
            foreach (var coluna in tabela.Colunas) Exigir(tipo, coluna.Caminho);
            foreach (var campo in tabela.Campos ?? []) CampoValido(tipo, campo);
            foreach (var acao in tabela.Acoes) AcaoValida(acao, tipo);
            CondicaoValida(tipoVm, tabela.Habilitado);
        }
        return erros;
    }
}
