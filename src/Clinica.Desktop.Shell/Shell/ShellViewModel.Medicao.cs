using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Medição de cada troca de tela, NO POSTO (out/2026).
/// </summary>
/// <remarks>
/// O tempo que importa é o da rede da clínica, que nenhum ensaio local reproduz. A cada
/// navegação, conta quantas idas ao banco a tela fez e quanto tempo levou até o banco ficar
/// quieto, e grava uma linha em <c>desempenho-AAAA-MM.csv</c> na pasta de logs. Só números e o
/// nome da tela: nenhum dado de paciente, nenhum SQL. Uma manhã de uso real diz quais telas
/// concentram a espera.
///
/// O fim da tela é o último comando terminado antes de <see cref="QuietoPor"/> sem nenhum no ar;
/// tela que não lê nada termina na hora. Releituras de fundo que caiam dentro da janela entram
/// na conta — é o que a pessoa de fato espera.
/// </remarks>
public sealed partial class ShellViewModel
{
    private static readonly TimeSpan QuietoPor = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan TetoDaMedicao = TimeSpan.FromSeconds(60);
    private DispatcherTimer? _relogioMedicao;
    private (string Tela, bool Reaproveitada, long Inicio, long Comandos, TimeSpan NoBanco)? _medindo;

    /// <summary>
    /// A tela voltou do histórico e está relendo por trás: o conteúdo já está na tela, e o
    /// cabeçalho diz que ele está sendo atualizado (item 3 do plano de desempenho).
    /// </summary>
    [ObservableProperty] private bool _atualizandoTela;

    private void MedirTroca(string tela, bool reaproveitada)
    {
        if (_medindo is { } anterior) Registrar(anterior, interrompida: true);
        _medindo = (tela, reaproveitada, Stopwatch.GetTimestamp(), MedicaoBanco.Comandos, MedicaoBanco.TempoNoBanco);
        AtualizandoTela = reaproveitada;
        _relogioMedicao ??= CriarRelogio();
        _relogioMedicao.Start();
    }

    private DispatcherTimer CriarRelogio()
    {
        var t = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        t.Tick += (_, _) =>
        {
            if (_medindo is not { } m) { t.Stop(); return; }
            var agora = Stopwatch.GetTimestamp();
            var quieto = MedicaoBanco.EmVoo == 0
                && Stopwatch.GetElapsedTime(Math.Max(MedicaoBanco.UltimaAtividade, m.Inicio), agora) >= QuietoPor;
            if (quieto || Stopwatch.GetElapsedTime(m.Inicio, agora) >= TetoDaMedicao)
            {
                t.Stop();
                Registrar(m, interrompida: !quieto);
            }
        };
        return t;
    }

    private void Registrar((string Tela, bool Reaproveitada, long Inicio, long Comandos, TimeSpan NoBanco) m, bool interrompida)
    {
        _medindo = null;
        AtualizandoTela = false;
        try
        {
            // Até o último comando (não até o fim da janela de silêncio): é quando a tela ficou pronta.
            var fim = Math.Max(MedicaoBanco.UltimaAtividade, m.Inicio);
            var ms = Stopwatch.GetElapsedTime(m.Inicio, fim).TotalMilliseconds;
            var comandos = MedicaoBanco.Comandos - m.Comandos;
            var noBanco = (MedicaoBanco.TempoNoBanco - m.NoBanco).TotalMilliseconds;
            var pasta = LogSuite.Pasta;
            Directory.CreateDirectory(pasta);
            var arquivo = Path.Combine(pasta, $"desempenho-{DateTime.Now:yyyy-MM}.csv");
            var novo = !File.Exists(arquivo);
            var c = CultureInfo.InvariantCulture;
            var tela = m.Tela.Replace(';', ',');
            var linha = string.Join(';', DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", c), Titulo.Replace(';', ','), tela,
                m.Reaproveitada ? "voltou" : "abriu", ((int)ms).ToString(c), comandos.ToString(c), ((int)noBanco).ToString(c),
                interrompida ? "interrompida" : "ok");
            File.AppendAllText(arquivo, (novo ? "quando;app;tela;como;ms_ate_pronta;idas_ao_banco;ms_somados_no_banco;situacao" + Environment.NewLine : "")
                + linha + Environment.NewLine);
        }
        catch (Exception ex) { LogSuite.Registrar("Medição de desempenho da troca de tela", ex); }
    }
}
