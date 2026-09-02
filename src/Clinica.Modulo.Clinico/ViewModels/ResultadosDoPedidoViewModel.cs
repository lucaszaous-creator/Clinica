using System.Collections.ObjectModel;
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

/// <summary>Um resultado no modal — os campos do laudo como foram registrados.</summary>
public sealed record ResultadoResumido(
    int Id, string Nome, string Valor, string? Referencia, string? Laboratorio,
    string DataTexto, string? Observacoes)
{
    /// <summary>Nome do arquivo do laudo; nulo quando o registro é só o valor digitado.</summary>
    public string? ArquivoNome { get; init; }
    public bool TemArquivo => !string.IsNullOrWhiteSpace(ArquivoNome);
}

/// <summary>
/// O modal "Resultado — exame" da tela de Exames (o quick-view do mockup): os resultados
/// REGISTRADOS que respondem a este pedido, sem sair da lista.
///
/// O "Baixar PDF" do mockup é o <b>Abrir laudo</b> daqui, e só aparece na linha que TEM
/// arquivo: o registro pode ser só o valor digitado ("glicada 6,1"), e um botão de baixar
/// aceso sobre um resultado sem arquivo seria o clique que não faz nada (parcela 41).
/// </summary>
public sealed partial class ResultadosDoPedidoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;
    private readonly PedidoDeExameLinha _pedido;

    /// <summary>A janela observa para fechar quando a navegação foi disparada.</summary>
    public event Action? NavegouParaOPaciente;

    public string Paciente => _pedido.Paciente;
    public string ExameRotulo => _pedido.ExameRotulo;
    public string PedidoTexto => $"Pedido {_pedido.Numero} · {_pedido.Data:dd/MM/yyyy}";

    public ObservableCollection<ResultadoResumido> Resultados { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public ResultadosDoPedidoViewModel(
        IServiceScopeFactory escopos, PacienteEmFoco foco, PedidoDeExameLinha pedido)
    {
        _escopos = escopos;
        _foco = foco;
        _pedido = pedido;

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        Carregando = true;
        try
        {
            IReadOnlyList<ResultadoExame> doPaciente;
            using (var scope = _escopos.CreateScope())
            {
                // Modal de dado de saúde deixa rastro (parcela 60).
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_pedido.PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ProntuarioClinico);

                var servico = scope.ServiceProvider.GetRequiredService<ResultadoExameService>();
                doPaciente = await servico.DoPacienteAsync(_pedido.PacienteId);
            }

            // O recorte é DESTE pedido — os resultados do paciente são poucos, e o
            // vínculo já veio na linha; uma consulta própria seria peça a mais.
            Resultados.Clear();
            foreach (var r in doPaciente.Where(r => r.PedidoDocumentoId == _pedido.DocumentoId))
                Resultados.Add(new ResultadoResumido(
                    r.Id, r.Nome, r.ResumoDoResultado, r.Referencia, r.Laboratorio,
                    $"{r.Data:dd/MM/yyyy}", r.Observacoes) { ArquivoNome = r.ArquivoNome });

            NaoVerificado = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — os resultados do pedido não puderam ser carregados", ex);
            NaoVerificado = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Abre o laudo: salva o arquivo onde a pessoa escolher e deixa o Windows abri-lo no
    /// programa padrão — é o MESMO caminho do anexo de prontuário (não há visualizador
    /// embutido, e inventar um aqui seria a segunda definição de "abrir arquivo clínico").
    /// Dado de saúde saindo para arquivo deixa rastro (parcela 60).
    /// </summary>
    [RelayCommand]
    private async Task AbrirLaudoAsync(ResultadoResumido? linha)
    {
        if (linha is null || !linha.TemArquivo) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir o laudo do exame");

            byte[]? bytes;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<ResultadoExameService>();
                bytes = await servico.ConteudoDoLaudoAsync(linha.Id);

                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_pedido.PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ExportacaoClinica);
            }

            if (bytes is null || bytes.Length == 0)
            {
                // Falha nunca aparece como sucesso: o arquivo prometido pela linha não
                // está no banco, e dizer "abrindo" mandaria procurar no lugar errado.
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

    /// <summary>Abre o paciente na seção Exames e anexos — laudos em arquivo, registro de
    /// novos resultados e o histórico inteiro moram lá.</summary>
    [RelayCommand]
    private void AbrirNoPaciente()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir os exames do paciente");
            _foco.Definir(_pedido.PacienteId, _pedido.Paciente);
            if (NavegacaoSuite.Ir(ModuloClinico.ChaveExamesDoPaciente))
            {
                NavegouParaOPaciente?.Invoke();
                return;
            }
            Mensagem = "Não deu para abrir a seção de exames do paciente.";
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
