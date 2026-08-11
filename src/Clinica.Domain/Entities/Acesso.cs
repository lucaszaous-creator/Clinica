namespace Clinica.Domain.Entities;

/// <summary>
/// O que um usuário pode fazer na suíte. É um conjunto de bits de propósito: perfil
/// resolve 90% dos casos, mas clínica pequena sempre tem a exceção ("a recepcionista
/// também lança o caixa") — e sem permissão fina a exceção vira senha compartilhada,
/// que é o fim de qualquer trilha de auditoria.
///
/// Gravada como INTEIRO (não como texto): a lista de bits muda com o tempo e um valor
/// serializado por nome ("VerAgenda, EditarAgenda") quebraria ao renomear qualquer um.
/// </summary>
[Flags]
public enum Permissao
{
    Nenhuma = 0,

    /// <summary>Ver a agenda, a fila do dia e o painel da recepção.</summary>
    VerAgenda = 1 << 0,

    /// <summary>Marcar, remarcar, cancelar, dar check-in e concluir atendimento.</summary>
    EditarAgenda = 1 << 1,

    /// <summary>
    /// Ler o PRONTUÁRIO CLÍNICO: evolução, EVA, mapa corporal, anexos, medidas, escalas e
    /// a lista de problemas/alergias.
    ///
    /// <b>Deixou de significar "abrir a ficha do paciente" na parcela 49.</b> Até aqui um
    /// bit só governava o cadastro administrativo e o registro clínico, e o efeito era o
    /// que a cliente apontou: quem marca horário no balcão lia a evolução inteira de todo
    /// mundo. São coisas de natureza diferente — o telefone do paciente é dado de
    /// contato, a evolução é dado de SAÚDE, e a LGPD trata o segundo como sensível
    /// (art. 5º, II). Quem precisa dos dois recebe os dois.
    /// </summary>
    VerProntuario = 1 << 2,

    /// <summary>Escrever evolução, aplicar escala, colher medida e anexar arquivo clínico.</summary>
    EditarProntuario = 1 << 3,

    /// <summary>Ver caixa, conciliação e produção.</summary>
    VerFinanceiro = 1 << 4,

    /// <summary>Lançar, realizar e cancelar movimento de caixa.</summary>
    EditarFinanceiro = 1 << 5,

    /// <summary>
    /// LER o faturamento: guias, pendências, lotes, consulta de guias e relatórios.
    ///
    /// Até a parcela 45 este bit era o faturamento inteiro — quem podia ver podia baixar,
    /// estornar e glosar. Agora ele é só a leitura, e cada escrita tem o bit dela logo
    /// abaixo. Quem tinha o perfil Faturista continua podendo tudo, porque o padrão do
    /// perfil ganhou os bits novos: a permissão é resolvida na LEITURA, então ninguém
    /// precisou ser reeditado.
    /// </summary>
    VerFaturamento = 1 << 6,

    /// <summary>Ver os indicadores gerenciais.</summary>
    VerIndicadores = 1 << 7,

    /// <summary>Gerar e disparar campanhas: confirmação, NPS e recall.</summary>
    GerenciarCampanhas = 1 << 8,

    /// <summary>Cadastrar profissionais e salas.</summary>
    GerenciarEquipe = 1 << 9,

    /// <summary>Criar usuário, trocar senha e mexer em permissão.</summary>
    GerenciarUsuarios = 1 << 10,

    /// <summary>Ler a trilha de auditoria.</summary>
    VerAuditoria = 1 << 11,

    /// <summary>
    /// Anonimizar o cadastro a pedido do titular (LGPD, art. 18). Nasce separada de
    /// <see cref="EditarProntuario"/> de propósito: escrever uma evolução se corrige,
    /// anonimizar NÃO tem volta — nome, documento e telefone não voltam. Quem decide
    /// atender ao pedido de eliminação é o controlador, não o balcão. EXPORTAR os dados
    /// do titular continua em <see cref="VerProntuario"/>: entregar ao paciente o que é
    /// dele é atendimento comum, e é a recepção que atende.
    /// </summary>
    AnonimizarDados = 1 << 12,

    /// <summary>
    /// Escrever e ASSINAR prescrição de execução interna (parcela 42). Separada de
    /// <see cref="EditarProntuario"/> porque prescrever não é escrever no prontuário: é
    /// mandar administrar uma droga em alguém, e a folha assinada é o documento que
    /// sustenta o ato. Quem digita a evolução do fisioterapeuta não prescreve infusão.
    /// </summary>
    Prescrever = 1 << 13,

    /// <summary>
    /// Checar a execução — dizer "foi prescrito assim e foi realizado assim" (parcela 42).
    ///
    /// É o espelho da de cima e nunca anda junto dela: quem prescreve não checa a própria
    /// prescrição. Não é regra de sistema, é a razão de a checagem existir — a conferência
    /// vale porque foram duas pessoas. O sistema não IMPEDE que o mesmo login tenha as
    /// duas (numa clínica pequena o profissional às vezes administra ele mesmo), mas os
    /// perfis padrão as mantêm separadas, e a folha imprime os dois nomes.
    /// </summary>
    ChecarPrescricao = 1 << 14,

    // ------------------------------------------------------------------------
    // Faturamento em detalhe (parcela 45)
    //
    // O pedido da cliente foi "permissões granulares no faturamento, para a gerente
    // liberar ou não e auditar o que está sendo feito". Um bit só (VerFaturamento) não
    // respondia: dar baixa, estornar uma baixa e recusar faturamento são atos de peso
    // muito diferente, e a clínica precisava conceder um sem conceder os outros.
    //
    // O corte segue o que é IRREVERSÍVEL ou o que MUDA O NÚMERO do mês, não a tela onde
    // o botão está — quebrar por tela daria uma lista que muda a cada leiaute novo.
    // ------------------------------------------------------------------------

    /// <summary>
    /// Dar baixa na guia (individual, em lote e na rodada de pendências). É o ato central
    /// do faturista e o único que a clínica quase sempre concede junto com a leitura.
    /// </summary>
    BaixarGuia = 1 << 15,

    /// <summary>
    /// Estornar uma baixa já registrada. Separada de <see cref="BaixarGuia"/> porque
    /// desfazer é o ato que apaga o trabalho de outra pessoa: a guia volta a pendente, o
    /// número real some da linha e a conciliação do Financeiro perde o elo. Errar a baixa
    /// é acidente; estornar é decisão.
    /// </summary>
    EstornarBaixa = 1 << 16,

    /// <summary>
    /// Registrar glosa, reapresentar e marcar recuperada. Mexe no que a clínica tem a
    /// receber e dispara o prazo de recurso — quem anota glosa está dizendo que a
    /// operadora recusou, e essa afirmação sai do faturamento e chega ao caixa.
    /// </summary>
    RegistrarGlosa = 1 << 17,

    /// <summary>
    /// Gerar, exportar, enviar e dar retorno de lote TISS. É o que sai da clínica para a
    /// operadora: guia exportada num lote não entra em outro, então um lote gerado por
    /// engano não se desfaz com um clique.
    /// </summary>
    GerenciarLotesTiss = 1 << 18,

    /// <summary>
    /// Lançar atendimento — o ato que CRIA as guias pela regra do convênio, e junto dele a
    /// consulta e a autorização de sessões que o lançamento consome.
    /// </summary>
    LancarAtendimento = 1 << 19,

    /// <summary>
    /// Decidir NÃO faturar uma guia (não conformidade) e reabrir o que foi decidido.
    ///
    /// É a permissão mais delicada do faturamento: ela é a única que faz uma pendência
    /// sumir do painel sem que a guia tenha sido faturada. Sem bit próprio, quem tivesse
    /// acesso à tela poderia zerar o alarme do sistema justamente sobre o trabalho que ele
    /// existe para cobrar.
    /// </summary>
    MarcarNaoConformidade = 1 << 20,

    /// <summary>
    /// Mexer nas Configurações do faturamento: catálogo de convênios (incluindo o formato
    /// do número da guia), modalidades, especialidades, prazos e dados do prestador. Muda
    /// a regra para todo mundo, e não só o registro de uma guia.
    /// </summary>
    ConfigurarFaturamento = 1 << 21,

    // ------------------------------------------------------------------------
    // A ficha do paciente, separada do prontuário clínico (parcela 49)
    //
    // O pedido da direção foi direto: "não faz sentido a recepção ter acesso a dados
    // pessoais dos pacientes". A causa não era o padrão do perfil — era o BIT: um só
    // (`VerProntuario`) abria a ficha administrativa E a evolução clínica, então não
    // havia como conceder um sem o outro. Permissão granular que não distingue o que a
    // clínica distingue não é granular; é uma caixinha a mais na tela.
    //
    // O corte é o da LGPD: dado de contato de um lado, dado de SAÚDE do outro.
    // ------------------------------------------------------------------------

    /// <summary>
    /// Abrir a FICHA do paciente: cadastro, contato, convênio e carteirinha,
    /// elegibilidade, autorizações de sessões, consentimentos e os documentos emitidos.
    ///
    /// É o que o balcão precisa para marcar, receber e cobrar o documento — e não inclui
    /// uma linha do que foi feito na sessão.
    /// </summary>
    VerFichaPaciente = 1 << 22,

    /// <summary>
    /// Escrever na FICHA: cadastrar e editar paciente, registrar consentimento LGPD,
    /// registrar a autorização do convênio e emitir/cancelar documento.
    ///
    /// Separada de <see cref="EditarProntuario"/> pela mesma razão de cima: digitar o
    /// telefone de alguém e escrever a evolução dele são atos de peso diferente, e antes
    /// da parcela 49 o mesmo bit dava os dois.
    /// </summary>
    EditarPaciente = 1 << 23,

    /// <summary>
    /// Entrar no faturamento SEM ser travado pela rodada de pendências.
    ///
    /// A rodada bloqueante existe para que a guia vencida não seja adiada indefinidamente:
    /// passado o prazo, o app abre uma janela que só fecha quando cada guia tem uma decisão.
    /// Ela é dirigida a QUEM FATURA — é a pessoa que tem o número da guia na mão e resolve.
    ///
    /// A direção não fatura: ela entra no faturamento para conferir, e travar a tela dela
    /// com uma fila de guias faz a conferência não acontecer. Este bit é a dispensa, e ele
    /// é uma DISPENSA e não uma obrigação de propósito: o perfil Gerente recebe
    /// <see cref="PerfisAcesso.Todas"/>, então um bit com o sentido invertido ("está
    /// sujeito à rodada") chegaria ligado à direção justamente por ela ter tudo.
    ///
    /// ⚠️ Dispensa NÃO é cegueira: quem tem o bit continua vendo o banner de rodada
    /// vencida no painel e o botão "Rodar pendências". O que muda é só a janela que
    /// TRANCA na abertura. Esconder o aviso junto faria a direção deixar de saber que há
    /// guia vencida — que é o oposto do que a rodada existe para garantir.
    /// </summary>
    DispensarRodadaPendencias = 1 << 24
}

/// <summary>
/// Papel do usuário na clínica. Define o conjunto BASE de permissões; o ajuste fino
/// fica em <see cref="UsuarioSistema.PermissoesExtras"/> e
/// <see cref="UsuarioSistema.PermissoesNegadas"/>.
/// </summary>
public enum PerfilAcesso
{
    /// <summary>Balcão: agenda, fila, cadastro e prontuário.</summary>
    Recepcao,

    /// <summary>Quem atende: a própria agenda e o prontuário; nada de dinheiro.</summary>
    Profissional,

    /// <summary>Administrativo: caixa, conciliação e produção.</summary>
    Financeiro,

    /// <summary>Faturista: opera o faturamento inteiro, menos as Configurações dele.</summary>
    Faturista,

    /// <summary>
    /// Enfermagem (parcela 42): executa e CHECA a prescrição, e não prescreve nem escreve
    /// evolução. Nasceu junto da checagem porque ela não vale nada sem login próprio —
    /// checagem feita no usuário compartilhado do balcão é uma assinatura de ninguém, e
    /// era esse o buraco que a folha de papel já não tinha.
    /// </summary>
    Enfermagem,

    /// <summary>Direção: tudo, inclusive criar usuário.</summary>
    Gerente
}

/// <summary>Conjunto padrão de permissões de cada perfil, e o rótulo em português.</summary>
public static class PerfisAcesso
{
    /// <summary>
    /// Permissões que o perfil concede por padrão. NÃO é gravado na linha do usuário:
    /// é resolvido na leitura, para que corrigir o padrão de um perfil valha para todo
    /// mundo que o usa, em vez de exigir reeditar usuário por usuário.
    /// </summary>
    /// <summary>
    /// O PADRÃO de cada perfil — o que a direção decidiu que cada função faz (parcela 49).
    ///
    /// O que mudou, e por quê
    /// ----------------------
    /// A direção apontou o buraco: <i>"não adianta ter permissão granular se todo perfil
    /// nasce podendo tudo"</i>. Os padrões não davam literalmente tudo, mas dois deles
    /// davam demais, e por um motivo que não era escolha — era o BIT sobrecarregado. Até
    /// a parcela 48, <c>VerProntuario</c> significava "abrir a ficha E ler a evolução", e
    /// <c>EditarProntuario</c> significava "cadastrar paciente E escrever no prontuário".
    /// Não havia como dar um sem o outro; a granularidade existia na tela e não no
    /// domínio. A parcela 49 separou os dois (<see cref="Permissao.VerFichaPaciente"/> e
    /// <see cref="Permissao.EditarPaciente"/>) e refez os padrões em cima do corte novo.
    ///
    /// ⚠️ <b>Isto TIRA capacidade de quem já a usava, e é de propósito.</b> A regra do
    /// projeto ("não tire função de quem a tinha ontem") vale para efeito COLATERAL de
    /// atualização; aqui a remoção É o pedido — a direção quer poder auditar e liberar
    /// caso a caso. O que a regra continua exigindo é que a devolução seja barata: cada
    /// bit tirado se concede de volta a uma pessoa específica em Acessos, num clique, sem
    /// mexer no perfil dos outros.
    ///
    /// As três perguntas que decidiram cada linha
    /// ------------------------------------------
    /// 1. <b>A pessoa precisa disto para fazer o trabalho dela?</b> Não é "pode dar sem
    ///    risco" — é "sem isto, ela para". Bit que ninguém usa vira bit que ninguém
    ///    revisa.
    /// 2. <b>O ato apaga o trabalho de outra pessoa, ou some com uma cobrança do
    ///    sistema?</b> Se sim, é da chefia (estorno de baixa, não conformidade,
    ///    anonimização).
    /// 3. <b>É dado de SAÚDE?</b> Se sim, só quem cuida do paciente — a LGPD trata
    ///    prontuário como dado sensível, e o balcão não precisa dele para marcar horário.
    /// </summary>
    public static Permissao Padrao(PerfilAcesso perfil) => perfil switch
    {
        // ===== BALCÃO =====
        // Marca, recebe, cadastra, cobra o documento e chama de volta quem sumiu.
        //
        // NÃO recebe o prontuário CLÍNICO (parcela 49). Era o exemplo que a direção deu, e
        // ele é o corte da LGPD: telefone e convênio são dado de contato; a evolução da
        // sessão é dado de SAÚDE, e não é preciso lê-la para marcar um horário. Clínica
        // pequena em que a recepcionista também digita a evolução do profissional
        // continua possível — a direção concede `EditarProntuario` àquela pessoa em
        // Acessos, que é exatamente o controle que ela pediu.
        //
        // LancarAtendimento fica: a recepção JÁ cria atendimento com guia pelo caminho da
        // agenda (Fila → Finalizar) desde a parcela 6, e tirá-lo quebraria o fluxo do dia.
        PerfilAcesso.Recepcao =>
            Permissao.VerAgenda | Permissao.EditarAgenda |
            Permissao.VerFichaPaciente | Permissao.EditarPaciente |
            Permissao.LancarAtendimento |
            Permissao.GerenciarCampanhas,

        // ===== QUEM ATENDE =====
        // A ficha para saber quem é, o prontuário para saber o que foi feito, e a receita.
        // Não mexe em agenda de terceiros nem em dinheiro.
        PerfilAcesso.Profissional =>
            Permissao.VerAgenda |
            Permissao.VerFichaPaciente |
            Permissao.VerProntuario | Permissao.EditarProntuario |
            Permissao.Prescrever,

        // ===== ENFERMAGEM =====
        // A técnica vê a agenda (para saber quem está na sala), lê o prontuário (alergia
        // antes de infundir não é opcional) e CHECA. Não recebe EditarProntuario nem
        // Prescrever: a checagem já é o registro dela, e é assinado. A conferência vale
        // porque foram duas pessoas.
        PerfilAcesso.Enfermagem =>
            Permissao.VerAgenda |
            Permissao.VerFichaPaciente | Permissao.VerProntuario |
            Permissao.ChecarPrescricao,

        // ===== ADMINISTRATIVO/CAIXA =====
        // Ganhou a FICHA na parcela 49: sem ela a tela de inadimplência mostrava dívida de
        // gente que o operador não podia abrir para conferir o telefone. Continua sem o
        // prontuário — cobrar não precisa saber o diagnóstico de ninguém.
        PerfilAcesso.Financeiro =>
            Permissao.VerAgenda |
            Permissao.VerFichaPaciente |
            Permissao.VerFinanceiro | Permissao.EditarFinanceiro,

        // ===== FATURISTA =====
        // Opera o faturamento do dia: lê, dá baixa, glosa e manda o lote. O que saiu na
        // parcela 49, e por quê:
        //
        //  · EstornarBaixa — desfazer apaga o trabalho de outra pessoa e desfaz o elo com
        //    a conciliação do Financeiro. Errar a baixa é acidente; estornar é decisão, e
        //    decisão é da chefia.
        //  · MarcarNaoConformidade — foi o segundo exemplo da direção. É a ÚNICA permissão
        //    que faz uma pendência sumir do painel sem a guia ter sido faturada: quem a
        //    tem pode zerar o alarme do sistema justamente sobre o trabalho que ele existe
        //    para cobrar. Reabrir uma NC é da mesma família.
        //  · VerProntuario / EditarProntuario — faturar não exige ler a evolução. Ficou a
        //    FICHA, que é o que a baixa realmente usa (convênio, carteirinha, autorização).
        //  · VerIndicadores nunca esteve aqui, e é o que agora guarda os RELATÓRIOS
        //    gerenciais do faturamento — o terceiro exemplo da direção.
        //
        // Continua sem ConfigurarFaturamento: mudar catálogo de convênio ou prazo da
        // rodada muda a regra para todo mundo.
        // ⚠️ SEM `EditarAgenda` (parcela 58, a pedido da direção): quem marca horário é o
        // BALCÃO, que tem o paciente na frente. O faturista continua VENDO a agenda —
        // é o que ele precisa para conferir o que foi atendido — e o bit volta num clique
        // em Acessos para quem a clínica quiser. Isto TIRA o que ele fazia ontem, e é de
        // propósito: é o mesmo movimento da parcela 49, onde a remoção É o pedido.
        PerfilAcesso.Faturista =>
            Permissao.VerAgenda |
            Permissao.VerFichaPaciente | Permissao.EditarPaciente |
            Permissao.VerFaturamento | Permissao.BaixarGuia |
            Permissao.RegistrarGlosa | Permissao.GerenciarLotesTiss |
            Permissao.LancarAtendimento,

        PerfilAcesso.Gerente => Todas,

        _ => Permissao.Nenhuma
    };

    /// <summary>Todos os bits definidos — o que o Gerente recebe.</summary>
    public static Permissao Todas
    {
        get
        {
            var todas = Permissao.Nenhuma;
            foreach (var p in Enum.GetValues<Permissao>()) todas |= p;
            return todas;
        }
    }

    public static string Rotular(PerfilAcesso perfil) => perfil switch
    {
        PerfilAcesso.Recepcao => "Recepção",
        PerfilAcesso.Profissional => "Profissional",
        PerfilAcesso.Financeiro => "Financeiro",
        PerfilAcesso.Faturista => "Faturista",
        PerfilAcesso.Enfermagem => "Enfermagem",
        PerfilAcesso.Gerente => "Gerente Geral",
        _ => perfil.ToString()
    };

    public static string Rotular(Permissao permissao) => permissao switch
    {
        Permissao.VerAgenda => "Ver agenda e fila",
        Permissao.EditarAgenda => "Marcar e remarcar",
        Permissao.VerFichaPaciente => "Ver ficha do paciente",
        Permissao.EditarPaciente => "Cadastrar e editar paciente",
        Permissao.DispensarRodadaPendencias => "Entrar sem responder à rodada de pendências",
        Permissao.VerProntuario => "Ver prontuário clínico",
        Permissao.EditarProntuario => "Escrever no prontuário",
        Permissao.VerFinanceiro => "Ver financeiro",
        Permissao.EditarFinanceiro => "Lançar no caixa",
        Permissao.VerFaturamento => "Ver faturamento",
        Permissao.BaixarGuia => "Dar baixa em guia",
        Permissao.EstornarBaixa => "Estornar baixa de guia",
        Permissao.RegistrarGlosa => "Registrar e recorrer de glosa",
        Permissao.GerenciarLotesTiss => "Gerar e enviar lote TISS",
        Permissao.LancarAtendimento => "Lançar atendimento",
        Permissao.MarcarNaoConformidade => "Decidir não faturar (NC)",
        Permissao.ConfigurarFaturamento => "Configurar o faturamento",
        Permissao.VerIndicadores => "Ver indicadores",
        Permissao.GerenciarCampanhas => "Gerenciar campanhas",
        Permissao.GerenciarEquipe => "Cadastrar equipe",
        Permissao.GerenciarUsuarios => "Gerenciar usuários",
        Permissao.VerAuditoria => "Ver auditoria",
        Permissao.AnonimizarDados => "Anonimizar dados do titular (LGPD)",
        Permissao.Prescrever => "Prescrever e assinar prescrição",
        Permissao.ChecarPrescricao => "Checar execução de prescrição",
        _ => permissao.ToString()
    };

    /// <summary>Bits individuais (sem <see cref="Permissao.Nenhuma"/>), para montar a tela.</summary>
    public static IReadOnlyList<Permissao> Individuais { get; } =
        Enum.GetValues<Permissao>().Where(p => p != Permissao.Nenhuma).ToList();

    /// <summary>
    /// O ASSUNTO de cada permissão, para a tela de Acessos agrupar (parcela 49).
    ///
    /// Vinte e quatro caixinhas numa lista corrida não são uma decisão: são um formulário.
    /// Quem precisa responder "o que a recepcionista pode fazer?" tem de conseguir ler a
    /// resposta por blocos — e é lendo por blocos que se percebe o bit solto que ninguém
    /// queria ter concedido.
    /// </summary>
    public static string Assunto(Permissao permissao) => permissao switch
    {
        Permissao.VerAgenda or Permissao.EditarAgenda => "Agenda e balcão",

        Permissao.VerFichaPaciente or Permissao.EditarPaciente => "Paciente (cadastro)",

        Permissao.VerProntuario or Permissao.EditarProntuario
            or Permissao.Prescrever or Permissao.ChecarPrescricao => "Clínico (dado sensível)",

        Permissao.VerFinanceiro or Permissao.EditarFinanceiro => "Financeiro",

        Permissao.VerFaturamento or Permissao.BaixarGuia or Permissao.EstornarBaixa
            or Permissao.RegistrarGlosa or Permissao.GerenciarLotesTiss
            or Permissao.LancarAtendimento or Permissao.MarcarNaoConformidade
            or Permissao.ConfigurarFaturamento
            or Permissao.DispensarRodadaPendencias => "Faturamento",

        _ => "Direção"
    };

    /// <summary>
    /// O que a permissão deixa fazer, EM UMA FRASE — e, quando o bit é dos delicados, por
    /// que ele é separado.
    ///
    /// Existe porque o rótulo sozinho não decide nada: "Estornar baixa de guia" parece
    /// inofensivo até alguém dizer que estornar apaga o trabalho de outra pessoa. Quem
    /// concede permissão precisa da consequência escrita ao lado da caixinha, não num
    /// manual que ninguém abre.
    /// </summary>
    public static string Explicar(Permissao permissao) => permissao switch
    {
        Permissao.VerAgenda => "Abrir a agenda, a fila do dia e o painel do balcão.",
        Permissao.EditarAgenda => "Marcar, remarcar, cancelar, dar check-in e concluir.",

        Permissao.VerFichaPaciente =>
            "Cadastro, contato, convênio, carteirinha, autorizações e documentos emitidos. "
            + "NÃO inclui o que foi feito nas sessões.",
        Permissao.EditarPaciente =>
            "Cadastrar e editar paciente, colher consentimento LGPD, registrar a senha do "
            + "convênio e emitir documento.",
        Permissao.DispensarRodadaPendencias =>
            "Passado o prazo, a abertura do faturamento TRAVA numa janela que só fecha com "
            + "uma decisão por guia. Quem tem este acesso entra direto — continua vendo o "
            + "aviso de rodada vencida no painel e pode rodá-la quando quiser. Dê a quem "
            + "ENTRA para conferir, não a quem fatura: sem ninguém travado, a guia vencida "
            + "volta a depender de alguém lembrar.",

        Permissao.VerProntuario =>
            "Ler evolução, EVA, mapa corporal, anexos, medidas e alergias — DADO DE SAÚDE. "
            + "Quem não cuida do paciente não precisa disto para trabalhar.",
        Permissao.EditarProntuario =>
            "Escrever evolução, aplicar escala e colher medida.",
        Permissao.Prescrever =>
            "Escrever e assinar receita e prescrição de infusão. É mandar administrar "
            + "medicação em alguém.",
        Permissao.ChecarPrescricao =>
            "Afirmar que o prescrito foi realizado. Vale porque são DUAS pessoas: quem "
            + "checa não deve ser quem prescreve.",

        Permissao.VerFinanceiro => "Caixa, conciliação, contas e produção.",
        Permissao.EditarFinanceiro => "Lançar, realizar e cancelar movimento de caixa.",

        Permissao.VerFaturamento => "Ler guias, pendências, lotes e a consulta de guias.",
        Permissao.BaixarGuia =>
            "Efetivar a guia no sistema do convênio. É o ato central do faturista.",
        Permissao.EstornarBaixa =>
            "Desfazer uma baixa. APAGA o trabalho de outra pessoa e desfaz o elo com a "
            + "conciliação — errar a baixa é acidente, estornar é decisão.",
        Permissao.RegistrarGlosa =>
            "Anotar que a operadora recusou, recorrer e marcar recuperada. Dispara o prazo "
            + "de recurso e chega ao caixa.",
        Permissao.GerenciarLotesTiss =>
            "Gerar, enviar e dar retorno de lote. Guia exportada num lote não entra em outro.",
        Permissao.LancarAtendimento =>
            "Criar o atendimento — e, com ele, as guias, pela regra do convênio.",
        Permissao.MarcarNaoConformidade =>
            "Decidir NÃO faturar uma guia, e reabrir o que foi decidido. É a única "
            + "permissão que faz uma pendência sumir do painel sem a guia ter sido faturada.",
        Permissao.ConfigurarFaturamento =>
            "Catálogo de convênios, modalidades, prazos e dados do prestador. Muda a regra "
            + "para todo mundo, não o registro de uma guia.",

        Permissao.VerIndicadores =>
            "Indicadores gerenciais, BI e os relatórios do faturamento.",
        Permissao.GerenciarCampanhas => "Gerar e disparar confirmação, NPS e recall.",
        Permissao.GerenciarEquipe => "Cadastrar profissionais e salas.",
        Permissao.GerenciarUsuarios => "Criar usuário, trocar senha e mexer em permissão.",
        Permissao.VerAuditoria => "Ler a trilha de quem fez o quê.",
        Permissao.AnonimizarDados =>
            "Anonimizar o cadastro a pedido do titular. NÃO TEM VOLTA — nome, documento e "
            + "telefone não voltam.",

        _ => string.Empty
    };
}

/// <summary>
/// Quem entra na suíte. É o outro lado do <see cref="Profissional"/>, que a parcela 1
/// criou: o profissional ocupa a agenda e assina o atendimento; o usuário é o login.
/// Ligar os dois (<see cref="ProfissionalId"/>) é o que permite a um profissional ver
/// a própria agenda sem duplicar cadastro — e é por isso que a permissão fina só
/// pôde vir depois da fundação.
///
/// A senha NUNCA é guardada: só o hash PBKDF2 e o sal, um por usuário
/// (<see cref="Clinica.Domain.HashSenha"/>).
///
/// Desde a parcela 45 o app de FATURAMENTO também exige login: ele era o único posto sem
/// autenticação, e enquanto foi assim a auditoria dele gravava o usuário do Windows — isto
/// é, o nome da MÁQUINA — em vez do nome de quem deu a baixa.
/// </summary>
public class UsuarioSistema
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Identificador de entrada, sempre em minúsculas e sem espaço.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 da senha, em Base64. Nunca a senha.</summary>
    public string SenhaHash { get; set; } = string.Empty;

    /// <summary>Sal do hash, em Base64. Um por usuário — duas senhas iguais não colidem.</summary>
    public string SenhaSalt { get; set; } = string.Empty;

    public PerfilAcesso Perfil { get; set; } = PerfilAcesso.Recepcao;

    /// <summary>Permissões concedidas ALÉM do perfil (a exceção da clínica pequena).</summary>
    public Permissao PermissoesExtras { get; set; } = Permissao.Nenhuma;

    /// <summary>
    /// Permissões retiradas do perfil. Vence as extras de propósito: tirar acesso é a
    /// decisão que não pode ser anulada por engano de configuração.
    /// </summary>
    public Permissao PermissoesNegadas { get; set; } = Permissao.Nenhuma;

    /// <summary>Profissional correspondente, quando o usuário atende. Null para o balcão.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Desativado não entra, mas continua existindo na auditoria.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Senha provisória: o usuário troca no próximo acesso.</summary>
    public bool DeveTrocarSenha { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public DateTime? UltimoAcessoEm { get; set; }

    /// <summary>Tentativas erradas seguidas. Zera a cada acesso bem-sucedido.</summary>
    public int TentativasFalhas { get; set; }

    /// <summary>Até quando o login está travado por excesso de tentativas.</summary>
    public DateTime? BloqueadoAte { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>
    /// Permissões que valem de fato: o padrão do perfil, mais as extras, menos as
    /// negadas. Resolver na leitura (em vez de gravar o resultado) é o que faz um
    /// ajuste no perfil alcançar quem já estava cadastrado.
    /// </summary>
    public Permissao Efetivas
        => (PerfisAcesso.Padrao(Perfil) | PermissoesExtras) & ~PermissoesNegadas;

    /// <summary>Tem a permissão pedida? <see cref="Permissao.Nenhuma"/> é sempre sim (tela livre).</summary>
    public bool Pode(Permissao permissao)
        => permissao == Permissao.Nenhuma || (Efetivas & permissao) == permissao;

    /// <summary>Está travado por tentativas erradas neste instante?</summary>
    public bool Travado(DateTime agora) => BloqueadoAte is { } ate && agora < ate;

    /// <summary>Login normalizado: minúsculas, sem espaço nas pontas.</summary>
    public static string NormalizarLogin(string? login)
        => (login ?? string.Empty).Trim().ToLowerInvariant();
}
