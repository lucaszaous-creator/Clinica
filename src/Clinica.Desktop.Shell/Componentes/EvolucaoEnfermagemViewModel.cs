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
        EvolucaoEnfermagem e, bool substituida, bool mostrarData)
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
            SinaisVitais = e.SinaisVitaisResumidos,
            Autor = string.IsNullOrWhiteSpace(e.AutorConselho)
                ? e.AutorNome
                : $"{e.AutorNome} · {e.AutorConselho}",
            Registro = $"registrado {e.RegistradoEm:dd/MM/yyyy HH\\:mm}",
            Intercorrencia = e.Intercorrencia,
            Cancelada = e.Cancelada,
            Substituida = substituida,
            Marca = marca,
            Folha = e.Prescricao?.Numero
        };
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

    /// <summary>O catálogo, filtrado pela busca — atalho, e a tela diz que é atalho.</summary>
    public ObservableCollection<DiagnosticoCatalogo> CatalogoDiagnosticos { get; } = new();
    public ObservableCollection<CuidadoCatalogo> CatalogoCuidados { get; } = new();

    [ObservableProperty] private string _buscaDiagnostico = string.Empty;
    [ObservableProperty] private string _buscaCuidado = string.Empty;

    partial void OnBuscaDiagnosticoChanged(string value) => FiltrarDiagnosticos();
    partial void OnBuscaCuidadoChanged(string value) => FiltrarCuidados();

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

    partial void OnConsultaCompletaChanged(bool value)
    {
        if (value && CatalogoDiagnosticos.Count == 0)
        {
            FiltrarDiagnosticos();
            FiltrarCuidados();
        }

        OnPropertyChanged(nameof(EtapasEmFalta));
    }

    partial void OnHistoricoChanged(string value) => OnPropertyChanged(nameof(EtapasEmFalta));
    partial void OnAvaliacaoChanged(string value) => OnPropertyChanged(nameof(EtapasEmFalta));

    private void FiltrarDiagnosticos()
    {
        var achados = CatalogoEnfermagem.BuscarDiagnosticos(BuscaDiagnostico).ToList();
        CatalogoDiagnosticos.Clear();
        foreach (var d in achados) CatalogoDiagnosticos.Add(d);
    }

    private void FiltrarCuidados()
    {
        var achados = CatalogoEnfermagem.BuscarCuidados(BuscaCuidado).ToList();
        CatalogoCuidados.Clear();
        foreach (var c in achados) CatalogoCuidados.Add(c);
    }

    /// <summary>
    /// Traz o diagnóstico do catálogo E os cuidados que ele costuma pedir.
    ///
    /// ⚠️ Os cuidados entram para a enfermeira DESMARCAR o que não vale, e cada um sai
    /// marcado como "sugerido" na linha. Aplicar em silêncio produziria uma prescrição de
    /// enfermagem que ninguém leu — e cuidado prescrito é cuidado que alguém vai ter de
    /// checar depois.
    /// </summary>
    [RelayCommand]
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

    [RelayCommand]
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

        Contexto = prescricaoId is null
            // ⚠️ A janela DIZ que não há folha, em vez de deixar a pessoa supor: é a
            // passagem avulsa (curativo, observação, triagem), e o registro é do paciente.
            ? "Registro do paciente — esta passagem não está ligada a uma folha de infusão."
            : $"Durante a folha de infusão {folha}.";

        _ = CarregarAsync();
    }

    public async Task CarregarAsync()
    {
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
                    e, substituidas.Contains(e.Id), mostrarData: _prescricaoId is null))
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
                        Intercorrencia, sinais, ColherProcesso());
                else
                    await servico.RegistrarAsync(
                        _pacienteId, hoje, hora, Texto, Executante(),
                        _prescricaoId, _agendamentoId, Intercorrencia, sinais,
                        string.IsNullOrWhiteSpace(AlergiaObservada) ? null : AlergiaObservada,
                        ColherProcesso());
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

            ConsultaCompleta = original.EhConsulta;

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
        BuscaDiagnostico = BuscaCuidado = string.Empty;
        ConsultaCompleta = false;

        _retificando = null;
        _dataCorrigida = null;
        Corrigindo = false;
        RotuloDoBotao = "Registrar";
        Texto = string.Empty;
        Intercorrencia = false;
        AlergiaObservada = string.Empty;
        Sistolica = Diastolica = Cardiaca = Respiratoria = string.Empty;
        Temperatura = Saturacao = Dor = string.Empty;
        Hora = DateTime.Now.ToString("HH:mm");
    }

    private SinaisVitais? LerSinaisVitais()
    {
        var sinais = new SinaisVitais(
            Inteiro(Sistolica), Inteiro(Diastolica), Inteiro(Cardiaca),
            Inteiro(Respiratoria), Decimal(Temperatura), Inteiro(Saturacao), Inteiro(Dor));

        return sinais == new SinaisVitais() ? null : sinais;
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
