using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// Seção temática da sidebar. É a divisão da PROPOSTA COMERCIAL, e não a divisão por
/// módulo carregado.
///
/// A diferença importa no Gerente Geral, que carrega os três módulos: agrupar por módulo
/// lhe dava três cabeçalhos ("Recepção", "Financeiro", "Direção") que dizem de onde a tela
/// veio — informação de arquitetura, que só interessa a quem programa. Quem usa pergunta
/// outra coisa: "onde mexo no paciente?". Por isso Prontuário (que mora no módulo da
/// Recepção) e Faturamento (que mora no do Gerente) aparecem sob PACIENTE e FINANCEIRO,
/// junto do que é da mesma natureza.
///
/// A ordem dos valores é a ordem em que os grupos aparecem na sidebar.
///
/// ⚠️ PACIENTE é a PESSOA; ATENDIMENTO é o ATO (set/2026, pedido da direção sobre o
/// mockup <c>docs/mockups/sidebar-tres-desenhos.html</c>: "precisa separar o que é de
/// atendimento de paciente"). Até aqui um grupo só juntava a ficha (cadastro, documentos,
/// pacotes, preços) com o gesto clínico (lançar, prontuário, prescrever, enfermagem). O
/// corte que separa os dois é o MESMO da permissão (parcela 49): o que fica em PACIENTE
/// se abre com <c>VerFichaPaciente</c>; o que fica em ATENDIMENTO é dado de saúde ou o
/// gesto que o cria. Um grupo que mistura os dois é o bit sobrecarregado da parcela 49
/// vestido de menu — e o mesmo item mora no mesmo grupo nos quatro apps, sempre.
/// </summary>
public enum GrupoSidebar
{
    /// <summary>O dia da clínica: painel, agenda, equipe.</summary>
    Gestao,

    /// <summary>A pessoa atendida: cadastro, documentos, pacotes, preços, retorno.</summary>
    Paciente,

    /// <summary>O ato de atender: lançar e marcar, prontuário, prescrições, enfermagem.</summary>
    Atendimento,

    /// <summary>O dinheiro: caixa, contas, faturamento, estoque.</summary>
    Financeiro,

    /// <summary>A leitura do negócio: marketing, BI, configurações.</summary>
    Inteligencia
}

/// <summary>Rótulo e ícone de cada grupo, como saem na sidebar.</summary>
public static class GruposSidebar
{
    public static string Rotulo(GrupoSidebar grupo) => grupo switch
    {
        GrupoSidebar.Gestao => "GESTÃO",
        GrupoSidebar.Paciente => "PACIENTE",
        GrupoSidebar.Atendimento => "ATENDIMENTO",
        GrupoSidebar.Financeiro => "FINANCEIRO",
        GrupoSidebar.Inteligencia => "INTELIGÊNCIA",
        _ => grupo.ToString().ToUpperInvariant()
    };

    /// <summary>
    /// Nome curto do grupo, para caber sob o ícone no rail de 56px (parcela 55).
    /// Abreviar no XAML com <c>Substring</c> cortaria "INTELIGÊNCIA" em "INTEL" —
    /// a abreviação é escolhida, não truncada.
    /// </summary>
    public static string RotuloCurto(GrupoSidebar grupo) => grupo switch
    {
        GrupoSidebar.Gestao => "Dia",
        GrupoSidebar.Paciente => "Paciente",
        GrupoSidebar.Atendimento => "Atender",
        GrupoSidebar.Financeiro => "Dinheiro",
        GrupoSidebar.Inteligencia => "Direção",
        _ => Rotulo(grupo)
    };

    /// <summary>
    /// Glifo do grupo. Os cinco são DIFERENTES entre si por obrigação: onde só o ícone
    /// identifica a categoria, dois desenhos iguais fazem a pessoa abrir os dois para
    /// descobrir qual é qual.
    /// </summary>
    public static string Glifo(GrupoSidebar grupo) => grupo switch
    {
        GrupoSidebar.Gestao => "\uE80F",          // Home — o dia da clínica
        GrupoSidebar.Paciente => "\uE77B",        // Contact — a pessoa atendida
        GrupoSidebar.Atendimento => "\uE95E",     // Health — o ato de atender
        GrupoSidebar.Financeiro => "\uE825",      // Bank — o dinheiro
        GrupoSidebar.Inteligencia => "\uE9D2",    // BarChart — a leitura do negócio
        _ => "\uE700"
    };
}

/// <summary>
/// Uma sub-aba de um item de menu COMPOSTO (parcela 55).
///
/// A aba não carrega a tela: ela carrega a <see cref="Chave"/> da tela, e quem resolve a
/// chave é o shell, que sabe qual módulo constrói o quê. É essa indireção que permite um
/// item compor telas de MÓDULOS DIFERENTES — "Agenda" junta a agenda do balcão
/// (Recepção) com "Minha semana" (Consultório), e nenhum dos dois passa a conhecer o
/// outro. Compor por referência de tipo obrigaria o módulo dono a referenciar os outros,
/// que é exatamente o que a arquitetura da suíte proíbe.
///
/// A consequência prática é boa: num executável que não carrega o módulo da aba (a
/// Recepção não carrega o Consultório), a aba simplesmente não aparece, e o item continua
/// funcionando com as que sobraram.
/// </summary>
/// <param name="Rotulo">Texto da aba. É ele que a busca global indexa.</param>
/// <param name="Chave">Chave do <see cref="ItemMenuModulo"/> que constrói a tela.</param>
public sealed record AbaMenu(string Rotulo, string Chave);

/// <summary>
/// Item do menu lateral publicado por um módulo. Substitui o enum <c>Secao</c> do
/// faturamento: como os módulos são compostos em tempo de execução, a identidade do
/// item é uma string (<see cref="Chave"/>) e não um valor de enum fechado.
/// </summary>
public sealed partial class ItemMenuModulo : ObservableObject
{
    /// <summary>Identifica a tela dentro do módulo (ex.: "fila"). Única dentro do módulo.</summary>
    public required string Chave { get; init; }

    /// <summary>Texto exibido na sidebar.</summary>
    public required string Rotulo { get; init; }

    /// <summary>
    /// Glifo Segoe Fluent/MDL2. Desde set/2026 é o CAMINHO DE BAIXO: a sidebar desenha
    /// <see cref="Icone"/> quando ele existe, e só cai no glifo quando não existe — é o
    /// que mantém um item novo, publicado sem ícone, visível em vez de sem desenho.
    /// </summary>
    public required string Glifo { get; init; }

    /// <summary>
    /// Nome do ícone de TRAÇO no dicionário <c>Styles/Componentes/Icones.xaml</c>
    /// ("prancheta", "ficha", "rx"…) — os desenhos do mockup que a direção aprovou
    /// (set/2026: "queremos os ícones"). Nulo = usa o <see cref="Glifo"/>.
    /// </summary>
    public string? Icone { get; init; }

    /// <summary>
    /// Seção temática onde o item aparece. Declarada pelo módulo, porque só ele sabe a
    /// natureza da tela. O padrão existe para um módulo novo não deixar de funcionar por
    /// esquecer de declarar — mas todo item da suíte declara o seu explicitamente.
    /// </summary>
    public GrupoSidebar Grupo { get; init; } = GrupoSidebar.Gestao;

    /// <summary>
    /// Módulo dono — preenchido pelo shell ao montar o menu. É por ele que a navegação
    /// acha quem sabe construir a tela; não confundir com <see cref="Grupo"/>, que é
    /// onde o item aparece para quem usa.
    /// </summary>
    public string ModuloNome { get; internal set; } = string.Empty;

    /// <summary>
    /// Permissão exigida para o item aparecer na sidebar (parcela 5).
    /// <see cref="Permissao.Nenhuma"/>, que é o padrão, significa "sempre visível" —
    /// assim um módulo que ainda não declarou permissão nenhuma continua funcionando
    /// exatamente como antes.
    /// </summary>
    public Permissao Requer { get; init; } = Permissao.Nenhuma;

    /// <summary>
    /// Este item é a tela de ABERTURA do app, quando visível (parcela 22).
    ///
    /// Sem ele o shell abre no primeiro item do primeiro módulo carregado — e o Gerente
    /// Geral, que carrega os três, abria no painel da RECEPÇÃO: quem manda na clínica
    /// entrava no sistema e via a fila do balcão. Resolver isso reordenando os módulos
    /// reordenaria a sidebar inteira, que já está na ordem do dia de trabalho.
    ///
    /// Quem não tem a permissão do item marcado cai no primeiro visível, como antes.
    /// </summary>
    public bool Inicial { get; init; }

    /// <summary>
    /// A tela existe e é NAVEGÁVEL, mas não aparece na sidebar (parcela 37, 4ª rodada).
    ///
    /// Existe porque a navegação da suíte é por CHAVE e o shell só sabe abrir o que está
    /// na lista de itens (<c>ShellViewModel.IrPara</c>). Quando o Consultório passou a ter
    /// telas que só fazem sentido COM um paciente escolhido — e por isso saíram do menu —,
    /// tirá-las da lista matou junto todo botão que navegava até elas: "Atender" na fila
    /// do dia, os atalhos da carteira, o painel da direção. O app ficou com botões que não
    /// faziam nada.
    ///
    /// Item oculto resolve os dois lados: continua sendo destino de <c>NavegacaoSuite</c>
    /// e não ocupa linha no menu. Quem monta a sidebar filtra por esta marca; quem navega,
    /// não.
    /// </summary>
    public bool Oculto { get; init; }

    /// <summary>
    /// As sub-abas deste item (parcela 55). Vazia = o item É uma tela.
    ///
    /// Por que isto existe
    /// -------------------
    /// O Gerente Geral carrega os quatro módulos e a sidebar dele chegou a <b>46 itens</b>
    /// — 1.824px de menu para 610px de tela, ou seja, um terço visível. A regra que
    /// resolve isso já estava escrita em <c>docs/design-system/layout-navegacao.md</c>
    /// desde a parcela 7 ("quando um item da proposta cobre vários assuntos, use sub-abas
    /// dentro dele em vez de criar entradas novas") e tinha sido aplicada UMA vez, no
    /// "Faturamento (TISS)". Isto é a mesma regra levada ao resto.
    ///
    /// ⚠️ <b>A tela que vira aba CONTINUA sendo um item</b>, com <see cref="Oculto"/> —
    /// nunca se apaga o item. <c>NavegacaoSuite.Ir(chave)</c> procura a chave na lista de
    /// itens e, sem achar, <b>devolve false em silêncio</b>: foi assim que a 4ª rodada da
    /// parcela 37 deixou "Atender", os atalhos da carteira e o painel da direção sem
    /// abrir nada, com as três redes verdes. O shell resolve a chave da sub-tela abrindo
    /// o item PAI já na aba certa, então toda navegação que existia continua valendo.
    /// </summary>
    public IReadOnlyList<AbaMenu> Abas { get; init; } = [];

    /// <summary>
    /// Enquanto esta tela está aberta, o app é ELA: o shell recolhe a sidebar e a barra
    /// de cima (set/2026, o mockup <c>docs/mockups/atendimento-sem-barras-cinco.html</c>,
    /// modelo 3, aprovado pela direção — "some toda barra vertical, inclusive a tira de
    /// ícones do shell").
    ///
    /// ⚠️ <b>Item imersivo declara as PORTAS DE SAÍDA dentro da própria tela</b>, porque
    /// as do shell somem junto: a do paciente tem o "← Meu dia" e o "Trocar paciente" no
    /// cabeçalho, e o estado vazio dela tem o "Ir para Pacientes" (o cabeçalho colapsa
    /// quando não há ninguém escolhido). Tela imersiva sem saída própria é a pessoa
    /// trancada no app — o oposto do que o desenho quer.
    ///
    /// ⚠️ Quem DESLIGA o modo é a navegação: <c>ShellViewModel.Navegar</c> lê esta marca
    /// a cada destino, então ir para qualquer outro item devolve as duas barras sozinho.
    /// Ctrl+B e Ctrl+F também as devolvem — atalho que não faz nada numa tela é o defeito
    /// da parcela 41 vestido de teclado.
    /// </summary>
    public bool Imersivo { get; init; }

    [ObservableProperty]
    private bool _estaAtivo;
}

/// <summary>
/// Um GRUPO da sidebar — e, desde set/2026, um grupo que ABRE E FECHA.
///
/// Por que ele abre e fecha: o Gerente Geral carrega os quatro módulos e a sidebar fixa
/// de 240 px lista 25 itens em cinco grupos — ~1290 px de menu para 658 px de janela, com
/// FINANCEIRO e INTELIGÊNCIA abaixo da dobra (medido no mockup
/// <c>docs/mockups/sidebar-tres-desenhos.html</c>, desenho A, aprovado pela direção). Com
/// um grupo aberto por vez a lista cabe em qualquer app sem rolar, e o cabeçalho
/// fechado diz quantos itens tem — é a pista que dispensa abrir para lembrar onde mora
/// "Estoque".
///
/// A regra de quem abre: NAVEGAR abre o grupo do destino e fecha os outros (o
/// acordeão); CLICAR no cabeçalho só alterna aquele grupo, sem mexer nos vizinhos — quem
/// abriu FINANCEIRO para olhar não quer que GESTÃO se feche por isso. Com a sidebar
/// RECOLHIDA (Ctrl+B) todos os itens aparecem como ícone, como sempre: 56 px não têm
/// onde escrever um cabeçalho, e cabeçalho que não se lê não se clica.
/// </summary>
public sealed partial class GrupoMenuModulo : ObservableObject
{
    public GrupoMenuModulo(GrupoSidebar grupo, IReadOnlyList<ItemMenuModulo> itens)
    {
        Grupo = grupo;
        Nome = GruposSidebar.Rotulo(grupo);
        NomeCurto = GruposSidebar.RotuloCurto(grupo);
        Glifo = GruposSidebar.Glifo(grupo);
        Itens = itens;
    }

    public GrupoSidebar Grupo { get; }
    public string Nome { get; }
    public string NomeCurto { get; }
    public string Glifo { get; }
    public IReadOnlyList<ItemMenuModulo> Itens { get; }

    /// <summary>Quantos itens o grupo tem — o cabeçalho FECHADO escreve este número.</summary>
    public int Quantidade => Itens.Count;

    /// <summary>O corpo do grupo está à vista. Quem decide é <c>ShellViewModel.Navegar</c> e o clique no cabeçalho.</summary>
    [ObservableProperty]
    private bool _aberto;

    /// <summary>
    /// O item ativo mora aqui. Serve ao cabeçalho FECHADO: quem fechou o grupo da tela
    /// aberta precisa continuar vendo, na cor do ativo, em que parte do sistema está.
    /// </summary>
    [ObservableProperty]
    private bool _temAtivo;

    /// <summary>Clique no cabeçalho: alterna SÓ este grupo.</summary>
    [RelayCommand]
    private void Alternar() => Aberto = !Aberto;
}
