using System.Windows.Threading;
using Clinica.Infrastructure;

namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// A tela relê quando o BANCO avisa que uma tabela dela mudou (out/2026, item 4 do plano de
/// desempenho) — em vez de esperar o próximo minuto do relógio.
/// </summary>
/// <remarks>
/// Ligada no <c>AoEntrarEmCena</c> e desligada no <c>AoSairDeCena</c>, como o relógio: tela
/// fora de vista não relê nada. Os avisos que chegam juntos (a outra máquina remarcou, depois
/// marcou a chegada) viram UMA releitura, depois de uma pausa curta. O relógio da tela
/// continua existindo: aviso perdido não pode deixar a tela parada.
/// </remarks>
public sealed class EscutaDoBanco
{
    private static readonly TimeSpan Pausa = TimeSpan.FromMilliseconds(800);

    private readonly HashSet<string> _tabelas;
    private readonly Func<Task> _reler;
    private readonly DispatcherTimer _agrupar;
    private bool _ligada;

    public EscutaDoBanco(Func<Task> reler, params string[] tabelas)
    {
        _reler = reler;
        _tabelas = new HashSet<string>(tabelas, StringComparer.Ordinal);
        _agrupar = new DispatcherTimer(DispatcherPriority.Background) { Interval = Pausa };
        _agrupar.Tick += async (_, _) =>
        {
            _agrupar.Stop();
            if (!_ligada) return;
            try { await _reler(); }
            catch (Exception ex) { Configuracao.LogSuite.Registrar("Releitura por aviso do banco", ex); }
        };
    }

    /// <summary>O banco está avisando? Quem escolhe o intervalo do relógio pergunta isto.</summary>
    public static bool AvisosChegando => OuvinteDeMudancas.Ativo;

    public void Ligar()
    {
        if (_ligada) return;
        _ligada = true;
        OuvinteDeMudancas.Mudou += AoMudar;
    }

    public void Desligar()
    {
        _ligada = false;
        OuvinteDeMudancas.Mudou -= AoMudar;
        _agrupar.Stop();
    }

    private void AoMudar(string tabela)
    {
        if (tabela != "*" && !_tabelas.Contains(tabela)) return;
        // O aviso chega numa thread do Npgsql; o relógio de agrupamento é da tela.
        _agrupar.Dispatcher.BeginInvoke(() =>
        {
            if (!_ligada) return;
            _agrupar.Stop();
            _agrupar.Start();
        });
    }
}
