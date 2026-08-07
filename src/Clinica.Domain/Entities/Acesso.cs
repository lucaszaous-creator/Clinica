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

    /// <summary>Abrir a ficha do paciente e o prontuário.</summary>
    VerProntuario = 1 << 2,

    /// <summary>Escrever evolução, anexar arquivo e registrar consentimento.</summary>
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
    ConfigurarFaturamento = 1 << 21
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
    public static Permissao Padrao(PerfilAcesso perfil) => perfil switch
    {
        PerfilAcesso.Recepcao =>
            Permissao.VerAgenda | Permissao.EditarAgenda |
            Permissao.VerProntuario | Permissao.EditarProntuario |
            Permissao.GerenciarCampanhas,

        PerfilAcesso.Profissional =>
            Permissao.VerAgenda | Permissao.VerProntuario | Permissao.EditarProntuario |
            Permissao.Prescrever,

        // A técnica vê a agenda (para saber quem está na sala), lê o prontuário (alergia
        // antes de infundir não é opcional) e CHECA. Não recebe EditarProntuario nem
        // Prescrever: a checagem já é o registro dela, e é assinado.
        PerfilAcesso.Enfermagem =>
            Permissao.VerAgenda | Permissao.VerProntuario | Permissao.ChecarPrescricao,

        PerfilAcesso.Financeiro =>
            Permissao.VerAgenda | Permissao.VerFinanceiro | Permissao.EditarFinanceiro,

        // O faturista recebe o faturamento inteiro, MENOS as Configurações: mudar o
        // catálogo de convênios ou o prazo da rodada muda a regra para todo mundo, e quem
        // decide isso é a direção.
        //
        // O padrão foi montado para reproduzir EXATAMENTE o que o app de faturamento
        // deixava fazer antes de ganhar login (parcela 45) — inclusive marcar na agenda e
        // cadastrar paciente, que é o que a secretária que fatura faz o dia inteiro. Uma
        // versão que introduz permissão e, de quebra, tira uma capacidade que a pessoa
        // usava ontem vira chamado de suporte na segunda de manhã, e o pedido da cliente
        // era o contrário: poder LIBERAR OU NÃO, caso a caso. Agora a direção tira o bit
        // de quem não deve ter — que é uma decisão dela, tomada na tela de Acessos, e não
        // um efeito colateral da atualização.
        PerfilAcesso.Faturista =>
            Permissao.VerAgenda | Permissao.EditarAgenda |
            Permissao.VerProntuario | Permissao.EditarProntuario |
            Permissao.VerFaturamento | Permissao.BaixarGuia | Permissao.EstornarBaixa |
            Permissao.RegistrarGlosa | Permissao.GerenciarLotesTiss |
            Permissao.LancarAtendimento | Permissao.MarcarNaoConformidade,

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
        Permissao.VerProntuario => "Ver prontuário",
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
