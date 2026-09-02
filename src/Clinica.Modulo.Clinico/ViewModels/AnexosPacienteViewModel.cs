using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// EXAMES E ANEXOS do paciente — a seção que faltava (parcela 74).
///
/// O que ela corrige
/// -----------------
/// Os anexos existiam, e só se alcançavam <b>sessão a sessão</b>, dentro de uma janela
/// aberta a partir de uma linha do prontuário. Isso responde <i>"o que tem nesta
/// consulta"</i>. A pergunta que quem atende faz é outra, e é a mesma que a parcela 37
/// nomeou ao trazer os anexos para o Consultório — <b>"eu pedi a ressonância; ela
/// chegou?"</b> —, e ela não se responde abrindo quarenta sessões uma por uma.
///
/// É o defeito recorrente do projeto na variante de EIXO: o dado tem leitor, e o leitor
/// pergunta pela chave errada.
///
/// O que ela NÃO é
/// ---------------
/// Não é a tela de ANEXAR. Anexar é um ato da sessão — o arquivo pertence à consulta em
/// que ele foi discutido, e é esse vínculo que faz o laudo aparecer ao lado da conduta que
/// ele motivou. Aqui se LÊ e se leva embora; quem anexa continua sendo a janela da sessão,
/// no prontuário.
/// </summary>
public sealed partial class AnexosPacienteViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;
    private readonly IDialogoService _dialogo;
    private readonly ISnackbarService _snackbar;

    /// <summary>Descarte de resposta fora de ordem — a regra da parcela 60.</summary>
    private int _geracaoCarga;

    public ObservableCollection<AnexoDoPaciente> Anexos { get; } = [];

    /// <summary>
    /// Os resultados de exame ESTRUTURADOS (ago/2026) — o valor que se consulta e se
    /// compara, ao lado dos laudos digitalizados logo abaixo. Um não substitui o outro:
    /// o anexo é a prova; o resultado é o número que responde "qual era a glicada dele
    /// em março?" sem abrir laudo por laudo.
    /// </summary>
    public ObservableCollection<LinhaResultadoExame> Resultados { get; } = [];

    /// <summary>
    /// Os ARQUIVOS DA FICHA (set/2026): a receita, o laudo, o PDF que pertence à pessoa e
    /// não a uma sessão — e o acervo importado do sistema anterior. Diferente do anexo
    /// logo abaixo, que pende da consulta em que foi recebido.
    /// </summary>
    public ObservableCollection<LinhaArquivoDaFicha> ArquivosDaFicha { get; } = [];

    /// <summary>Há arquivo da ficha — a região dele SOME vazia (o convite é o botão).</summary>
    [ObservableProperty] private bool _temArquivosDaFicha;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Há resultado estruturado — a região dele SOME vazia (o convite é o botão).</summary>
    [ObservableProperty] private bool _temResultados;

    public bool PodeLer => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <summary>A metade VISÍVEL da barreira de escrita; quem impede é o Exigir.</summary>
    public bool PodeRegistrar => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>
    /// Anexar arquivo à ficha é dado de saúde entrando: quem escreve prontuário OU quem
    /// registra enfermagem (a técnica recebe o laudo pelo WhatsApp tanto quanto o médico).
    /// </summary>
    public bool PodeAnexar => SessaoUsuario.Atual.PodeAlgum(
        Permissao.EditarProntuario | Permissao.RegistrarEvolucaoEnfermagem);

    public AnexosPacienteViewModel(
        IServiceScopeFactory escopos, PacienteEmFoco foco,
        IDialogoService dialogo, ISnackbarService snackbar)
    {
        _escopos = escopos;
        _foco = foco;
        _dialogo = dialogo;
        _snackbar = snackbar;
        _ = CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        if (_foco.PacienteId is not { } id || !PodeLer)
        {
            Anexos.Clear();
            Resultados.Clear();
            ArquivosDaFicha.Clear();
            TemResultados = false;
            TemArquivosDaFicha = false;
            Resumo = string.Empty;
            return;
        }

        Carregando = true;
        NaoVerificado = false;
        try
        {
            using var escopo = _escopos.CreateScope();
            var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
            // SEQUENCIAL, nunca WhenAll: mesmo repositório, mesmo DbContext.
            var lista = await repo.AnexosDoPacienteAsync(id);
            var exames = await repo.ResultadosExameDoPacienteAsync(id);
            var daFicha = await repo.AnexosDaFichaAsync(id);

            if (geracao != _geracaoCarga) return;

            // ⚠️ Entre o Clear() e o último Add não pode haver await (parcela 62): duas
            // cargas intercaladas na MESMA coleção saem com linhas repetidas ou faltando,
            // e o contador de geração não impede isso — ele impede a resposta velha
            // sobrescrever a nova.
            Anexos.Clear();
            foreach (var a in lista) Anexos.Add(a);
            Resultados.Clear();
            foreach (var r in exames) Resultados.Add(LinhaResultadoExame.De(r));
            TemResultados = exames.Count > 0;
            ArquivosDaFicha.Clear();
            foreach (var a in daFicha) ArquivosDaFicha.Add(LinhaArquivoDaFicha.De(a));
            TemArquivosDaFicha = daFicha.Count > 0;

            var partes = new List<string>();
            if (exames.Count > 0) partes.Add($"{exames.Count} resultado(s) registrado(s)");
            if (daFicha.Count > 0) partes.Add($"{daFicha.Count} arquivo(s) da ficha");
            if (lista.Count > 0) partes.Add($"{lista.Count} arquivo(s) anexado(s) a sessões");
            Resumo = string.Join(" · ", partes);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anexos do paciente não puderam ser lidos", ex);
            // Lista vazia por FALHA se leria como "nenhum exame chegou", e a conduta de
            // hoje sairia sem o laudo que existe. Terceiro estado, sempre.
            NaoVerificado = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// Leva o arquivo para o disco. É DADO DE SAÚDE saindo, então tem as duas barreiras e
    /// deixa linha na trilha com origem própria — o que separa "abriu o prontuário" de
    /// "levou o arquivo embora" (parcela 60).
    /// </summary>
    [RelayCommand]
    private async Task BaixarAsync(AnexoDoPaciente? anexo)
    {
        if (anexo is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "exportar anexo do prontuário");

            var dialogo = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salvar anexo",
                FileName = anexo.NomeArquivo
            };
            if (dialogo.ShowDialog() != true) return;

            using var escopo = _escopos.CreateScope();
            var prontuario = escopo.ServiceProvider.GetRequiredService<ProntuarioService>();
            var bytes = await prontuario.ConteudoAnexoAsync(anexo.Id);

            if (bytes is null)
            {
                Mensagem = "O arquivo não foi encontrado no banco.";
                MensagemEhErro = true;
                return;
            }

            await File.WriteAllBytesAsync(dialogo.FileName, bytes);

            if (_foco.PacienteId is { } pacienteId)
                await escopo.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ExportacaoClinica);

            Mensagem = $"Anexo salvo em {dialogo.FileName}.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anexo não pôde ser salvo em disco", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Abre o diálogo de registro — o molde da colheita de medida.</summary>
    /// <summary>
    /// Abre o laudo em arquivo do resultado — o MESMO caminho do anexo: salva onde a
    /// pessoa escolher e o Windows abre no programa padrão. Dado de saúde saindo para
    /// arquivo deixa rastro (parcela 60).
    /// </summary>
    [RelayCommand]
    private async Task AbrirLaudoAsync(LinhaResultadoExame? linha)
    {
        if (linha is null || !linha.TemArquivo) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir o laudo do exame");

            byte[]? bytes;
            using (var escopo = _escopos.CreateScope())
            {
                var servico = escopo.ServiceProvider.GetRequiredService<ResultadoExameService>();
                bytes = await servico.ConteudoDoLaudoAsync(linha.Id);

                await escopo.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_foco.PacienteId ?? 0, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ExportacaoClinica);
            }

            if (bytes is null || bytes.Length == 0)
            {
                Mensagem = "O arquivo deste laudo não foi encontrado no banco.";
                MensagemEhErro = true;
                return;
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                bytes, ImpressaoPdf.NomeSeguro(linha.ArquivoNome!));
            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — o laudo não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task RegistrarResultadoAsync()
    {
        if (_foco.PacienteId is not { } id)
        {
            // A guarda DIZ por que não dá, em vez de voltar calada (parcela 41).
            Mensagem = "Escolha um paciente antes de registrar um resultado — ele entra "
                     + "no prontuário de alguém.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new ResultadoExameEdicaoViewModel(_escopos, id, _foco.Nome);
            var janela = new RegistrarResultadoExameWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };
            if (janela.ShowDialog() != true) return;

            _snackbar.Sucesso("Resultado registrado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — registro de resultado não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// CANCELA o resultado, com motivo — nunca "excluir": registro clínico não se apaga
    /// (parcela 52), e o rótulo do botão diz o ato verdadeiro.
    /// </summary>
    // ============================================================ arquivos da FICHA

    /// <summary>
    /// Anexa um arquivo à FICHA (set/2026): o laudo que chegou pelo WhatsApp, a receita de
    /// outro serviço — o que pertence à pessoa e não a uma consulta. Pergunta o que é e a
    /// data do documento (a data CLÍNICA, nunca a de hoje por padrão silencioso: em branco
    /// vale hoje, e a pergunta diz isso).
    /// </summary>
    [RelayCommand]
    private async Task AnexarArquivoDaFichaAsync()
    {
        if (_foco.PacienteId is not { } pacienteId)
        {
            Mensagem = "Escolha o paciente primeiro.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarProntuario | Permissao.RegistrarEvolucaoEnfermagem,
                "anexar arquivo à ficha do paciente");

            var escolha = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Anexar arquivo à ficha",
                Filter = "Documentos e imagens|*.pdf;*.jpg;*.jpeg;*.png|Todos os arquivos|*.*"
            };
            if (escolha.ShowDialog() != true) return;

            var nome = Path.GetFileName(escolha.FileName);
            var titulo = _dialogo.PerguntarTexto(
                "Anexar arquivo à ficha",
                "O que é este arquivo? (ex.: Ressonância lombar, Receita de outro serviço)",
                Path.GetFileNameWithoutExtension(nome));
            if (string.IsNullOrWhiteSpace(titulo)) return;

            // Cancelar não é resposta em branco (checagem 39): `null` é desistir; vazio
            // é "hoje".
            var dataTexto = _dialogo.PerguntarTexto(
                "Data do documento",
                "Data do documento (dd/mm/aaaa). Deixe em branco se for de hoje.",
                obrigatorio: false);
            if (dataTexto is null) return;
            DateOnly data;
            if (string.IsNullOrWhiteSpace(dataTexto))
                data = DateOnly.FromDateTime(DateTime.Today);
            else if (!DateOnly.TryParseExact(dataTexto.Trim(), "dd/MM/yyyy",
                         System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.None, out data))
            {
                Mensagem = "Data inválida — use dd/mm/aaaa.";
                MensagemEhErro = true;
                return;
            }

            var bytes = await File.ReadAllBytesAsync(escolha.FileName);
            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<AnexoPacienteService>().AnexarAsync(
                pacienteId, data, titulo, nome, bytes,
                tipoConteudo: TipoConteudoDe(nome),
                operador: SessaoUsuario.Atual.Operador);

            _snackbar.Info("Arquivo anexado à ficha.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — arquivo não pôde ser anexado à ficha", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Abre o arquivo da ficha pelo ponto único do shell (<see cref="ArquivosDaFicha"/>):
    /// bytes sob demanda e trilha de acesso — o MESMO caminho da ficha da Recepção e da
    /// tela da Enfermagem, para nenhuma cópia abrir o PDF sem registrar quem leu.
    /// </summary>
    [RelayCommand]
    private async Task AbrirArquivoDaFichaAsync(LinhaArquivoDaFicha? linha)
    {
        if (linha is null) return;

        try
        {
            var erro = await Clinica.Desktop.Shell.Componentes.ArquivosDaFicha.AbrirAsync(_escopos, linha.Id);
            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — arquivo da ficha não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Cancela com motivo, nunca "excluir" (parcela 52) — pelo mesmo ponto único.</summary>
    [RelayCommand]
    private async Task CancelarArquivoDaFichaAsync(LinhaArquivoDaFicha? linha)
    {
        if (linha is null) return;

        try
        {
            if (!await Clinica.Desktop.Shell.Componentes.ArquivosDaFicha.CancelarAsync(_escopos, _dialogo, linha.Id, linha.Titulo)) return;

            _snackbar.Info("Arquivo cancelado (guardado no prontuário).");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — arquivo da ficha não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private static string? TipoConteudoDe(string nome) => Path.GetExtension(nome).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => null
    };

    [RelayCommand]
    private async Task CancelarResultadoAsync(LinhaResultadoExame? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var motivo = _dialogo.PerguntarTexto(
                "Cancelar resultado de exame",
                $"Por que {linha.Nome} de {linha.Data} está sendo cancelado? Ele sai da "
                + "lista e fica guardado, com este motivo — sem ele não haveria como "
                + "distinguir \"não houve exame\" de \"apagaram o valor\".");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var escopo = _escopos.CreateScope();
            var servico = escopo.ServiceProvider.GetRequiredService<ResultadoExameService>();
            await servico.CancelarAsync(linha.Id, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Resultado cancelado (guardado no prontuário).");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — resultado não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}

/// <summary>Uma linha da lista de resultados, já formatada para a tela.</summary>
public sealed class LinhaResultadoExame
{
    public required int Id { get; init; }
    public required string Data { get; init; }
    public required string Nome { get; init; }
    public required string Valor { get; init; }

    /// <summary>"ref.: 4,0 a 5,6 · Lab Vida" — a procedência, quando o laudo a trouxe.</summary>
    public string? Contexto { get; init; }

    public string? Observacoes { get; init; }

    /// <summary>Nome do laudo em arquivo; nulo quando o registro é só o valor digitado.</summary>
    public string? ArquivoNome { get; init; }

    /// <summary>Só a linha que TEM arquivo mostra o botão de abrir (parcela 41).</summary>
    public bool TemArquivo => !string.IsNullOrWhiteSpace(ArquivoNome);

    public static LinhaResultadoExame De(ResultadoExame r) => new()
    {
        Id = r.Id,
        ArquivoNome = r.ArquivoNome,
        Data = r.Data.ToString("dd/MM/yyyy"),
        Nome = r.Nome,
        Valor = r.ResumoDoResultado,
        Contexto = string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(r.Referencia) ? null : $"ref.: {r.Referencia}",
            r.Laboratorio
        }.Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } c ? c : null,
        Observacoes = r.Observacoes
    };
}

/// <summary>Uma linha da região "Arquivos da ficha".</summary>
public sealed class LinhaArquivoDaFicha
{
    public required int Id { get; init; }
    public required string Titulo { get; init; }
    public required string NomeArquivo { get; init; }

    /// <summary>"26/10/2024 · receita-164001527.pdf · 31 KB · sistema anterior".</summary>
    public required string Contexto { get; init; }

    public string? Observacoes { get; init; }

    public static LinhaArquivoDaFicha De(AnexoPaciente a) => new()
    {
        Id = a.Id,
        Titulo = a.Titulo,
        NomeArquivo = a.NomeArquivo,
        Contexto = string.Join(" · ", new[]
        {
            a.Data.ToString("dd/MM/yyyy"),
            a.NomeArquivo,
            a.TamanhoLegivel,
            a.Importado ? "sistema anterior" : null
        }.Where(p => !string.IsNullOrWhiteSpace(p))),
        Observacoes = a.Observacoes
    };
}
