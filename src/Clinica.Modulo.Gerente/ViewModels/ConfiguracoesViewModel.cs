using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>
/// Configurações da clínica — item de primeiro nível da proposta que a suíte não tinha.
///
/// As chaves do <see cref="ParametrosService"/> existem desde a parcela 0, mas só o app
/// de FATURAMENTO sabia editá-las. Como ele está congelado e roda num posto só, a direção
/// precisava sentar naquela máquina para mudar a jornada que o indicador de ocupação usa
/// ou o prazo de recurso de glosa que o painel vigia. Agora edita do Gerente.
///
/// O que NÃO está aqui, de propósito:
/// - taxa de cartão e alíquota de imposto têm tela própria no Financeiro, onde o assunto
///   é dinheiro e quem mexe é quem concilia;
/// - o número do próximo lote TISS é sequência viva do faturamento; mexer nela de fora
///   produziria dois lotes com o mesmo número, e o convênio recusa o segundo.
///
/// Salvar é POR BLOCO e não numa tecla só: são assuntos diferentes, e um botão único
/// gravaria a jornada junto com o prazo de glosa que o usuário nem olhou.
/// </summary>
public sealed partial class ConfiguracoesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;

    // ---- Clínica / prestador ----
    [ObservableProperty] private string? _razaoSocial;
    [ObservableProperty] private string? _nomeFantasia;
    [ObservableProperty] private string? _cnpj;
    [ObservableProperty] private string? _cnes;
    [ObservableProperty] private string? _endereco;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private string? _email;

    // ---- Agenda e indicadores ----
    [ObservableProperty] private string? _jornadaDiariaMinutos;

    // ---- Marketing ----
    [ObservableProperty] private string? _diasInatividadeRecall;

    // ---- Faturamento (a direção lê; o app congelado continua sendo quem fatura) ----
    [ObservableProperty] private string? _janelaAlertaConsulta;
    [ObservableProperty] private string? _prazoRecursoGlosa;
    [ObservableProperty] private string? _intervaloRodadaPendencias;
    [ObservableProperty] private bool _rodadaAplicaConsultas;
    [ObservableProperty] private bool _rodadaAplicaCarteirinhas;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// Metade VISÍVEL da permissão. Configuração vale para a clínica inteira, então exige
    /// a mesma permissão de quem administra acessos — não é ajuste de tela pessoal.
    /// </summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.GerenciarUsuarios);

    public ConfiguracoesViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var p = scope.ServiceProvider.GetRequiredService<ParametrosService>();

            var prestador = await p.ObterPrestadorAsync();
            RazaoSocial = prestador.RazaoSocial;
            NomeFantasia = prestador.NomeFantasia;
            Cnpj = prestador.Cnpj;
            Cnes = prestador.Cnes;
            Endereco = prestador.Endereco;
            Telefone = prestador.Telefone;
            Email = prestador.Email;

            JornadaDiariaMinutos = (await p.ObterJornadaDiariaAsync()).ToString();
            DiasInatividadeRecall = (await p.ObterDiasInatividadeRecallAsync()).ToString();
            JanelaAlertaConsulta = (await p.ObterJanelaAlertaConsultaAsync()).ToString();
            PrazoRecursoGlosa = (await p.ObterPrazoRecursoGlosaAsync()).ToString();
            IntervaloRodadaPendencias = (await p.ObterIntervaloRodadaPendenciasAsync()).ToString();
            RodadaAplicaConsultas = await p.ObterRodadaAplicaConsultasAsync();
            RodadaAplicaCarteirinhas = await p.ObterRodadaAplicaCarteirinhasAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — configurações não puderam ser lidas", ex);
            Erro($"Não foi possível ler as configurações: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Dados que saem impressos na capa de lote, na guia TISS e nos documentos clínicos.
    /// Errado aqui é errado em todo papel que o paciente leva.
    /// </summary>
    [RelayCommand]
    private async Task SalvarPrestadorAsync()
        => await ExecutarAsync(async p =>
        {
            var atual = await p.ObterPrestadorAsync();

            // Só os campos desta tela são reescritos: os códigos TUSS e o registro ANS
            // vêm da tela do faturamento, e sobrescrevê-los com vazio quebraria o XML do
            // próximo lote sem ninguém perceber até a operadora recusar.
            atual.RazaoSocial = Limpar(RazaoSocial);
            atual.NomeFantasia = Limpar(NomeFantasia);
            atual.Cnpj = Limpar(Cnpj);
            atual.Cnes = Limpar(Cnes);
            atual.Endereco = Limpar(Endereco);
            atual.Telefone = Limpar(Telefone);
            atual.Email = Limpar(Email);

            await p.SalvarPrestadorAsync(atual);
            return "Dados da clínica salvos.";
        });

    [RelayCommand]
    private async Task SalvarOperacaoAsync()
        => await ExecutarAsync(async p =>
        {
            if (!TentarLerInteiro(JornadaDiariaMinutos, 1, 24 * 60, out var jornada))
                throw new InvalidOperationException(
                    "A jornada diária vai de 1 a 1440 minutos (8 h = 480).");
            if (!TentarLerInteiro(DiasInatividadeRecall, 1, 3650, out var recall))
                throw new InvalidOperationException("Os dias de inatividade do recall vão de 1 a 3650.");

            await p.SalvarJornadaDiariaAsync(jornada);
            await p.SalvarDiasInatividadeRecallAsync(recall);
            return "Operação e marketing salvos.";
        });

    [RelayCommand]
    private async Task SalvarFaturamentoAsync()
        => await ExecutarAsync(async p =>
        {
            if (!TentarLerInteiro(JanelaAlertaConsulta, 1, 365, out var janela))
                throw new InvalidOperationException("A janela de alerta da consulta vai de 1 a 365 dias.");
            if (!TentarLerInteiro(PrazoRecursoGlosa, 1, 365, out var glosa))
                throw new InvalidOperationException("O prazo de recurso de glosa vai de 1 a 365 dias.");
            if (!TentarLerInteiro(IntervaloRodadaPendencias, 1, 365, out var rodada))
                throw new InvalidOperationException("O prazo de decisão da rodada vai de 1 a 365 dias.");

            await p.SalvarJanelaAlertaConsultaAsync(janela);
            await p.SalvarPrazoRecursoGlosaAsync(glosa);
            await p.SalvarIntervaloRodadaPendenciasAsync(rodada);
            await p.SalvarRodadaAplicaAsync(RodadaAplicaConsultas, RodadaAplicaCarteirinhas);
            return "Regras de faturamento salvas.";
        });

    /// <summary>
    /// Envelope dos três blocos: exige a permissão, abre escopo, grava e recarrega.
    /// Recarregar depois de salvar não é zelo: o serviço aplica limites (clamp), e a tela
    /// tem de mostrar o que ficou GRAVADO, não o que foi digitado.
    /// </summary>
    private async Task ExecutarAsync(Func<ParametrosService, Task<string>> acao)
    {
        Mensagem = null;
        MensagemEhErro = false;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "mudar as configurações da clínica");

            using var scope = _escopos.CreateScope();
            var p = scope.ServiceProvider.GetRequiredService<ParametrosService>();

            var ok = await acao(p);
            _snackbar.Sucesso(ok);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — configuração não pôde ser salva", ex);
            Erro(ex.Message);
        }
    }

    private static bool TentarLerInteiro(string? texto, int minimo, int maximo, out int valor)
        => int.TryParse(texto, out valor) && valor >= minimo && valor <= maximo;

    private static string? Limpar(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
