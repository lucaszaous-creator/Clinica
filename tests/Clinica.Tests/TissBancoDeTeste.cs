using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// O banco dos testes da fila TISS: um SQLite em memória com o modelo inteiro, e a
/// possibilidade de abrir um SEGUNDO contexto sobre a mesma conexão — é assim que os testes
/// fazem o papel de dois conectores disputando a mesma linha. Só dados fictícios.
/// </summary>
public sealed class TissBancoDeTeste : IDisposable
{
    public const string Petrobras = "Petrobras";
    public const string Operador = "faturista.teste";
    public const string CarteiraFicticia = "000123456789";

    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly List<ClinicaDbContext> _contextos = [];

    public DateTime Agora = new(2026, 11, 12, 9, 0, 0);
    public ClinicaDbContext Db { get; }
    public ClinicaRepositorio Repo { get; }
    public ProtecaoSegredoGlobal Protecao { get; } = new(Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()));

    public TissBancoDeTeste()
    {
        _conn.Open();
        Db = NovoContexto();
        Db.Database.EnsureCreated();
        Repo = new ClinicaRepositorio(Db);
    }

    /// <summary>Outro contexto sobre o MESMO banco: outro processo, do ponto de vista do EF.</summary>
    public ClinicaDbContext NovoContexto()
    {
        var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _contextos.Add(db);
        return db;
    }

    public FilaTissService Fila(ClinicaRepositorio? repo = null) => new(repo ?? Repo, () => Agora);

    public CredenciaisTissService Credenciais(ClinicaRepositorio? repo = null) => new(repo ?? Repo, Protecao);

    /// <summary>Um paciente fictício da Petrobras com uma guia de consulta em aberto.</summary>
    public async Task<(Paciente Paciente, CodigoFaturamento Guia)> GuiaAsync()
    {
        var paciente = new Paciente { Nome = "Paciente de Teste", Convenio = Convenio.Petrobras, Carteirinha = CarteiraFicticia };
        Db.Pacientes.Add(paciente);
        await Db.SaveChangesAsync();
        var atendimento = new Atendimento { PacienteId = paciente.Id, Data = DateOnly.FromDateTime(Agora), Modalidade = ModalidadeAtendimento.Consulta };
        Db.Atendimentos.Add(atendimento);
        await Db.SaveChangesAsync();
        var guia = new CodigoFaturamento
        {
            AtendimentoId = atendimento.Id, Tipo = TipoCodigo.Consulta, Ordem = OrdemCodigo.Primeiro,
            DataPrevistaFaturamento = DateOnly.FromDateTime(Agora)
        };
        Db.Codigos.Add(guia);
        await Db.SaveChangesAsync();
        return (paciente, guia);
    }

    /// <summary>Os dados da clínica que toda mensagem leva (CNPJ e registro ANS fictícios).</summary>
    public async Task PrestadorAsync()
        => await new ParametrosService(Repo, Protecao).SalvarPrestadorAsync(new DadosPrestador
        {
            Cnpj = "00.000.000/0001-91", RazaoSocial = "Clínica de Teste Ltda", RegistroAnsOperadora = "422631"
        });

    public void Dispose()
    {
        foreach (var db in _contextos) db.Dispose();
        _conn.Dispose();
    }
}
