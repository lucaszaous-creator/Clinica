namespace Clinica.Domain.Entities;

/// <summary>Uma visita do paciente em uma data. Ao ser lançado, gera os códigos de faturamento pela regra do convênio.</summary>
public class Atendimento
{
    public int Id { get; set; }

    /// <summary>Número/protocolo do atendimento (ex.: 2026-000123) — base do lastro de faturamento.</summary>
    public string? Numero { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public DateOnly Data { get; set; }

    public ModalidadeAtendimento Modalidade { get; set; }

    /// <summary>Código da modalidade no catálogo (identifica a variante/nome). Null = embutida = Modalidade.ToString().</summary>
    public string? ModalidadeCodigo { get; set; }

    /// <summary>Especialidade da consulta quando a modalidade é Consulta (discrimina para os relatórios).</summary>
    public Especialidade? EspecialidadeConsulta { get; set; }

    /// <summary>Código da especialidade no catálogo. Null = embutida = EspecialidadeConsulta.ToString().</summary>
    public string? EspecialidadeConsultaCodigo { get; set; }

    /// <summary>Categoria definida pela regra no momento do atendimento.</summary>
    public Categoria Categoria { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>
    /// Quem LANÇOU isto no sistema, e quando (parcela 58).
    ///
    /// A direção pediu para ver de quem é cada lançamento. A trilha de auditoria já
    /// responde "quem fez isso?" (parcela 21), mas ela é uma tela à parte, filtrada por
    /// período — e a pergunta que se faz olhando a agenda é sobre AQUELA linha, agora.
    ///
    /// É o operador do LOGIN (`SessaoUsuario.Atual.Operador`), nunca o usuário do Windows:
    /// no balcão duas pessoas dividem a mesma máquina, e o login do Windows apagaria a
    /// diferença entre elas — foi por isso que `SessaoUsuario` existe.
    ///
    /// Nulo nas linhas anteriores a esta parcela, e a tela DIZ isso em vez de deixar em
    /// branco: em branco não se distingue de "não carregou".
    /// </summary>
    public string? LancadoPor { get; set; }

    /// <summary>Quando foi lançado. Nulo nas linhas anteriores à parcela 58.</summary>
    public DateTime? LancadoEm { get; set; }

    /// <summary>
    /// Quando a SESSÃO aconteceu (a presença foi confirmada) — parcela 70.
    ///
    /// Com a guia nascendo na MARCAÇÃO (regime "guia no agendamento"), "existir
    /// atendimento" deixou de significar "a sessão aconteceu": passou a significar "a
    /// sessão está registrada". Todo leitor que quer dizer ACONTECEU — BI, rentabilidade,
    /// origem dos pacientes, retenção — ancora aqui, nunca na existência da linha. Nulo =
    /// sessão registrada e ainda não realizada (marcada para o futuro, ou cancelada). As
    /// linhas anteriores à parcela recebem backfill na migration, e a ATIVAÇÃO da chave
    /// repete o backfill — na janela de atualização o app antigo grava sem este carimbo,
    /// e ligar a chave é o momento em que tudo o que existe é, por definição, realizado.
    /// </summary>
    public DateTime? RealizadoEm { get; set; }


    /// <summary>
    /// Quando este atendimento foi ESTORNADO (parcela 94) — a sessão foi lançada por
    /// engano e desfeita.
    ///
    /// Por que existe uma marca, em vez de apagar a linha
    /// --------------------------------------------------
    /// Atendimento é lastro de faturamento: apagá-lo levaria junto os códigos por cascata
    /// e, pior, o horário que aponta para ele ficaria ÓRFÃO — <c>OnDelete(SetNull)</c>
    /// deixa <c>Status = Realizado</c> com <c>AtendimentoId</c> nulo, que é o estado dos
    /// três encaixes de 12/08/2026: o kanban diz "Finalizado", o repasse exclui a sessão
    /// em silêncio e ninguém detecta. O estorno ANULA e deixa rastro; não apaga nada.
    ///
    /// ⚠️ E é esta marca que impede a RESSURREIÇÃO. O backfill do <see cref="RealizadoEm"/>
    /// (<c>MarcarAtendimentosSemCarimboComoRealizadosAsync</c>) carimba como realizado todo
    /// atendimento sem carimbo que não tenha um horário em outro estado apontando para ele
    /// — e o estorno solta o horário justamente para que a sessão possa ser relançada
    /// limpa. Sem esta coluna no filtro, ligar a chave "guia no agendamento" devolveria
    /// todo atendimento estornado à contagem de sessões realizadas, corrompendo BI,
    /// retenção e origem de pacientes sem um sintoma sequer.
    /// </summary>
    public DateTime? EstornadoEm { get; set; }

    /// <summary>Quem estornou (o operador do LOGIN, nunca o usuário do Windows).</summary>
    public string? EstornadoPor { get; set; }

    /// <summary>Por que foi estornado. Obrigatório no ato — é o que fica para quem auditar.</summary>
    public string? MotivoEstorno { get; set; }

    /// <summary>Atalho de leitura. NUNCA use dentro de um Where traduzido — é derivada.</summary>
    public bool Estornado => EstornadoEm is not null;

    public List<CodigoFaturamento> Codigos { get; set; } = new();
}
