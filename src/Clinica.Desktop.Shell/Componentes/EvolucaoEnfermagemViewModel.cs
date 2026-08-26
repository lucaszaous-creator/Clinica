using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Um DIAGNÓSTICO de enfermagem sendo escrito — as etapas 2 e 3 do Processo (parcela 73).
///
/// A redação em três partes é o que o torna um diagnóstico e não um rótulo, e a tela pede
/// as três: o problema, o <b>relacionado a</b> e o <b>evidenciado por</b>. Sem a terceira,
/// a etapa 5 não tem contra o que avaliar.
/// </summary>
public sealed partial class LinhaDiagnosticoEnfermagem : ObservableObject
{
    /// <summary>Do catálogo, quando veio de lá. Nulo = escrito à mão, e isso é legítimo.</summary>
    public string? Codigo { get; init; }

    [ObservableProperty] private string _titulo = string.Empty;
    [ObservableProperty] private string _relacionadoA = string.Empty;
    [ObservableProperty] private string _evidenciadoPor = string.Empty;
    [ObservableProperty] private string _resultadoEsperado = string.Empty;

    /// <summary>Sugestões do catálogo para o "relacionado a" — atalho, não lista fechada.</summary>
    public IReadOnlyList<string> CausasSugeridas { get; init; } = [];

    public IReadOnlyList<string> EvidenciasSugeridas { get; init; } = [];

    public bool TemSugestoes => CausasSugeridas.Count > 0 || EvidenciasSugeridas.Count > 0;

    public DiagnosticoEnfermagem Colher() => new()
    {
        Codigo = Codigo,
        Titulo = Titulo.Trim(),
        RelacionadoA = string.IsNullOrWhiteSpace(RelacionadoA) ? null : RelacionadoA.Trim(),
        EvidenciadoPor = string.IsNullOrWhiteSpace(EvidenciadoPor) ? null : EvidenciadoPor.Trim(),
        ResultadoEsperado = string.IsNullOrWhiteSpace(ResultadoEsperado)
            ? null : ResultadoEsperado.Trim()
    };

    public static LinhaDiagnosticoEnfermagem Do(DiagnosticoCatalogo c) => new()
    {
        Codigo = c.Codigo,
        Titulo = c.Titulo,
        ResultadoEsperado = c.ResultadoEsperado,
        CausasSugeridas = c.CausasComuns,
        EvidenciasSugeridas = c.EvidenciasComuns
    };
}

/// <summary>Um CUIDADO prescrito — a etapa 4. A frequência é parte do cuidado, não detalhe.</summary>
public sealed partial class LinhaCuidadoEnfermagem : ObservableObject
{
    public string? Codigo { get; init; }

    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string _frequencia = string.Empty;

    /// <summary>
    /// "Se necessário" — o cuidado só se executa quando a condição acontece ("se dor > 5").
    ///
    /// ⚠️ Sem esta caixinha o campo do domínio não teria PORTA, e a regra que ele existe
    /// para sustentar — SOS não conta como pendência — nunca valeria: todo cuidado
    /// condicional apareceria eternamente aguardando no quadro da sala, e o contador que
    /// diz o que falta fazer passaria a apontar para nada.
    /// </summary>
    [ObservableProperty] private bool _seNecessario;

    /// <summary>
    /// Veio SUGERIDO por um diagnóstico do catálogo. A tela marca isso na linha para a
    /// enfermeira saber o que ela escolheu e o que o sistema propôs — proposta que não se
    /// distingue da decisão é proposta que ninguém confere.
    /// </summary>
    public bool Sugerido { get; init; }

    public CuidadoEnfermagem Colher() => new()
    {
        Codigo = Codigo,
        Descricao = Descricao.Trim(),
        Frequencia = string.IsNullOrWhiteSpace(Frequencia) ? null : Frequencia.Trim(),
        SeNecessario = SeNecessario
    };

    public static LinhaCuidadoEnfermagem Do(CuidadoCatalogo c, bool sugerido) => new()
    {
        Codigo = c.Codigo,
        Descricao = c.Descricao,
        Frequencia = c.FrequenciaSugerida,
        Sugerido = sugerido
    };
}

/// <summary>Uma linha da evolução de enfermagem, do jeito que ela se lê.</summary>
public sealed class LinhaEvolucaoEnfermagem
{
    public required int Id { get; init; }
    public required string Hora { get; init; }
    public required string Data { get; init; }
    public required string Texto { get; init; }
    public required string? SinaisVitais { get; init; }
    public required string Autor { get; init; }
    public required string Registro { get; init; }
    public required bool Intercorrencia { get; init; }
    public required bool Cancelada { get; init; }
    public required bool Substituida { get; init; }

    /// <summary>
    /// Esta passagem é do horário que está aberto agora.
    ///
    /// ⚠️ Ela é o primeiro LEITOR de <c>EvolucaoEnfermagem.AgendamentoId</c>. O campo era
    /// gravado desde que a entidade nasceu (parcela 71), preservado na retificação — e
    /// nenhuma consulta, tela ou papel o lia: dado gravado sem leitor, o defeito recorrente
    /// do projeto. Sem este marcador, dizer que "a passagem fica ligada a esta sessão" seria
    /// uma promessa que o código não cumpre (a armadilha da parcela 67).
    ///
    /// O que ele responde é a pergunta de quem está com o paciente na frente: <i>já
    /// registrei alguma coisa NESTA passagem, ou o que estou vendo é de outro dia?</i>
    /// </summary>
    public required bool DestaSessao { get; init; }
    public required string? Marca { get; init; }
    public required string? Folha { get; init; }

    /// <summary>
    /// A linha de assinatura já montada: autor · registro · folha, com o separador
    /// resolvido AQUI (parcela 72).
    ///
    /// ⚠️ A tela montava a frase com dois <c>&lt;Run Text=" · " /&gt;</c> FIXOS, e
    /// <see cref="Folha"/> é nulo no caso NORMAL — a passagem avulsa, que é a maioria. A
    /// seção nascia com quase toda linha terminando num <c>" · "</c> pendurado. Separador
    /// fixo em volta de campo opcional é sempre isto; quem monta a frase é quem sabe o que
    /// existe nela.
    /// </summary>
    public string Assinatura => string.Join(" · ", new[] { Autor, Registro, Folha }
        .Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>Vigente é o que se pode corrigir ou cancelar — o resto é histórico.</summary>
    public bool Vigente => !Cancelada && !Substituida;

    /// <summary>Destaque só no que está valendo: intercorrência já corrigida é histórico.</summary>
    public bool EmDestaque => Intercorrencia && Vigente;

    public static LinhaEvolucaoEnfermagem De(
        EvolucaoEnfermagem e, bool substituida, bool mostrarData,
        int? agendamentoAberto = null)
    {
        var marca = e.Cancelada
            ? $"CANCELADA — {e.MotivoCancelamento}"
            : substituida
                ? "CORRIGIDA — vale o registro seguinte"
                : e.EhRetificacao
                    ? $"corrige o registro anterior — {e.MotivoRetificacao}"
                    : null;

        return new LinhaEvolucaoEnfermagem
        {
            Id = e.Id,
            Hora = e.Hora.ToString("HH\\:mm"),
            Data = mostrarData ? e.Data.ToString("dd/MM/yyyy") : string.Empty,
            Texto = e.Texto,
            // Os sinais e o ACESSO na mesma linha: quem lê a passagem anterior precisa
            // dos dois — o acesso é o achado que a próxima punção consulta.
            SinaisVitais = e.AcessoResumo is { } acesso
                ? (e.SinaisVitaisResumidos is { } sv
                    ? $"{sv} \u00B7 acesso: {acesso}"
                    : $"acesso: {acesso}")
                : e.SinaisVitaisResumidos,
            Autor = string.IsNullOrWhiteSpace(e.AutorConselho)
                ? e.AutorNome
                : $"{e.AutorNome} · {e.AutorConselho}",
            Registro = $"registrado {e.RegistradoEm:dd/MM/yyyy HH\\:mm}",
            Intercorrencia = e.Intercorrencia,
            Cancelada = e.Cancelada,
            Substituida = substituida,
            // Nulo dos dois lados NÃO casa: sem horário aberto, nada é "desta sessão" —
            // senão toda passagem avulsa se anunciaria como da sessão que não existe.
            DestaSessao = agendamentoAberto is { } aberto && e.AgendamentoId == aberto,
            Marca = marca,
            Folha = e.Prescricao?.Numero
        };
    }
}

/// <summary>
/// Uma linha do catálogo de enfermagem, como ela aparece na janela de escolha.
///
/// ⚠️ Ela existe porque o registro do DOMÍNIO não pode carregar estado de tela: quem sabe
/// se este cuidado JÁ ESTÁ no plano é a passagem que está sendo escrita, não o catálogo.
/// E mostrar isso não é enfeite — <c>AdicionarCuidado</c> recusa o repetido **em
/// silêncio**, e clique que não faz nada é o defeito da parcela 41. Aqui o botão some e
/// no lugar dele fica a marca de que já está lá.
/// </summary>
public sealed partial class ItemCatalogoEnfermagem : ObservableObject
{
    public required string Codigo { get; init; }
    public required string Titulo { get; init; }

    /// <summary>O que a caixinha de 240 px nunca teve espaço para mostrar.</summary>
    public string? Detalhe { get; init; }

    [ObservableProperty] private bool _noPlano;
}

/// <summary>
/// UM catálogo de enfermagem aberto em JANELA — o de diagnósticos ou o de cuidados.
///
/// Por que janela, e não a caixinha de antes
/// -----------------------------------------
/// O catálogo é o que a enfermeira FAZ uma ou duas vezes por consulta; a prescrição é o que
/// ela VÊ o tempo todo. A regra de leiaute do projeto decide sozinha: o primeiro é
/// botão/janela, o segundo é a tela (parcela 37, 3ª rodada — o mapa corporal e o formulário
/// de medida saíram de painéis abertos pelo mesmo argumento).
///
/// A caixinha custava caro nos dois sentidos. Ela ocupava 240 px permanentes ao lado de uma
/// lista que nasce VAZIA — a tela abria com um quarto da largura gasto num atalho e o resto
/// em branco —, e o que ela mostrava era um recorte de três linhas com rolagem: a
/// frequência sugerida de cada cuidado e o resultado esperado de cada diagnóstico **nunca
/// apareciam**, porque não cabiam. Dado que o sistema tem e a tela não mostra é o defeito
/// recorrente deste projeto, aqui na variante mais discreta: a caixa está lá, ela só é
/// pequena demais para dizer o que sabe.
///
/// ⚠️ UMA classe para os DOIS catálogos, e uma janela só. Duas seriam duas definições de
/// "escolher do catálogo", e a segunda correção já sairia divergente.
/// </summary>
public sealed partial class CatalogoDeEnfermagem : ObservableObject
{
    private readonly Func<string?, IReadOnlyList<ItemCatalogoEnfermagem>> _buscar;
    private readonly Action<string> _adicionar;
    private readonly Func<string, bool> _estaNoPlano;

    public CatalogoDeEnfermagem(
        string titulo, string explicacao, string dicaDaBusca,
        Func<string?, IReadOnlyList<ItemCatalogoEnfermagem>> buscar,
        Action<string> adicionar,
        Func<string, bool> estaNoPlano)
    {
        Titulo = titulo;
        Explicacao = explicacao;
        DicaDaBusca = dicaDaBusca;
        _buscar = buscar;
        _adicionar = adicionar;
        _estaNoPlano = estaNoPlano;
    }

    public string Titulo { get; }
    public string Explicacao { get; }
    public string DicaDaBusca { get; }

    public ObservableCollection<ItemCatalogoEnfermagem> Itens { get; } = new();

    [ObservableProperty] private string _busca = string.Empty;
    [ObservableProperty] private string _resumo = string.Empty;

    partial void OnBuscaChanged(string value) => Recarregar();

    /// <summary>
    /// Monta a lista com o termo atual. Chamada ao ABRIR a janela, sempre: entre uma
    /// abertura e outra o plano mudou (escolher um diagnóstico traz os cuidados dele
    /// junto), e uma lista que não sabe disso ofereceria "+" para o que já está lá.
    /// </summary>
    public void Recarregar()
    {
        var achados = _buscar(Busca);

        // Entre o Clear() e o último Add não pode haver await — não há nenhum aqui, e a
        // montagem é síncrona de propósito (a regra da parcela 62).
        Itens.Clear();
        foreach (var i in achados)
        {
            i.NoPlano = _estaNoPlano(i.Codigo);
            Itens.Add(i);
        }

        AtualizarResumo();
    }

    /// <summary>
    /// ⚠️ Depois de acrescentar, a lista NÃO é remontada: o estado de cada linha é
    /// corrigido no lugar. Remontar jogaria a rolagem para o topo a cada escolha, e quem
    /// está escolhendo cinco cuidados seguidos perderia o lugar cinco vezes.
    /// </summary>
    [RelayCommand]
    private void Adicionar(ItemCatalogoEnfermagem? item)
    {
        if (item is null || item.NoPlano) return;

        _adicionar(item.Codigo);

        // Um diagnóstico traz os CUIDADOS dele junto — então não basta marcar a linha
        // clicada: o plano pode ter crescido em vários pontos de uma vez.
        foreach (var i in Itens) i.NoPlano = _estaNoPlano(i.Codigo);

        AtualizarResumo();
    }

    /// <summary>
    /// O tamanho do catálogo inteiro, medido uma vez. Ele é uma lista em CÓDIGO e não muda
    /// em tempo de execução — refazer a busca vazia a cada tecla para escrever um número no
    /// resumo seria trabalho a cada letra digitada.
    /// </summary>
    private int? _totalDoCatalogo;

    private void AtualizarResumo()
    {
        _totalDoCatalogo ??= _buscar(null).Count;

        var noPlano = Itens.Count(i => i.NoPlano);
        var termo = Busca.Trim();
        var jaNoPlano = noPlano > 0 ? $", {noPlano} já no plano." : ".";

        // A lista filtrada DIZ que está filtrada — "6 no catálogo" sozinho faria a
        // enfermeira concluir que a clínica só tem seis cuidados cadastrados.
        Resumo = termo.Length > 0
            ? $"“{termo}” — {Itens.Count} de {_totalDoCatalogo} no catálogo{jaNoPlano}"
            : $"{Itens.Count} no catálogo{jaNoPlano}";
    }
}

/// <summary>
/// A EVOLUÇÃO DE ENFERMAGEM (parcela 71) — a janela onde quem executa escreve o que
/// observou no paciente.
///
/// Por que é JANELA e não aba da folha de execução
/// -----------------------------------------------
/// Três razões, e a primeira sozinha decide:
/// 1. <b>A folha encerrada apaga a janela dela inteira</b> (<c>PodeMexer =&gt; PodeChecar
///    &amp;&amp; EmExecucao</c>). Um campo ali dentro nasceria morto exatamente no caso que
///    justifica a feature: a reação que aparece meia hora depois da última bomba.
/// 2. <b>A folha é modal.</b> Com a folha da cadeira 1 aberta, registrar uma reação na
///    cadeira 3 custaria fechar, achar, abrir, escrever — dez gestos com o paciente
///    reagindo. Ela não faz isso; ela chama a médica e escreve depois, que é o que
///    acontece hoje.
/// 3. <b>Dois campos de hora com significados diferentes na mesma tela</b>, num registro
///    cuja razão de existir é a hora ser a certa.
///
/// A janela abre da LINHA DA FILA da sala e também de dentro da folha, e nos dois casos
/// pergunta uma coisa só: <i>o que eu observei nesta passagem</i>.
/// </summary>
public partial class EvolucaoEnfermagemViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private readonly int _pacienteId;
    private readonly int? _prescricaoId;
    private readonly int? _agendamentoId;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 60): a janela recarrega a cada
    /// gravação, e a leitura velha não pode sobrescrever a nova.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>Quando está corrigindo, o registro que vai ser substituído.</summary>
    private int? _retificando;

    /// <summary>
    /// A data do FATO que está sendo corrigido. A retificação a preserva: a técnica que
    /// corrige na segunda um registro observado no sábado não pode mover o fato de dia — a
    /// folha do sábado perderia a linha, e a do dia da correção ganharia uma que não
    /// aconteceu nele.
    /// </summary>
    private DateOnly? _dataCorrigida;

    public string Paciente { get; }

    /// <summary>Contexto numa LINHA de texto, nunca uma faixa: faixa permanente vira moldura.</summary>
    [ObservableProperty] private string _contexto = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public ObservableCollection<LinhaEvolucaoEnfermagem> Registros { get; } = new();

    // ---- O compositor ----

    [ObservableProperty] private string _hora = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private string _texto = string.Empty;
    [ObservableProperty] private bool _intercorrencia;

    [ObservableProperty] private string _sistolica = string.Empty;
    [ObservableProperty] private string _diastolica = string.Empty;
    [ObservableProperty] private string _cardiaca = string.Empty;
    [ObservableProperty] private string _respiratoria = string.Empty;
    [ObservableProperty] private string _temperatura = string.Empty;
    [ObservableProperty] private string _saturacao = string.Empty;
    [ObservableProperty] private string _dor = string.Empty;

    // ==================== O ACESSO VENOSO (parcela 77) ====================
    //
    // A dica do exame físico desta mesma janela manda avaliar "pele, ACESSO VENOSO, edema"
    // — em texto corrido. Estruturado, ele responde a pergunta que a técnica faz antes de
    // puncionar de novo: há quantos dias está esse acesso? A prática troca cateter
    // periférico por TEMPO, e tempo não se conta lendo parágrafo.

    [ObservableProperty] private string _acessoLocal = string.Empty;
    [ObservableProperty] private string _acessoCalibre = string.Empty;

    /// <summary>Quando foi puncionado. Pode ser de ANTES desta passagem: quem assume o
    /// plantão registra o acesso que outra puncionou anteontem.</summary>
    [ObservableProperty] private DateTime? _acessoPuncionadoEm;

    /// <summary>
    /// A reação observada vira ALERGIA na lista de problemas, no mesmo SaveChanges — e é
    /// aqui que o circuito se fecha para o caso que a checagem não cobre: o paciente que
    /// teve reação e mesmo assim completou a infusão.
    /// </summary>
    [ObservableProperty] private string _alergiaObservada = string.Empty;

    // ---- O PROCESSO DE ENFERMAGEM (parcela 73) ----
    //
    // ⚠️ A tela abre no modo ANOTAÇÃO, e a consulta completa é UM clique. É a decisão que
    // o domínio já registra: a técnica que troca um curativo não abre um processo de
    // enfermagem, e obrigá-la faria a clínica escrever "idem" em cinco campos — pior do
    // que o campo vazio, porque parece registro.

    /// <summary>As cinco etapas estão à vista. Desligado, a janela é a anotação de sempre.</summary>
    [ObservableProperty] private bool _consultaCompleta;

    [ObservableProperty] private string _historico = string.Empty;
    [ObservableProperty] private string _exameFisico = string.Empty;
    [ObservableProperty] private string _avaliacao = string.Empty;

    public ObservableCollection<LinhaDiagnosticoEnfermagem> Diagnosticos { get; } = new();
    public ObservableCollection<LinhaCuidadoEnfermagem> Cuidados { get; } = new();

    // ==================== OS DOIS CATÁLOGOS, em janela (parcela 88, 4ª rodada) =========
    //
    // A clínica pediu: "preciso de um pop out para que a enfermeira consiga maximizar e ler
    // tudo e todas as opções". Eles eram duas caixinhas de 240 px com teto de 180 —
    // permanentes ao lado de uma lista que nasce VAZIA, e pequenas demais para mostrar o
    // que o catálogo sabe (a frequência sugerida de cada cuidado, o resultado esperado de
    // cada diagnóstico). Agora são botão + janela redimensionável.
    //
    // ⚠️ A janela recebe ESTE objeto, e não uma cópia: quem grava é a passagem de trás, e
    // dois VMs dariam duas verdades sobre o mesmo plano (a regra da parcela 49).

    public CatalogoDeEnfermagem CatalogoDeDiagnosticos { get; }
    public CatalogoDeEnfermagem CatalogoDeCuidados { get; }

    /// <summary>
    /// As etapas que ficaram vazias — a frase que AVISA sem impedir.
    ///
    /// A enfermeira que colhe o histórico agora e fecha a avaliação depois da infusão está
    /// fazendo o processo certo; recusar o registro incompleto a faria escrever tudo de
    /// memória no fim do dia, que é o que o módulo do Consultório existe para combater.
    /// </summary>
    public string? EtapasEmFalta
    {
        get
        {
            if (!ConsultaCompleta) return null;

            var faltam = new List<string>();
            if (string.IsNullOrWhiteSpace(Historico)) faltam.Add("histórico");
            if (Diagnosticos.Count == 0) faltam.Add("diagnóstico");
            if (Diagnosticos.Count > 0
                && Diagnosticos.All(d => string.IsNullOrWhiteSpace(d.ResultadoEsperado)))
                faltam.Add("resultado esperado");
            if (Cuidados.Count == 0) faltam.Add("prescrição de enfermagem");
            if (string.IsNullOrWhiteSpace(Avaliacao)) faltam.Add("avaliação");

            return faltam.Count == 0
                ? null
                : "Processo de enfermagem incompleto — falta: " + string.Join(", ", faltam)
                  + ". Você pode registrar assim e completar depois (COFEN 358/2009).";
        }
    }

    /// <summary>
    /// ALGUMA das cinco etapas tem conteúdo — é o que decide se desmarcar custa alguma
    /// coisa. Ele NÃO é <c>EtapasEmFalta</c> pelo avesso: aquele responde "o que ainda
    /// falta", este responde "o que já foi escrito", e são perguntas diferentes.
    /// </summary>
    public bool ConsultaTemConteudo
        => !string.IsNullOrWhiteSpace(Historico)
           || !string.IsNullOrWhiteSpace(ExameFisico)
           || !string.IsNullOrWhiteSpace(Avaliacao)
           || Diagnosticos.Any(d => !string.IsNullOrWhiteSpace(d.Titulo))
           || Cuidados.Any(c => !string.IsNullOrWhiteSpace(c.Descricao));

    /// <summary>
    /// A propriedade está sendo mudada por CÓDIGO, e não pela pessoa.
    ///
    /// ⚠️ Ele protege três caminhos, e os dois últimos não são teoria: a reversão da
    /// própria confirmação abaixo; a limpeza depois de gravar; e o <c>Corrigir</c>, que
    /// carrega um registro antigo — abrir uma ANOTAÇÃO com uma consulta pela metade na
    /// tela perguntaria "voltar para anotação?" no meio de uma carga que a pessoa não
    /// pediu. Pergunta que aparece sem gesto é pergunta que se fecha sem ler.
    /// </summary>
    private bool _ajustandoConsultaPorCodigo;

    /// <summary>Muda <see cref="ConsultaCompleta"/> sem disparar a confirmação.</summary>
    private void DefinirConsultaCompleta(bool valor)
    {
        _ajustandoConsultaPorCodigo = true;
        ConsultaCompleta = valor;
        _ajustandoConsultaPorCodigo = false;
    }

    /// <summary>
    /// ⚠️ DESMARCAR DESCARTA AS CINCO ETAPAS na gravação — <see cref="ColherProcesso"/>
    /// devolve <c>null</c> no modo anotação — e esse custo deixou de ser visível quando a
    /// consulta saiu da tela e foi para a janela (parcela 88, 5ª rodada). Antes, as abas
    /// sumiam na frente da pessoa; agora não muda nada na tela, e o que ela escreveu não
    /// vai para o prontuário sem uma palavra.
    ///
    /// Por isso a pergunta — e só quando há o que perder: cobrar confirmação para
    /// desmarcar uma consulta em branco treinaria a equipe a confirmar sem ler, que é
    /// exatamente o que este projeto recusa desde a parcela 65.
    ///
    /// O texto DIZ que as etapas continuam na tela: elas ficam no ViewModel e voltam ao
    /// marcar de novo. Quem as perde é quem grava como anotação.
    /// </summary>
    partial void OnConsultaCompletaChanged(bool value)
    {
        if (!value && !_ajustandoConsultaPorCodigo && ConsultaTemConteudo)
        {
            var seguir = _dialogo.ConfirmarPerigo(
                "Voltar para anotação de passagem?",
                "As cinco etapas que você escreveu NÃO vão para o prontuário se este "
                + "registro for gravado como anotação.\n\nElas continuam na tela — basta "
                + "marcar “Consulta de enfermagem” de novo para voltar a elas.\n\n"
                + "Deseja continuar?");

            if (!seguir)
            {
                DefinirConsultaCompleta(true);
                return;
            }
        }

        OnPropertyChanged(nameof(EtapasEmFalta));
    }

    partial void OnHistoricoChanged(string value) => OnPropertyChanged(nameof(EtapasEmFalta));
    partial void OnAvaliacaoChanged(string value) => OnPropertyChanged(nameof(EtapasEmFalta));

    /// <summary>
    /// Traz o diagnóstico do catálogo E os cuidados que ele costuma pedir.
    ///
    /// ⚠️ Os cuidados entram para a enfermeira DESMARCAR o que não vale, e cada um sai
    /// marcado como "sugerido" na linha. Aplicar em silêncio produziria uma prescrição de
    /// enfermagem que ninguém leu — e cuidado prescrito é cuidado que alguém vai ter de
    /// checar depois.
    /// </summary>
    /// <summary>
    /// A definição ÚNICA de "acrescentar este diagnóstico" — chamada pelo catálogo em
    /// janela. Deixou de ser <c>[RelayCommand]</c> quando a caixinha saiu da tela: comando
    /// que nenhum XAML amarra é superfície sem leitor.
    /// </summary>
    private void AdicionarDiagnostico(DiagnosticoCatalogo? escolhido)
    {
        if (escolhido is null) return;
        if (Diagnosticos.Any(d => d.Codigo == escolhido.Codigo)) return;

        Diagnosticos.Add(LinhaDiagnosticoEnfermagem.Do(escolhido));

        foreach (var c in CatalogoEnfermagem.CuidadosDe(escolhido.Codigo))
            if (!Cuidados.Any(x => x.Codigo == c.Codigo))
                Cuidados.Add(LinhaCuidadoEnfermagem.Do(c, sugerido: true));

        OnPropertyChanged(nameof(EtapasEmFalta));
    }

    /// <summary>O diagnóstico escrito à MÃO — o catálogo é atalho, não lista fechada.</summary>
    [RelayCommand]
    private void NovoDiagnostico()
    {
        Diagnosticos.Add(new LinhaDiagnosticoEnfermagem());
        OnPropertyChanged(nameof(EtapasEmFalta));
    }

    [RelayCommand]
    private void RemoverDiagnostico(LinhaDiagnosticoEnfermagem? linha)
    {
        if (linha is null) return;
        Diagnosticos.Remove(linha);
        OnPropertyChanged(nameof(EtapasEmFalta));
    }

    /// <summary>A definição ÚNICA de "acrescentar este cuidado" — ver o irmão acima.</summary>
    private void AdicionarCuidado(CuidadoCatalogo? escolhido)
    {
        if (escolhido is null) return;
        if (Cuidados.Any(c => c.Codigo == escolhido.Codigo)) return;

        Cuidados.Add(LinhaCuidadoEnfermagem.Do(escolhido, sugerido: false));
        OnPropertyChanged(nameof(EtapasEmFalta));
    }

    [RelayCommand]
    private void NovoCuidado()
    {
        Cuidados.Add(new LinhaCuidadoEnfermagem());
        OnPropertyChanged(nameof(EtapasEmFalta));
    }

    [RelayCommand]
    private void RemoverCuidado(LinhaCuidadoEnfermagem? linha)
    {
        if (linha is null) return;
        Cuidados.Remove(linha);
        OnPropertyChanged(nameof(EtapasEmFalta));
    }

    /// <summary>Está corrigindo um registro anterior — o rótulo do botão muda e o motivo é pedido.</summary>
    [ObservableProperty] private bool _corrigindo;

    [ObservableProperty] private string _rotuloDoBotao = "Registrar";

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeRegistrar =>
        SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem);

    /// <summary>
    /// O compositor está vazio — nada foi digitado desta passagem.
    ///
    /// ⚠️ Ela existe para o FINALIZAR do posto clínico não perguntar <i>"você não escreveu
    /// nada desta sessão?"</i> a quem acabou de escrever uma consulta de enfermagem
    /// inteira. Perguntar sobre o registro que a pessoa tem na frente é o jeito mais rápido
    /// de ensinar alguém a fechar diálogo sem ler — que é a causa raiz do incidente da
    /// parcela 65.
    ///
    /// ⚠️ Ela olha TUDO o que grava, e não só o texto: sinais vitais, acesso venoso, alergia
    /// observada e as cinco etapas contam. É a lição da parcela 74 — <c>SessaoEmBranco</c>
    /// decide se a tela PERGUNTA, e usá-la para decidir se GRAVA descartou em silêncio a
    /// sessão mais comum da casa.
    /// </summary>
    public bool CompositorEmBranco =>
        string.IsNullOrWhiteSpace(Texto)
        && string.IsNullOrWhiteSpace(AlergiaObservada)
        && LerSinaisVitais() is null
        && LerAcesso() is null
        && string.IsNullOrWhiteSpace(Historico)
        && string.IsNullOrWhiteSpace(ExameFisico)
        && string.IsNullOrWhiteSpace(Avaliacao)
        && Diagnosticos.All(d => string.IsNullOrWhiteSpace(d.Titulo))
        && Cuidados.All(c => string.IsNullOrWhiteSpace(c.Descricao));

    public EvolucaoEnfermagemViewModel(
        IServiceScopeFactory escopos, IDialogoService dialogo,
        int pacienteId, string paciente,
        int? prescricaoId = null, string? folha = null, int? agendamentoId = null)
    {
        _escopos = escopos;
        _dialogo = dialogo;
        _pacienteId = pacienteId;
        _prescricaoId = prescricaoId;
        _agendamentoId = agendamentoId;
        Paciente = paciente;

        // Os dois catálogos, com a LEITURA e a ESCRITA apontando para este mesmo objeto —
        // a janela é uma porta, nunca uma segunda verdade sobre o plano.
        CatalogoDeDiagnosticos = new CatalogoDeEnfermagem(
            titulo: "Diagnósticos de enfermagem",
            explicacao: "Lista desta clínica — NÃO é a NANDA-I, que é licenciada. Escolher um "
                        + "traz junto os cuidados que ele costuma pedir, para você desmarcar o "
                        + "que não vale. O que não estiver aqui se escreve à mão.",
            dicaDaBusca: "Buscar por diagnóstico ou causa",
            buscar: termo => CatalogoEnfermagem.BuscarDiagnosticos(termo)
                .Select(d => new ItemCatalogoEnfermagem
                {
                    Codigo = d.Codigo,
                    Titulo = d.Titulo,
                    // O que a caixinha de 240 px nunca teve como mostrar.
                    Detalhe = $"Resultado esperado: {d.ResultadoEsperado}"
                              + (d.Cuidados.Count > 0
                                  ? $"  ·  traz {d.Cuidados.Count} cuidado(s)"
                                  : string.Empty)
                })
                .ToList(),
            adicionar: codigo => AdicionarDiagnostico(CatalogoEnfermagem.Diagnostico(codigo)),
            estaNoPlano: codigo => Diagnosticos.Any(d => d.Codigo == codigo));

        CatalogoDeCuidados = new CatalogoDeEnfermagem(
            titulo: "Cuidados de enfermagem",
            explicacao: "O que a ENFERMAGEM prescreve. A frequência vem sugerida e é sua para "
                        + "ajustar: “verificar o acesso” sem dizer de quanto em quanto tempo é "
                        + "lembrete, não prescrição. O que não estiver aqui se escreve à mão.",
            dicaDaBusca: "Buscar cuidado",
            buscar: termo => CatalogoEnfermagem.BuscarCuidados(termo)
                .Select(c => new ItemCatalogoEnfermagem
                {
                    Codigo = c.Codigo,
                    Titulo = c.Descricao,
                    Detalhe = string.IsNullOrWhiteSpace(c.FrequenciaSugerida)
                        ? null
                        : $"Frequência sugerida: {c.FrequenciaSugerida}"
                })
                .ToList(),
            adicionar: codigo => AdicionarCuidado(CatalogoEnfermagem.Cuidado(codigo)),
            estaNoPlano: codigo => Cuidados.Any(c => c.Codigo == codigo));

        // ⚠️ A tela DIZ a que a passagem fica ligada, em vez de deixar a pessoa supor — e
        // são três casos diferentes, não dois. Ligada ao HORÁRIO ela entra na ficha DAQUELA
        // sessão e na conferência do consultório; solta, é registro do paciente e não de
        // sessão nenhuma. Escrever "não está ligada a uma folha" para quem veio da agenda
        // seria verdade pela metade, e é a metade que não interessa.
        Contexto = prescricaoId is not null
            ? $"Durante a folha de infusão {folha}."
            : agendamentoId is not null
                ? "Ligada ao horário do atendimento — a passagem fica registrada nesta sessão."
                : "Registro do paciente — esta passagem não está ligada a uma folha de infusão.";

        _ = CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        // ⚠️ Sem paciente não há o que ler. A SEÇÃO do posto clínico constrói o compositor
        // antes de saber quem é (o workspace monta as nove seções de uma vez), e ir ao
        // banco perguntar pelo paciente zero é uma ida a mais por navegação, num banco
        // remoto — e uma linha de log por navegação, que é como uma trilha útil vira ruído.
        if (_pacienteId == 0 && _prescricaoId is null)
        {
            Registros.Clear();
            Carregando = false;
            return;
        }

        var geracao = ++_geracaoCarga;
        Carregando = true;
        NaoVerificado = false;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>();

            // Dentro de uma folha, a lista é a DAQUELA sessão; solta, é a do paciente.
            var lista = _prescricaoId is { } id
                ? await servico.DaPrescricaoAsync(id)
                : await servico.DoPacienteAsync(_pacienteId, limite: 50);

            if (geracao != _geracaoCarga) return;

            var substituidas = lista
                .Where(e => e.RetificaEvolucaoId is not null)
                .Select(e => e.RetificaEvolucaoId!.Value)
                .ToHashSet();

            // ⚠️ Entre o Clear() e o último Add não pode haver await (parcela 62): monta-se
            // em lista local e só então publica.
            var linhas = lista
                .OrderByDescending(e => e.Data).ThenByDescending(e => e.Hora).ThenByDescending(e => e.Id)
                .Select(e => LinhaEvolucaoEnfermagem.De(
                    e, substituidas.Contains(e.Id), mostrarData: _prescricaoId is null,
                    agendamentoAberto: _agendamentoId))
                .ToList();

            Registros.Clear();
            foreach (var l in linhas) Registros.Add(l);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            NaoVerificado = true;
            Diagnostico.Registrar("Enfermagem — evolução não pôde ser carregada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        try
        {
            // A segunda barreira: o IsEnabled explica, este impede (atalho de teclado passa
            // pelo primeiro).
            SessaoUsuario.Atual.Exigir(
                Permissao.RegistrarEvolucaoEnfermagem, "registrar evolução de enfermagem");

            if (!TimeOnly.TryParseExact(Hora.Trim(), "HH\\:mm", out var hora)
                && !TimeOnly.TryParse(Hora.Trim(), CultureInfo.GetCultureInfo("pt-BR"), out hora))
            {
                Mensagem = "Informe a hora do que foi observado, no formato 14:20.";
                MensagemEhErro = true;
                return;
            }

            var sinais = LerSinaisVitais();
            var acesso = LerAcesso();

            string? motivo = null;
            if (Corrigindo)
            {
                motivo = _dialogo.PerguntarTexto(
                    "Corrigir o registro",
                    "Por que o registro anterior estava errado? Ele não é apagado — fica na "
                    + "folha, marcado, com esta explicação ao lado.",
                    string.Empty);

                // Diálogo cancelado: sair calado é o certo (a exceção da checagem 21).
                if (string.IsNullOrWhiteSpace(motivo)) return;
            }

            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>();
                var hoje = DateOnly.FromDateTime(DateTime.Today);

                if (Corrigindo && _retificando is { } alvo)
                    // ⚠️ A data é a DO FATO (_dataCorrigida), nunca `hoje`: a técnica que
                    // corrige na segunda um registro observado no sábado estaria movendo o
                    // fato de dia — e a folha do sábado perderia a linha. E o PROCESSO vai
                    // junto: sem ele, corrigir uma vírgula descartava a consulta inteira.
                    await servico.RetificarAsync(
                        alvo, _dataCorrigida ?? hoje, hora, Texto, Executante(), motivo!,
                        Intercorrencia, sinais, ColherProcesso(), acesso);
                else
                    await servico.RegistrarAsync(
                        _pacienteId, hoje, hora, Texto, Executante(),
                        _prescricaoId, _agendamentoId, Intercorrencia, sinais,
                        string.IsNullOrWhiteSpace(AlergiaObservada) ? null : AlergiaObservada,
                        ColherProcesso(), acesso);
            }

            var alergia = !string.IsNullOrWhiteSpace(AlergiaObservada);
            LimparCompositor();
            await CarregarAsync();

            Mensagem = alergia
                ? "Registrado. A alergia entrou na lista de problemas do paciente e vai "
                  + "alertar na próxima prescrição."
                : "Registrado no prontuário do paciente.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — evolução não pôde ser registrada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Traz o registro para o compositor a fim de CORRIGI-LO. O anterior não é apagado:
    /// grava-se outro apontando para ele, com o motivo.
    /// </summary>
    [RelayCommand]
    private async Task CorrigirAsync(LinhaEvolucaoEnfermagem? linha)
    {
        // Guarda sobre PARÂMETRO: nunca dispara vindo de botão de linha, e sair calado
        // aqui é o certo (a exceção declarada da checagem 21).
        if (linha is null) return;

        try
        {
            // ⚠️ CARREGA O REGISTRO INTEIRO (parcela 74, 2ª rodada). Antes esta função
            // copiava só hora, texto e intercorrência da LINHA da tela — e a linha é um
            // resumo formatado: os sinais vitais vêm como frase ("PA 160/100"), e o
            // processo de enfermagem não vem. Resultado: gravar a correção descartava a
            // pressão aferida e as cinco etapas da consulta, com a tela dizendo
            // "Registrado".
            using var scope = _escopos.CreateScope();
            var repo = scope.ServiceProvider
                .GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>();
            var original = await repo.ObterEvolucaoEnfermagemAsync(linha.Id);
            if (original is null)
            {
                Mensagem = "O registro não foi encontrado — recarregue a folha.";
                MensagemEhErro = true;
                return;
            }

            _retificando = original.Id;
            _dataCorrigida = original.Data;
            Corrigindo = true;
            RotuloDoBotao = "Gravar correção";

            Hora = original.Hora.ToString("HH\\:mm");
            Texto = original.Texto;
            Intercorrencia = original.Intercorrencia;

            // Os sinais voltam como NÚMEROS, que é o que o compositor edita.
            Sistolica = Texto2(original.PressaoSistolica);
            Diastolica = Texto2(original.PressaoDiastolica);
            Cardiaca = Texto2(original.FrequenciaCardiaca);
            Respiratoria = Texto2(original.FrequenciaRespiratoria);
            Temperatura = original.Temperatura?.ToString(
                System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
            Saturacao = Texto2(original.SaturacaoOxigenio);
            Dor = Texto2(original.Dor);

            // O acesso volta junto: correção que o perde faria a versão corrigida afirmar
            // que o paciente não tinha acesso venoso.
            AcessoLocal = original.AcessoLocal ?? string.Empty;
            AcessoCalibre = original.AcessoCalibre ?? string.Empty;
            AcessoPuncionadoEm = original.AcessoPuncionadoEm?.ToDateTime(TimeOnly.MinValue);

            // E as cinco etapas, quando o registro era uma CONSULTA.
            Historico = original.Historico ?? string.Empty;
            ExameFisico = original.ExameFisico ?? string.Empty;
            Avaliacao = original.Avaliacao ?? string.Empty;

            Diagnosticos.Clear();
            foreach (var d in original.Diagnosticos.OrderBy(d => d.Ordem))
                Diagnosticos.Add(new LinhaDiagnosticoEnfermagem
                {
                    Codigo = d.Codigo,
                    Titulo = d.Titulo,
                    RelacionadoA = d.RelacionadoA ?? string.Empty,
                    EvidenciadoPor = d.EvidenciadoPor ?? string.Empty,
                    ResultadoEsperado = d.ResultadoEsperado ?? string.Empty
                });

            Cuidados.Clear();
            foreach (var c in original.Cuidados.OrderBy(c => c.Ordem))
                Cuidados.Add(new LinhaCuidadoEnfermagem
                {
                    Codigo = c.Codigo,
                    Descricao = c.Descricao,
                    Frequencia = c.Frequencia ?? string.Empty,
                    // Sem esta linha, retificar a evolução DESLIGARIA o "se necessário" de
                    // todo cuidado condicional, em silêncio.
                    SeNecessario = c.SeNecessario
                });

            DefinirConsultaCompleta(original.EhConsulta);

            Mensagem = $"Corrigindo o registro de {original.Data:dd/MM/yyyy} às "
                     + $"{original.Hora:HH\\:mm}. O original fica na folha, marcado, e a "
                     + "correção mantém a data do fato.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — registro não pôde ser aberto para correção", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private static string Texto2(int? valor) => valor?.ToString() ?? string.Empty;

    [RelayCommand]
    private void CancelarCorrecao()
    {
        LimparCompositor();
        Mensagem = null;
    }

    /// <summary>
    /// Cancela o registro lançado no paciente ou na sessão ERRADA. A linha FICA, com o
    /// motivo — registro clínico não se apaga.
    /// </summary>
    [RelayCommand]
    private async Task CancelarRegistroAsync(LinhaEvolucaoEnfermagem? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(
                Permissao.RegistrarEvolucaoEnfermagem, "cancelar evolução de enfermagem");

            var motivo = _dialogo.PerguntarTexto(
                "Cancelar o registro",
                "Cancelar é para o registro lançado no paciente ou na sessão ERRADA — para "
                + "corrigir o texto, use Corrigir. Por que este registro está sendo "
                + "cancelado? Ele não é apagado: fica no prontuário, marcado.",
                string.Empty);

            if (string.IsNullOrWhiteSpace(motivo)) return;

            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>();
                await servico.CancelarAsync(linha.Id, motivo, SessaoUsuario.Atual.Operador);
            }

            await CarregarAsync();
            Mensagem = "Registro cancelado. Ele continua no prontuário, com o motivo.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — registro não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // ---- Apoio ----

    /// <summary>
    /// As cinco etapas, como a tela as tem agora. Devolve <c>null</c> no modo anotação —
    /// e é o que faz o registro curto continuar sendo curto.
    /// </summary>
    private ProcessoDeEnfermagem? ColherProcesso()
        => !ConsultaCompleta
            ? null
            : new ProcessoDeEnfermagem(
                Historico,
                ExameFisico,
                Avaliacao,
                // A ORDEM da tela é a ordem da folha: ela é a sequência em que a
                // enfermeira pensou o caso, e reordenar daria uma lista que não é a dela.
                Diagnosticos.Where(d => !string.IsNullOrWhiteSpace(d.Titulo))
                    .Select(d => d.Colher()).ToList(),
                Cuidados.Where(c => !string.IsNullOrWhiteSpace(c.Descricao))
                    .Select(c => c.Colher()).ToList());

    private void LimparCompositor()
    {
        // ⚠️ As etapas saem JUNTO. Sem isto, o próximo registro nasce com os diagnósticos
        // do anterior — e a janela da sala abre para o paciente da cadeira seguinte.
        Historico = ExameFisico = Avaliacao = string.Empty;
        Diagnosticos.Clear();
        Cuidados.Clear();
        CatalogoDeDiagnosticos.Busca = CatalogoDeCuidados.Busca = string.Empty;

        // ⚠️ Por CÓDIGO: aqui a limpeza dos campos acima já zerou `ConsultaTemConteudo`,
        // mas depender dessa ordem seria uma armadilha para quem reordenar as linhas —
        // e o preço seria a pergunta "voltar para anotação?" logo depois de um Registrar
        // que deu certo.
        DefinirConsultaCompleta(false);

        _retificando = null;
        _dataCorrigida = null;
        Corrigindo = false;
        RotuloDoBotao = "Registrar";
        Texto = string.Empty;
        Intercorrencia = false;
        AlergiaObservada = string.Empty;
        Sistolica = Diastolica = Cardiaca = Respiratoria = string.Empty;
        Temperatura = Saturacao = Dor = string.Empty;
        AcessoLocal = AcessoCalibre = string.Empty;
        AcessoPuncionadoEm = null;
        Hora = DateTime.Now.ToString("HH:mm");
    }

    private SinaisVitais? LerSinaisVitais()
    {
        var sinais = new SinaisVitais(
            Inteiro(Sistolica), Inteiro(Diastolica), Inteiro(Cardiaca),
            Inteiro(Respiratoria), Decimal(Temperatura), Inteiro(Saturacao), Inteiro(Dor));

        return sinais == new SinaisVitais() ? null : sinais;
    }

    /// <summary>
    /// O acesso descrito nesta passagem, ou nulo quando nada foi dito dele.
    ///
    /// ⚠️ Nulo é "não foi avaliado", e não "não há acesso" — são coisas diferentes e a
    /// segunda se escreve no texto da passagem. Gravar campo vazio como se fosse a
    /// afirmação de ausência é inventar um achado que ninguém fez.
    /// </summary>
    private AcessoVenoso? LerAcesso()
    {
        var acesso = new AcessoVenoso(
            string.IsNullOrWhiteSpace(AcessoLocal) ? null : AcessoLocal.Trim(),
            string.IsNullOrWhiteSpace(AcessoCalibre) ? null : AcessoCalibre.Trim(),
            AcessoPuncionadoEm is { } d ? DateOnly.FromDateTime(d) : null);

        return acesso == new AcessoVenoso() ? null : acesso;
    }

    private static int? Inteiro(string texto)
        => int.TryParse(texto.Trim(), out var valor) ? valor : null;

    /// <summary>
    /// Aceita "36,4" e "36.4": a técnica digita com vírgula, e o teclado numérico do
    /// notebook manda ponto. Recusar um dos dois seria transformar o teclado da máquina
    /// num requisito de registro clínico.
    /// </summary>
    private static decimal? Decimal(string texto)
    {
        var limpo = texto.Trim().Replace('.', ',');
        return decimal.TryParse(limpo, NumberStyles.Number,
            CultureInfo.GetCultureInfo("pt-BR"), out var valor) ? valor : null;
    }

    /// <summary>
    /// Quem escreve é quem fez LOGIN, com o COREN do cadastro dele. ⚠️ Evolução de
    /// enfermagem sem registro no conselho não é evolução de enfermagem — o número é parte
    /// da assinatura profissional.
    /// </summary>
    private static IdentificacaoExecutante Executante() => new(
        UsuarioId: SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
        Nome: SessaoUsuario.Atual.Autenticado
            ? SessaoUsuario.Atual.Nome
            : SessaoUsuario.Atual.Operador,
        Conselho: SessaoUsuario.Atual.RegistroConselho);
}
