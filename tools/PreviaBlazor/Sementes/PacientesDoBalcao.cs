// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// PACIENTES do balcão (out/2026, migração para Blazor): cadastro o bastante para a listagem
/// passar de UMA página de 50 ("Carregar mais"), com fichas que caem em cada recorte — sem
/// documento, convênio "a definir" (vindas do sistema anterior), CPF e RG gravados, e quem faz
/// aniversário nesta semana.
/// </summary>
public sealed class SementePacientesDoBalcao : ISemente
{
    public int Ordem => 78;

    public void Semear(ClinicaDbContext db)
    {
        string[] nomes = ["Adriana", "Bruno", "Cecília", "Daniel", "Elisa", "Fábio", "Gabriela", "Heitor", "Isabela", "Jorge",
            "Karina", "Leonardo", "Márcia", "Nelson", "Olga", "Pedro", "Quitéria", "Rafael", "Sílvia", "Túlio"];
        string[] sobrenomes = ["Andrade", "Barbosa", "Cardoso", "Duarte", "Esteves", "Freitas", "Gomes"];
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var i = 0;
        foreach (var s in sobrenomes)
        foreach (var n in nomes.Take(9))
        {
            i++;
            var p = new Paciente
            {
                Nome = $"{n} {s}",
                Telefone = i % 7 == 0 ? null : $"(21) 9{i % 10}{(i * 3) % 10}{(i * 7) % 10}1-{1000 + i * 13}",
                DataNascimento = i % 11 == 0
                    ? new DateOnly(1960 + i % 30, hoje.AddDays(i % 6).Month, hoje.AddDays(i % 6).Day)
                    : new DateOnly(1950 + i % 50, 1 + i % 12, 1 + i % 27),
                Convenio = i % 3 == 0 ? Convenio.UnimedPadrao : Convenio.Amil,
                ConvenioCodigo = i % 5 == 0 ? ConvenioCadastro.CodigoADefinir : null,
                Documento = (i % 4) switch
                {
                    0 => null,
                    1 => $"{100 + i:000}{200 + i:000}{300 + i:000}{i % 100:00}",
                    2 => $"{10 + i % 80}.{100 + i}.{200 + i}-{i % 10}",
                    _ => null
                }
            };
            db.Add(p);
        }
        db.SaveChanges();
    }
}
