using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// "ESTA SESSÃO É PARTICULAR?" — a resposta num lugar só (nov/2026).
///
/// Até aqui particular era só "o convênio da ficha não gera guia" (parcela 60), e cada
/// leitor repetia essa conta. As médicas pediram a segunda resposta: a paciente TEM plano,
/// mas quem atende não o aceita — a consulta é particular e a ficha continua com o plano,
/// porque é por ele que a acupuntura dela segue faturando.
///
/// São duas fontes para a mesma pergunta, e o risco é o de sempre: o leitor que só olha
/// uma delas. Por isso a regra mora aqui, e os leitores (fechamento, fila, relatório por
/// médico, recuperação de guias, tablet, cabeçalho do consultório) perguntam aqui:
///
/// 1. a sessão foi marcada como particular (<see cref="Agendamento.Particular"/> /
///    <see cref="Atendimento.Particular"/>); OU
/// 2. o convênio da ficha é o particular (<see cref="CatalogoConvenios.GeraGuia"/> falso).
///
/// ⚠️ <see cref="PelaFicha"/> é, de propósito, a MESMA conta que os leitores já faziam
/// (nenhum comportamento antigo muda): ela inclui a ficha "a definir", cujo convênio também
/// não gera guia. Quem precisa separar "ninguém escolheu" de "escolheu particular" continua
/// conferindo <see cref="Paciente.ConvenioADefinir"/> antes — o lançamento recusa essa ficha
/// (parcela 92), salvo quando a SESSÃO é marcada particular, que é uma escolha registrada.
/// </summary>
public static class SessaoParticular
{
    /// <summary>O convênio da ficha não gera guia — o particular "de ficha" (parcela 60).</summary>
    public static bool PelaFicha(Paciente? paciente)
        => paciente is not null
           && !CatalogoConvenios.GeraGuia(paciente.ConvenioCodigo ?? paciente.Convenio.ToString());

    /// <summary>A sessão é particular: marcada assim, ou pela ficha.</summary>
    public static bool Eh(Paciente? paciente, bool marcadaComoParticular)
        => marcadaComoParticular || PelaFicha(paciente);

    /// <summary>O horário é particular (a marca dele, a do atendimento que ele já tem, ou a ficha).</summary>
    public static bool Eh(Agendamento ag)
        => Eh(ag.Paciente, ag.Particular || ag.Atendimento is { Particular: true });

    /// <summary>
    /// Particular POR ESCOLHA: a ficha tem convênio que fatura, e esta sessão não vai por ele.
    /// É o caso que as telas precisam DIZER — o cabeçalho do paciente continua mostrando o
    /// plano, e sem o aviso ao lado a sessão parece de convênio.
    /// </summary>
    public static bool ApesarDoConvenio(Paciente? paciente, bool marcadaComoParticular)
        => marcadaComoParticular && !PelaFicha(paciente);

    /// <summary>
    /// O nome do pagador DESTA sessão, para lista e relatório: "Particular" quando ela é
    /// particular por escolha, senão o convênio da ficha (que pode ser o próprio particular,
    /// com o nome que a clínica deu a ele).
    /// </summary>
    public static string Pagador(Paciente? paciente, bool marcadaComoParticular)
        => ApesarDoConvenio(paciente, marcadaComoParticular)
            ? "Particular"
            : paciente?.ConvenioNome ?? "—";

    /// <summary>
    /// O pagador com o plano da ficha ao lado — "Particular (ficha: Unimed Costa do Sol)" —,
    /// para as telas de quem atende: saber que a pessoa TEM plano é o que evita a pergunta
    /// "por que estou cobrando de quem tem convênio?". Relatório usa <see cref="Pagador"/>.
    /// </summary>
    public static string PagadorComFicha(Paciente? paciente, bool marcadaComoParticular)
        => ApesarDoConvenio(paciente, marcadaComoParticular)
            ? $"Particular (ficha: {paciente!.ConvenioNome})"
            : paciente?.ConvenioNome ?? "—";
}
