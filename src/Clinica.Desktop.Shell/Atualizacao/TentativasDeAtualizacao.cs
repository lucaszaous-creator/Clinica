using System.IO;
using System.Text.Json;

namespace Clinica.Atualizacao;

/// <summary>
/// Memória, entre uma abertura e outra, de quantas vezes o app já tentou instalar a
/// MESMA versão.
///
/// Out/2026: na máquina da clínica o Velopack não conseguia trocar a pasta do app
/// ("O arquivo já está sendo usado por outro processo") e, ao falhar, o Update.exe
/// reabre o app ANTIGO — que acha a mesma versão nova e manda instalar de novo. Sem
/// memória, cada abertura é a primeira: a tela de atualização voltava sem fim até
/// alguém reinstalar por acesso remoto.
///
/// Regra: no máximo <see cref="Limite"/> tentativas por versão dentro de
/// <see cref="Janela"/>. Esgotadas, o app abre na versão que tem; passada a janela,
/// ganha mais uma rodada (a trava passageira — antivírus, outra instância — costuma
/// ter saído). Versão diferente começa do zero.
///
/// Nunca lança: arquivo ilegível ou pasta sem permissão valem como "nenhuma tentativa"
/// — o pior caso é o comportamento de antes, nunca um app que não abre.
/// </summary>
public sealed class TentativasDeAtualizacao(string arquivo)
{
    public const int Limite = 2;
    public static readonly TimeSpan Janela = TimeSpan.FromHours(12);

    private sealed record Registro(string Versao, int Tentativas, DateTime PrimeiraUtc);

    /// <summary>
    /// <c>null</c> quando ainda pode tentar instalar <paramref name="alvo"/>; senão, a
    /// frase que explica no log por que o app abriu sem atualizar.
    /// </summary>
    public string? MotivoParaNaoAplicar(string alvo, DateTime agoraUtc)
    {
        var r = Ler();
        if (r is null || r.Versao != alvo || agoraUtc - r.PrimeiraUtc >= Janela || r.Tentativas < Limite)
            return null;
        return $"A instalação da versão {alvo} já foi tentada {r.Tentativas} vez(es) desde " +
               $"{r.PrimeiraUtc.ToLocalTime():dd/MM/yyyy HH:mm} sem concluir. O app abre na versão atual " +
               $"e tenta de novo depois de {(r.PrimeiraUtc + Janela).ToLocalTime():dd/MM HH:mm}. " +
               "O motivo da falha está no log do Velopack (%LOCALAPPDATA%\\velopack\\velopack_<app>.log); " +
               "persistindo, feche todas as janelas do app (e o Gerenciador de Tarefas) ou reinstale.";
    }

    /// <summary>Anota uma tentativa de instalar <paramref name="alvo"/>. Chamar ANTES de entregar ao Velopack.</summary>
    public void Registrar(string alvo, DateTime agoraUtc)
    {
        var r = Ler();
        var novo = r is not null && r.Versao == alvo && agoraUtc - r.PrimeiraUtc < Janela
            ? r with { Tentativas = r.Tentativas + 1 }
            : new Registro(alvo, 1, agoraUtc);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            File.WriteAllText(arquivo, JsonSerializer.Serialize(novo));
        }
        catch
        {
            // Sem disco/permissão: segue sem memória, como antes.
        }
    }

    private Registro? Ler()
    {
        try
        {
            return File.Exists(arquivo) ? JsonSerializer.Deserialize<Registro>(File.ReadAllText(arquivo)) : null;
        }
        catch
        {
            return null;
        }
    }
}
