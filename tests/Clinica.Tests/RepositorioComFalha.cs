using System.Reflection;
using Clinica.Application.Abstracoes;

namespace Clinica.Tests;

/// <summary>
/// Repositório que FALHA sob encomenda — injeção de falha para testar a degradação.
///
/// O projeto degrada de propósito em vez de derrubar o app: o aviso que não carregou, a
/// foto que não abriu, o pacote que não pôde ser lido. Essas ramificações são regra de
/// negócio de verdade — "só a conclusão do atendimento derruba a operação; pacote, insumo
/// e caixa que falham viram AVISO" —, e até aqui nenhuma delas tinha teste: não havia como
/// fazer o banco falhar, porque a suíte inteira roda contra um SQLite que funciona.
///
/// O resultado era o pior tipo de buraco de cobertura: o caminho que só roda quando algo
/// já deu errado é justamente o que ninguém exercita à mão, e é onde um `catch` que
/// engole a exceção ou um aviso com a mensagem errada passa despercebido para sempre.
///
/// Usa <see cref="DispatchProxy"/> em vez de uma classe que implementa a interface inteira:
/// <see cref="IClinicaRepositorio"/> tem mais de duzentos membros, e um dublê escrito à mão
/// viraria mil linhas que precisam ser mantidas a cada método novo do repositório — o
/// jeito mais barato de a suíte parar de compilar por um motivo que não é o teste.
///
/// A exceção é lançada de forma SÍNCRONA, antes de o método devolver a Task. Isso é o que
/// o chamador espera: dentro de um <c>try { await _repo.X(); }</c>, o `throw` na própria
/// chamada cai no mesmo `catch` que uma Task com falha cairia.
///
/// Não é <c>sealed</c> de propósito: <see cref="DispatchProxy"/> gera em runtime uma
/// classe que HERDA desta, e selá-la faz o <c>Create</c> estourar.
/// </summary>
public class RepositorioComFalha : DispatchProxy
{
    private IClinicaRepositorio _real = null!;
    private Func<string, bool> _falhaEm = _ => false;

    /// <summary>
    /// Embrulha o repositório real; <paramref name="falhaEm"/> recebe o NOME do método e
    /// decide se ele deve estourar.
    /// </summary>
    public static IClinicaRepositorio Embrulhar(
        IClinicaRepositorio real, Func<string, bool> falhaEm)
    {
        var proxy = Create<IClinicaRepositorio, RepositorioComFalha>()
            ?? throw new InvalidOperationException("Não foi possível criar o proxy do repositório.");

        var interno = (RepositorioComFalha)(object)proxy;
        interno._real = real;
        interno._falhaEm = falhaEm;
        return proxy;
    }

    /// <summary>Atalho para o caso comum: falhar num método específico.</summary>
    public static IClinicaRepositorio QueFalhaEm(IClinicaRepositorio real, string metodo)
        => Embrulhar(real, nome => nome == metodo);

    protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos)
    {
        if (metodo is null)
            throw new InvalidOperationException("Chamada sem método no proxy do repositório.");

        if (_falhaEm(metodo.Name))
            throw new InvalidOperationException(
                $"Falha simulada em {metodo.Name} (injeção de falha do teste).");

        try
        {
            return metodo.Invoke(_real, argumentos);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // A reflexão embrulha o que o repositório real lançou. Sem desembrulhar, o
            // teste veria TargetInvocationException e o código sob teste também — e o
            // `catch (InvalidOperationException)` de quem chama não pegaria.
            throw ex.InnerException;
        }
    }
}
