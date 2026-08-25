using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
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

    /// <summary>Descarte de resposta fora de ordem — a regra da parcela 60.</summary>
    private int _geracaoCarga;

    public ObservableCollection<AnexoDoPaciente> Anexos { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _resumo = string.Empty;

    public bool PodeLer => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    public AnexosPacienteViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;
        _ = CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        if (_foco.PacienteId is not { } id || !PodeLer)
        {
            Anexos.Clear();
            Resumo = string.Empty;
            return;
        }

        Carregando = true;
        NaoVerificado = false;
        try
        {
            using var escopo = _escopos.CreateScope();
            var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
            var lista = await repo.AnexosDoPacienteAsync(id);

            if (geracao != _geracaoCarga) return;

            // ⚠️ Entre o Clear() e o último Add não pode haver await (parcela 62): duas
            // cargas intercaladas na MESMA coleção saem com linhas repetidas ou faltando,
            // e o contador de geração não impede isso — ele impede a resposta velha
            // sobrescrever a nova.
            Anexos.Clear();
            foreach (var a in lista) Anexos.Add(a);

            Resumo = lista.Count switch
            {
                0 => string.Empty,
                1 => "1 arquivo no prontuário",
                _ => $"{lista.Count} arquivos no prontuário"
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
}
