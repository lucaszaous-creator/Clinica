using System.Globalization;
using Clinica.Desktop.Controls;
using System.Windows;
using Clinica.Application.Servicos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace Clinica.Financeiro.ViewModels;

/// <summary>
/// Gera o código Pix para o paciente pagar no balcão (parcela 35).
///
/// O `PixService` foi entregue na parcela 34 e nenhuma tela o chamava — o defeito que
/// este projeto documenta como recorrente. Esta é a porta.
///
/// A janela **não confirma pagamento**, e diz isso: o código é montado offline, sem
/// passar por banco nenhum, e quem confere se o dinheiro caiu continua sendo quem olha
/// o extrato. Prometer baixa automática aqui seria mentir sobre o que o código garante.
/// </summary>
public sealed partial class CobrancaPixViewModel : ObservableObject
{
    private readonly ParametrosService _parametros;
    private readonly PixService _pix;

    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private string _referencia = string.Empty;
    [ObservableProperty] private string _copiaECola = string.Empty;
    [ObservableProperty] private string _procedencia = string.Empty;
    [ObservableProperty] private bool _temCodigo;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public CobrancaPixViewModel(ParametrosService parametros, PixService pix,
        decimal? valorSugerido = null, string? referenciaSugerida = null)
    {
        _parametros = parametros;
        _pix = pix;

        // O valor vem preenchido quando a cobrança nasce de um lançamento: redigitar o
        // que o sistema já sabe é onde entra o erro de um dígito.
        if (valorSugerido is { } v)
            Valor = v.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));

        Referencia = referenciaSugerida ?? string.Empty;
    }

    /// <summary>Qualquer edição invalida o código já gerado — ele deixou de corresponder.</summary>
    partial void OnValorChanged(string value) => Invalidar();

    partial void OnReferenciaChanged(string value) => Invalidar();

    private void Invalidar()
    {
        if (!TemCodigo) return;
        TemCodigo = false;
        CopiaECola = string.Empty;
        Procedencia = string.Empty;
    }

    [RelayCommand]
    private async Task GerarAsync()
    {
        Mensagem = null;
        MensagemEhErro = false;

        try
        {
            // `Valores.TentarLerDecimal` (que resolve por `LeituraNumero`), e NÃO um
            // `TryParse` com a cultura pt-BR fixa.
            //
            // ⚠️ Cultura explícita aqui era PIOR do que cultura da máquina, e é por isso
            // que a varredura da parcela 68 passou reto por este ponto na primeira
            // rodada: o filtro dela descartava as linhas que já declaravam `CultureInfo`,
            // supondo que declarar cultura fosse o conserto. Em pt-BR o ponto é separador
            // de MILHAR — "50.00" era lido como 5.000 e o QR saía cobrando cinco mil reais
            // de uma sessão de cinquenta, com o paciente lendo o valor no celular dele.
            if (!Valores.TentarLerDecimal(Valor, out var valor) || valor <= 0m)
            {
                Erro("Informe o valor da cobrança — Pix sem valor deixa o paciente digitar quanto quiser.");
                return;
            }

            var prestador = await _parametros.ObterPrestadorAsync();

            if (string.IsNullOrWhiteSpace(prestador.ChavePix))
            {
                Erro("A clínica ainda não tem chave Pix cadastrada. "
                     + "Cadastre-a em Configurações → Dados da clínica (no Gerente).");
                return;
            }

            var cobranca = _pix.Gerar(
                prestador.ChavePix,
                prestador.NomeFantasia ?? prestador.RazaoSocial ?? "Clinica",
                prestador.Cidade ?? string.Empty,
                valor,
                string.IsNullOrWhiteSpace(Referencia) ? null : Referencia);

            CopiaECola = cobranca.CopiaECola;
            TemCodigo = true;

            // A PROCEDÊNCIA do código, como a conciliação faz com o valor sugerido: quem
            // vai mostrar isso ao paciente precisa saber para qual chave o dinheiro vai.
            Procedencia = $"Para {cobranca.Recebedor} · chave {Resumir(cobranca.Chave)} "
                          + $"({Rotular(cobranca.TipoChave)}).";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — código Pix não pôde ser gerado", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void Copiar()
    {
        if (!TemCodigo) return;

        try
        {
            Clipboard.SetText(CopiaECola);
            Mensagem = "Código copiado. Cole no aplicativo do paciente ou mande pelo WhatsApp.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            // A área de transferência falha quando outro programa a está segurando —
            // acontece, e o código continua na tela para copiar à mão.
            Clinica.Application.Diagnostico.Registrar("Financeiro — cópia do Pix falhou", ex);
            Erro("Não foi possível copiar. Selecione o texto acima e copie manualmente.");
        }
    }

    /// <summary>
    /// A chave aparece encurtada no meio: ela é dado sensível e a tela fica virada para o
    /// balcão, mas o começo e o fim bastam para quem cadastrou reconhecer se é a certa.
    /// </summary>
    private static string Resumir(string chave)
        => chave.Length <= 12 ? chave : $"{chave[..5]}…{chave[^4..]}";

    private static string Rotular(TipoChavePix tipo) => tipo switch
    {
        TipoChavePix.Cpf => "CPF",
        TipoChavePix.Cnpj => "CNPJ",
        TipoChavePix.Email => "e-mail",
        TipoChavePix.Telefone => "telefone",
        TipoChavePix.Aleatoria => "aleatória",
        _ => "formato não reconhecido"
    };

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
