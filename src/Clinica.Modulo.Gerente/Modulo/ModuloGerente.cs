using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Gerente.ViewModels;
using Clinica.Gerente.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.Modulo;

/// <summary>
/// Módulo do Gerente Geral (parcela 5): Indicadores (BI), Faturamento consolidado
/// (leitura), Campanhas (confirmação, NPS e recall) e Acessos (usuários e permissões).
///
/// Só entram aqui as telas que SÓ fazem sentido para a direção — o Gerente já carrega
/// Recepção e Financeiro inteiros, e repetir o que eles fazem seria manter a mesma
/// tela em dois lugares.
///
/// A ordem é a da pergunta que a direção faz: "como a clínica está" (indicadores),
/// "estamos perdendo faturamento?" (faturamento), "o que estamos fazendo a respeito"
/// (campanhas), "quem fez isso?" (auditoria) e, por último, quem pode o quê — que se
/// mexe raramente.
/// </summary>
public sealed class ModuloGerente : IModuloApp
{
    public const string ChaveIndicadores = "indicadores";
    public const string ChaveFaturamento = "faturamento-gerencial";
    public const string ChaveCusto = "custo-transacao";
    public const string ChaveRentabilidade = "rentabilidade-convenio";
    public const string ChavePrecos = "precos-convenio";
    public const string ChaveCampanhas = "campanhas";
    public const string ChaveAuditoria = "auditoria";
    public const string ChaveAcessos = "acessos";
    public const string ChaveConfiguracoes = "configuracoes";

    public string Nome => "Direção";

    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        // O faturamento entra na seção FINANCEIRO, junto do caixa e dos pacotes: para
        // quem usa, é tudo dinheiro da clínica. Que a tela venha do módulo da Direção é
        // detalhe de arquitetura, e arquitetura não organiza menu.
        new ItemMenuModulo
        {
            Chave = ChaveFaturamento, Rotulo = "Faturamento (TISS)", Glifo = "\uE8C7",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFaturamento
        },
        new ItemMenuModulo
        {
            Chave = ChavePrecos, Rotulo = "Tabela de pre\u00E7o (conv\u00EAnios)", Glifo = "\uE8EF",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveCusto, Rotulo = "Custo de taxas e impostos", Glifo = "\uE9F9",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveRentabilidade, Rotulo = "Rentabilidade por conv\u00EAnio", Glifo = "\uE9F3",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveCampanhas, Rotulo = "Marketing / Recall", Glifo = "\uE715",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarCampanhas
        },
        new ItemMenuModulo
        {
            Chave = ChaveIndicadores, Rotulo = "Relat\u00F3rios / BI", Glifo = "\uE9D2",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerIndicadores
        },
        // Auditoria vem ANTES de Acessos de propósito: "quem fez o quê" é a pergunta que
        // se faz toda semana, e "quem pode o quê" a que se mexe raramente. E fica sob a
        // própria permissão (VerAuditoria), não sob GerenciarUsuarios: ler a trilha e mexer
        // em permissão são coisas diferentes, e amarrar as duas obrigaria a dar poder de
        // criar usuário a quem só precisa conferir o que aconteceu.
        new ItemMenuModulo
        {
            Chave = ChaveAuditoria, Rotulo = "Auditoria", Glifo = "\uE81C",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerAuditoria
        },
        new ItemMenuModulo
        {
            Chave = ChaveAcessos, Rotulo = "Acessos", Glifo = "\uE72E",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarUsuarios
        },
        new ItemMenuModulo
        {
            Chave = ChaveConfiguracoes, Rotulo = "Configura\u00E7\u00F5es", Glifo = "\uE713",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarUsuarios
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<IndicadoresViewModel>();
        servicos.AddTransient<FaturamentoGerencialViewModel>();
        servicos.AddTransient<FaturamentoTissViewModel>();
        servicos.AddTransient<PrecosConvenioViewModel>();
        servicos.AddTransient<CustoTransacaoViewModel>();
        servicos.AddTransient<RentabilidadeConvenioViewModel>();
        servicos.AddTransient<CampanhasViewModel>();
        servicos.AddTransient<AuditoriaViewModel>();
        servicos.AddTransient<AcessosViewModel>();
        servicos.AddTransient<ConfiguracoesViewModel>();
        // UsuarioEdicaoViewModel é construído à mão pela tela: precisa receber o id do
        // usuário no construtor, como os demais formulários da suíte.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveIndicadores => new IndicadoresView
        {
            DataContext = servicos.GetRequiredService<IndicadoresViewModel>()
        },
        ChaveFaturamento => new FaturamentoTissView
        {
            DataContext = servicos.GetRequiredService<FaturamentoTissViewModel>()
        },
        ChavePrecos => new PrecosConvenioView
        {
            DataContext = servicos.GetRequiredService<PrecosConvenioViewModel>()
        },
        ChaveCusto => new CustoTransacaoView
        {
            DataContext = servicos.GetRequiredService<CustoTransacaoViewModel>()
        },
        ChaveRentabilidade => new RentabilidadeConvenioView
        {
            DataContext = servicos.GetRequiredService<RentabilidadeConvenioViewModel>()
        },
        ChaveCampanhas => new CampanhasView
        {
            DataContext = servicos.GetRequiredService<CampanhasViewModel>()
        },
        ChaveAuditoria => new AuditoriaView
        {
            DataContext = servicos.GetRequiredService<AuditoriaViewModel>()
        },
        ChaveAcessos => new AcessosView
        {
            DataContext = servicos.GetRequiredService<AcessosViewModel>()
        },
        ChaveConfiguracoes => new ConfiguracoesView
        {
            DataContext = servicos.GetRequiredService<ConfiguracoesViewModel>()
        },
        _ => null
    };
}
