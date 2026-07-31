using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Infrastructure;
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

    /// <summary>Chave Pix e cidade: os dois campos que o código Pix exige e não existiam.</summary>
    [ObservableProperty] private string? _chavePix;
    [ObservableProperty] private string? _cidade;

    /// <summary>
    /// O tipo deduzido da chave, mostrado ao lado dela.
    ///
    /// CPF e celular têm os mesmos onze dígitos, e é o único caso em que a dedução pode
    /// errar — mostrar o resultado aqui faz o erro aparecer no CADASTRO, e não no
    /// extrato do mês que vem.
    /// </summary>
    public string TipoChavePixRotulo => string.IsNullOrWhiteSpace(ChavePix)
        ? "Sem chave cadastrada — o sistema não consegue gerar código Pix."
        : PixService.Classificar(ChavePix) switch
        {
            TipoChavePix.Cpf => "Reconhecida como CPF.",
            TipoChavePix.Cnpj => "Reconhecida como CNPJ.",
            TipoChavePix.Email => "Reconhecida como e-mail.",
            TipoChavePix.Telefone => "Reconhecida como telefone.",
            TipoChavePix.Aleatoria => "Reconhecida como chave aleatória.",
            _ => "Formato não reconhecido — confira antes de usar."
        };

    partial void OnChavePixChanged(string? value) => OnPropertyChanged(nameof(TipoChavePixRotulo));

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
            ChavePix = prestador.ChavePix;
            Cidade = prestador.Cidade;

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
            atual.ChavePix = Limpar(ChavePix);
            atual.Cidade = Limpar(Cidade);

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

    // ==================== Backup da base (parcela 35) ====================
    //
    // O BackupService existia desde a parcela 34 e NENHUMA TELA O CHAMAVA — o defeito que
    // este repositório documenta como recorrente, cometido na parcela anterior. Sem esta
    // porta a clínica tem backup no código e nenhum no disco.
    //
    // Mora em Configurações e não em tela própria porque é ato de manutenção, feito uma
    // vez por semana pela direção, e não trabalho do dia.

    [ObservableProperty] private string _resumoBackup = string.Empty;

    /// <summary>
    /// Gera o backup completo e deixa o usuário escolher onde gravar.
    /// </summary>
    /// <remarks>
    /// O destino é escolhido de propósito, e não fixado numa pasta: cópia que fica na
    /// mesma máquina do banco não é cópia de segurança — o pendrive ou a nuvem é que
    /// fazem dela um plano B de verdade. A tela diz isso ao lado do botão.
    /// </remarks>
    [RelayCommand]
    private async Task FazerBackupAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "gerar o backup da base");

            ResumoBackup = "Lendo a base…";

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<BackupService>();

            ManifestoBackup? manifesto = null;

            var caminho = await ImpressaoPdf.SalvarAsync(
                async saida => manifesto = await servico.GerarAsync(saida),
                ImpressaoPdf.NomeSeguro($"backup-clinica-{DateTime.Today:yyyy-MM-dd}.json"),
                "Backup da clínica (*.json)|*.json", ".json");

            if (caminho is null)
            {
                ResumoBackup = string.Empty;
                return;
            }

            ResumoBackup = manifesto is null
                ? $"Backup gravado em {caminho}."
                : $"{manifesto.TotalLinhas:N0} registro(s) de {manifesto.TotalTabelas} tabela(s) "
                  + $"gravados em {caminho}.";

            _snackbar.Sucesso("Backup gerado.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — backup não pôde ser gerado", ex);
            Erro(ex.Message);
            ResumoBackup = string.Empty;
        }
    }

    /// <summary>
    /// Confere um arquivo de backup sem restaurar nada.
    /// </summary>
    /// <remarks>
    /// É o que faz o backup valer alguma coisa. Arquivo que ninguém sabe se prestou é o
    /// mesmo que não ter backup — e a clínica só descobriria no dia em que precisasse
    /// dele, que é o único dia em que não dá para descobrir.
    /// </remarks>
    [RelayCommand]
    private async Task ConferirBackupAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "conferir um backup");

            var caminho = ImpressaoPdf.Escolher(
                "Backup da clínica (*.json)|*.json", "Escolha o backup para conferir");
            if (caminho is null) return;

            ResumoBackup = "Conferindo…";

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<BackupService>();

            await using var entrada = System.IO.File.OpenRead(caminho);
            var m = await servico.ConferirAsync(entrada);

            // Backup vazio é tecnicamente válido e praticamente inútil: a diferença
            // precisa estar escrita, senão a direção guarda um arquivo que não serve.
            ResumoBackup = m.Vazio
                ? $"ATENÇÃO: o arquivo abriu, tem {m.TotalTabelas} tabela(s) e NENHUM registro. "
                  + "Ele não serve para restaurar a clínica."
                : $"Backup de {m.GeradoEm:dd/MM/yyyy HH:mm} — {m.TotalLinhas:N0} registro(s) "
                  + $"em {m.TotalTabelas} tabela(s). "
                  + $"Pacientes: {m.Tabela("Pacientes")?.Linhas ?? 0:N0} · "
                  + $"Evoluções: {m.Tabela("Evolucoes")?.Linhas ?? 0:N0} · "
                  + $"Lançamentos: {m.Tabela("Lancamentos")?.Linhas ?? 0:N0}.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — backup não pôde ser conferido", ex);
            Erro(ex.Message);
            ResumoBackup = string.Empty;
        }
    }

    /// <summary>
    /// A tela NÃO restaura.
    /// </summary>
    /// <remarks>
    /// `BackupService.RestaurarAsync` existe, é testado e só entra em base vazia — mas
    /// restaurar é ato de implantação ou de desastre, feito com alguém que sabe o que
    /// está fazendo, e não um botão ao lado de "salvar jornada diária". Um clique errado
    /// aqui não tem volta, e a proteção certa para isso não é uma caixa de confirmação:
    /// é não haver botão.
    /// </remarks>
    private const string SobreRestauracao =
        "Para RESTAURAR um backup, fale com o suporte: a restauração só entra numa base "
        + "vazia e é feita junto com quem acompanha a operação.";

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
