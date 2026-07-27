using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Situação de uma autorização em relação à cota: quanto já foi usado e quanto sobra.</summary>
public sealed record SaldoAutorizacao(
    AutorizacaoSessoes Autorizacao,
    int Utilizadas,
    int Restantes,
    bool Vencida)
{
    public int Autorizadas => Autorizacao.QuantidadeAutorizada;

    /// <summary>Cota estourada ou zerada: a próxima sessão vira glosa 2006.</summary>
    public bool Esgotada => Restantes <= 0;

    /// <summary>Sobrou só uma: hora de pedir a renovação da senha.</summary>
    public bool NaUltima => Restantes == 1;

    /// <summary>A autorização ainda serve para lançar hoje?</summary>
    public bool Utilizavel => !Vencida && !Esgotada;

    /// <summary>Ex.: "7 de 10 usadas — restam 3".</summary>
    public string Resumo => $"{Utilizadas} de {Autorizadas} usadas — {Rotulo}";

    private string Rotulo => Restantes switch
    {
        <= 0 => "cota esgotada",
        1 => "resta 1",
        _ => $"restam {Restantes}"
    };
}
