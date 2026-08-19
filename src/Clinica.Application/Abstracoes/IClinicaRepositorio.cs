using Clinica.Application.Modelos;
using Clinica.Domain;
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

    /// <summary>
    /// Uma linha por paciente que já foi atendido: quando foi a última sessão e quantas
    /// foram no total, agrupadas NO BANCO.
    ///
    /// Existe para não repetir o que a leitura de retenção fazia: carregar toda a tabela
    /// de pacientes com toda a de atendimentos para, no fim, usar duas agregações por
    /// pessoa. Numa base de alguns milhares de pacientes isso é o banco inteiro na
    /// memória do cliente — e o banco é remoto.
    /// </summary>
    Task<IReadOnlyList<ResumoAtendimentosPaciente>> ResumoAtendimentosPorPacienteAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Dos pacientes informados, quais têm agendamento ainda de pé a partir de
    /// <paramref name="dia"/>. Uma consulta para o conjunto, não uma por pessoa.
    /// </summary>
    Task<IReadOnlyList<int>> PacientesComAgendamentoFuturoAsync(
        IReadOnlyCollection<int> pacienteIds, DateOnly dia, CancellationToken ct = default);

    /// <summary>
    /// Dos pacientes informados, quais já receberam contato do tipo indicado com
    /// referência igual ou posterior à data da última sessão de cada um.
    /// </summary>
    Task<IReadOnlyList<int>> PacientesJaContatadosAsync(
        IReadOnlyDictionary<int, DateOnly> desdeQuandoPorPaciente,
        TipoContato tipo, CancellationToken ct = default);

    /// <summary>Pacotes dos pacientes informados, numa consulta só.</summary>
    Task<IReadOnlyList<PacotePaciente>> PacotesDosPacientesAsync(
        IReadOnlyCollection<int> pacienteIds, CancellationToken ct = default);

    Task<CodigoFaturamento?> ObterCodigoAsync(int codigoId, CancellationToken ct = default);

    Task AdicionarAtendimentoAsync(Atendimento atendimento, CancellationToken ct = default);

    /// <summary>Atendimento com paciente e códigos carregados (para gerar a capa de faturamento).</summary>
    Task<Atendimento?> ObterAtendimentoAsync(int atendimentoId, CancellationToken ct = default);

    /// <summary>
    /// Atendimentos que CONSOMEM a autorização do convênio no período (parcela 70):
    /// sessão realizada, guia aberta ou baixada — a marcada para o futuro conta (é o
    /// alerta de cota chegando na marcação), a cancelada/falta não.
    /// </summary>
    Task<int> ContarAtendimentosAtivosDoPacienteAsync(
        int pacienteId, DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>
    /// Backfill do <c>Atendimento.RealizadoEm</c> na ATIVAÇÃO do regime "guia no
    /// agendamento" (parcela 70): tudo o que ainda não tem o carimbo é, por definição,
    /// sessão realizada — a chave nasce desligada e, desligada, não existe atendimento de
    /// sessão futura. Cobre as linhas que um app antigo gravou depois da migration.
    /// </summary>
    Task MarcarAtendimentosSemCarimboComoRealizadosAsync(CancellationToken ct = default);

    // ---- Busca / ficha do paciente / faturados ----

    /// <summary>
    /// Busca pacientes por nome ou CPF (termo normalizado). Termo vazio devolve todos.
    /// <paramref name="limite"/> corta o resultado no BANCO — os seletores da UI só mostram as
    /// primeiras linhas, e trazer a base inteira para descartar em memória é desperdício de rede
    /// (o banco é remoto). Null = sem corte, para quem precisa varrer todo mundo.
    /// </summary>
    Task<IReadOnlyList<Paciente>> BuscarPacientesAsync(string? termo, int? limite = null, CancellationToken ct = default);

    /// <summary>
    /// Origem e "indicado por" de TODA a base, projetados — três colunas, nunca a linha
    /// inteira: o relatório de origem não precisa da miniatura da foto nem do endereço, e
    /// arrastar a carteira completa para contar oito grupos seria o custo que a parcela 69
    /// tirou do "Meus pacientes".
    /// </summary>
    Task<IReadOnlyList<(int PacienteId, OrigemPaciente? Origem, string? IndicadoPor)>> OrigensDosPacientesAsync(
        CancellationToken ct = default);

    /// <summary>
    /// A data do PRIMEIRO atendimento de cada paciente que já tem algum — o fato que o
    /// relatório de origem usa como "estreia", porque <see cref="Paciente"/> não guarda
    /// data de cadastro. Agregado no SQL: uma linha por paciente, nunca a lista inteira.
    /// </summary>
    Task<IReadOnlyDictionary<int, DateOnly>> PrimeiroAtendimentoPorPacienteAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Pacientes que fazem aniversario no dia (ou na janela de dias a partir dele).
    /// A comparacao e por DIA E MES, nunca pela data inteira — o ano de nascimento nao
    /// tem nada a ver com a pergunta.
    /// </summary>
    Task<IReadOnlyList<Paciente>> AniversariantesAsync(
        DateOnly dia, int janelaDias = 0, CancellationToken ct = default);

    /// <summary>
    /// Historico de agendamentos de UM paciente — a base do padrao de falta (parcela 28).
    /// A agenda registra StatusAgendamento.Faltou desde a parcela 1, e os indicadores
    /// calculam a taxa da CLINICA; a do paciente nunca foi lida por ninguem.
    /// </summary>
    Task<IReadOnlyList<Agendamento>> AgendamentosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Os horários de UM paciente num dia (parcela 66). Existe separado do histórico
    /// completo acima porque a pergunta do balcão é sobre HOJE, e carregar quarenta
    /// sessões de um tratamento longo para responder sobre uma seria pagar a leitura
    /// inteira a cada check-in, num banco remoto.
    /// </summary>
    Task<IReadOnlyList<Agendamento>> AgendamentosDoPacienteNoDiaAsync(
        int pacienteId, DateOnly dia, CancellationToken ct = default);

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

    /// <summary>
    /// Pacientes cujo documento é este CPF, comparando só os DÍGITOS.
    ///
    /// A comparação ignora máscara dos dois lados porque a coluna aceita 30 caracteres e
    /// guarda o que foi digitado: desde que `PacienteService` normaliza, o que entra é só
    /// dígito — mas a base da clínica tem linhas anteriores a isso, com "123.456.789-00".
    /// Comparar o texto cru deixaria o duplicado mascarado passar, que é justamente o
    /// caso antigo que se quer pegar.
    /// </summary>
    Task<IReadOnlyList<Paciente>> PacientesPorCpfAsync(string cpfSoDigitos, CancellationToken ct = default);

    Task AdicionarPacienteAsync(Paciente paciente, CancellationToken ct = default);
    Task RemoverPacienteAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>
    /// O paciente tem algum REGISTRO CLÍNICO (evolução, avaliação, medida, documento,
    /// prescrição, problema)? É a pergunta que decide se a ficha pode ser removida:
    /// as FKs clínicas apagam em cascata, e prontuário está sob guarda legal de 20 anos
    /// (Lei 13.787/2018) — remover a linha do paciente levaria tudo junto, em silêncio.
    /// </summary>
    Task<bool> PacienteTemRegistroClinicoAsync(int pacienteId, CancellationToken ct = default);

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

    /// <summary>
    /// Os atendimentos do paciente num dia, com os códigos — a "capa" que o balcão
    /// confere antes de lançar de novo (parcela 70): número, modalidade, quem lançou e
    /// quais guias já foram baixadas. É o que transforma a pergunta "atendimento
    /// repetido?" de um número seco numa decisão informada.
    /// </summary>
    Task<IReadOnlyList<Atendimento>> AtendimentosDoPacienteNoDiaAsync(
        int pacienteId, DateOnly dia, CancellationToken ct = default);

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

    /// <summary>
    /// Consultas de um conjunto de pacientes, numa leitura só. Existe para a agenda poder
    /// avaliar a renovação de um dia inteiro sem varrer a base de pacientes — que é o que
    /// <see cref="PacientesComConsultasAsync"/> faz, e é caro num banco remoto para
    /// responder sobre as vinte pessoas marcadas hoje.
    /// </summary>
    Task<IReadOnlyList<Consulta>> ConsultasDosPacientesAsync(
        IReadOnlyCollection<int> pacienteIds, CancellationToken ct = default);

    Task AdicionarAgendamentoAsync(Agendamento agendamento, CancellationToken ct = default);
    Task<Agendamento?> ObterAgendamentoAsync(int agendamentoId, CancellationToken ct = default);
    Task<IReadOnlyList<Agendamento>> AgendamentosNoPeriodoAsync(DateTime inicio, DateTime fim, CancellationToken ct = default);

    /// <summary>
    /// Sessoes marcadas de uma vez (o pacote de dez), na ordem em que acontecem.
    /// RASTREADAS: cancelar a serie escreve nelas.
    /// </summary>
    Task<IReadOnlyList<Agendamento>> AgendamentosDaSerieAsync(string serieId, CancellationToken ct = default);
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

    // ---- Bloqueio de agenda (ferias, feriado, folga) ----

    /// <summary>
    /// Bloqueios que alcancam o intervalo informado. A consulta e feita a cada marcacao,
    /// entao ela corta pelo periodo no banco em vez de trazer o historico inteiro.
    /// </summary>
    Task<IReadOnlyList<BloqueioAgenda>> BloqueiosNoPeriodoAsync(
        DateTime inicio, DateTime fim, CancellationToken ct = default);

    /// <summary>Bloqueios cadastrados, do mais proximo para o mais distante.</summary>
    Task<IReadOnlyList<BloqueioAgenda>> BloqueiosAsync(
        DateTime? aPartirDe = null, CancellationToken ct = default);

    Task AdicionarBloqueioAsync(BloqueioAgenda bloqueio, CancellationToken ct = default);

    Task<BloqueioAgenda?> ObterBloqueioAsync(int bloqueioId, CancellationToken ct = default);

    Task RemoverBloqueioAsync(int bloqueioId, CancellationToken ct = default);

    // ---- Prontuário ----

    Task AdicionarEvolucaoAsync(Evolucao evolucao, CancellationToken ct = default);

    /// <summary>Evolução com profissional carregado (entidade rastreada, para editar).</summary>
    Task<Evolucao?> ObterEvolucaoAsync(int evolucaoId, CancellationToken ct = default);

    /// <summary>
    /// Prontuário do paciente, da sessão mais recente para a mais antiga. NÃO traz os
    /// anexos — abrir o prontuário não pode arrastar os arquivos junto.
    ///
    /// Sessão CANCELADA fica de fora (parcela 52).
    /// </summary>
    Task<IReadOnlyList<Evolucao>> EvolucoesDoPacienteAsync(
        int pacienteId, CancellationToken ct = default);

    /// <summary>
    /// O mesmo, podendo incluir as canceladas — é o que a guarda de 20 anos e a
    /// exportação do prontuário precisam ver.
    /// </summary>
    Task<IReadOnlyList<Evolucao>> EvolucoesDoPacienteAsync(
        int pacienteId, bool incluirCanceladas, CancellationToken ct = default);

    /// <summary>
    /// O par de EVA (primeira e última medida completa) de VÁRIOS pacientes, numa consulta.
    ///
    /// A carteira do consultório precisa de dois números por paciente e nada mais. Lê-los
    /// com <see cref="EvolucoesDoPacienteAsync(int, CancellationToken)"/> num laço custava
    /// uma ida ao banco REMOTO por paciente — até duzentas para desenhar uma tela — e cada
    /// uma arrastava o prontuário inteiro daquela pessoa (texto da evolução, conduta,
    /// orientações) para calcular dois inteiros.
    ///
    /// Só o par COMPLETO entra, como no resto do projeto: uma medida solta não diz se o
    /// tratamento funcionou e puxaria a leitura por falta de dado. Sessão cancelada fica
    /// de fora (parcela 52).
    /// </summary>
    Task<IReadOnlyDictionary<int, (int Inicial, int Ultima)>> ParesDeEvaDosPacientesAsync(
        IReadOnlyCollection<int> pacienteIds, CancellationToken ct = default);

    /// <summary>
    /// As versões anteriores de uma sessão, da mais antiga para a mais nova.
    ///
    /// É a leitura que torna a retificação RASTREÁVEL (Lei 13.787/2018, art. 3º).
    /// Guardar a versão e não ter por onde lê-la seria o defeito recorrente do projeto —
    /// dado gravado sem leitor — na variante mais cara, porque aqui o leitor é uma perícia.
    /// </summary>
    Task<IReadOnlyList<VersaoEvolucao>> VersoesDaEvolucaoAsync(
        int evolucaoId, CancellationToken ct = default);

    // NÃO existe RemoverEvolucaoAsync (parcela 52). Registro clínico não se apaga: a Lei
    // 13.787/2018 manda guardar por 20 anos a partir do último registro, e não há como
    // garantir isso mantendo um método que destrói a linha. Cancela-se com motivo, pelo
    // ProntuarioService.CancelarAsync — o mesmo padrão do documento clínico, da não
    // conformidade do faturamento e da checagem de enfermagem.
    //
    // A remoção destes quatro métodos é DE PROPÓSITO irreversível pela interface: enquanto
    // eles existirem, alguma tela futura vai chamá-los.

    /// <summary>
    /// Quantas correções cada sessão já teve, numa consulta só. É o que faz a lista do
    /// prontuário marcar "corrigida 2x" sem uma ida ao banco por linha.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> ContagemDeVersoesAsync(
        IReadOnlyCollection<int> evolucaoIds, CancellationToken ct = default);

    /// <summary>Anexo com a evolução carregada (entidade rastreada, para cancelar).</summary>
    Task<AnexoProntuario?> ObterAnexoAsync(int anexoId, CancellationToken ct = default);

    /// <summary>Anexos de uma evolução, por projeção — sem os bytes (corte no SQL).</summary>
    Task<IReadOnlyList<Modelos.AnexoResumo>> AnexosDaEvolucaoAsync(
        int evolucaoId, CancellationToken ct = default);

    /// <summary>Bytes de UM anexo. É a única consulta que materializa o arquivo.</summary>
    Task<byte[]?> ConteudoDoAnexoAsync(int anexoId, CancellationToken ct = default);

    Task AdicionarAnexoAsync(AnexoProntuario anexo, CancellationToken ct = default);

    /// <summary>
    /// Quantos anexos tem cada sessão de um prontuário, em UMA consulta (parcela 37).
    ///
    /// A lista de sessões precisa do número para desenhar o clipe, e perguntar sessão a
    /// sessão dá uma ida ao banco por linha — num prontuário de quarenta sessões, quarenta
    /// viagens a um banco remoto para desenhar quarenta números. Sessões sem anexo não
    /// aparecem no dicionário; quem lê trata a ausência como zero.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> ContagemDeAnexosAsync(
        IReadOnlyCollection<int> evolucaoIds, CancellationToken ct = default);

    // ---- Medidas clínicas seriadas (parcela 37) ----

    Task AdicionarMedidaAsync(MedidaClinica medida, CancellationToken ct = default);

    Task<MedidaClinica?> ObterMedidaAsync(int medidaId, CancellationToken ct = default);

    /// <summary>
    /// Série do paciente, da mais recente para a mais antiga; um tipo ou todos.
    /// <paramref name="incluirCanceladas"/> é para a EXPORTAÇÃO — a curva da tela nunca
    /// desenha cancelada, mas o prontuário sob guarda a contém.
    /// </summary>
    Task<IReadOnlyList<MedidaClinica>> MedidasDoPacienteAsync(
        int pacienteId, string? tipoCodigo = null, bool incluirCanceladas = false,
        CancellationToken ct = default);


    // ---- Lista de problemas (parcela 37) ----

    Task AdicionarProblemaAsync(ProblemaPaciente problema, CancellationToken ct = default);

    Task<ProblemaPaciente?> ObterProblemaAsync(int problemaId, CancellationToken ct = default);

    /// <summary>
    /// Lista de problemas do paciente. Os ativos primeiro, porque é o que se lê antes de
    /// atender; dentro de cada situação, o mais recente na frente.
    /// </summary>
    Task<IReadOnlyList<ProblemaPaciente>> ProblemasDoPacienteAsync(
        int pacienteId, bool somenteAtivos = false, CancellationToken ct = default);

    // ---- Avaliações clínicas por instrumento (parcela 36) ----

    Task AdicionarAvaliacaoAsync(AvaliacaoClinica avaliacao, CancellationToken ct = default);

    /// <summary>Avaliação COM as respostas carregadas — a leitura de uma aplicação inteira.</summary>
    Task<AvaliacaoClinica?> ObterAvaliacaoAsync(int avaliacaoId, CancellationToken ct = default);

    /// <summary>
    /// Avaliações do paciente, da mais recente para a mais antiga, SEM as respostas.
    ///
    /// O corte é o mesmo dos anexos do prontuário: a lista precisa de escore, faixa e
    /// data, e trazer junto as nove a dez respostas de cada aplicação multiplicaria por
    /// dez o que passa pela rede para desenhar uma tabela que não as mostra.
    /// </summary>
    /// <param name="incluirCanceladas">
    /// A EXPORTAÇÃO passa true: a cancelada fica fora da curva da tela, mas faz parte do
    /// prontuário sob guarda — sem ela, o LEIA-ME da exportação prometia o que o arquivo
    /// não continha.
    /// </param>
    Task<IReadOnlyList<AvaliacaoClinica>> AvaliacoesDoPacienteAsync(
        int pacienteId, string? instrumentoCodigo = null, bool incluirCanceladas = false,
        CancellationToken ct = default);


    /// <summary>
    /// Pacientes que este profissional atende, do que veio por último para o mais antigo.
    ///
    /// A contagem e a última visita saem do SQL (agrupamento), não de materializar os
    /// agendamentos: um profissional com dois anos de casa tem milhares deles, e a tela
    /// mostra uma linha por paciente.
    /// </summary>
    Task<IReadOnlyList<Modelos.PacienteDoProfissional>> PacientesDoProfissionalAsync(
        int profissionalId, int limite = 200, CancellationToken ct = default);

    // ---- Consentimento LGPD ----

    /// <summary>Todos os registros de consentimento do paciente, do mais recente ao mais antigo.</summary>
    Task<IReadOnlyList<ConsentimentoLgpd>> ConsentimentosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default);

    Task<ConsentimentoLgpd?> ObterConsentimentoAsync(int consentimentoId, CancellationToken ct = default);
    Task AdicionarConsentimentoAsync(ConsentimentoLgpd consentimento, CancellationToken ct = default);

    // ---- Ato clínico: mapa corporal e protocolos ----

    /// <summary>Mapa da sessão, com os pontos carregados e RASTREADO (a edição substitui os pontos).</summary>
    Task<MapaCorporal?> ObterMapaDaEvolucaoAsync(int evolucaoId, CancellationToken ct = default);

    Task AdicionarMapaAsync(MapaCorporal mapa, CancellationToken ct = default);

    /// <summary>Apaga os pontos de um mapa. Editar o mapa é regravar o conjunto inteiro.</summary>
    Task RemoverPontosDoMapaAsync(int mapaId, CancellationToken ct = default);

    /// <summary>
    /// Protocolos disponíveis para um paciente: os DA CLÍNICA (sem dono) mais os dele.
    /// <paramref name="pacienteId"/> nulo traz só os da clínica.
    /// </summary>
    Task<IReadOnlyList<ProtocoloCorporal>> ProtocolosCorporaisAsync(
        int? pacienteId, bool somenteAtivos = true, CancellationToken ct = default);

    /// <summary>Protocolo com os pontos carregados.</summary>
    Task<ProtocoloCorporal?> ObterProtocoloCorporalAsync(int protocoloId, CancellationToken ct = default);

    Task AdicionarProtocoloCorporalAsync(ProtocoloCorporal protocolo, CancellationToken ct = default);
    Task RemoverProtocoloCorporalAsync(int protocoloId, CancellationToken ct = default);

    // ---- Documentos clínicos (receita, atestado, relatório…) ----

    Task AdicionarDocumentoAsync(DocumentoClinico documento, CancellationToken ct = default);

    /// <summary>Documento com itens, paciente e profissional carregados.</summary>
    Task<DocumentoClinico?> ObterDocumentoAsync(int documentoId, CancellationToken ct = default);

    /// <summary>Documento pelo código impresso no rodapé — a conferência da via em papel.</summary>
    Task<DocumentoClinico?> ObterDocumentoPorCodigoAsync(string codigo, CancellationToken ct = default);

    /// <summary>Documentos do paciente, do mais recente para o mais antigo (sem os itens).</summary>
    /// <summary>
    /// Documentos com link no ar cuja janela de publicação já venceu (parcela 53). É o que
    /// a expiração varre para tirá-los do ar.
    /// </summary>
    Task<IReadOnlyList<DocumentoClinico>> DocumentosPublicadosVencidosAsync(
        DateOnly hoje, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentoClinico>> DocumentosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Documentos clínicos emitidos no período (parcela 24), com paciente e profissional
    /// carregados. Cancelado ENTRA na lista, marcado: documento não se apaga neste sistema,
    /// e esconder o cancelado faria a lista mentir sobre o que o paciente levou para casa.
    ///
    /// Faltava para responder "que papéis saíram este mês?" — até aqui só dava para
    /// perguntar paciente por paciente, e ninguém sabe de antemão qual paciente procurar.
    /// </summary>
    Task<IReadOnlyList<DocumentoClinico>> DocumentosClinicosNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, TipoDocumentoClinico? tipo = null,
        int? pacienteId = null, CancellationToken ct = default);

    /// <summary>Próximo sequencial do ano para numerar o documento (<c>2026/0001</c>).</summary>
    Task<int> ProximoNumeroDocumentoAsync(int ano, CancellationToken ct = default);

    // ---- Prescrição de execução interna e checagem de enfermagem (parcela 42) ----

    Task AdicionarPrescricaoInternaAsync(
        PrescricaoInterna prescricao, CancellationToken ct = default);

    /// <summary>
    /// A prescrição INTEIRA e rastreada: itens, checagens de cada item, assinaturas,
    /// paciente e profissional. É a carga da tela de execução, e ela precisa do grafo
    /// todo — a situação de cada item é derivada das checagens, então uma prescrição
    /// carregada sem elas apareceria com tudo pendente.
    /// </summary>
    Task<PrescricaoInterna?> ObterPrescricaoInternaAsync(
        int prescricaoId, CancellationToken ct = default);

    /// <summary>Pela via impressa: o código curto do rodapé.</summary>
    Task<PrescricaoInterna?> ObterPrescricaoInternaPorCodigoAsync(
        string codigo, CancellationToken ct = default);

    /// <summary>
    /// Prescrições do paciente, da mais recente para a mais antiga, COM os itens e as
    /// checagens (a lista mostra "3 de 5 realizados", que é derivado delas).
    /// </summary>
    Task<IReadOnlyList<PrescricaoInterna>> PrescricoesInternasDoPacienteAsync(
        int pacienteId, int limite = 50, CancellationToken ct = default);

    /// <summary>
    /// A fila da sala de infusão: as prescrições do dia, com o grafo carregado.
    ///
    /// Só as ASSINADAS por padrão — rascunho não se executa, e mostrá-lo na sala
    /// convidaria a técnica a administrar sobre uma folha que ninguém assinou. Encerrada
    /// entra quando <paramref name="incluirEncerradas"/>, para a conferência do fim do dia.
    /// </summary>
    Task<IReadOnlyList<PrescricaoInterna>> PrescricoesInternasDoDiaAsync(
        DateOnly data, int? profissionalId = null, bool incluirEncerradas = false,
        CancellationToken ct = default);

    /// <summary>
    /// As folhas ENCERRADAS que ainda devem a assinatura eletrônica da enfermagem —
    /// <b>de qualquer dia</b>.
    ///
    /// Existe porque a folha só fica assinável DEPOIS de encerrar, e encerrar é
    /// exatamente o que a tirava da fila da sala: ela sumia da lista padrão (encerradas
    /// nascem escondidas) e, no dia seguinte, sumia de vez (a fila é travada em
    /// <c>p.Data == hoje</c>). Quem recusasse assinar na hora só reencontrava a folha
    /// digitando o código impresso no papel.
    ///
    /// Sem filtro de data de propósito: a pendência não vence à meia-noite, e uma lista
    /// que a esconde no dia seguinte é o "alerta sem porta" na pior variante — o que a
    /// pessoa precisa reencontrar é justamente o que a lista some.
    /// </summary>
    Task<IReadOnlyList<PrescricaoInterna>> PrescricoesInternasAguardandoAssinaturaAsync(
        int? profissionalId = null, CancellationToken ct = default);

    /// <summary>Item rastreado, com a prescrição e as checagens — o alvo de checar/retificar.</summary>
    Task<ItemPrescricaoInterna?> ObterItemPrescricaoInternaAsync(
        int itemId, CancellationToken ct = default);

    Task AdicionarChecagemPrescricaoAsync(
        ChecagemPrescricao checagem, CancellationToken ct = default);

    /// <summary>Próximo sequencial do ano para numerar a prescrição (<c>PRE 2026/0001</c>).</summary>
    Task<int> ProximoNumeroPrescricaoInternaAsync(int ano, CancellationToken ct = default);

    /// <summary>
    /// Os bytes do PDF assinado, carregados SOB DEMANDA — a listagem nunca os traz, pela
    /// mesma razão da foto do paciente (a tabela existe separada para isso).
    /// </summary>
    Task<ArquivoAssinado?> ObterArquivoAssinadoAsync(
        int arquivoId, CancellationToken ct = default);

    /// <summary>Guarda o PDF assinado. Não persiste — chame <c>SalvarAsync</c>.</summary>
    Task AdicionarArquivoAssinadoAsync(
        ArquivoAssinado arquivo, CancellationToken ct = default);

    /// <summary>
    /// Quantas prescrições internas do dia ainda têm item sem checagem, por prescrição.
    /// Alimenta o contador da sala sem materializar o grafo de todas elas.
    /// </summary>
    Task<int> PrescricoesInternasPendentesAsync(
        DateOnly data, int? profissionalId = null, CancellationToken ct = default);

    /// <summary>
    /// Modelos de evolução visíveis para um profissional (parcela 63): os DELE mais os da
    /// clínica. Sem profissional informado, só os da clínica.
    ///
    /// O modelo de evolução é a única coisa "de prontuário" que se APAGA mesmo, e isso é
    /// decisão: ele não registra o que aconteceu com ninguém — é rascunho de apoio, e
    /// aplicar COPIA, então nenhuma sessão escrita com ele muda quando ele some.
    /// </summary>
    Task<IReadOnlyList<ModeloEvolucao>> ModelosEvolucaoAsync(
        int? profissionalId = null, CancellationToken ct = default);

    Task<ModeloEvolucao?> ObterModeloEvolucaoAsync(int modeloId, CancellationToken ct = default);

    /// <summary>Pelo par dono+nome — salvar com nome repetido SOBRESCREVE em vez de duplicar.</summary>
    Task<ModeloEvolucao?> ObterModeloEvolucaoPorNomeAsync(
        int? profissionalId, string nome, CancellationToken ct = default);

    Task AdicionarModeloEvolucaoAsync(ModeloEvolucao modelo, CancellationToken ct = default);
    Task RemoverModeloEvolucaoAsync(int modeloId, CancellationToken ct = default);

    /// <summary>Modelos de documento, opcionalmente de um tipo só.</summary>
    Task<IReadOnlyList<ModeloDocumento>> ModelosDocumentoAsync(
        TipoDocumentoClinico? tipo = null, CancellationToken ct = default);

    /// <summary>Modelo com os itens carregados e rastreado.</summary>
    Task<ModeloDocumento?> ObterModeloDocumentoAsync(int modeloId, CancellationToken ct = default);

    /// <summary>Modelo pelo par tipo+nome — salvar com nome repetido SOBRESCREVE em vez de duplicar.</summary>
    Task<ModeloDocumento?> ObterModeloDocumentoPorNomeAsync(
        TipoDocumentoClinico tipo, string nome, CancellationToken ct = default);

    Task AdicionarModeloDocumentoAsync(ModeloDocumento modelo, CancellationToken ct = default);
    Task RemoverModeloDocumentoAsync(int modeloId, CancellationToken ct = default);

    /// <summary>Apaga os itens de um modelo (regravados por inteiro a cada salvamento).</summary>
    Task RemoverItensDoModeloAsync(int modeloId, CancellationToken ct = default);

    // ---- Termo assinado pelo paciente (parcela 66) ----

    /// <summary>
    /// Exigências de termo por modalidade, com o modelo carregado. Traz as INATIVAS
    /// também: quem lê para configurar precisa vê-las, e quem lê para exigir filtra.
    /// </summary>
    Task<IReadOnlyList<ExigenciaTermoProcedimento>> ExigenciasTermoAsync(
        CancellationToken ct = default);

    Task<ExigenciaTermoProcedimento?> ObterExigenciaTermoAsync(
        int exigenciaId, CancellationToken ct = default);

    Task AdicionarExigenciaTermoAsync(
        ExigenciaTermoProcedimento exigencia, CancellationToken ct = default);

    /// <summary>
    /// Termos de procedimento do paciente numa data, com os itens carregados.
    ///
    /// A data é a chave porque a validade é POR SESSÃO (decisão da clínica, ago/2026): o
    /// termo assinado ontem não vale para hoje, e o de hoje vale para a sessão de hoje.
    /// </summary>
    Task<IReadOnlyList<DocumentoClinico>> TermosDoPacienteNaDataAsync(
        int pacienteId, DateOnly data, CancellationToken ct = default);

    /// <summary>
    /// Todos os termos de procedimento emitidos numa data, com os itens.
    ///
    /// Em lote de propósito: a fila do balcão pergunta por trinta pacientes de uma vez, e
    /// uma consulta por cartão transformaria a abertura do quadro em sessenta idas a um
    /// banco remoto — o mesmo argumento do consentimento em lote das campanhas.
    /// </summary>
    Task<IReadOnlyList<DocumentoClinico>> TermosDaDataAsync(
        DateOnly data, CancellationToken ct = default);

    /// <summary>
    /// Todos os termos de procedimento de um paciente, de QUALQUER data, com os itens.
    ///
    /// Existe porque a validade deixou de ser sempre "por sessão" (parcela 66, 3ª rodada):
    /// o termo sem prazo é assinado quando o paciente aparece — na consulta em que ele vem
    /// tirar dúvidas, semanas antes — e continua valendo no dia do procedimento.
    /// </summary>
    Task<IReadOnlyList<DocumentoClinico>> TermosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Termos de procedimento de VÁRIOS pacientes, de qualquer data, com os itens.
    ///
    /// Em lote pela razão da leitura do dia: a fila pergunta por trinta cartões de uma vez,
    /// e uma consulta por paciente transformaria a abertura do quadro em trinta idas a um
    /// banco remoto.
    /// </summary>
    Task<IReadOnlyList<DocumentoClinico>> TermosDosPacientesAsync(
        IReadOnlyCollection<int> pacienteIds, CancellationToken ct = default);

    /// <summary>O traço da assinatura — a única leitura que traz a imagem do banco.</summary>
    Task<TracoAssinatura?> ObterTracoAssinaturaAsync(int tracoId, CancellationToken ct = default);
    /// <summary>
    /// Quais destes pacientes têm consentimento VIGENTE para a finalidade (o registro
    /// mais recente concedeu e não foi revogado). Em lote de propósito: as campanhas
    /// perguntam por dezenas de pacientes de uma vez, e uma consulta por paciente
    /// transformaria a geração da rodada em dezenas de idas ao banco remoto.
    /// </summary>
    Task<IReadOnlyList<int>> PacientesComConsentimentoVigenteAsync(
        FinalidadeConsentimento finalidade,
        IReadOnlyCollection<int> pacienteIds,
        CancellationToken ct = default);

    // ---- Pacotes, planos e vouchers ----

    /// <summary>Catálogo de pacotes à venda.</summary>
    Task<IReadOnlyList<PacoteCatalogo>> PacotesCatalogoAsync(
        bool somenteAtivos = true, CancellationToken ct = default);

    Task<PacoteCatalogo?> ObterPacoteCatalogoAsync(int catalogoId, CancellationToken ct = default);
    Task AdicionarPacoteCatalogoAsync(PacoteCatalogo pacote, CancellationToken ct = default);
    Task RemoverPacoteCatalogoAsync(int catalogoId, CancellationToken ct = default);

    /// <summary>Pacotes vendidos ao paciente, com os consumos carregados.</summary>
    Task<IReadOnlyList<PacotePaciente>> PacotesDoPacienteAsync(
        int pacienteId, CancellationToken ct = default);

    /// <summary>Pacote com consumos e paciente carregados (rastreado, para consumir).</summary>
    Task<PacotePaciente?> ObterPacotePacienteAsync(int pacoteId, CancellationToken ct = default);

    Task AdicionarPacotePacienteAsync(PacotePaciente pacote, CancellationToken ct = default);

    /// <summary>Todos os pacotes vendidos, com consumos — base dos totais do módulo.</summary>
    Task<IReadOnlyList<PacotePaciente>> PacotesVendidosAsync(CancellationToken ct = default);

    /// <summary>Consumo já registrado para este atendimento? Evita debitar duas vezes.</summary>
    Task<bool> AtendimentoJaConsumiuPacoteAsync(int atendimentoId, CancellationToken ct = default);

    Task<ConsumoPacote?> ObterConsumoPacoteAsync(int consumoId, CancellationToken ct = default);

    // ---- Repasse por profissional ----

    Task<IReadOnlyList<RegraRepasse>> RegrasRepasseAsync(
        int? profissionalId = null, CancellationToken ct = default);

    Task<RegraRepasse?> ObterRegraRepasseAsync(int regraId, CancellationToken ct = default);
    Task AdicionarRegraRepasseAsync(RegraRepasse regra, CancellationToken ct = default);
    Task RemoverRegraRepasseAsync(int regraId, CancellationToken ct = default);

    /// <summary>Apurações de repasse, opcionalmente de um profissional.</summary>
    Task<IReadOnlyList<RepasseApurado>> RepassesApuradosAsync(
        int? profissionalId = null, CancellationToken ct = default);

    Task<RepasseApurado?> ObterRepasseApuradoAsync(int repasseId, CancellationToken ct = default);

    // ---- Taxas de cartao e imposto (parcela 9) ----

    /// <summary>Catalogo de taxas da maquininha.</summary>
    Task<IReadOnlyList<TaxaCartao>> TaxasCartaoAsync(
        bool somenteAtivas = false, CancellationToken ct = default);

    /// <summary>Taxa rastreada, para a tela poder edita-la.</summary>
    Task<TaxaCartao?> ObterTaxaCartaoAsync(int taxaId, CancellationToken ct = default);

    Task AdicionarTaxaCartaoAsync(TaxaCartao taxa, CancellationToken ct = default);

    Task RemoverTaxaCartaoAsync(int taxaId, CancellationToken ct = default);

    // ---- Rentabilidade por convenio (parcela 19) ----

    /// <summary>
    /// Lancamentos vinculados as guias informadas (nao cancelados). E o encontro dos dois
    /// modulos por convenio: o faturamento sabe quantas guias sairam, o financeiro sabe
    /// quanto entrou, e ate a parcela 19 os dois numeros nunca se cruzavam.
    /// </summary>
    Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosDeCodigosAsync(
        IReadOnlyCollection<int> codigoIds, CancellationToken ct = default);

    // ---- Custo de transacao, visao da direcao (parcela 17) ----

    /// <summary>
    /// Entradas REALIZADAS do periodo com o que foi descontado delas. So realizada: taxa
    /// de recebimento que ainda nao aconteceu e desconto de receita que nao existe.
    /// </summary>
    Task<IReadOnlyList<Modelos.RecebimentoComDeducao>> RecebimentosComDeducaoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    // ---- Recebiveis de cartao (parcela 16) ----

    /// <summary>
    /// Recebimentos de cartao ainda nao creditados, com previsao ate a data. A previsao
    /// era gravada desde a parcela 9 e nenhuma tela a lia — dado gravado sem leitor passa
    /// no CI e nao faz nada na clinica.
    /// </summary>
    Task<IReadOnlyList<LancamentoFinanceiro>> RecebiveisEmAbertoAsync(
        DateOnly ate, CancellationToken ct = default);

    /// <summary>Lancamentos por id, RASTREADOS — a confirmacao do deposito escreve neles.</summary>
    Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosPorIdAsync(
        IReadOnlyCollection<int> ids, CancellationToken ct = default);

    /// <summary>
    /// Recebimentos de cartao JA creditados, pela data real do credito — o que a tela
    /// precisa para desfazer uma confirmacao lancada no dia errado.
    ///
    /// O corte e pela data do CREDITO e nao pela previsao: quem procura o deposito
    /// confirmado esta com o extrato na mao, e o extrato traz o dia em que o dinheiro
    /// caiu.
    /// </summary>
    Task<IReadOnlyList<LancamentoFinanceiro>> RecebiveisConfirmadosAsync(
        DateOnly de, DateOnly ate, CancellationToken ct = default);

    // ---- Tabela de preco por convenio (parcela 20) ----

    /// <summary>
    /// Tabela de preco por convenio: quanto cada operadora paga por tipo de guia. Cadastrada
    /// no Gerente (quem negocia tabela e a direcao) e lida pela conciliacao do Financeiro
    /// (quem concilia guia e o balcao) — mesmo banco, sem sincronizacao.
    /// </summary>
    Task<IReadOnlyList<PrecoConvenio>> PrecosConvenioAsync(
        bool somenteAtivos = false, CancellationToken ct = default);

    /// <summary>Preco rastreado, para a tela poder edita-lo.</summary>
    Task<PrecoConvenio?> ObterPrecoConvenioAsync(int precoId, CancellationToken ct = default);

    Task AdicionarPrecoConvenioAsync(PrecoConvenio preco, CancellationToken ct = default);

    Task RemoverPrecoConvenioAsync(int precoId, CancellationToken ct = default);

    // ---- Metas da direcao (parcela 28) ----

    /// <summary>
    /// Metas do ano, com o profissional carregado (a meta pode ser da clinica ou de
    /// alguem). O recorte e o ANO porque a tela mostra o ano inteiro: a direção define
    /// doze meses de uma vez e acompanha mes a mes.
    /// </summary>
    Task<IReadOnlyList<MetaMensal>> MetasDoAnoAsync(int ano, CancellationToken ct = default);

    /// <summary>A meta de um mes/indicador/dono, se existir. Nulo = a direcao nao definiu.</summary>
    Task<MetaMensal?> ObterMetaAsync(
        int ano, int mes, IndicadorMeta indicador, int? profissionalId,
        CancellationToken ct = default);

    Task<MetaMensal?> ObterMetaPorIdAsync(int metaId, CancellationToken ct = default);

    Task AdicionarMetaAsync(MetaMensal meta, CancellationToken ct = default);

    Task RemoverMetaAsync(int metaId, CancellationToken ct = default);

    // ---- Teto de gasto por categoria (parcela 31) ----

    /// <summary>Tetos do mes, com a categoria carregada.</summary>
    Task<IReadOnlyList<OrcamentoCategoria>> OrcamentosDoMesAsync(
        int ano, int mes, CancellationToken ct = default);

    Task<OrcamentoCategoria?> ObterOrcamentoAsync(
        int ano, int mes, int categoriaId, CancellationToken ct = default);

    Task<OrcamentoCategoria?> ObterOrcamentoPorIdAsync(int orcamentoId, CancellationToken ct = default);

    Task AdicionarOrcamentoAsync(OrcamentoCategoria orcamento, CancellationToken ct = default);

    Task RemoverOrcamentoAsync(int orcamentoId, CancellationToken ct = default);

    // ---- Regime tributario (parcela 15) ----

    /// <summary>Catalogo de tributos (ISS, PIS, COFINS, IRPJ, CSLL, Simples).</summary>
    Task<IReadOnlyList<Tributo>> TributosAsync(
        bool somenteAtivos = false, CancellationToken ct = default);

    /// <summary>Tributo rastreado, para a tela poder edita-lo.</summary>
    Task<Tributo?> ObterTributoAsync(int tributoId, CancellationToken ct = default);

    Task AdicionarTributoAsync(Tributo tributo, CancellationToken ct = default);

    Task RemoverTributoAsync(int tributoId, CancellationToken ct = default);
    Task AdicionarRepasseApuradoAsync(RepasseApurado repasse, CancellationToken ct = default);

    /// <summary>
    /// Agendamentos realizados no período que geraram atendimento, com profissional.
    /// É a ponte que permite saber QUEM atendeu: o atendimento não guarda profissional,
    /// o agendamento sim.
    /// </summary>
    Task<IReadOnlyList<Agendamento>> AgendamentosComAtendimentoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Entradas realizadas ligadas a uma lista de atendimentos (base do repasse).</summary>
    Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosDosAtendimentosAsync(
        IReadOnlyCollection<int> atendimentoIds, CancellationToken ct = default);

    // ---- Estoque ----

    Task<IReadOnlyList<ItemEstoque>> ItensEstoqueAsync(
        bool somenteAtivos = false, CancellationToken ct = default);

    Task<ItemEstoque?> ObterItemEstoqueAsync(int itemId, CancellationToken ct = default);
    Task AdicionarItemEstoqueAsync(ItemEstoque item, CancellationToken ct = default);
    Task RemoverItemEstoqueAsync(int itemId, CancellationToken ct = default);

    /// <summary>Movimentos de um item, do mais recente ao mais antigo.</summary>
    Task<IReadOnlyList<MovimentoEstoque>> MovimentosDoItemAsync(
        int itemId, CancellationToken ct = default);

    /// <summary>Movimentos do período (todos os itens), para o custo e o extrato.</summary>
    Task<IReadOnlyList<MovimentoEstoque>> MovimentosNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Movimentos de saída ligados a um atendimento — o custo daquela sessão.</summary>
    Task<IReadOnlyList<MovimentoEstoque>> MovimentosDoAtendimentoAsync(
        int atendimentoId, CancellationToken ct = default);

    /// <summary>
    /// Saídas do período que TÊM atendimento — o custo de insumo sessão a sessão.
    ///
    /// Filtra no banco em vez de trazer o extrato inteiro para peneirar em memória: a
    /// maior parte dos movimentos é entrada de compra, e nenhuma delas interessa aqui.
    /// </summary>
    Task<IReadOnlyList<MovimentoEstoque>> ConsumosDeSessaoNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>
    /// Saídas da última sessão que baixou insumo — a sugestão de consumo do fechamento
    /// da Recepção. Vazia quando a clínica nunca baixou insumo por atendimento.
    ///
    /// A clínica não cadastra "kit da sessão", e inventar um cadastro novo para isso
    /// seria pedir manutenção de uma lista que ninguém mantém. O que a última sessão
    /// gastou é a melhor sugestão disponível sem inventar dado: se estiver errada, a
    /// recepção corrige na tela antes de gravar.
    /// </summary>
    Task<IReadOnlyList<MovimentoEstoque>> UltimoConsumoDeSessaoAsync(CancellationToken ct = default);

    Task AdicionarMovimentoEstoqueAsync(MovimentoEstoque movimento, CancellationToken ct = default);

    /// <summary>
    /// Saldo por item (id → quantidade). A projeção corta as colunas no SQL, mas a soma
    /// é feita em memória: o SQLite não traduz <c>Sum</c> sobre <c>decimal</c>.
    /// </summary>
    Task<IReadOnlyDictionary<int, decimal>> SaldosEstoqueAsync(CancellationToken ct = default);

    // ---- Recibo e orçamento ----

    Task AdicionarDocumentoFinanceiroAsync(DocumentoFinanceiro documento, CancellationToken ct = default);

    Task<DocumentoFinanceiro?> ObterDocumentoFinanceiroAsync(
        int documentoId, CancellationToken ct = default);

    /// <summary>Documentos financeiros do período, do mais recente ao mais antigo.</summary>
    Task<IReadOnlyList<DocumentoFinanceiro>> DocumentosFinanceirosAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Próximo sequencial do ano para o tipo (recibo e orçamento não se misturam).</summary>
    Task<int> ProximoNumeroDocumentoFinanceiroAsync(
        int ano, TipoDocumentoFinanceiro tipo, CancellationToken ct = default);

    // ---- Auditoria ----

    /// <summary>Acrescenta um evento à trilha de auditoria (persistido junto com o SalvarAsync da ação).</summary>
    Task RegistrarAuditoriaAsync(EventoAuditoria evento, CancellationToken ct = default);

    /// <summary>Eventos de auditoria, do mais recente ao mais antigo (limitado).</summary>
    Task<IReadOnlyList<EventoAuditoria>> EventosAuditoriaAsync(int limite = 200, CancellationToken ct = default);

    /// <summary>
    /// Trilha filtrada (parcela 21). O filtro vai todo para o SQL: esta e a tabela que mais
    /// cresce no sistema — toda acao de dinheiro escreve nela — e materializa-la para
    /// filtrar em memoria e o que a convencao do projeto proibe.
    /// </summary>
    Task<IReadOnlyList<EventoAuditoria>> ConsultarAuditoriaAsync(
        Modelos.FiltroAuditoria filtro, CancellationToken ct = default);

    /// <summary>
    /// As acoes distintas ja registradas, para o filtro oferecer a lista em vez de exigir
    /// que a pessoa saiba escrever "ContasRecorrentesGeradas" de cabeca.
    /// </summary>
    Task<IReadOnlyList<string>> AcoesDeAuditoriaAsync(CancellationToken ct = default);

    // ---- Financeiro ----

    Task AdicionarLancamentoAsync(LancamentoFinanceiro lancamento, CancellationToken ct = default);
    Task<LancamentoFinanceiro?> ObterLancamentoAsync(int lancamentoId, CancellationToken ct = default);

    /// <summary>
    /// Lançamentos por data de competência, com categoria e paciente carregados.
    /// <paramref name="limite"/> corta no SQL (nunca <c>Take()</c> depois de
    /// materializar); null = sem corte.
    /// </summary>
    Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, int? limite = null, CancellationToken ct = default);

    /// <summary>
    /// Última entrada REALIZADA do paciente. É de onde o fechamento da sessão tira o
    /// valor sugerido: a clínica não cadastra tabela de preço avulso, e o que este
    /// paciente pagou da última vez é um palpite com procedência — que a tela mostra
    /// junto ("igual à sessão de 15/07"), para ninguém confirmar um número sem saber de
    /// onde veio. Null quando ele nunca pagou nada.
    /// </summary>
    Task<LancamentoFinanceiro?> UltimoRecebimentoDoPacienteAsync(
        int pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Só (tipo, situação, valor) dos lançamentos do período — a projeção que os totais
    /// do caixa precisam. Existe para o resumo não repetir a carga completa da lista.
    /// </summary>
    Task<IReadOnlyList<Modelos.ValorLancamento>> ValoresDeLancamentoNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>
    /// Ids de guias que já possuem lançamento vinculado — base da conciliação
    /// (guia efetivada no convênio que ainda não virou dinheiro no caixa).
    /// </summary>
    Task<IReadOnlyList<int>> CodigosComLancamentoAsync(
        IReadOnlyCollection<int> codigoIds, CancellationToken ct = default);

    /// <summary>
    /// Guias de UM paciente com glosa ainda em aberto (parcela 27). O recorte é por
    /// paciente e resolvido no SQL: quem pergunta é o balcão com uma pessoa na frente, e
    /// trazer as glosas da clínica inteira para filtrar em memória é a dívida que a busca
    /// de pacientes já tinha resolvido uma vez.
    /// </summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosGlosadosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Lançamentos do período com as três datas e o NOME da categoria — a projeção que o
    /// fluxo de caixa precisa (parcela 13). O período é comparado contra a competência OU
    /// o vencimento OU o pagamento: um lançamento com competência em julho e vencimento
    /// em agosto pertence ao fluxo dos dois meses, e filtrar por uma data só o faria
    /// sumir de um deles.
    /// </summary>
    Task<IReadOnlyList<Modelos.LancamentoDatado>> LancamentosDatadosNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    // ---- Fechamento de caixa (parcela 14) ----

    /// <summary>
    /// Movimentos em ESPÉCIE já realizados no dia. Só dinheiro vivo: cartão e PIX não
    /// passam pela gaveta, e incluí-los faria a conferência nunca bater.
    /// </summary>
    Task<IReadOnlyList<Modelos.LancamentoEspecie>> LancamentosEmEspecieDoDiaAsync(
        DateOnly dia, CancellationToken ct = default);

    /// <summary>O mesmo, no período — base dos dias que ninguém conferiu.</summary>
    Task<IReadOnlyList<Modelos.LancamentoEspecie>> LancamentosEmEspecieNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>
    /// O fechamento VIGENTE do dia — o último gravado, porque reabrir para recontagem
    /// guarda o anterior e grava outro por cima. Rastreado, para a reabertura poder
    /// marcá-lo.
    /// </summary>
    Task<FechamentoCaixa?> FechamentoCaixaDoDiaAsync(DateOnly dia, CancellationToken ct = default);

    Task AdicionarFechamentoCaixaAsync(FechamentoCaixa fechamento, CancellationToken ct = default);

    /// <summary>Fechamentos do período, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<FechamentoCaixa>> FechamentosCaixaNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    // ---- Contas a pagar e a receber (parcela 12) ----

    /// <summary>
    /// Contas em ABERTO (previstas) com vencimento até a data, da mais antiga para a mais
    /// nova — a ordem em que se paga. Categoria e paciente vêm carregados, porque a lista
    /// mostra os dois. Filtra por tipo quando a tela quer só o que se paga ou só o que se
    /// recebe.
    /// </summary>
    Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosComVencimentoAteAsync(
        DateOnly ate, TipoLancamento? tipo = null, CancellationToken ct = default);

    /// <summary>
    /// Quais das origens de recorrência informadas já existem. É a checagem de
    /// idempotência da geração: uma consulta em bloco em vez de uma por ocorrência.
    /// </summary>
    Task<IReadOnlyList<string>> OrigensRecorrenciaExistentesAsync(
        IReadOnlyCollection<string> origens, CancellationToken ct = default);

    Task<IReadOnlyList<LancamentoRecorrente>> RecorrentesAsync(
        bool somenteAtivas = false, CancellationToken ct = default);
    Task AdicionarRecorrenteAsync(LancamentoRecorrente recorrente, CancellationToken ct = default);

    /// <summary>Recorrência rastreada, para a tela poder editá-la.</summary>
    Task<LancamentoRecorrente?> ObterRecorrenteAsync(int recorrenteId, CancellationToken ct = default);

    Task<IReadOnlyList<CategoriaFinanceira>> CategoriasFinanceirasAsync(CancellationToken ct = default);
    Task AdicionarCategoriaFinanceiraAsync(CategoriaFinanceira categoria, CancellationToken ct = default);

    /// <summary>Categoria rastreada, para a tela do plano de contas poder editá-la.</summary>
    Task<CategoriaFinanceira?> ObterCategoriaFinanceiraAsync(int categoriaId, CancellationToken ct = default);
    // ---- Indicadores gerenciais (parcela 5) ----

    /// <summary>
    /// Evoluções registradas no período, com o profissional carregado. É o que dá ao BI
    /// a produtividade CLÍNICA (quantas sessões foram documentadas, e quanto a dor caiu)
    /// além da produtividade de agenda — e, desde a parcela 36, o que responde ao
    /// consultório "o que eu atendi e ainda não escrevi".
    ///
    /// <paramref name="profissionalId"/> vem DEPOIS do <c>ct</c> (o mesmo arranjo de
    /// <c>AgendaService.AgendarAsync</c>) para os chamadores antigos não mudarem. Ele
    /// existe aqui, e não numa sobrecarga nova, porque duas consultas respondendo à mesma
    /// pergunta divergem no dia em que alguém corrigir só uma delas — e a que ficou para
    /// trás continua compilando.
    ///
    /// Evolução SEM profissional entra quando se filtra por um: ela é a sessão escrita
    /// antes de a clínica cadastrar a equipe, e escondê-la faria o consultório cobrar de
    /// novo um registro que já existe.
    /// </summary>
    Task<IReadOnlyList<Evolucao>> EvolucoesNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default, int? profissionalId = null);

    /// <summary>
    /// Quando cada paciente veio pela última vez e se já tem horário futuro — por
    /// PROJEÇÃO, resolvida no banco. É a base do recall, e a pergunta é sobre a base
    /// inteira: trazer todo paciente com o histórico junto arrastaria a tabela de
    /// atendimentos pela rede a cada rodada.
    /// </summary>
    Task<IReadOnlyList<Modelos.InatividadePaciente>> InatividadeAsync(
        DateOnly referencia, CancellationToken ct = default);

    // ---- Campanhas: confirmação, NPS e recall (parcela 5) ----

    Task AdicionarContatoAsync(ContatoCampanha contato, CancellationToken ct = default);

    /// <summary>Contato com paciente e agendamento carregados (entidade rastreada, para editar).</summary>
    Task<ContatoCampanha?> ObterContatoAsync(int contatoId, CancellationToken ct = default);

    /// <summary>Contatos no período (pela data de referência), filtrando por tipo e situação.</summary>
    Task<IReadOnlyList<ContatoCampanha>> ContatosAsync(
        TipoContato? tipo, StatusContato? status,
        DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>
    /// Histórico de contatos de UM paciente, do mais recente para o mais antigo — a
    /// aba de CRM da ficha. O corte vai no SQL: quem abre a ficha quer as últimas
    /// conversas, não a carteira inteira de mensagens desde a instalação.
    /// </summary>
    Task<IReadOnlyList<ContatoCampanha>> ContatosDoPacienteAsync(
        int pacienteId, int limite = 20, CancellationToken ct = default);

    /// <summary>
    /// Quais destas origens já viraram contato deste tipo. É a checagem de
    /// idempotência da campanha: rodar de novo não pode mandar a mesma mensagem duas
    /// vezes para o mesmo paciente.
    /// </summary>
    Task<IReadOnlyList<string>> OrigensDeContatoAsync(
        TipoContato tipo, IReadOnlyCollection<string> origens, CancellationToken ct = default);

    // ---- Usuários e permissões (parcela 5) ----

    /// <summary>Usuários da suíte (ativos e inativos), com o profissional vinculado carregado.</summary>
    Task<IReadOnlyList<UsuarioSistema>> UsuariosAsync(CancellationToken ct = default);

    Task<UsuarioSistema?> ObterUsuarioAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>Usuário pelo login já normalizado (minúsculas). Null quando não existe.</summary>
    Task<UsuarioSistema?> ObterUsuarioPorLoginAsync(string login, CancellationToken ct = default);

    Task AdicionarUsuarioAsync(UsuarioSistema usuario, CancellationToken ct = default);
    Task RemoverUsuarioAsync(int usuarioId, CancellationToken ct = default);

    Task<int> SalvarAsync(CancellationToken ct = default);
}
