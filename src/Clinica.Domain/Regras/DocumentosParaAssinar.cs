using System.Linq.Expressions;
using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// "QUE DOCUMENTOS ESTA PESSOA AINDA PRECISA ASSINAR?" — a resposta num lugar só (nov/2026).
///
/// A mesma leitura serve ao "Documentos para assinar" do Consultório do portal (<c>/posto/pendencias</c>) e à aba
/// "Para assinar" da central de Documentos (aplicativos e gestão web): os documentos PRÓPRIOS, não cancelados, sem a
/// assinatura do profissional nem a do paciente, dos tipos que o profissional assina — com ou sem atendimento (a
/// receita emitida pela gestão, sem horário, entra). Duas cópias divergiriam na primeira correção, e a que ficasse
/// para trás diria "nada para assinar" com a receita parada.
/// </summary>
public static class DocumentosParaAssinar
{
    /// <summary>Os tipos que o profissional assina pelo Consultório e pela central.</summary>
    public static readonly TipoDocumentoClinico[] Tipos =
    [
        TipoDocumentoClinico.Receita, TipoDocumentoClinico.ReceituarioControleEspecial, TipoDocumentoClinico.Atestado, TipoDocumentoClinico.PedidoExame,
        TipoDocumentoClinico.Comparecimento, TipoDocumentoClinico.RelatorioEvolucao, TipoDocumentoClinico.Anamnese
    ];

    /// <summary>O filtro (traduzível pelo EF): só COLUNAS, nunca propriedade derivada.</summary>
    public static Expression<Func<DocumentoClinico, bool>> Do(int profissionalId)
        => d => d.ProfissionalId == profissionalId && d.CanceladoEm == null && d.AssinadoEm == null
                && d.PacienteAssinadoEm == null && Tipos.Contains(d.Tipo);
}
