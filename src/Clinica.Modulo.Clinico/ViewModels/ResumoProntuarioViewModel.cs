using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma evolução no modal de leitura rápida — data, autor e o texto composto.</summary>
public sealed record EvolucaoResumida(string DataTexto, string Autor, string Texto);

/// <summary>Um bloco da anamnese no modal — rótulo + texto; só existe se foi escrito.</summary>
public sealed record BlocoAnamnese(string Rotulo, string Texto);

/// <summary>
/// O modal "Prontuário — fulano" da tela de Prontuários (o quick-view do mockup): ler as
/// últimas evoluções, a anamnese e os anexos SEM sair da lista. Ele não substitui a tela
/// do paciente — o botão "Abrir prontuário completo" leva até ela — e não escreve nada.
///
/// O "Assinar evolução" do mockup NÃO existe aqui de propósito: evolução não é assinável
/// no domínio, e o botão prometeria uma garantia que o código não dá. Quem tem assinatura
/// de verdade é a ANAMNESE emitida, e o Assinar dela mora na linha da lista.
/// </summary>
public sealed partial class ResumoProntuarioViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;
    private readonly int _pacienteId;
    private readonly int? _documentoAnamneseId;

    /// <summary>A janela observa para fechar quando a navegação foi disparada.</summary>
    public event Action? NavegouParaOPaciente;

    [ObservableProperty] private string _paciente;
    [ObservableProperty] private string? _linhaIdentificacao;
    [ObservableProperty] private string? _alergiasTexto;
    [ObservableProperty] private string? _diagnosticosTexto;
    [ObservableProperty] private string? _planoTerapeutico;

    public ObservableCollection<EvolucaoResumida> Evolucoes { get; } = [];
    public ObservableCollection<BlocoAnamnese> Anamnese { get; } = [];
    public ObservableCollection<AnexoDoPaciente> Anexos { get; } = [];

    [ObservableProperty] private bool _semEvolucoes;
    [ObservableProperty] private bool _semAnamnese;
    [ObservableProperty] private bool _semAnexos;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>A 2ª via só existe quando o modal foi aberto de uma ANAMNESE emitida.</summary>
    public bool TemFolhaDeAnamnese => _documentoAnamneseId is not null;

    /// <summary>Aberto de uma linha de anamnese, o modal já cai na aba dela.
    /// Atribuída no CONSTRUTOR (parâmetro, não init — a armadilha da parcela 72).</summary>
    public int AbaInicial => _documentoAnamneseId is null ? 0 : 1;

    public ResumoProntuarioViewModel(
        IServiceScopeFactory escopos, PacienteEmFoco foco, int pacienteId, string paciente,
        int? documentoAnamneseId = null)
    {
        _escopos = escopos;
        _foco = foco;
        _pacienteId = pacienteId;
        _documentoAnamneseId = documentoAnamneseId;
        Paciente = paciente;

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        Carregando = true;
        try
        {
            CabecalhoClinicoPaciente? cabecalho;
            IReadOnlyList<Evolucao> evolucoes;
            AnamnesePaciente? anamnese;
            IReadOnlyList<AnexoDoPaciente> anexos;

            // SEQUENCIAL, nunca WhenAll: mesmo DbContext do escopo (parcela 74).
            using (var scope = _escopos.CreateScope())
            {
                // Modal de dado de saúde deixa rastro — uma vez por abertura, nunca por aba.
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ProntuarioClinico);

                var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();
                var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
                var anamneses = scope.ServiceProvider.GetRequiredService<AnamneseService>();
                var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();

                cabecalho = await consultorio.CabecalhoAsync(_pacienteId);
                evolucoes = await prontuario.DoPacienteAsync(_pacienteId);
                anamnese = await anamneses.DoPacienteAsync(_pacienteId);
                anexos = await repo.AnexosDoPacienteAsync(_pacienteId);
            }

            if (cabecalho is not null)
            {
                LinhaIdentificacao = cabecalho.Linha;
                AlergiasTexto = cabecalho.AlergiasTexto;
                DiagnosticosTexto = cabecalho.UltimosDiagnosticos.Count == 0
                    ? null
                    : string.Join(" · ", cabecalho.UltimosDiagnosticos);
            }

            // O plano do mockup é o último escrito — a decisão vigente, não um histórico.
            PlanoTerapeutico = evolucoes
                .OrderByDescending(e => e.Data).ThenByDescending(e => e.Id)
                .Select(e => e.PlanoTerapeutico)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

            Evolucoes.Clear();
            foreach (var e in evolucoes.OrderByDescending(x => x.Data).ThenByDescending(x => x.Id))
                Evolucoes.Add(new EvolucaoResumida(
                    $"{e.Data:dd/MM/yyyy}",
                    e.Profissional?.Nome ?? e.CriadoPor ?? "sem autor registrado",
                    ComporTexto(e)));

            Anamnese.Clear();
            if (anamnese is not null)
            {
                Adicionar("Antecedentes pessoais", anamnese.AntecedentesPessoais);
                Adicionar("Antecedentes familiares", anamnese.AntecedentesFamiliares);
                Adicionar("Hábitos de vida", anamnese.HabitosDeVida);
                Adicionar("História obstétrica", anamnese.HistoriaObstetrica);
                Adicionar("Revisão de sistemas", anamnese.RevisaoDeSistemas);
                Adicionar("Observações", anamnese.Observacoes);
            }

            Anexos.Clear();
            foreach (var a in anexos) Anexos.Add(a);

            SemEvolucoes = Evolucoes.Count == 0;
            SemAnamnese = Anamnese.Count == 0;
            SemAnexos = Anexos.Count == 0;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — o resumo do prontuário não pôde ser carregado", ex);
            Mensagem = "Não foi possível carregar o prontuário — tente pelo botão "
                + "\"Abrir prontuário completo\".";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private void Adicionar(string rotulo, string? texto)
    {
        if (!string.IsNullOrWhiteSpace(texto))
            Anamnese.Add(new BlocoAnamnese(rotulo, texto.Trim()));
    }

    /// <summary>O texto da entrada: a evolução escrita, e o que a sessão registrou quando
    /// não há texto (EVA e conduta) — entrada em branco não explica nada.</summary>
    private static string ComporTexto(Evolucao e)
    {
        if (!string.IsNullOrWhiteSpace(e.TextoEvolucao)) return e.TextoEvolucao.Trim();

        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(e.QueixaPrincipal)) partes.Add(e.QueixaPrincipal.Trim());
        if (e.TemParEva) partes.Add($"EVA {e.EvaAntes} → {e.EvaDepois}");
        else if (e.EvaAntes is { } eva) partes.Add($"EVA {eva}");
        if (!string.IsNullOrWhiteSpace(e.Conduta)) partes.Add(e.Conduta.Trim());
        return partes.Count > 0 ? string.Join(" · ", partes) : "(sessão sem texto)";
    }

    [RelayCommand]
    private void AbrirCompleto()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir o prontuário");
            _foco.Definir(_pacienteId, Paciente);
            if (NavegacaoSuite.Ir(ModuloClinico.ChaveProntuario))
            {
                NavegouParaOPaciente?.Invoke();
                return;
            }
            Mensagem = "Não deu para abrir o prontuário do paciente.";
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Segunda via da ANAMNESE emitida — os bytes/conteúdo de quando ela saiu,
    /// nunca o que o prontuário diz hoje (a regra do documento clínico).</summary>
    [RelayCommand]
    private async Task SegundaViaAsync()
    {
        if (_documentoAnamneseId is not { } docId) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir a folha de anamnese");

            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.GerarAsync(docId, await parametros.ObterPrestadorAsync());

                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.Documento);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"Anamnese-{Paciente}.pdf"));
            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — a 2ª via da anamnese não pôde ser gerada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
