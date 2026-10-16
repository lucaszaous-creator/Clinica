using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Uma linha da listagem de Pacientes do balcão — o que a tabela diz de cada ficha.</summary>
public sealed record LinhaPacienteDoBalcao(
    Paciente Paciente, string Identificacao, string Telefone, string Convenio, string ConvenioCompleto, bool ConvenioADefinir);

/// <summary>
/// O que a tela Pacientes do balcão (Blazor, out/2026 — migrada do PacientesView/ViewModel
/// WPF) AFIRMA: o subtítulo com o tamanho do cadastro, o número de cada recorte, o rodapé da
/// listagem e se ainda há o que carregar. Pura e com teste, pela regra da casa — o que a tela
/// afirma mora onde o <c>dotnet test</c> alcança.
///
/// A listagem é PEDIDA (um recorte, ou "Todos os pacientes") e vem em páginas de
/// <see cref="TamanhoDaPagina"/>: o cadastro inteiro de uma vez foi a consulta mais cara do
/// sistema (2.284 fichas a cada abertura, set/2026).
/// </summary>
public static class PacientesDoBalcaoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const int TamanhoDaPagina = 50;

    /// <summary>
    /// Os recortes que o balcão oferece como chip, além dos que a busca já tem ("Com horário
    /// hoje" é a sugestão da busca; "Todos os pacientes" é o chip dela). "Em tratamento" é do
    /// Consultório (pede <c>VerProntuario</c>) e não entra aqui.
    /// </summary>
    public static IReadOnlyList<FiltroListaPacientes> RecortesDoBalcao { get; } =
        [FiltroListaPacientes.CadastroIncompleto, FiltroListaPacientes.SemConvenioDefinido, FiltroListaPacientes.AniversarioNaSemana];

    public static string Subtitulo(RecortesDePacientes? r)
    {
        if (r is null) return "Fichas cadastradas · busque por nome, CPF ou telefone, ou escolha um recorte";
        var fichas = r.Total == 1 ? "ficha cadastrada" : "fichas cadastradas";
        return $"{r.Total.ToString("N0", PtBr)} {fichas} · busque por nome, CPF ou telefone, ou escolha um recorte";
    }

    public static string Rotulo(FiltroListaPacientes filtro) => filtro switch
    {
        FiltroListaPacientes.ComHorarioHoje => "Com horário hoje",
        FiltroListaPacientes.CadastroIncompleto => "Cadastro incompleto",
        FiltroListaPacientes.SemConvenioDefinido => "Sem convênio definido",
        FiltroListaPacientes.AniversarioNaSemana => "Aniversário nesta semana",
        FiltroListaPacientes.EmTratamento => "Em tratamento",
        _ => "Todos os pacientes"
    };

    /// <summary>Quantos cabem no recorte — nulo quando a contagem não está disponível (não leu, ou falhou).</summary>
    public static int? Contagem(FiltroListaPacientes filtro, RecortesDePacientes? r) => r is null ? null : filtro switch
    {
        FiltroListaPacientes.Todos => r.Total,
        FiltroListaPacientes.ComHorarioHoje => r.ComHorarioHoje,
        FiltroListaPacientes.CadastroIncompleto => r.CadastroIncompleto,
        FiltroListaPacientes.SemConvenioDefinido => r.SemConvenio,
        FiltroListaPacientes.AniversarioNaSemana => r.AniversarioNaSemana,
        _ => null
    };

    /// <summary>"Cadastro incompleto · 12" — sem número quando a contagem falhou (o chip continua valendo).</summary>
    public static string Chip(FiltroListaPacientes filtro, RecortesDePacientes? r)
        => Contagem(filtro, r) is { } n ? $"{Rotulo(filtro)} · {n.ToString("N0", PtBr)}" : Rotulo(filtro);

    /// <summary>O que o recorte É, dito acima da tabela — e o que fazer com quem está nele.</summary>
    public static string Explicacao(FiltroListaPacientes filtro, DateOnly hoje) => filtro switch
    {
        FiltroListaPacientes.CadastroIncompleto => "Fichas sem documento (CPF ou RG). Complete na ficha, com o paciente na frente.",
        FiltroListaPacientes.SemConvenioDefinido => "Vieram do sistema anterior sem convênio. Pergunte e escolha na ficha — sem isso a sessão não gera guia.",
        FiltroListaPacientes.AniversarioNaSemana => $"Fazem aniversário de hoje até {hoje.AddDays(6).ToString("dd/MM", PtBr)}.",
        FiltroListaPacientes.ComHorarioHoje => "Quem tem horário marcado ou realizado hoje.",
        FiltroListaPacientes.EmTratamento => "Quem já teve sessão realizada na clínica.",
        _ => $"Todo o cadastro, em ordem de nome, {TamanhoDaPagina} por vez."
    };

    /// <summary>O vazio do recorte — nunca "nenhum paciente" solto, que se leria como cadastro vazio.</summary>
    public static string Vazio(FiltroListaPacientes filtro) => filtro switch
    {
        FiltroListaPacientes.CadastroIncompleto => "Nenhuma ficha sem documento.",
        FiltroListaPacientes.SemConvenioDefinido => "Nenhuma ficha com convênio a definir.",
        FiltroListaPacientes.AniversarioNaSemana => "Ninguém faz aniversário nesta semana.",
        FiltroListaPacientes.ComHorarioHoje => "Ninguém com horário hoje.",
        FiltroListaPacientes.EmTratamento => "Ninguém em tratamento.",
        _ => "Nenhum paciente cadastrado."
    };

    /// <summary>
    /// Há mais a carregar? A página que veio CHEIA diz que pode haver; com o total conhecido,
    /// carregar até ele. A página incompleta é o fim — mesmo que o total (contado antes) diga
    /// outra coisa, porque outra máquina pode ter mudado o cadastro no meio.
    /// </summary>
    public static bool TemMais(int carregados, int ultimaPagina, int? total)
        => ultimaPagina >= TamanhoDaPagina && (total is null || carregados < total);

    /// <summary>"50 de 2.284 pacientes · todos, em ordem de nome" — o rodapé diz o que está na tela.</summary>
    public static string Rodape(int carregados, FiltroListaPacientes filtro, int? total, bool temMais)
    {
        var recorte = filtro == FiltroListaPacientes.Todos ? "todos, em ordem de nome" : Rotulo(filtro).ToLower(PtBr);
        var conta = temMais && total is { } t && t > carregados
            ? $"{carregados.ToString("N0", PtBr)} de {t.ToString("N0", PtBr)} pacientes"
            : carregados == 1 ? "1 paciente" : $"{carregados.ToString("N0", PtBr)} pacientes";
        return $"{conta} · {recorte}";
    }

    /// <summary>A linha da tabela: a mesma identificação da lista WPF (<see cref="ListaDePacientes"/>).</summary>
    public static LinhaPacienteDoBalcao Linha(Paciente p, DateOnly hoje) => new(
        p,
        ListaDePacientes.Identificacao(p, hoje),
        string.IsNullOrWhiteSpace(p.Telefone) ? "—" : p.TelefoneFormatado,
        p.ConvenioADefinir ? "sem convênio" : p.ConvenioNome,
        p.ConvenioNome,
        p.ConvenioADefinir);
}
