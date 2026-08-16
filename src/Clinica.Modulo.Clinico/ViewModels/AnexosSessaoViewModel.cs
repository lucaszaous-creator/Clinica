using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// Os ANEXOS de uma sessão, na máquina de quem atende (parcela 37).
///
/// Por que existe
/// --------------
/// <c>ProntuarioService.AnexarAsync</c> e <c>AnexosAsync</c> estão prontos e testados
/// desde a parcela 2, e até aqui só a Recepção os chamava. O Consultório EMITE pedido de
/// exame desde a parcela 36 — e quando o laudo voltava, quem anexava era o balcão e quem
/// precisava ler não alcançava. É o circuito aberto mais gritante do módulo: ele pedia e
/// não recebia.
///
/// É a sexta ocorrência do defeito que o projeto documenta desde a parcela 25, na variante
/// que a parcela 36 acrescentou: não é dado gravado sem leitor nem serviço sem chamador —
/// é <b>capacidade cujo leitor está no módulo errado</b>. O CI fica verde, a Recepção usa
/// todo dia, e o médico abre a gaveta atrás do papel.
///
/// A janela é do mesmo tamanho e feitio da emissão de documento porque faz o mesmo tipo de
/// trabalho: entra, resolve um arquivo, sai. O que ela NÃO faz é reimplementar anexo
/// nenhum — chama o serviço dono, como a central de documentos.
/// </summary>
public sealed partial class AnexosSessaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _evolucaoId;

    public ObservableCollection<AnexoResumo> Anexos { get; } = [];

    /// <summary>Cabeçalho da janela: de que sessão são estes arquivos.</summary>
    [ObservableProperty] private string _sessao = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>Teto de um arquivo, dito na tela em vez de descoberto no erro.</summary>
    public string LimiteTexto =>
        $"Até {ProntuarioService.TamanhoMaximoAnexo / (1024 * 1024)} MB por arquivo.";

    public AnexosSessaoViewModel(
        IServiceScopeFactory escopos, int evolucaoId, string sessao, int pacienteId)
    {
        _escopos = escopos;
        _evolucaoId = evolucaoId;
        _pacienteId = pacienteId;
        Sessao = sessao;
    }

    private readonly int _pacienteId;

    /// <summary>A janela é de UMA sessão — registra uma vez por abertura.</summary>
    private bool _acessoRegistrado;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Anexos.Clear();

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            // Laudo e imagem de exame são dado de saúde: abrir a janela entra na trilha
            // da parcela 52 (origem DOCUMENTO), uma vez por abertura — recarregar depois
            // de anexar não é um acesso novo.
            if (!_acessoRegistrado && _pacienteId != 0)
            {
                _acessoRegistrado = true;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.Documento);
            }

            foreach (var a in await prontuario.AnexosAsync(_evolucaoId))
                Anexos.Add(a);
        }
        catch (Exception ex)
        {
            // Terceiro estado: lista vazia por falha não pode se parecer com "esta sessão
            // não tem anexo" — o profissional concluiria que o laudo não chegou.
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anexos da sessão não puderam ser lidos", ex);
            Erro(ex.Message);
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>Anexa um arquivo (laudo, exame, foto da região) à sessão.</summary>
    [RelayCommand]
    private async Task AnexarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "anexar ao prontuário");

            var dialogo = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Anexar ao prontuário",
                Filter = "Imagens e documentos (*.jpg;*.jpeg;*.png;*.pdf)|*.jpg;*.jpeg;*.png;*.pdf"
                         + "|Todos os arquivos (*.*)|*.*"
            };
            if (dialogo.ShowDialog() != true) return;

            var bytes = await File.ReadAllBytesAsync(dialogo.FileName);
            var extensao = Path.GetExtension(dialogo.FileName).ToLowerInvariant();
            var ehImagem = extensao is ".jpg" or ".jpeg" or ".png" or ".bmp";

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            await prontuario.AnexarAsync(
                _evolucaoId, Path.GetFileName(dialogo.FileName), bytes,
                ehImagem ? TipoAnexo.Imagem : TipoAnexo.Documento,
                tipoConteudo: ehImagem ? $"image/{extensao.TrimStart('.')}" : null,
                operador: SessaoUsuario.Atual.Operador);

            await CarregarAsync();
            Mensagem = "Arquivo anexado à sessão.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anexo não pôde ser gravado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Salva o anexo em disco para abrir no programa padrão do Windows.
    ///
    /// A tela não tenta VISUALIZAR o arquivo: um visualizador de PDF e de imagem embutido
    /// seria dependência de UI nova em cinco apps que se auto-atualizam por Velopack, e o
    /// Windows já tem programa padrão para os dois formatos.
    /// </summary>
    [RelayCommand]
    private async Task BaixarAsync(AnexoResumo? anexo)
    {
        if (anexo is null) return;

        try
        {
            // Duas barreiras, e a segunda faltava inteira: baixar um laudo é DADO DE SAÚDE
            // saindo para arquivo — a tela irmã da Recepção já exigia o bit e registrava a
            // trilha no mesmo ato (parcela 60). Sem isso, a exportação clínica do app do
            // médico não deixava linha nenhuma para uma investigação encontrar.
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "exportar anexo do prontuário");

            var dialogo = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salvar anexo",
                FileName = anexo.NomeArquivo
            };
            if (dialogo.ShowDialog() != true) return;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            var bytes = await prontuario.ConteudoAnexoAsync(anexo.Id);

            if (bytes is null)
            {
                Erro("O arquivo não foi encontrado no banco.");
                return;
            }

            await File.WriteAllBytesAsync(dialogo.FileName, bytes);

            // A trilha registra o que SAIU, com origem própria — é o que separa "abriu o
            // prontuário" de "levou o arquivo embora".
            await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                .RegistrarAsync(_pacienteId, SessaoUsuario.Atual.Operador,
                    OrigemAcessoProntuario.ExportacaoClinica);

            Mensagem = $"Anexo salvo em {dialogo.FileName}.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anexo não pôde ser salvo em disco", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoverAsync(AnexoResumo? anexo)
    {
        if (anexo is null) return;

        // Retirar, nunca apagar (parcela 52) — ver EvolucaoEdicaoViewModel.
        using var escopoDialogo = _escopos.CreateScope();
        var motivo = escopoDialogo.ServiceProvider.GetRequiredService<IDialogoService>().PerguntarTexto(
            "Retirar anexo",
            $"Por que \"{anexo.NomeArquivo}\" está saindo do prontuário? O arquivo NÃO é "
            + "apagado — sai da lista e fica guardado, com este motivo.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "mexer no prontuário");

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            await prontuario.CancelarAnexoAsync(anexo.Id, motivo, SessaoUsuario.Atual.Operador);

            await CarregarAsync();
            Mensagem = "Anexo retirado (guardado na sessão).";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anexo não pôde ser removido", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
