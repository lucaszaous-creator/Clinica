using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a gerência pediu junto com a trava (nov/2026): o PREÇO POR PROFISSIONAL na tabela do
/// particular, a PRÉ-MARCAÇÃO de particular quando quem atende não atende o plano, o indicador
/// de ATENDIDOS POR CONVÊNIO e a marca de QUEM APROVA liberações, em Acessos.
/// </summary>
public class PrecoPorProfissionalEConveniosTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrecoParticularService _precos;
    private readonly int _ana, _bia;

    private static readonly DateOnly Dia = new(2026, 11, 10);

    public PrecoPorProfissionalEConveniosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var ana = new Profissional { Nome = "Dra. Ana", ConveniosNaoAtendidos = "UnimedIntercambio" };
        var bia = new Profissional { Nome = "Dra. Bia" };
        _db.Profissionais.AddRange(ana, bia);
        _db.SaveChanges();
        (_ana, _bia) = (ana.Id, bia.Id);
        _repo = new ClinicaRepositorio(_db);
        _precos = new PrecoParticularService(_repo);

        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio("UnimedIntercambio", "Unimed Intercâmbio", Convenio.UnimedIntercambio, true, GeraGuia: true),
            new EntradaConvenio("Amil", "Amil", Convenio.Amil, true, GeraGuia: true),
            new EntradaConvenio(ConvenioCadastro.CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica(), FormatoNumeroGuia.SemValidacao, GeraGuia: false)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task Preco(decimal valor, int? profissionalId = null, string? especialidade = null)
        => _precos.SalvarAsync(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta), EspecialidadeCodigo = especialidade,
            ProfissionalId = profissionalId, Valor = valor
        });

    private Task<PrecoProposto> Propor(int? profissionalId, string? especialidade = nameof(Especialidade.Endocrinologia))
        => _precos.ProporAsync(nameof(ModalidadeAtendimento.Consulta), ModalidadeAtendimento.Consulta, especialidade, Dia,
            profissionalId: profissionalId);

    // ==================== Preço por profissional ====================

    [Fact]
    public async Task O_preco_do_profissional_ganha_do_geral_e_nunca_serve_a_outro()
    {
        await Preco(300m);                                                        // consulta, geral
        await Preco(350m, especialidade: nameof(Especialidade.Endocrinologia));   // endócrino, geral
        await Preco(500m, _ana);                                                  // a consulta da Dra. Ana

        (await Propor(_ana)).Valor.Should().Be(500m, "o preço de quem atende ganha até do preço da especialidade");
        (await Propor(_bia)).Valor.Should().Be(350m, "o preço da Ana não serve à Bia: vale o geral mais específico");
        (await Propor(null)).Valor.Should().Be(350m, "sem saber quem atende (a Conciliação), só concorrem os gerais");
        (await Propor(_ana)).Procedencia.Should().Contain("Dra. Ana", "a procedência gravada no lançamento diz de quem é o preço");
    }

    [Fact]
    public async Task So_preco_de_outro_profissional_nao_propoe_valor_nenhum()
    {
        await Preco(500m, _ana);

        (await Propor(_bia)).Houve.Should().BeFalse("propor à Bia o preço da Ana seria inventar valor");
    }

    [Fact]
    public async Task Editar_o_preco_guarda_e_limpa_o_profissional()
    {
        await Preco(500m, _ana);
        var gravado = (await _precos.CatalogoAsync()).Single();
        gravado.ProfissionalId.Should().Be(_ana);
        PrecosParticularNaTela.LinhaDe(gravado, Dia).Profissional.Should().Be("Dra. Ana");

        // A tela manda objeto NOVO com o Id (o mesmo caminho do formulário): voltar a "todos".
        await _precos.SalvarAsync(new PrecoParticular
        {
            Id = gravado.Id, ModalidadeCodigo = gravado.ModalidadeCodigo, Valor = 520m, ProfissionalId = null
        });
        var depois = (await _precos.CatalogoAsync()).Single();
        depois.ProfissionalId.Should().BeNull();
        PrecosParticularNaTela.LinhaDe(depois, Dia).Profissional.Should().Be(PrecosParticularNaTela.TodosOsProfissionais);
    }

    // ==================== Convênios que o profissional não atende ====================

    [Fact]
    public async Task Quem_nao_atende_o_plano_da_ficha_pre_marca_particular_e_so_nesse_caso()
    {
        var ana = await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == _ana);
        var bia = await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == _bia);
        var comUnimed = new Paciente { Convenio = Convenio.UnimedIntercambio, ConvenioCodigo = "UnimedIntercambio" };
        var comAmil = new Paciente { Convenio = Convenio.Amil, ConvenioCodigo = "Amil" };
        var particular = new Paciente { Convenio = Convenio.Personalizado, ConvenioCodigo = ConvenioCadastro.CodigoParticular };

        FormaDeAtendimentoNaTela.SugereParticular(ana, comUnimed).Should().BeTrue();
        FormaDeAtendimentoNaTela.SugereParticular(ana, comAmil).Should().BeFalse("a Amil ela atende");
        FormaDeAtendimentoNaTela.SugereParticular(bia, comUnimed).Should().BeFalse("sem nada marcado, atende todos");
        FormaDeAtendimentoNaTela.SugereParticular(null, comUnimed).Should().BeFalse("sem profissional escolhido não há o que sugerir");
        FormaDeAtendimentoNaTela.SugereParticular(ana, particular).Should().BeFalse("ficha particular não tem escolha a pré-marcar");
        FormaDeAtendimentoNaTela.PreMarcado(ana, comUnimed).Should().Contain("Dra. Ana").And.Contain("Unimed Intercâmbio");
    }

    [Fact]
    public async Task O_cadastro_guarda_a_lista_limpa_e_quem_nao_edita_preserva()
    {
        var equipe = new EquipeService(_repo);

        // O formulário do profissional manda a lista (com sujeira): grava limpa.
        await equipe.SalvarProfissionalAsync(new Profissional
        {
            Id = _bia, Nome = "Dra. Bia", Ativo = true, ConveniosNaoAtendidos = " Amil ;amil;;UnimedIntercambio"
        });
        _db.ChangeTracker.Clear();
        (await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == _bia)).CodigosNaoAtendidos
            .Should().BeEquivalentTo(["Amil", "UnimedIntercambio"]);

        // A tela de Acessos regrava o profissional só para o CPF, SEM saber dos convênios (nulo): fica o que estava.
        await equipe.SalvarProfissionalAsync(new Profissional { Id = _bia, Nome = "Dra. Bia", Ativo = true });
        _db.ChangeTracker.Clear();
        (await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == _bia)).CodigosNaoAtendidos.Should().HaveCount(2);

        // Vazio é "atende todos".
        await equipe.SalvarProfissionalAsync(new Profissional { Id = _bia, Nome = "Dra. Bia", Ativo = true, ConveniosNaoAtendidos = "" });
        _db.ChangeTracker.Clear();
        (await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == _bia)).ConveniosNaoAtendidos.Should().BeNull();
    }

    [Fact]
    public void O_cadastro_do_profissional_so_lista_convenio_que_fatura()
    {
        var lista = FormaDeAtendimentoNaTela.ConveniosParaOProfissional(
            [("Amil", "Amil", true, true), ("Particular", "Particular", false, true), ("Antigo", "Convênio antigo", true, false),
             ("Unimed", "Unimed", true, true)],
            jaMarcados: ["Antigo"]);

        lista.Select(c => c.Nome).Should().Equal(["Amil", "Convênio antigo", "Unimed"],
            "particular não é escolha; o desativado que já estava marcado continua para poder ser desmarcado");
    }

    // ==================== Atendidos por convênio ====================

    [Fact]
    public async Task O_indicador_por_convenio_conta_a_sessao_pelo_pagador_dela()
    {
        var parametros = new ParametrosService(_repo);
        var agenda = new AgendaService(_repo, new AtendimentoService(_repo, parametros: parametros), parametros);
        Paciente Novo(string nome, Convenio c, string codigo)
        {
            var p = new Paciente { Nome = nome, Convenio = c, ConvenioCodigo = codigo, Sexo = Sexo.Feminino };
            _db.Pacientes.Add(p);
            return p;
        }
        var mariane = Novo("Mariane", Convenio.UnimedIntercambio, "UnimedIntercambio");
        var patricia = Novo("Patrícia", Convenio.UnimedIntercambio, "UnimedIntercambio");
        var carla = Novo("Carla", Convenio.Personalizado, ConvenioCadastro.CodigoParticular);
        await _db.SaveChangesAsync();

        var quando = Dia.ToDateTime(new TimeOnly(9, 0));
        async Task Atender(int pacienteId, int hora, bool particular)
        {
            var ag = await agenda.AgendarAsync(pacienteId, quando.AddHours(hora), ModalidadeAtendimento.Consulta, null,
                especialidadeConsultaCodigo: nameof(Especialidade.Endocrinologia), profissionalId: _ana, operador: "recepcao",
                particular: particular);
            await agenda.ConfirmarPresencaAsync(ag.Id, operador: "recepcao");
        }
        await Atender(mariane.Id, 0, particular: true);    // tem Unimed, consulta particular
        await Atender(patricia.Id, 1, particular: false);  // pela Unimed
        await Atender(carla.Id, 2, particular: false);     // particular de ficha
        _db.ChangeTracker.Clear();

        var indicadores = await new IndicadoresService(_repo, parametros).AgendaAsync(Dia, Dia);

        indicadores.PorPagador.Should().BeEquivalentTo(
        [
            new AtendidosPorPagador("Particular", true, 2, 2),
            new AtendidosPorPagador("Unimed Intercâmbio", false, 1, 1)
        ], o => o.WithStrictOrdering(), "o particular de ficha e o marcado no horário somam numa linha só");
    }

    // ==================== Quem aprova, em Acessos ====================

    [Fact]
    public async Task So_gerente_pode_ser_marcado_como_aprovador_e_a_marca_fica_na_trilha()
    {
        var acesso = new AcessoService(_repo);
        var gerente = new UsuarioSistema { Nome = "Ana Gerente", Login = "ana", Perfil = PerfilAcesso.Gerente, Ativo = true };
        var recepcao = new UsuarioSistema { Nome = "Rayssa", Login = "rayssa", Perfil = PerfilAcesso.Recepcao, Ativo = true };
        _db.Usuarios.AddRange(gerente, recepcao);
        await _db.SaveChangesAsync();

        await acesso.DefinirAprovacaoDeLiberacaoAsync(gerente.Id, true, "direcao");
        var naoGerente = async () => await acesso.DefinirAprovacaoDeLiberacaoAsync(recepcao.Id, true, "direcao");
        (await naoGerente.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("gerente");

        _db.ChangeTracker.Clear();
        var gravada = await _db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == gerente.Id);
        gravada.PodeAprovarLiberacao.Should().BeTrue();
        (await _db.Auditoria.AsNoTracking().AnyAsync(e => e.Acao == "AprovacaoDeLiberacaoAlterada" && e.Operador == "direcao"))
            .Should().BeTrue();

        // Deixou de ser gerente: a marca esquecida não libera mais nada.
        gravada.Perfil = PerfilAcesso.Recepcao;
        gravada.PodeAprovarLiberacao.Should().BeFalse();
    }
}
