namespace Clinica.Domain.Entities;

/// <summary>
/// RESERVA DE HORÁRIO SEM FICHA (nov/2026, pedido do dono, caminho B: "os dados não se apagam").
///
/// Os anúncios da Meta trazem muita gente só perguntando; a clínica não quer cadastrar cada uma nem pedir CPF pelo
/// WhatsApp (LGPD). A reserva guarda o MÍNIMO — o nome que a pessoa deu, o telefone da conversa, o horário, quem atende
/// e "particular/convênio X" — e OCUPA o horário de verdade (a busca de vagas e a trava de conflito a consideram).
///
/// ⚠️ Ela NÃO é paciente e não vira <see cref="Agendamento"/> sozinha: Agendamento.PacienteId continua obrigatório
/// (faturamento, guias, relatórios e BI dependem dele) e a reserva fica fora deles por construção. Quando a pessoa vem
/// (ou antes), "Virar paciente" cria/escolhe a ficha e a reserva vira um agendamento normal no MESMO horário.
///
/// Nada se apaga: desmarcada, não comparecida ou convertida, a linha fica, com a situação (decisão do dono). A base
/// legal está em docs/conformidade-lgpd.md, pendente de validação jurídica.
/// </summary>
public class ReservaDeHorario
{
    /// <summary>O selo na agenda e no detalhe.</summary>
    public const string Marca = "Reserva via WhatsApp — sem ficha";

    /// <summary>O selo curto (bloco da grade e cartão da conversa).</summary>
    public const string MarcaCurta = "Reserva · sem ficha";

    /// <summary>Os motivos de desmarcar — exatamente estes (Design SemDor).</summary>
    public static readonly IReadOnlyList<string> MotivosDeDesmarcar = ["A pessoa pediu", "Não respondeu", "A clínica precisou"];

    public const int NomeMaximo = 120;
    public const int ConvenioMaximo = 80;

    public int Id { get; set; }

    /// <summary>Como a pessoa se apresentou no WhatsApp (corrigível).</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>O telefone da CONVERSA, normalizado (FichaPeloTelefone.Normalizar: só dígitos, com 55).</summary>
    public string Telefone { get; set; } = string.Empty;

    /// <summary>A conversa do CRM em que a reserva nasceu (para o "Virar paciente" ligar a ficha nela).</summary>
    public string? ConversaId { get; set; }

    public DateTime DataHora { get; set; }
    public int DuracaoMinutos { get; set; }

    public int ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }
    public int? SalaId { get; set; }

    /// <summary>O "o quê" da agenda (código do catálogo de modalidades) e a especialidade da consulta, quando há.</summary>
    public string ModalidadeCodigo { get; set; } = string.Empty;
    public string? EspecialidadeConsultaCodigo { get; set; }

    /// <summary>Particular, ou o NOME do convênio que a pessoa disse — sem carteirinha (ela traz no dia).</summary>
    public bool Particular { get; set; }
    public string? ConvenioNome { get; set; }

    public SituacaoDaReserva Situacao { get; set; } = SituacaoDaReserva.Reservada;
    public string? MotivoDesmarcacao { get; set; }

    /// <summary>Preenchidos quando virou paciente: o agendamento que a substituiu e a ficha.</summary>
    public int? AgendamentoId { get; set; }
    public int? PacienteId { get; set; }

    public DateTime CriadaEm { get; set; }
    public string CriadaPor { get; set; } = string.Empty;
    public DateTime? AlteradaEm { get; set; }
    public string? AlteradaPor { get; set; }

    /// <summary>Concorrência otimista: dois balcões mexendo na mesma reserva — o segundo relê.</summary>
    public int Versao { get; set; }

    public DateTime Fim => DataHora.AddMinutes(DuracaoMinutos);

    /// <summary>Ocupa a agenda: só a reservada. Desmarcada, não comparecida ou convertida devolvem (ou passam) a vaga.</summary>
    public bool OcupaAgenda => Situacao == SituacaoDaReserva.Reservada;

    public bool ColideCom(DateTime inicio, DateTime fim) => DataHora < fim && inicio < Fim;

    /// <summary>
    /// A ocupação da reserva no formato que a busca de vagas entende (um <see cref="Agendamento"/> SÓ EM MEMÓRIA, nunca
    /// gravado: id negativo, sem paciente). É o que faz a reserva tirar a vaga da busca dos apps e da ponte.
    /// </summary>
    public Agendamento ComoOcupacao() => new()
    {
        Id = -Id, DataHora = DataHora, DuracaoMinutos = DuracaoMinutos, ProfissionalId = ProfissionalId,
        SalaId = SalaId, Status = StatusAgendamento.Agendado, ModalidadeCodigo = ModalidadeCodigo
    };
}

public enum SituacaoDaReserva
{
    Reservada,
    Desmarcada,
    NaoCompareceu,
    VirouAgendamento
}
