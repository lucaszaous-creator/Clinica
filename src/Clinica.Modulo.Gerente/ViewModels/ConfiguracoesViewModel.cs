using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Email;
using Clinica.Application.Assinatura.SafeID;
using Clinica.Application.Modelos;
using Clinica.Domain;
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

    /// <summary>
    /// A fábrica de escopos, para a View montar a janela dos termos (parcela 66). Ela é
    /// exposta em vez de a View pedir o escopo ao DI por conta própria porque a tela é
    /// criada pelo shell e não passa pelo contêiner — quem o tem é este ViewModel.
    /// </summary>
    public IServiceScopeFactory Escopos => _escopos;
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

    /// <summary>
    /// O regime "a guia nasce quando o horário entra no sistema" (parcela 70 —
    /// docs/guia-no-agendamento.md). A caixinha diz na tela: ligue DEPOIS de atualizar
    /// todas as máquinas — um app antigo confirmando presença de horário marcado com
    /// guia pelo novo criaria a segunda.
    /// </summary>
    [ObservableProperty] private bool _guiaNoAgendamento;

    /// <summary>O valor que está no banco — a chave só é regravada quando mudou (o ligar dispara backfill).</summary>
    private bool _guiaNoAgendamentoGravada;

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

    // ---- Publicação do documento assinado (parcela 53) ----
    //
    // Duas coisas que parecem uma. O DOMÍNIO é o endereço público que vai selado dentro do
    // QR de cada PDF assinado; o ENDPOINT é para onde o sistema escreve o arquivo. Ter os
    // dois separados é o que permite trocar de provedor um dia mexendo só no DNS — se o QR
    // apontasse para o endereço do provedor, trocar de fornecedor mataria todas as receitas
    // que os pacientes já têm na mão, e elas não podem ser regeradas (a assinatura sela os
    // bytes).

    /// <summary>
    /// Domínio da clínica onde os documentos ficam acessíveis. <b>Vazio DESLIGA a
    /// publicação</b>, e o sistema volta a funcionar como sempre funcionou: o QR aponta
    /// para o validador do ITI e nada sobe para lugar nenhum.
    /// </summary>
    [ObservableProperty] private string? _dominioPublicacao;

    [ObservableProperty] private string? _diasPublicacao;

    [ObservableProperty] private string? _armazenamentoEndpoint;
    [ObservableProperty] private string? _armazenamentoRegiao;
    [ObservableProperty] private string? _armazenamentoBucket;
    [ObservableProperty] private string? _armazenamentoChave;
    [ObservableProperty] private string? _armazenamentoSegredo;

    /// <summary>Mesma regra do SafeID: ambiente em vigor deixa os campos só de leitura.</summary>
    [ObservableProperty] private bool _armazenamentoVemDoAmbiente;

    /// <summary>Negado para o XAML amarrar `IsEnabled` sem precisar de conversor.</summary>
    public bool ArmazenamentoEditavel => !ArmazenamentoVemDoAmbiente;

    partial void OnArmazenamentoVemDoAmbienteChanged(bool value)
        => OnPropertyChanged(nameof(ArmazenamentoEditavel));

    /// <summary>
    /// Como a publicação está agora, em uma frase. A tela ABRE dizendo isto porque
    /// "desligada" e "ligada e quebrada" são estados que se parecem quando não há nada
    /// escrito na tela — e o segundo só apareceria na primeira receita.
    /// </summary>
    [ObservableProperty] private string _situacaoPublicacao = string.Empty;

    [ObservableProperty] private bool _testandoConexao;

    /// <summary>
    /// Negado para o XAML, pela mesma razão de <c>SafeIdEditavel</c>: a suíte não tem
    /// conversor de booleano invertido, e inventar um para um botão só espalharia peça de
    /// design system que ninguém mais usa.
    /// </summary>
    public bool PodeTestarConexao => !TestandoConexao;

    /// <summary>
    /// O botão DIZ que está trabalhando. A ida ao provedor tem tempo-limite de 30 s e até
    /// duas repetições — com endpoint errado, quase um minuto e meio. Só ficar cinza faz
    /// quem clicou concluir que o botão não fez nada, que é o defeito da parcela 41 pela
    /// porta de trás: aqui a ação ACONTECEU, e o que falta é dizer isso.
    /// </summary>
    public string RotuloTesteConexao => TestandoConexao ? "Testando…" : "Testar conexão";

    partial void OnTestandoConexaoChanged(bool value)
    {
        OnPropertyChanged(nameof(PodeTestarConexao));
        OnPropertyChanged(nameof(RotuloTesteConexao));
    }

    // ---- Lembretes por e-mail (set/2026) ----

    /// <summary>
    /// O servidor de saída do lembrete automático da sessão. <b>Servidor e remetente em
    /// branco DESLIGAM o lembrete</b>, e o aviso volta a ser só o WhatsApp de um clique do
    /// balcão — exatamente como antes de o campo existir.
    /// </summary>
    [ObservableProperty] private string? _emailSmtpHost;
    [ObservableProperty] private string? _emailSmtpPorta;
    [ObservableProperty] private string? _emailSmtpUsuario;
    [ObservableProperty] private string? _emailSmtpSenha;
    [ObservableProperty] private string? _emailRemetente;
    [ObservableProperty] private string? _emailRemetenteNome;
    [ObservableProperty] private bool _emailSmtpTls = true;

    /// <summary>Como o lembrete está agora — a tela ABRE dizendo, pela regra da publicação.</summary>
    [ObservableProperty] private string _situacaoEmail = string.Empty;

    /// <summary>
    /// Para onde vai o e-mail de teste. Não é configuração: nasce com o e-mail da clínica
    /// e não é regravado a cada recarga, senão apagaria o que a pessoa acabou de digitar.
    /// </summary>
    [ObservableProperty] private string? _emailDestinoTeste;

    [ObservableProperty] private bool _testandoEmail;

    /// <summary>As DUAS metades visíveis num botão só: a permissão e o "já estou enviando".</summary>
    public bool PodeTestarEmail => PodeEditar && !TestandoEmail;

    /// <summary>O botão DIZ que está trabalhando — a ida ao servidor tem 30 s de tempo-limite.</summary>
    public string RotuloTesteEmail => TestandoEmail ? "Enviando…" : "Enviar e-mail de teste";

    partial void OnTestandoEmailChanged(bool value)
    {
        OnPropertyChanged(nameof(PodeTestarEmail));
        OnPropertyChanged(nameof(RotuloTesteEmail));
    }

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
            GuiaNoAgendamento = _guiaNoAgendamentoGravada = await p.GuiaNoAgendamentoAsync();

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
            await CarregarPublicacaoAsync(p);
            await CarregarTelasAsync(p);
            await CarregarEmailAsync(p);

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

            // A chave do regime "guia no agendamento" (parcela 70) só é tocada quando a
            // direção de fato a mudou: LIGAR dispara o backfill do RealizadoEm, e repeti-lo
            // a cada "Salvar" seria escrever a tabela inteira à toa.
            if (GuiaNoAgendamento != _guiaNoAgendamentoGravada)
            {
                await p.DefinirGuiaNoAgendamentoAsync(GuiaNoAgendamento);
                _guiaNoAgendamentoGravada = GuiaNoAgendamento;
            }
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

    private async Task CarregarPublicacaoAsync(ParametrosService p)
    {
        DominioPublicacao = await p.ObterUrlPublicacaoAsync();
        DiasPublicacao = (await p.ObterDiasPublicacaoAsync()).ToString();

        // O ambiente vence o banco, como no SafeID — e a tela precisa DIZER isso, senão a
        // direção edita, salva, recebe "salvo" e nada muda.
        ArmazenamentoVemDoAmbiente = ProvedorOpcoesArmazenamento.VemDoAmbiente();

        if (OpcoesArmazenamento.DoAmbiente() is { } doAmbiente)
        {
            ArmazenamentoEndpoint = doAmbiente.Endpoint;
            ArmazenamentoRegiao = doAmbiente.Regiao;
            ArmazenamentoBucket = doAmbiente.Bucket;
            ArmazenamentoChave = doAmbiente.Chave;
            ArmazenamentoSegredo = "(definido por variável de ambiente)";
        }
        else
        {
            var (endpoint, regiao, bucket, chave, segredo) =
                await p.ObterCredenciaisArmazenamentoAsync();
            ArmazenamentoEndpoint = endpoint;
            ArmazenamentoRegiao = regiao;
            ArmazenamentoBucket = bucket;
            ArmazenamentoChave = chave;
            ArmazenamentoSegredo = segredo;
        }

        SituacaoPublicacao = DescreverPublicacao();
    }

    /// <summary>
    /// Os três estados que a tela precisa distinguir — e o do meio é o perigoso: domínio
    /// cadastrado sem armazenamento faz a clínica acreditar que está publicando, enquanto
    /// cada assinatura devolve um aviso que ninguém associa a esta tela.
    /// </summary>
    private string DescreverPublicacao()
    {
        var temDominio = !string.IsNullOrWhiteSpace(DominioPublicacao);

        // Vindo do ambiente já está provado: DoAmbiente() só devolve conjunto completo. Do
        // banco, quem decide é a mesma regra que o serviço usa — perguntar de outro jeito
        // aqui faria a tela e a publicação discordarem sobre a mesma configuração.
        var temArmazenamento = ArmazenamentoVemDoAmbiente
            || OpcoesArmazenamento.De(
                ArmazenamentoEndpoint, ArmazenamentoRegiao, ArmazenamentoBucket,
                ArmazenamentoChave, ArmazenamentoSegredo) is not null;

        if (!temDominio)
            return "Publicação DESLIGADA. O QR impresso na receita aponta para o validador "
                + "do ITI, e o paciente precisa levar o arquivo até a farmácia.";

        if (!temArmazenamento)
            return "Domínio cadastrado, mas SEM armazenamento configurado: os documentos "
                + "continuam sendo assinados normalmente e o link não sobe. Preencha "
                + "endpoint, balde e credenciais abaixo — ou limpe o domínio para desligar.";

        return $"Publicação LIGADA em {DominioPublicacao}. O documento assinado fica no ar "
            + $"por {DiasPublicacao} dias e o farmacêutico o abre escaneando o QR.";
    }

    /// <summary>
    /// O domínio e a janela são política; o endpoint e as credenciais são o provedor. Vão
    /// no mesmo botão porque a clínica os configura de uma vez — mas a validação separa,
    /// porque desligar a publicação (domínio vazio) tem de continuar possível mesmo com
    /// credencial pela metade.
    /// </summary>
    [RelayCommand]
    private async Task SalvarPublicacaoAsync()
        => await ExecutarAsync(async p =>
        {
            var dominio = Limpar(DominioPublicacao);

            // Endereço malformado é recusado AQUI. Aceitá-lo produziria um QR apontando
            // para lugar nenhum dentro de um PDF já assinado — e PDF assinado não se
            // corrige: teria de ser cancelado e reemitido.
            if (dominio is not null
                && (!Uri.TryCreate(dominio, UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException(
                    "O domínio de publicação precisa ser um endereço https completo "
                    + "(ex.: https://documentos.suaclinica.com.br). Deixe em branco para "
                    + "desligar a publicação.");

            if (!TentarLerInteiro(
                    DiasPublicacao,
                    PublicacaoDocumento.DiasPublicadoMinimo,
                    PublicacaoDocumento.DiasPublicadoMaximo,
                    out var dias))
                throw new InvalidOperationException(
                    $"Os dias no ar vão de {PublicacaoDocumento.DiasPublicadoMinimo} a "
                    + $"{PublicacaoDocumento.DiasPublicadoMaximo}.");

            var endpoint = Limpar(ArmazenamentoEndpoint);
            var bucket = Limpar(ArmazenamentoBucket);
            var chave = Limpar(ArmazenamentoChave);
            var segredo = Limpar(ArmazenamentoSegredo);

            // Meia credencial é recusada pelo mesmo motivo do SafeID: aceitá-la faria a
            // publicação parecer ligada e falhar no clique de quem está assinando.
            var preenchidos = new[] { endpoint, bucket, chave, segredo }.Count(c => c is not null);
            if (!ArmazenamentoVemDoAmbiente && preenchidos is > 0 and < 4)
                throw new InvalidOperationException(
                    "O armazenamento precisa de endpoint, balde, chave e segredo. Preencha "
                    + "os quatro, ou deixe os quatro em branco.");

            await p.SalvarUrlPublicacaoAsync(dominio);
            await p.SalvarDiasPublicacaoAsync(dias);

            // Com o ambiente em vigor, o campo do segredo mostra um texto explicativo e não
            // o segredo. Gravá-lo apagaria a credencial real da clínica no dia em que a
            // variável fosse removida — a mesma armadilha do SafeID.
            if (!ArmazenamentoVemDoAmbiente)
                await p.SalvarCredenciaisArmazenamentoAsync(
                    endpoint, Limpar(ArmazenamentoRegiao), bucket, chave, segredo);

            return ArmazenamentoVemDoAmbiente
                ? "Publicação salva. As credenciais não foram alteradas: elas estão vindo de "
                  + "variável de ambiente desta máquina."
                : "Publicação salva.";
        });

    /// <summary>
    /// Prova as credenciais gravando e apagando um arquivo de teste, que é exatamente o que
    /// a publicação real faz. Ver <c>PublicacaoDocumentoService.TestarConexaoAsync</c>.
    /// </summary>
    [RelayCommand]
    private async Task TestarConexaoAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        try
        {
            // A segunda barreira. O IsEnabled do botão explica; esta impede — atalho de
            // teclado passa direto pelo primeiro.
            SessaoUsuario.Atual.Exigir(
                Permissao.GerenciarUsuarios, "testar a conexão com o armazenamento");

            TestandoConexao = true;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PublicacaoDocumentoService>();

            if (await servico.TestarConexaoAsync() is { } erro)
            {
                Erro(erro);
                return;
            }

            _snackbar.Sucesso(
                "Conexão com o armazenamento funcionando: o sistema gravou e apagou um "
                + "arquivo de teste.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — teste de conexão do armazenamento falhou", ex);
            Erro(ex.Message);
        }
        finally
        {
            TestandoConexao = false;
        }
    }

    /// <summary>
    /// O endereço do último arquivo de exemplo enviado. Fica na tela para ser aberto e
    /// copiado — dizer "enviado com sucesso" sem dar o endereço deixaria a pessoa
    /// procurando no painel do provedor qual dos objetos é o dela.
    /// </summary>
    [ObservableProperty] private string? _urlDoExemplo;

    /// <summary>Negado para o XAML, como <c>PodeTestarConexao</c>: sem conversor de texto.</summary>
    public bool TemUrlDoExemplo => !string.IsNullOrWhiteSpace(UrlDoExemplo);

    partial void OnUrlDoExemploChanged(string? value)
        => OnPropertyChanged(nameof(TemUrlDoExemplo));

    /// <summary>
    /// Sobe UM PDF de exemplo e o deixa lá, para conferir no navegador que o arquivo ABRE
    /// pela URL pública. É a metade que o "Testar conexão" não cobre — ver
    /// <c>PublicacaoDocumentoService.EnviarExemploAsync</c>.
    /// </summary>
    [RelayCommand]
    private async Task EnviarExemploAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;
        UrlDoExemplo = null;

        try
        {
            SessaoUsuario.Atual.Exigir(
                Permissao.GerenciarUsuarios, "enviar arquivo de teste ao armazenamento");

            TestandoConexao = true;

            using var scope = _escopos.CreateScope();
            var resultado = await scope.ServiceProvider
                .GetRequiredService<PublicacaoDocumentoService>()
                .EnviarExemploAsync();

            if (!resultado.Publicou)
            {
                Erro(resultado.Erro ?? "O arquivo de exemplo não foi enviado.");
                return;
            }

            UrlDoExemplo = resultado.Url;
            _snackbar.Sucesso(
                "Arquivo de exemplo enviado. Abra o endereço abaixo para conferir que ele "
                + "abre — e apague o objeto no painel do provedor depois.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — envio do arquivo de exemplo falhou", ex);
            Erro(ex.Message);
        }
        finally
        {
            TestandoConexao = false;
        }
    }

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

    // ==================== A tela do paciente (parcela 66, 5ª rodada) ====================
    //
    // O monitor virado para quem assina — o modelo da maquininha do cartão. Configurar isto
    // é escolher em qual das telas do Windows a área de assinar deve aparecer.
    //
    // ⚠️ O que se GRAVA é o NOME do dispositivo (`\\.\DISPLAY2`), nunca a posição na lista.
    // O índice muda quando alguém desliga um cabo ou o Windows reordena as telas depois de
    // um reinício — e a tela do paciente passaria a ser a da recepcionista, com o termo em
    // tela cheia por cima do trabalho dela, sem ninguém ter mexido em nada.

    public ObservableCollection<TelaDoSistema> TelasDisponiveis { get; } = [];

    /// <summary>
    /// A escolhida. Nula = a clínica não tem segunda tela, e a coleta acontece numa janela
    /// só — que NÃO é um modo degradado: é o modo de quem tem um monitor, e a clínica pode
    /// ficar meses sem comprar o touch.
    /// </summary>
    [ObservableProperty] private TelaDoSistema? _telaDoPaciente;

    /// <summary>
    /// A tela GRAVADA não está mais ligada. Terceiro estado escrito, e não silêncio: sem
    /// ele, a assinatura voltaria a acontecer numa janela só e a clínica concluiria que a
    /// feature quebrou — quando o que houve foi um cabo solto.
    /// </summary>
    [ObservableProperty] private string _avisoTelaDoPaciente = string.Empty;

    private async Task CarregarEmailAsync(ParametrosService p)
    {
        var c = await p.ObterCamposEmailAsync();
        EmailSmtpHost = c.Host;
        EmailSmtpPorta = c.Porta;
        EmailSmtpUsuario = c.Usuario;
        EmailSmtpSenha = c.Senha;
        EmailRemetente = c.Remetente;
        EmailRemetenteNome = c.NomeRemetente;
        EmailSmtpTls = c.UsarTls;

        // O e-mail da clínica (bloco "A clínica", já carregado acima) é quem costuma testar.
        EmailDestinoTeste ??= Email;

        SituacaoEmail = DescreverEmail(
            OpcoesEmail.De(c.Host, c.Porta, c.Usuario, c.Senha, c.Remetente, c.NomeRemetente, c.UsarTls));
    }

    /// <summary>"Desligado" e "ligado" são frases diferentes — e a ligada diz DE ONDE sai.</summary>
    private static string DescreverEmail(OpcoesEmail? opcoes)
        => opcoes is null
            ? "Lembrete por e-mail DESLIGADO. Preencha o servidor de saída e o remetente para ligar — "
              + "até lá, o aviso da sessão continua sendo o WhatsApp de um clique, na rodada do balcão."
            : "Lembrete por e-mail LIGADO: na abertura da Recepção e do Gerente, quem tem e-mail na ficha "
              + "recebe a confirmação das sessões de hoje, de amanhã e do fim de semana que vier. "
              + $"Sai de {opcoes.Descricao}.";

    /// <summary>
    /// Servidor e remetente andam JUNTOS: um sem o outro não manda nada, e gravar metade
    /// deixaria a tela dizendo "ligado" com o envio quebrado (a regra de
    /// <see cref="OpcoesEmail.De"/>, dita ANTES de gravar).
    /// </summary>
    [RelayCommand]
    private async Task SalvarEmailAsync()
        => await ExecutarAsync(async p =>
        {
            var host = Limpar(EmailSmtpHost);
            var remetente = Limpar(EmailRemetente);

            if (host is null && remetente is null)
            {
                await p.SalvarCamposEmailAsync(new CamposEmail(
                    null, Limpar(EmailSmtpPorta), Limpar(EmailSmtpUsuario), EmailSmtpSenha,
                    null, Limpar(EmailRemetenteNome), EmailSmtpTls));
                return "Lembrete por e-mail desligado — o aviso volta a ser só o WhatsApp da rodada.";
            }

            if (host is null || remetente is null)
                throw new InvalidOperationException(
                    "Informe o servidor de saída E o remetente — ou deixe os dois em branco para desligar o lembrete.");

            if (!EnderecoDeEmail.Valido(remetente))
                throw new InvalidOperationException(
                    "O remetente precisa ser um e-mail válido (ex.: contato@suaclinica.com.br).");

            if (!string.IsNullOrWhiteSpace(EmailSmtpPorta)
                && (!int.TryParse(EmailSmtpPorta.Trim(), out var porta) || porta is < 1 or > 65535))
                throw new InvalidOperationException(
                    "A porta precisa ser um número de 1 a 65535 — 587 é a mais comum; 465 em alguns provedores.");

            await p.SalvarCamposEmailAsync(new CamposEmail(
                host, Limpar(EmailSmtpPorta), Limpar(EmailSmtpUsuario), EmailSmtpSenha,
                remetente, Limpar(EmailRemetenteNome), EmailSmtpTls));

            return "Lembrete por e-mail salvo. Use \"Enviar e-mail de teste\" para conferir que o servidor aceita.";
        });

    /// <summary>
    /// Manda UM e-mail de teste pelo MESMO caminho do lembrete, com a configuração GRAVADA.
    /// A frase do erro traz a do servidor (<c>GetBaseException</c>): "Failure sending mail"
    /// não diz nada, e a causa — autenticação recusada, porta fechada — está um nível abaixo
    /// (a lição da parcela 67).
    /// </summary>
    [RelayCommand]
    private async Task TestarEmailAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "enviar o e-mail de teste");

            var destino = Limpar(EmailDestinoTeste)
                ?? throw new InvalidOperationException("Informe para qual e-mail mandar o teste.");

            TestandoEmail = true;

            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider
                .GetRequiredService<LembreteEmailService>()
                .EnviarTesteAsync(destino);

            _snackbar.Sucesso(
                $"E-mail de teste enviado para {destino}. Confira a caixa de entrada — e a pasta de "
                + "spam, na primeira vez.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — e-mail de teste falhou", ex);
            Erro($"O e-mail de teste não saiu: {ex.GetBaseException().Message}");
        }
        finally
        {
            TestandoEmail = false;
        }
    }

    private async Task CarregarTelasAsync(ParametrosService p)
    {
        var gravada = await p.ObterTelaDoPacienteAsync();

        // Sem await entre o Clear() e o último Add (a lição da parcela 62): duas cargas
        // intercaladas deixariam a lista com telas repetidas ou faltando.
        var telas = TelasDoSistema.Listar();

        TelasDisponiveis.Clear();
        foreach (var t in telas) TelasDisponiveis.Add(t);

        TelaDoPaciente = telas.FirstOrDefault(t => t.Id == gravada);

        AvisoTelaDoPaciente = gravada is not null && TelaDoPaciente is null
            ? $"A tela configurada ({gravada}) não está ligada agora. Enquanto isso, o "
              + "paciente assina na própria janela de quem conduz."
            : string.Empty;
    }

    [RelayCommand]
    private async Task SalvarTelaDoPacienteAsync()
        => await ExecutarAsync(async p =>
        {
            // A principal é recusada: ela é a da recepcionista, e o termo em tela cheia por
            // cima do trabalho dela seria o oposto do que a configuração serve.
            if (TelaDoPaciente is { Principal: true })
                throw new InvalidOperationException(
                    "Essa é a tela principal, onde o sistema é operado. Escolha o monitor "
                    + "virado para o paciente.");

            await p.SalvarTelaDoPacienteAsync(TelaDoPaciente?.Id);
            await CarregarTelasAsync(p);

            return TelaDoPaciente is null
                ? "Tela do paciente desligada. A assinatura volta a ser colhida na janela de quem conduz."
                : $"Tela do paciente: {TelaDoPaciente.Rotulo}.";
        });

    /// <summary>
    /// Abre a tela escolhida com um exemplo, para a direção CONFERIR que é a certa antes de
    /// haver um paciente esperando.
    ///
    /// Não é conforto: as telas se chamam "\\.\DISPLAY2" e ninguém sabe qual é qual olhando
    /// o nome. Sem o teste, o primeiro a descobrir que o termo abriu no monitor errado seria
    /// o paciente — vendo o próprio nome e o procedimento dele numa tela virada para a sala
    /// de espera.
    /// </summary>
    [RelayCommand]
    private void TestarTelaDoPaciente()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "testar a tela do paciente");

            if (TelaDoPaciente is null)
            {
                Erro("Escolha primeiro a tela virada para o paciente.");
                return;
            }

            PainelDoPacienteWindow.MostrarExemplo(TelaDoPaciente);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — teste da tela do paciente", ex);
            Erro(ex.Message);
        }
    }

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
