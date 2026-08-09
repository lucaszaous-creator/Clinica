using Clinica.Application.Assinatura.SafeID;
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

    /// <summary>
    /// URL da autoridade de carimbo do tempo (ACT, RFC 3161) usada nas assinaturas
    /// ICP-Brasil da parcela 42. Vazia por padrao: sem ela a assinatura continua valida,
    /// so que a data e a do relogio de quem assinou -- e o PDF escreve isso, em vez de
    /// fingir precisao que nao tem.
    /// </summary>
    [ObservableProperty] private string? _carimbadoraDeTempo;

    // SafeID (parcela 44): as credenciais da APLICAÇÃO no PSC, cadastradas aqui uma vez e
    // lidas por todas as máquinas. É o que evita ter de configurar cada consultório à mão.
    [ObservableProperty] private string? _safeIdClientId;
    [ObservableProperty] private string? _safeIdClientSecret;
    [ObservableProperty] private bool _safeIdHomologacao;

    /// <summary>
    /// As credenciais estão vindo de variável de ambiente, que vence o banco. Enquanto for
    /// verdade, os campos desta tela são só leitura e a tela DIZ por quê — deixar editar o
    /// que não terá efeito, e ainda confirmar "salvo", é pior do que não oferecer o campo.
    /// </summary>
    [ObservableProperty] private bool _safeIdVemDoAmbiente;

    /// <summary>Negado para o XAML amarrar `IsEnabled` sem precisar de conversor.</summary>
    public bool SafeIdEditavel => !SafeIdVemDoAmbiente;

    partial void OnSafeIdVemDoAmbienteChanged(bool value)
        => OnPropertyChanged(nameof(SafeIdEditavel));

    // ---- Faturamento (a direção lê; o app congelado continua sendo quem fatura) ----
    [ObservableProperty] private string? _janelaAlertaConsulta;
    [ObservableProperty] private string? _prazoRecursoGlosa;
    [ObservableProperty] private string? _intervaloRodadaPendencias;
    [ObservableProperty] private bool _rodadaAplicaConsultas;
    [ObservableProperty] private bool _rodadaAplicaCarteirinhas;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, tela vazia por erro fica idêntica
    /// a tela vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
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
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var p = scope.ServiceProvider.GetRequiredService<ParametrosService>();

            // A situação da cópia automática (parcela 52) entra junto do resto: ela é
            // configuração, e a tela que a configura tem de abrir dizendo como está.
            await CarregarPoliticaBackupAsync();

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
            CarimbadoraDeTempo = (await p.ObterCarimbadoraDeTempoAsync())?.ToString();

            // A variável de ambiente VENCE o banco (caminho de teste). Quando ela está em
            // vigor, esta tela passa a mostrar o que o ambiente manda e avisa que é assim —
            // senão a direção desmarcaria a caixa, salvaria, e nada mudaria: campo que
            // aceita edição e não tem efeito é a pior variante do botão que não faz nada,
            // porque o sistema confirma que salvou.
            var doAmbiente = ConfiguracaoSafeID.DoAmbiente();
            SafeIdVemDoAmbiente = doAmbiente is not null;

            if (doAmbiente is not null)
            {
                SafeIdClientId = doAmbiente.ClientId;
                SafeIdClientSecret = "(definido por variável de ambiente)";
                SafeIdHomologacao = doAmbiente.Base == OpcoesSafeID.BaseHomologacao;
            }
            else
            {
                var safeId = await p.ObterCredenciaisSafeIDAsync();
                SafeIdClientId = safeId.ClientId;
                SafeIdClientSecret = safeId.ClientSecret;
                SafeIdHomologacao = ConfiguracaoSafeID.EhHomologacao(safeId.Ambiente);
            }
            JanelaAlertaConsulta = (await p.ObterJanelaAlertaConsultaAsync()).ToString();
            PrazoRecursoGlosa = (await p.ObterPrazoRecursoGlosaAsync()).ToString();
            IntervaloRodadaPendencias = (await p.ObterIntervaloRodadaPendenciasAsync()).ToString();
            RodadaAplicaConsultas = await p.ObterRodadaAplicaConsultasAsync();
            RodadaAplicaCarteirinhas = await p.ObterRodadaAplicaCarteirinhasAsync();
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
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

            // URL vazia e o caso NORMAL (a clinica pode nao ter ACT contratada) e limpa a
            // configuracao. URL escrita errada e recusada aqui: aceita-la faria toda
            // assinatura falhar depois, com um erro de rede que ninguem liga a esta tela.
            var carimbadora = Limpar(CarimbadoraDeTempo);
            if (carimbadora is not null
                && !Uri.TryCreate(carimbadora, UriKind.Absolute, out _))
                throw new InvalidOperationException(
                    "O endereço da carimbadora de tempo precisa ser uma URL completa "
                    + "(ex.: https://act.exemplo.com.br/tsa). Deixe em branco se a clínica "
                    + "não tiver ACT contratada.");

            // Meia credencial do SafeID é recusada aqui. Aceitá-la faria a opção de assinar
            // em nuvem aparecer na tela de quem assina e falhar no clique — e o profissional
            // não tem como adivinhar que o problema mora numa tela do Gerente.
            var clientId = Limpar(SafeIdClientId);
            var clientSecret = Limpar(SafeIdClientSecret);

            if (!SafeIdVemDoAmbiente && clientId is null != (clientSecret is null))
                throw new InvalidOperationException(
                    "O SafeID precisa do client_id E do client_secret. Preencha os dois, ou "
                    + "deixe os dois em branco para assinar apenas com certificado da máquina.");

            await p.SalvarJornadaDiariaAsync(jornada);
            await p.SalvarDiasInatividadeRecallAsync(recall);
            await p.SalvarCarimbadoraDeTempoAsync(carimbadora);
            // Com a variável de ambiente em vigor, os campos mostram o que ELA manda — e o
            // do segredo mostra um texto explicativo, não o segredo. Gravar isso apagaria a
            // credencial real da clínica no banco no dia em que a variável fosse removida.
            if (!SafeIdVemDoAmbiente)
                await p.SalvarCredenciaisSafeIDAsync(
                    clientId, clientSecret, SafeIdHomologacao ? "homologacao" : "producao");

            return SafeIdVemDoAmbiente
                ? "Operação e marketing salvos. O SafeID não foi alterado: ele está vindo de "
                  + "variável de ambiente desta máquina."
                : "Operação e marketing salvos.";
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

    // ---- Cópia AUTOMÁTICA (parcela 52) ----
    //
    // A parcela 35 deu porta ao BackupService e parou no botão. A auditoria de fornecedor
    // da cliente pediu "política de backup, redundância e recuperação" — e botão não é
    // política: backup que depende de alguém lembrar de clicar toda semana existe no
    // manual, não no disco.
    //
    // Estes quatro campos são a política; quem a executa é a abertura do Gerente
    // (`App.xaml.cs` → `PoliticaBackupService.ExecutarSeVencidoAsync`).

    [ObservableProperty] private string _pastaBackup = string.Empty;
    [ObservableProperty] private int _intervaloBackupDias = ParametrosService.IntervaloBackupPadrao;
    [ObservableProperty] private int _copiasBackup = ParametrosService.CopiasBackupPadrao;

    /// <summary>Frase pronta sobre a situação — vem do serviço para a tela não recontar.</summary>
    [ObservableProperty] private string _situacaoBackup = string.Empty;

    /// <summary>
    /// Sem pasta escolhida a cópia automática não roda. É o que faz a tela avisar em vez
    /// de deixar a direção acreditar que está coberta.
    /// </summary>
    [ObservableProperty] private bool _backupDesligado = true;

    [RelayCommand]
    private async Task EscolherPastaBackupAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "configurar o backup");

            var escolhida = ImpressaoPdf.EscolherPasta(
                "Onde gravar as cópias de segurança da clínica");
            if (escolhida is null) return;

            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ParametrosService>()
                .SalvarPastaBackupAsync(escolhida);

            PastaBackup = escolhida;
            await CarregarPoliticaBackupAsync();
            _snackbar.Sucesso("Pasta da cópia automática definida.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — pasta de backup", ex);
            ResumoBackup = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SalvarPoliticaBackupAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "configurar o backup");

            using var scope = _escopos.CreateScope();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

            await parametros.SalvarIntervaloBackupAsync(IntervaloBackupDias);
            await parametros.SalvarCopiasBackupAsync(CopiasBackup);

            // Recarrega porque o serviço aplica limites: a tela tem de mostrar o que ficou
            // GRAVADO, não o que foi digitado.
            await CarregarPoliticaBackupAsync();
            _snackbar.Sucesso("Política de cópia salva.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — política de backup", ex);
            ResumoBackup = ex.Message;
        }
    }

    /// <summary>
    /// Grava uma cópia agora na pasta configurada, pelo MESMO caminho do automático — os
    /// dois não podem divergir no que produzem nem em como rotacionam.
    /// </summary>
    [RelayCommand]
    private async Task CopiarAgoraAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "gerar o backup da base");

            if (string.IsNullOrWhiteSpace(PastaBackup))
            {
                ResumoBackup = "Escolha primeiro a pasta onde as cópias devem ser gravadas.";
                return;
            }

            ResumoBackup = "Copiando a base…";

            using var scope = _escopos.CreateScope();
            var resultado = await scope.ServiceProvider
                .GetRequiredService<PoliticaBackupService>()
                .ExecutarAsync(PastaBackup, CopiasBackup);

            ResumoBackup = resultado.Falhou
                ? $"Não foi possível copiar: {resultado.Erro}"
                : $"Cópia gravada em {resultado.Caminho} — "
                  + $"{resultado.Manifesto!.TotalLinhas} registro(s) em "
                  + $"{resultado.Manifesto.TotalTabelas} tabela(s)."
                  + (resultado.CopiasApagadas > 0
                      ? $" {resultado.CopiasApagadas} cópia(s) antiga(s) saíram da pasta."
                      : string.Empty);

            await CarregarPoliticaBackupAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — cópia agora", ex);
            ResumoBackup = ex.Message;
        }
    }

    /// <summary>Lê a situação da política. Chamado no carregamento da tela e após salvar.</summary>
    private async Task CarregarPoliticaBackupAsync()
    {
        using var scope = _escopos.CreateScope();
        var situacao = await scope.ServiceProvider
            .GetRequiredService<PoliticaBackupService>().SituacaoAsync();

        PastaBackup = situacao.Pasta ?? string.Empty;
        IntervaloBackupDias = situacao.IntervaloDias;
        CopiasBackup = situacao.CopiasAGuardar;
        SituacaoBackup = situacao.Descrever();
        BackupDesligado = !situacao.Configurada;
    }

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
