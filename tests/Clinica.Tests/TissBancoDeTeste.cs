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

    // Uma data já passada: a guia ANS recusa data de atendimento futura, e o relógio da regra é o de verdade.
    public DateTime Agora = new(2026, 11, 10, 9, 0, 0);
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

    /// <summary>
    /// Um paciente fictício com uma guia em aberto. Por padrão é da Petrobras, de consulta e com
    /// a guia ANS já conferida — o que um envio pede; cada parâmetro tira uma dessas condições.
    /// </summary>
    public async Task<(Paciente Paciente, CodigoFaturamento Guia)> GuiaAsync(
        bool conferida = true, Convenio convenio = Convenio.Petrobras, TipoCodigo tipo = TipoCodigo.Consulta)
    {
        var paciente = new Paciente { Nome = "Paciente de Teste", Convenio = convenio, Carteirinha = CarteiraFicticia };
        Db.Pacientes.Add(paciente);
        await Db.SaveChangesAsync();
        var atendimento = new Atendimento { PacienteId = paciente.Id, Data = DateOnly.FromDateTime(Agora), Modalidade = ModalidadeAtendimento.Consulta };
        Db.Atendimentos.Add(atendimento);
        await Db.SaveChangesAsync();
        var guia = new CodigoFaturamento
        {
            AtendimentoId = atendimento.Id, Tipo = tipo, Ordem = OrdemCodigo.Primeiro,
            DataPrevistaFaturamento = DateOnly.FromDateTime(Agora)
        };
        if (conferida)
            guia.GuiaAns = new GuiaTissConsulta
            {
                RegistroAns = "422631", NumeroCarteira = CarteiraFicticia, NomeBeneficiario = paciente.Nome,
                CodigoPrestadorNaOperadora = "00000000000191", Cnes = "9999999", NomeProfissional = "Profissional de Teste",
                ConselhoProfissional = "06", NumeroConselho = "000000", UfConselho = "33", Cbos = "225133",
                IndicacaoAcidente = "9", RegimeAtendimento = "01", DataAtendimento = DateOnly.FromDateTime(Agora),
                CodigoTabela = "22", TipoConsulta = "1", CodigoProcedimento = "10101012", ValorProcedimento = 139m,
                ConferidaPor = Operador, ConferidaEm = Agora
            };
        Db.Codigos.Add(guia);
        await Db.SaveChangesAsync();
        return (paciente, guia);
    }

    /// <summary>Os campos de uma guia de consulta válida no padrão, como o formulário os conferiria (tudo fictício).</summary>
    public DadosGuiaConsultaAns DadosDaGuia(string? cbos = "225133") => new()
    {
        RegistroAns = "422631", NumeroCarteira = CarteiraFicticia, NomeBeneficiario = "Paciente de Teste",
        CodigoPrestadorNaOperadora = "00000000000191", Cnes = "9999999", NomeProfissional = "Profissional de Teste",
        ConselhoProfissional = "06", NumeroConselho = "000000", UfConselho = "33", Cbos = cbos ?? string.Empty,
        IndicacaoAcidente = "9", RegimeAtendimento = "01", DataAtendimento = DateOnly.FromDateTime(Agora),
        CodigoTabela = "22", TipoConsulta = "1", CodigoProcedimento = "10101012", ValorProcedimento = 139m
    };

    /// <summary>Várias guias conferidas da Petrobras, cada uma de um paciente fictício.</summary>
    public async Task<List<int>> GuiasAsync(int quantas)
    {
        var ids = new List<int>();
        for (var i = 0; i < quantas; i++) ids.Add((await GuiaAsync()).Guia.Id);
        return ids;
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
