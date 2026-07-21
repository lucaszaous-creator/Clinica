namespace Clinica.Domain;

/// <summary>Nome de exibição (pt-BR) de cada modalidade embutida.</summary>
public static class ModalidadeInfo
{
    public static string NomeExibicao(ModalidadeAtendimento modalidade) => modalidade switch
    {
        ModalidadeAtendimento.AcupunturaSimples => "Acupuntura (apenas)",
        ModalidadeAtendimento.AcupunturaComEletro => "Acupuntura + eletroacupuntura",
        ModalidadeAtendimento.BsvComAcupuntura => "BSV + acupuntura",
        ModalidadeAtendimento.BsvApenas => "BSV (apenas)",
        ModalidadeAtendimento.Consulta => "Consulta",
        _ => modalidade.ToString()
    };
}
