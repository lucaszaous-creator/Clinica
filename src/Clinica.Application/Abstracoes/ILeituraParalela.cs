namespace Clinica.Application.Abstracoes;

/// <summary>
/// Leituras INDEPENDENTES em paralelo, cada uma com o seu próprio escopo — e por isso o seu
/// próprio <c>DbContext</c> (out/2026, desempenho dos cinco apps).
/// </summary>
/// <remarks>
/// Com o banco remoto a ~250 ms por ida, seis leituras em fila custam 1,5 s; em paralelo custam
/// o tempo da mais lenta. A regra da casa continua valendo: NUNCA <c>Task.WhenAll</c> sobre o
/// MESMO repositório (o DbContext não aceita duas operações ao mesmo tempo, e o SQLite dos testes
/// esconde isso). Cada chamada aqui abre um escopo novo — o pool de conexões aquecido
/// (<c>ConexaoRemota</c>) é o que impede de cada uma pagar a abertura de uma conexão.
/// Use só para LEITURA: o que se grava fica no escopo de quem grava.
/// </remarks>
public interface ILeituraParalela
{
    Task<T> LerAsync<T>(Func<IClinicaRepositorio, Task<T>> leitura);

    /// <summary>Resolve um serviço num escopo próprio (para leituras que passam por serviço, não por repositório).</summary>
    Task<T> LerAsync<TServico, T>(Func<TServico, Task<T>> leitura) where TServico : notnull;
}
