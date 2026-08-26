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

    // ===================================================================
    // Chaves que um item COMPOSTO de outro módulo usa como sub-aba (parcela 55).
    //
    // Um item composto lista as abas por CHAVE, e quase sempre compõe telas de módulos
    // diferentes: "Relatórios / BI" é publicado pela Direção e inclui duas telas do
    // Financeiro e uma do Consultório. Como nenhum módulo referencia o outro, a chave
    // atravessaria como literal escrito à mão — e renomear a const do lado de lá
    // compilaria dos dois lados, deixando a aba silenciosamente de fora.
    //
    // É exatamente o risco que esta classe existe para cobrir; só mudou o motivo de a
    // chave atravessar (antes navegação, agora também composição).
    // ===================================================================

    /// <summary>Painel do balcão — "Início" (Recepção). 1ª aba de "Painel" fora do Gerente.</summary>
    public const string PainelRecepcao = "painel-recepcao";

    /// <summary>Agenda do balcão (Recepção).</summary>
    public const string AgendaRecepcao = "agenda-recepcao";

    /// <summary>Pacientes / CRM (Recepção).</summary>
    public const string PacientesRecepcao = "pacientes-recepcao";

    /// <summary>Emissão de receituário e afins (Recepção).</summary>
    public const string PrescricoesRecepcao = "prescricoes";

    /// <summary>Retorno de pacientes — o recall do balcão (Recepção).</summary>
    public const string RetornoPacientes = "retorno-pacientes";

    /// <summary>Produção — volume do faturamento (Financeiro).</summary>
    public const string Producao = "producao";

    /// <summary>Minha semana (Consultório).</summary>
    public const string ConsultorioSemana = "consultorio-semana";

    /// <summary>Meus pacientes — a carteira de quem atende (Consultório).</summary>
    public const string ConsultorioPacientes = "consultorio-pacientes";

    /// <summary>Prescrever a partir do paciente em foco (Consultório).</summary>
    public const string ConsultorioPrescricoes = "consultorio-prescricoes";

    /// <summary>Folha de infusão (Consultório).</summary>
    public const string ConsultorioPrescricaoInfusao = "consultorio-prescricao-infusao";

    /// <summary>Meus números — a produtividade de quem atende (Consultório).</summary>
    public const string ConsultorioMeusNumeros = "consultorio-meus-numeros";

    /// <summary>
    /// Sala de infusão — a fila de checagem da enfermagem, a tela do SHELL que
    /// Consultório e Recepção publicam sob a MESMA chave (parcela 48; a dedupe do shell
    /// funde as duas no Gerente). Era literal duplicado nos dois módulos, exatamente o
    /// que esta classe existe para impedir: renomear de um lado compilava dos dois e a
    /// Recepção deixava de abrir a sala.
    /// </summary>
    public const string SalaInfusao = "consultorio-sala-infusao";

    /// <summary>
    /// A tela da ENFERMAGEM (parcela 71): todos os pacientes cadastrados e a evolução de
    /// cada um. Terceira tela do SHELL publicada por DOIS módulos, pela razão da sala de
    /// infusão logo acima — a enfermagem entra pelo `Clinica.Recepcao.exe` e pelo
    /// `Clinica.Clinico.exe`, e a dedupe do shell funde as duas no Gerente.
    ///
    /// ⚠️ É tela SEPARADA da sala de infusão de propósito: a sala responde "o que executar
    /// agora" e só mostra as folhas do dia; esta responde "quem eu atendi e o que escrevi",
    /// e a clínica disse que TODO paciente passa pela enfermagem — a maioria dessas
    /// passagens não tem folha nenhuma.
    /// </summary>
    public const string Enfermagem = "enfermagem";

    /// <summary>
    /// O ATENDIMENTO DE ENFERMAGEM (parcela 88) — a seção do módulo Clínico onde a técnica
    /// escreve a passagem, dentro da tela do paciente.
    ///
    /// Ela atravessa módulo porque quem manda a enfermeira para lá é o <b>Atender</b> da
    /// tela da Enfermagem, que é do SHELL: quem ENTREGA o paciente está de um lado da
    /// fronteira e quem o RECEBE está do outro.
    ///
    /// ⚠️ E ela pode NÃO EXISTIR no executável em que a tela está aberta: o
    /// <c>Clinica.Recepcao.exe</c> publica a Enfermagem e não carrega o módulo Clínico.
    /// Por isso o "Atender" pergunta antes, com <c>NavegacaoSuite.Existe</c>, e cai no
    /// painel da própria tela quando o destino não está ali — <c>Ir</c> devolve
    /// <c>false</c> EM SILÊNCIO, e botão que não faz nada é o defeito da parcela 41.
    /// </summary>
    public const string AtendimentoEnfermagem = "consultorio-atendimento-enfermagem";

    /// <summary>
    /// Pacotes de sessões — a segunda tela do SHELL publicada por DOIS módulos
    /// (Financeiro e Recepção, parcela 60). Mora aqui pela razão da sala de infusão logo
    /// acima, e agora com um caso concreto no currículo: enquanto era literal dos dois
    /// lados, a Recepção publicou o item e não construiu a tela — o menu acendia e nada
    /// abria, e nenhuma rede viu, porque string à mão sempre compila.
    /// </summary>
    public const string Pacotes = "pacotes";
}
