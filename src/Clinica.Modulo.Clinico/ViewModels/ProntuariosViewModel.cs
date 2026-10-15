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

/// <summary>
/// A tela de PRONTUÁRIOS do handoff (set/2026): a lista plana do que o profissional
/// escreveu (evoluções), do que emitiu montado do prontuário (anamneses) e do que ainda
/// FALTA escrever (as sessões sem evolução), com busca por paciente.
///
/// A situação segue o que cada linha É no domínio — anamnese tem assinatura de verdade
/// ("A assinar"/"Assinada"); evolução não é assinável, e o pendente real dela é a sessão
/// sem registro ("A escrever"). Pintar "Assinada" numa evolução seria a garantia
/// aparente que o projeto recusa desde a parcela 3.
/// </summary>
public sealed partial class ProntuariosViewModel : ObservableObject, ICarregarAoAbrir, Clinica.Desktop.Shell.Modulos.IAtualizarAoVoltar
{
    /// <summary>A janela das ESCRITAS (evoluções e anamneses); o resumo DIZ o recorte.
    /// As pendências de sessão começam na data de corte definida pela política.</summary>
    public const int JanelaDias = 90;

    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    private int _geracaoCarga;
    private IReadOnlyList<LinhaProntuario> _todas = [];

    public ObservableCollection<LinhaProntuario> Linhas { get; } = [];

    [ObservableProperty] private string? _termo;
    [ObservableProperty] private bool _soAssinaturasPendentes;
    [ObservableProperty] private string _rotuloPendentes = "Assinar pendentes";
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _resumo;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    [ObservableProperty] private bool _listaDaClinica;
    [ObservableProperty] private string? _motivoDaLista;

    // As metades VISÍVEIS das barreiras; quem impede é o Exigir de cada comando.
    public bool PodeEscrever => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);
    public bool PodeAssinarDocumento => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public ProntuariosViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;
    }

    partial void OnTermoChanged(string? value) => Refiltrar();
    partial void OnSoAssinaturasPendentesChanged(bool value) => Refiltrar();

    [RelayCommand]
    public Task CarregarAsync() => LerAsync(silencioso: false);

    /// <summary>Voltou do histórico com a lista que mostrava: relê por trás, sem spinner.</summary>
    public void AtualizarAoVoltar()
    {
        if (Carregando) return;
        _ = LerAsync(silencioso: true);
    }

    private async Task LerAsync(bool silencioso)
    {
        var geracao = ++_geracaoCarga;
        if (!silencioso) Carregando = true;

        try
        {
            var profissionalId = PostoClinico.ProfissionalDaLista();
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            // As três leituras são independentes: cada uma no SEU escopo (seu DbContext) e
            // ao mesmo tempo — nunca WhenAll sobre o mesmo escopo (parcela 74).
            async Task<T> Ler<T>(Func<IServiceProvider, Task<T>> leitura)
            {
                using var scope = _escopos.CreateScope();
                return await leitura(scope.ServiceProvider);
            }
            var tPendentes = Ler(sp => sp.GetRequiredService<ConsultorioService>()
                .RegistrosPendentesAsync(hoje, profissionalId));
            var tEvolucoes = Ler(sp => sp.GetRequiredService<IClinicaRepositorio>()
                .EvolucoesParaProntuariosAsync(hoje.AddDays(-JanelaDias), hoje, profissionalId));
            var tAnamneses = Ler(sp => sp.GetRequiredService<IClinicaRepositorio>()
                .DocumentosClinicosNoPeriodoAsync(hoje.AddDays(-JanelaDias), hoje, TipoDocumentoClinico.Anamnese));
            await Task.WhenAll(tPendentes, tEvolucoes, tAnamneses);
            IReadOnlyList<RegistroPendente> pendentes = tPendentes.Result;
            IReadOnlyList<LinhaEvolucaoProntuarios> evolucoes = tEvolucoes.Result;
            IReadOnlyList<DocumentoClinico> anamneses = tAnamneses.Result;

            if (geracao != _geracaoCarga) return;

            // O filtro de profissional do documento segue a MESMA regra das evoluções
            // (documento sem profissional entra) — a consulta da central não filtra.
            if (profissionalId is { } id)
                anamneses = anamneses
                    .Where(d => d.ProfissionalId == id || d.ProfissionalId == null)
                    .ToList();

            ListaDaClinica = profissionalId is null;
            MotivoDaLista = PostoClinico.MotivoDaListaAmpla();

            _todas = ListaDeProntuarios.Montar(pendentes, evolucoes, anamneses);

            var aAssinar = _todas.Count(l => l.Situacao == SituacaoLinhaProntuario.AAssinar);
            RotuloPendentes = aAssinar > 0 ? $"Assinar pendentes ({aAssinar})" : "Assinar pendentes";

            NaoVerificado = false;
            Refiltrar();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — a lista de prontuários não pôde ser carregada", ex);
            if (!silencioso) NaoVerificado = true;
        }
        finally
        {
            if (!silencioso && geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>Remonta DA MEMÓRIA — filtrar não vai ao banco, e entre o Clear e o
    /// último Add não há await (parcela 62).</summary>
    private void Refiltrar()
    {
        var visiveis = _todas.AsEnumerable();

        if (SoAssinaturasPendentes)
            visiveis = visiveis.Where(l => l.Situacao == SituacaoLinhaProntuario.AAssinar);

        if (!string.IsNullOrWhiteSpace(Termo))
            visiveis = visiveis.Where(l =>
                l.Paciente.Contains(Termo.Trim(), StringComparison.CurrentCultureIgnoreCase));

        var lista = visiveis.ToList();
        Linhas.Clear();
        foreach (var l in lista) Linhas.Add(l);

        // O resumo diz o RECORTE — filtro esquecido respondendo "tudo em dia" faria a
        // lista mentir (a lição do painel de pendências).
        var recorte = SoAssinaturasPendentes || !string.IsNullOrWhiteSpace(Termo)
            ? $"{lista.Count} de {_todas.Count} registro(s)"
            : $"{_todas.Count} registro(s)";
        Resumo = $"{recorte} · escritas dos últimos {JanelaDias} dias · sessões sem evolução desde "
                 + $"{PoliticaRegistroPendente.DataInicio:dd/MM/yyyy}";
    }

    /// <summary>"Novo prontuário" = escrever um atendimento — a tela de sempre, que abre
    /// com a fila do dia para escolher quem.</summary>
    [RelayCommand]
    private void NovoProntuario()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");
            if (!NavegacaoSuite.Ir(PostoClinico.ChaveDoAtendimento()))
            {
                Mensagem = "Não deu para abrir a tela de atendimento.";
                MensagemEhErro = true;
            }
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Escreve a evolução da sessão pendente — o MESMO destino do "Sessões sem
    /// evolução": o atendimento daquele horário, com o vínculo viajando junto.</summary>
    [RelayCommand]
    private void Escrever(LinhaProntuario? linha)
    {
        if (linha is null || !linha.MostraEscrever) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId,
                          dataDoHorario: linha.Data);
            if (!NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento))
            {
                Mensagem = "Não deu para abrir o atendimento desta sessão.";
                MensagemEhErro = true;
            }
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Abre o QUICK-VIEW do prontuário (o modal do mockup): as evoluções, a anamnese e os
    /// anexos, sem sair da lista. Linha de anamnese abre já na aba dela, com a 2ª via da
    /// folha emitida no rodapé; o prontuário completo fica a um botão de distância.
    /// </summary>
    [RelayCommand]
    private void Abrir(LinhaProntuario? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir o prontuário");

            var vm = new ResumoProntuarioViewModel(
                _escopos, _foco, linha.PacienteId, linha.Paciente,
                linha.Natureza == NaturezaLinhaProntuario.Anamnese ? linha.DocumentoId : null);
            var janela = new Clinica.Clinico.Janelas.ResumoProntuarioWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };
            janela.ShowDialog();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — a linha do prontuário não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Assina a anamnese com o certificado ICP-Brasil — o MESMO fluxo da tela de
    /// Prescrições; o arquivo assinado é salvo e aberto (é o ARQUIVO que vale).</summary>
    [RelayCommand]
    private async Task AssinarAsync(LinhaProntuario? linha)
    {
        if (linha is null || !linha.MostraAssinar || linha.DocumentoId is not { } docId) return;

        try
        {
            SessaoUsuario.Atual.Exigir(
                CentralDocumentosService.AcessoParaEmitir(TipoDocumentoClinico.Anamnese),
                "assinar documento clínico");

            using var certificado = EscolherCertificadoWindow.Perguntar(
                $"Assinar anamnese {linha.Numero}", JanelaDona.Atual(), _escopos);
            if (certificado is null) return;

            DocumentoAssinado assinado;
            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDeDocumentoClinicoService>();
                assinado = await assinaturas.AssinarAsync(
                    docId, certificado,
                    SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                    SessaoUsuario.Atual.Operador);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                assinado.Pdf, ImpressaoPdf.NomeSeguro(assinado.NomeArquivo));

            await CarregarAsync();
            Mensagem = erro ?? $"Anamnese {linha.Numero} assinada.";
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — a anamnese não pôde ser assinada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
