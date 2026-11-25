using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que cada papel EXIGE da ficha do paciente para sair (nov/2026, "o mesmo modus operandi do receituário" em
/// todos os documentos): faltando, a emissão abre o modal "complete aqui" — grava na ficha, com auditoria — e segue.
/// Uma regra só para a folha (Blazor: Consultório, gestão, apps) e para o portal.
///
/// - Receituário de Controle Especial: o bloco do COMPRADOR inteiro (ReceituarioControleEspecialRegras).
/// - Receita: o endereço residencial (a receita sai com o endereço do paciente — farmácia e CFM).
/// - Os demais (atestado, comparecimento, pedido de exame, laudo, relatório, anamnese, termo): nada além do nome,
///   que toda ficha tem. Não se inventa exigência que a lei não faz — cada campo pedido é um clique a mais no balcão.
/// </summary>
public static class DadosDoPacienteNoDocumento
{
    public static IReadOnlyList<ReceituarioControleEspecialRegras.CampoDoComprador> Faltas(TipoDocumentoClinico tipo, Paciente? p)
    {
        if (p is null) return [];
        return tipo switch
        {
            TipoDocumentoClinico.ReceituarioControleEspecial => ReceituarioControleEspecialRegras.FaltasDoComprador(p),
            TipoDocumentoClinico.Receita when string.IsNullOrWhiteSpace(p.Endereco) => [ReceituarioControleEspecialRegras.Endereco],
            _ => []
        };
    }

    /// <summary>A frase do modal: para QUE o dado é pedido.</summary>
    public static string Motivo(TipoDocumentoClinico tipo) => tipo switch
    {
        TipoDocumentoClinico.ReceituarioControleEspecial =>
            "O Receituário de Controle Especial identifica o comprador (o paciente). Complete o que falta na ficha:",
        TipoDocumentoClinico.Receita => "A receita sai com o endereço residencial do paciente. Complete na ficha:",
        _ => "Complete o que falta na ficha do paciente:"
    };

    /// <summary>A ação gravada na trilha (o RCE mantém o nome que já existia).</summary>
    public static string AcaoDeAuditoria(TipoDocumentoClinico tipo)
        => tipo == TipoDocumentoClinico.ReceituarioControleEspecial ? "DadosCompradorCompletadosNaEmissao" : "DadosPacienteCompletadosNaEmissao";
}
