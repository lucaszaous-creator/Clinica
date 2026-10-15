namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// A tela sabe se RELER POR TRÁS, sem apagar o que mostra (out/2026, item 3 do plano de
/// desempenho).
/// </summary>
/// <remarks>
/// Com o banco a ~250 ms por ida, montar de novo uma tela visitada há pouco custava a carga
/// inteira, com "Carregando…" na frente. A ViewModel que implementa isto deixa o shell guardá-la
/// no histórico: ao voltar, ela reaparece NA HORA com os dados que tinha, o cabeçalho mostra
/// "Atualizando…" e a releitura troca os números quando chega — sem limpar a lista antes, sem
/// acender o estado de carregamento e sem escrever erro na tela se a releitura falhar (a tela
/// continua com o que tinha, e o log guarda o motivo).
///
/// Só implementa quem tem releitura silenciosa de verdade. Formulário em edição e prontuário
/// aberto NÃO implementam: lá a pessoa está escrevendo, e trocar os dados por baixo dela seria
/// pior do que esperar.
/// </remarks>
public interface IAtualizarAoVoltar
{
    void AtualizarAoVoltar();
}
