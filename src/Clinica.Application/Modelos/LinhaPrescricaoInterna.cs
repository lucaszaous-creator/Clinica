using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Uma folha de infusão na lista do paciente.</summary>
public sealed class LinhaPrescricaoInterna
{
    public required int PrescricaoId { get; init; }
    public required string Numero { get; init; }
    public required string Data { get; init; }
    public required string Situacao { get; init; }
    public string GrupoVisual { get; init; } = "";
    public required string Resumo { get; init; }
    public required string Execucao { get; init; }
    public required string Codigo { get; init; }
    public required bool Cancelada { get; init; }

    /// <summary>Já houve execução registrada — a folha de registro tem o que mostrar.</summary>
    public required bool TemRegistroExecucao { get; init; }
    public bool ExibirRegistroSeparado { get; init; }

    /// <summary>Só rascunho se cancela — depois de executada a folha é registro de um fato.</summary>
    public required bool PodeCancelar { get; init; }

    /// <summary>Rascunho se edita — é o que <c>PrescricaoInterna.PodeEditar</c> já dizia.</summary>
    public required bool PodeEditar { get; init; }

    public static LinhaPrescricaoInterna De(PrescricaoInterna p) => De(p, SessaoUsuario.Atual.Pode(Permissao.Prescrever));

    /// <summary>Monta a linha com a permissão INFORMADA — é o que o teste alcança.</summary>
    public static LinhaPrescricaoInterna De(PrescricaoInterna p, bool podePrescrever)
    {
        var itens = p.Itens.Count;
        var resumo = itens == 1 ? "1 item" : $"{itens} itens";

        var execucao = p.Situacao switch
        {
            SituacaoPrescricao.Rascunho => "ainda não assinada",
            SituacaoPrescricao.Cancelada => "cancelada",
            _ => $"{p.Realizados} realizados · {p.NaoRealizados} não realizados · {p.Pendentes} aguardando"
        };

        return new LinhaPrescricaoInterna
        {
            PrescricaoId = p.Id,
            Numero = p.Numero,
            Data = $"{p.Data:dd/MM/yyyy} às {p.Hora:HH\\:mm}",
            Situacao = RotulosEnum.De(p.Situacao),
            GrupoVisual = p.Situacao.ToString().ToLowerInvariant(),
            Resumo = resumo,
            Execucao = execucao,
            Codigo = p.CodigoVerificacao,
            Cancelada = p.Cancelada,
            TemRegistroExecucao = p.Realizados + p.NaoRealizados > 0,
            ExibirRegistroSeparado = p.AssinaturaDaExecucao is { ArquivoRegistroId: not null } antiga
                && antiga.ArquivoRegistroId != antiga.ArquivoId,
            // O estado da linha COMPÕE com a permissão (parcela 61): sem o bit, o botão
            // ficava aceso e o clique estourava no Exigir — botão aceso que só explode é
            // o defeito da parcela 41. O Exigir do comando continua sendo a barreira que
            // impede; esta é a metade que explica.
            PodeCancelar = (p.Situacao is SituacaoPrescricao.Rascunho or SituacaoPrescricao.Assinada)
                           && podePrescrever,
            PodeEditar = p.PodeEditar && podePrescrever
        };
    }
}
