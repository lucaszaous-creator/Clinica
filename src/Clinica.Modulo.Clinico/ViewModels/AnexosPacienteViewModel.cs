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
            TemResultados = false;
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

            Resumo = (lista.Count, exames.Count) switch
            {
                (0, 0) => string.Empty,
                (var a2, 0) => $"{a2} arquivo(s) no prontuário",
                (0, var e2) => $"{e2} resultado(s) registrado(s)",
                var (a2, e2) => $"{e2} resultado(s) registrado(s) · {a2} arquivo(s) no prontuário"
            };
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

    public static LinhaResultadoExame De(ResultadoExame r) => new()
    {
        Id = r.Id,
        Data = r.Data.ToString("dd/MM/yyyy"),
        Nome = r.Nome,
        Valor = r.ValorComUnidade,
        Contexto = string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(r.Referencia) ? null : $"ref.: {r.Referencia}",
            r.Laboratorio
        }.Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } c ? c : null,
        Observacoes = r.Observacoes
    };
}
