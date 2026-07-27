using Clinica.Domain.Entities;

namespace Clinica.Application.Abstracoes;

/// <summary>Acesso a dados usado pelos serviços. Implementado sobre EF Core na camada de infraestrutura.</summary>
public interface IClinicaRepositorio
{
    Task<Paciente?> ObterPacienteAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>Códigos do paciente lançados no mês informado (usado pela rotação de especialidades da Petrobras).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosDoPacienteNoMesAsync(int pacienteId, int ano, int mes, CancellationToken ct = default);

    /// <summary>Todos os códigos ainda em aberto (não baixados, não "não aplicável" e não "não conformidade"), com paciente carregado.</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosEmAbertoAsync(CancellationToken ct = default);

    /// <summary>Guias em não conformidade (justificadas numa rodada e silenciadas), com paciente carregado.</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosEmNaoConformidadeAsync(CancellationToken ct = default);

    /// <summary>Guias em não conformidade de UM paciente (para reabrir quando ele volta). Entidades rastreadas.</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosEmNaoConformidadeDoPacienteAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>Códigos cujo atendimento ocorreu no período [inicio, fim], com paciente carregado (usado nos relatórios).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Pacientes com seus atendimentos carregados (usado para calcular renovação de consulta).</summary>
    Task<IReadOnlyList<Paciente>> PacientesComAtendimentosAsync(CancellationToken ct = default);

    Task<CodigoFaturamento?> ObterCodigoAsync(int codigoId, CancellationToken ct = default);

    Task AdicionarAtendimentoAsync(Atendimento atendimento, CancellationToken ct = default);

    /// <summary>Atendimento com paciente e códigos carregados (para gerar a capa de faturamento).</summary>
    Task<Atendimento?> ObterAtendimentoAsync(int atendimentoId, CancellationToken ct = default);

    // ---- Busca / ficha do paciente / faturados ----

    /// <summary>
    /// Busca pacientes por nome ou CPF (termo normalizado). Termo vazio devolve todos.
    /// <paramref name="limite"/> corta o resultado no BANCO — os seletores da UI só mostram as
    /// primeiras linhas, e trazer a base inteira para descartar em memória é desperdício de rede
    /// (o banco é remoto). Null = sem corte, para quem precisa varrer todo mundo.
    /// </summary>
    Task<IReadOnlyList<Paciente>> BuscarPacientesAsync(string? termo, int? limite = null, CancellationToken ct = default);

    /// <summary>Paciente com todo o histórico (atendimentos e seus códigos) carregado.</summary>
    Task<Paciente?> ObterPacienteComHistoricoAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>Guias baixadas cujo atendimento ocorreu no período (tela de Faturados).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosBaixadosNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Guias glosadas. Se somenteEmAberto, traz apenas as ainda não recuperadas.</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosGlosadosAsync(bool somenteEmAberto, CancellationToken ct = default);

    // ---- Lotes TISS ----

    Task AdicionarLoteAsync(LoteTiss lote, CancellationToken ct = default);

    /// <summary>Todos os lotes TISS, do mais recente ao mais antigo, com as guias carregadas.</summary>
    Task<IReadOnlyList<LoteTiss>> LotesTissAsync(CancellationToken ct = default);

    /// <summary>Lote com guias, atendimentos e pacientes carregados.</summary>
    Task<LoteTiss?> ObterLoteTissAsync(int loteId, CancellationToken ct = default);

    /// <summary>Guias baixadas do período (data do atendimento) que ainda não entraram em nenhum lote.</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosBaixadosSemLoteAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Consulta central de guias com filtros combinados (paciente, nº guia, período, status, convênio).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> ConsultarCodigosAsync(Modelos.FiltroConsultaGuias filtro, CancellationToken ct = default);

    Task AdicionarPacienteAsync(Paciente paciente, CancellationToken ct = default);
    Task RemoverPacienteAsync(int pacienteId, CancellationToken ct = default);

    // ---- Retrato do paciente ----

    /// <summary>Foto em tamanho cheio do paciente (tabela à parte). Null quando não há retrato.</summary>
    Task<PacienteFoto?> ObterFotoPacienteAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Grava (ou substitui) o retrato do paciente: a foto cheia na tabela própria e a
    /// miniatura na linha do paciente. Não persiste — chame <c>SalvarAsync</c>.
    /// </summary>
    Task DefinirFotoPacienteAsync(int pacienteId, byte[] conteudo, byte[] miniatura, CancellationToken ct = default);

    /// <summary>Apaga o retrato do paciente (foto cheia e miniatura). Não persiste.</summary>
    Task RemoverFotoPacienteAsync(int pacienteId, CancellationToken ct = default);

    // ---- Autorizações de sessões (cota do convênio) ----

    /// <summary>Autorizações do paciente, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<AutorizacaoSessoes>> AutorizacoesDoPacienteAsync(int pacienteId, CancellationToken ct = default);

    Task<AutorizacaoSessoes?> ObterAutorizacaoAsync(int autorizacaoId, CancellationToken ct = default);

    Task AdicionarAutorizacaoAsync(AutorizacaoSessoes autorizacao, CancellationToken ct = default);

    Task RemoverAutorizacaoAsync(int autorizacaoId, CancellationToken ct = default);

    /// <summary>Quantos atendimentos o paciente teve no intervalo (base do consumo da cota).</summary>
    Task<int> ContarAtendimentosDoPacienteAsync(int pacienteId, DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    // ---- Agenda ----
    // ---- Parâmetros dos convênios ----
    Task<IReadOnlyList<ParametroConvenio>> ParametrosAsync(CancellationToken ct = default);
    Task SalvarParametroAsync(ParametroConvenio parametro, CancellationToken ct = default);

    /// <summary>Valor da configuração global (chave/valor no banco), ou nulo se nunca salva.</summary>
    Task<string?> ObterConfiguracaoAsync(string chave, CancellationToken ct = default);
    Task SalvarConfiguracaoAsync(string chave, string valor, CancellationToken ct = default);

    /// <summary>Catálogo de convênios (todos, ativos e inativos).</summary>
    Task<IReadOnlyList<ConvenioCadastro>> ConveniosAsync(CancellationToken ct = default);
    Task SalvarConvenioAsync(ConvenioCadastro convenio, CancellationToken ct = default);

    /// <summary>Exclui um convênio do catálogo. Não valida uso — chame <see cref="ConvenioEmUsoAsync"/> antes.</summary>
    Task ExcluirConvenioAsync(string codigo, CancellationToken ct = default);

    /// <summary>Há algum paciente cadastrado com este código de convênio?</summary>
    Task<bool> ConvenioEmUsoAsync(string codigo, CancellationToken ct = default);

    /// <summary>Catálogo de modalidades (todas, ativas e inativas).</summary>
    Task<IReadOnlyList<ModalidadeCadastro>> ModalidadesAsync(CancellationToken ct = default);
    Task SalvarModalidadeAsync(ModalidadeCadastro modalidade, CancellationToken ct = default);
    Task ExcluirModalidadeAsync(string codigo, CancellationToken ct = default);

    /// <summary>Há paciente, atendimento ou agendamento usando este código de modalidade?</summary>
    Task<bool> ModalidadeEmUsoAsync(string codigo, CancellationToken ct = default);

    /// <summary>Catálogo de especialidades (todas, ativas e inativas).</summary>
    Task<IReadOnlyList<EspecialidadeCadastro>> EspecialidadesAsync(CancellationToken ct = default);
    Task SalvarEspecialidadeAsync(EspecialidadeCadastro especialidade, CancellationToken ct = default);
    Task ExcluirEspecialidadeAsync(string codigo, CancellationToken ct = default);

    /// <summary>Há atendimento, código ou agendamento usando este código de especialidade?</summary>
    Task<bool> EspecialidadeEmUsoAsync(string codigo, CancellationToken ct = default);

    // ---- Consultas (renováveis) ----
    Task AdicionarConsultaAsync(Consulta consulta, CancellationToken ct = default);

    /// <summary>Consultas do paciente, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<Consulta>> ConsultasDoPacienteAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>Todos os pacientes com suas consultas carregadas (para a aba de Consultas).</summary>
    Task<IReadOnlyList<Paciente>> PacientesComConsultasAsync(CancellationToken ct = default);

    Task AdicionarAgendamentoAsync(Agendamento agendamento, CancellationToken ct = default);
    Task<Agendamento?> ObterAgendamentoAsync(int agendamentoId, CancellationToken ct = default);
    Task<IReadOnlyList<Agendamento>> AgendamentosNoPeriodoAsync(DateTime inicio, DateTime fim, CancellationToken ct = default);
    Task RemoverAgendamentoAsync(int agendamentoId, CancellationToken ct = default);

    // ---- Equipe: profissionais e salas (fundação da recepção) ----

    /// <summary>Profissionais cadastrados (ativos e inativos), na ordem de exibição.</summary>
    Task<IReadOnlyList<Profissional>> ProfissionaisAsync(CancellationToken ct = default);

    Task<Profissional?> ObterProfissionalAsync(int profissionalId, CancellationToken ct = default);
    Task AdicionarProfissionalAsync(Profissional profissional, CancellationToken ct = default);

    /// <summary>Há agendamento (ou pedido na lista de espera) apontando para este profissional?</summary>
    Task<bool> ProfissionalEmUsoAsync(int profissionalId, CancellationToken ct = default);

    Task RemoverProfissionalAsync(int profissionalId, CancellationToken ct = default);

    /// <summary>Salas cadastradas (ativas e inativas), na ordem de exibição.</summary>
    Task<IReadOnlyList<Sala>> SalasAsync(CancellationToken ct = default);

    Task<Sala?> ObterSalaAsync(int salaId, CancellationToken ct = default);
    Task AdicionarSalaAsync(Sala sala, CancellationToken ct = default);

    /// <summary>Há agendamento marcado nesta sala?</summary>
    Task<bool> SalaEmUsoAsync(int salaId, CancellationToken ct = default);

    Task RemoverSalaAsync(int salaId, CancellationToken ct = default);

    // ---- Lista de espera ----

    Task AdicionarListaEsperaAsync(ListaEspera pedido, CancellationToken ct = default);
    Task<ListaEspera?> ObterListaEsperaAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>
    /// Pedidos da lista de espera, com paciente e profissional carregados.
    /// <paramref name="somenteAguardando"/> filtra os que ainda procuram horário.
    /// </summary>
    Task<IReadOnlyList<ListaEspera>> ListaEsperaAsync(
        bool somenteAguardando = true, CancellationToken ct = default);

    // ---- Auditoria ----

    /// <summary>Acrescenta um evento à trilha de auditoria (persistido junto com o SalvarAsync da ação).</summary>
    Task RegistrarAuditoriaAsync(EventoAuditoria evento, CancellationToken ct = default);

    /// <summary>Eventos de auditoria, do mais recente ao mais antigo (limitado).</summary>
    Task<IReadOnlyList<EventoAuditoria>> EventosAuditoriaAsync(int limite = 200, CancellationToken ct = default);

    // ---- Financeiro ----

    Task AdicionarLancamentoAsync(LancamentoFinanceiro lancamento, CancellationToken ct = default);
    Task<LancamentoFinanceiro?> ObterLancamentoAsync(int lancamentoId, CancellationToken ct = default);

    /// <summary>Lançamentos por data de competência, com categoria e paciente carregados.</summary>
    Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>
    /// Ids de guias que já possuem lançamento vinculado — base da conciliação
    /// (guia efetivada no convênio que ainda não virou dinheiro no caixa).
    /// </summary>
    Task<IReadOnlyList<int>> CodigosComLancamentoAsync(
        IReadOnlyCollection<int> codigoIds, CancellationToken ct = default);

    Task<IReadOnlyList<CategoriaFinanceira>> CategoriasFinanceirasAsync(CancellationToken ct = default);
    Task AdicionarCategoriaFinanceiraAsync(CategoriaFinanceira categoria, CancellationToken ct = default);

    Task<int> SalvarAsync(CancellationToken ct = default);
}
