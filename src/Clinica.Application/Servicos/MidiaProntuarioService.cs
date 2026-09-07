using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Onde o arquivo do anexo MORA — no banco ou no armazenamento remoto (set/2026).
///
/// Existe para os dois lados do anexo (a sessão e a ficha) resolverem a mesma pergunta uma
/// vez só: duas definições de "cabe no banco?" divergiriam na primeira correção, e a que
/// ficasse para trás gravaria um vídeo de 80 MB numa coluna que trava a sincronização de
/// todo mundo — o defeito apareceria longe da causa.
///
/// ⚠️ <b>O objeto remoto é PRIVADO.</b> Ver <see cref="MidiaClinica"/> para a decisão: a
/// receita publicada abre para um anônimo de propósito; mídia de prontuário é dado de
/// saúde, e quem lê é o app, com credencial.
/// </summary>
public sealed class MidiaProntuarioService
{
    private readonly IArmazenamentoPublico _armazenamento;

    public MidiaProntuarioService(IArmazenamentoPublico armazenamento)
        => _armazenamento = armazenamento;

    /// <summary>Onde o arquivo vai ficar, e o que gravar na linha.</summary>
    /// <param name="NoBanco">Bytes a gravar na coluna — vazio quando foi para o remoto.</param>
    /// <param name="CaminhoRemoto">Caminho do objeto, ou nulo quando ficou no banco.</param>
    public sealed record Destino(byte[] NoBanco, string? CaminhoRemoto);

    /// <summary>
    /// Guarda o arquivo onde ele couber e devolve o que a linha do banco precisa saber.
    ///
    /// Pequeno continua no BANCO, e isso não é conservadorismo: é o que faz o anexo comum
    /// continuar funcionando numa clínica que nunca contratou armazenamento nenhum. Só o
    /// que passa de <see cref="MidiaClinica.LimiteDoBanco"/> vai para o remoto.
    ///
    /// ⚠️ <b>Falhar aqui IMPEDE</b> — e é a assimetria deliberada em relação a quase todo
    /// o resto do sistema, que degrada e avisa. Gravar a linha com o arquivo perdido daria
    /// um "abrir vídeo" que não abre, num registro que a lei manda guardar por 20 anos: o
    /// prontuário afirmaria ter uma prova que ninguém tem. Sem armazenamento configurado a
    /// recusa DIZ o que fazer, em vez de falar de bytes.
    /// </summary>
    public async Task<Destino> GuardarAsync(
        byte[] conteudo, string nomeArquivo, string? tipoConteudo, CancellationToken ct = default)
    {
        if (conteudo.Length == 0)
            throw new InvalidOperationException("O arquivo está vazio — confira o que foi escolhido.");

        if (conteudo.Length > MidiaClinica.TamanhoMaximo)
            throw new InvalidOperationException(
                $"O arquivo tem {MidiaClinica.TamanhoLegivel(conteudo.Length)} e o limite é "
                + $"{MidiaClinica.TamanhoLegivel(MidiaClinica.TamanhoMaximo)}. Acima disso não é "
                + "registro de sessão: guarde o original fora e anexe o trecho que importa.");

        if (conteudo.Length <= MidiaClinica.LimiteDoBanco)
            return new Destino(conteudo, null);

        var caminho = MidiaClinica.CaminhoDoObjeto(MidiaClinica.GerarToken(), nomeArquivo);

        try
        {
            await _armazenamento.GuardarPrivadoAsync(
                caminho, conteudo, tipoConteudo ?? "application/octet-stream", ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Prontuário — mídia não pôde ser guardada no armazenamento", ex);
            throw new InvalidOperationException(
                $"Arquivos acima de {MidiaClinica.TamanhoLegivel(MidiaClinica.LimiteDoBanco)} são "
                + "guardados no armazenamento da clínica, e ele não respondeu. Confira o "
                + "armazenamento em Configurações → Publicação de documentos e tente de novo — o "
                + "anexo NÃO foi gravado.", ex);
        }

        return new Destino([], caminho);
    }

    /// <summary>
    /// Os bytes de volta, venham eles de onde vierem: a tela chama UM método e não precisa
    /// saber onde o arquivo mora. Nulo quando o objeto não está lá — e nulo é diferente de
    /// erro: o chamador diz "não foi possível abrir", nunca abre um arquivo vazio.
    /// </summary>
    public async Task<byte[]?> LerAsync(
        string? caminhoRemoto, Func<CancellationToken, Task<byte[]?>> doBanco,
        CancellationToken ct = default)
        => string.IsNullOrEmpty(caminhoRemoto)
            ? await doBanco(ct)
            : await _armazenamento.LerAsync(caminhoRemoto, ct);
}
