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


    public List<CodigoFaturamento> Codigos { get; set; } = new();
}
