namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// Chaves de seção que são DESTINO DE NAVEGAÇÃO de outro módulo (parcela 22).
///
/// A regra da arquitetura é que nenhum módulo conhece os outros, e ela continua valendo:
/// o painel da direção não referencia o módulo Financeiro, ele referencia a chave. O que
/// esta classe evita é a alternativa — repetir <c>"fechamento-caixa"</c> escrito à mão do
/// outro lado, onde renomear a seção compila e só falha na clínica, com um botão que não
/// leva a lugar nenhum.
///
/// Só entram aqui as chaves que ATRAVESSAM módulo. As demais continuam sendo const do
/// módulo dono, que é onde pertencem: chave que só um módulo usa não é contrato de
/// ninguém.
/// </summary>
public static class ChavesSuite
{
    /// <summary>Painel da direção — a abertura do Gerente Geral.</summary>
    public const string PainelDirecao = "painel-direcao";

    /// <summary>Caixa — entradas e saídas (Financeiro). O recibo nasce de um lançamento dele.</summary>
    public const string Caixa = "caixa";

    /// <summary>Central de documentos — as nove folhas (Recepção).</summary>
    public const string Documentos = "documentos";

    /// <summary>Contas a pagar/receber (Financeiro).</summary>
    public const string Contas = "contas";

    /// <summary>Quem me deve — inadimplência por paciente (Financeiro).</summary>
    public const string Inadimplencia = "inadimplencia";

    /// <summary>Recebíveis de cartão (Financeiro).</summary>
    public const string Recebiveis = "recebiveis";

    /// <summary>Fechamento de caixa (Financeiro).</summary>
    public const string FechamentoCaixa = "fechamento-caixa";

    /// <summary>Conciliação guia × caixa (Financeiro).</summary>
    public const string Conciliacao = "conciliacao";

    /// <summary>Resultado do mês e teto de gasto (Financeiro).</summary>
    public const string Resultado = "resultado";

    /// <summary>Faturamento (TISS) consolidado (Direção) — pendências e glosas.</summary>
    public const string FaturamentoTiss = "faturamento-gerencial";

    /// <summary>
    /// O dia de quem atende (Consultório). É destino do painel da direção desde a
    /// parcela 36: o alerta de prontuário em aberto precisa LEVAR à tela onde as sessões
    /// sem evolução estão listadas — e essa tela mora noutro módulo.
    /// </summary>
    public const string ConsultorioMeuDia = "consultorio-meu-dia";
}
