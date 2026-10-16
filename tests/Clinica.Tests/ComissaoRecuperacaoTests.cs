using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A comissão de recuperação (out/2026): atendente assume quem sumiu → paciente volta →
/// conclui o atendimento → a guia nasce → a gestão valida. Os testes andam pelo fluxo de
/// verdade (a fila do acompanhamento e o lançamento pelo motor de regras), e conferem o
/// que a verificação do Gerente mostra e grava.
/// </summary>
public sealed class ComissaoRecuperacaoTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly AcompanhamentoPacienteService acompanhamento;
    private readonly ComissaoRecuperacaoService comissao;
    private readonly ClinicaRepositorio repo;
    private readonly UsuarioSistema gestora = new() { Nome = "Gestora teste", Login = "gestora", Perfil = PerfilAcesso.Gerente };
    private readonly UsuarioSistema recepcao = new() { Nome = "Ana Recepção", Login = "ana", Perfil = PerfilAcesso.Recepcao };
    private readonly Paciente paciente = new() { Nome = "Maria Souza", Telefone = "21999990000", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    public ComissaoRecuperacaoTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        repo = new ClinicaRepositorio(db);
        acompanhamento = new AcompanhamentoPacienteService(db, repo, new PacoteService(repo));
        comissao = new ComissaoRecuperacaoService(db);
        db.Usuarios.AddRange(gestora, recepcao);
        db.Pacientes.Add(paciente);
        db.SaveChanges();
        db.Consentimentos.Add(new() { PacienteId = paciente.Id, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing, Concedido = true });
        // A última sessão foi há 70 dias: o recall de 60 dias a encontra.
        db.Atendimentos.Add(new() { PacienteId = paciente.Id, Modalidade = ModalidadeAtendimento.AcupunturaComEletro,
            Data = Hoje.AddDays(-70), RealizadoEm = DateTime.Now.AddDays(-70) });
        db.SaveChanges();
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private async Task<LinhaAcompanhamento> CasoAsync()
    {
        await acompanhamento.GerarRecallAsync(gestora.Id, 60);
        return Assert.Single(await acompanhamento.ListarAsync(gestora.Id));
    }

    /// <summary>A volta pelo motor de regras de verdade: a sessão é realizada e a guia nasce.</summary>
    private async Task<Atendimento> VoltaAsync(int dias = 0)
        => (await new AtendimentoService(repo).LancarAsync(paciente.Id, Hoje.AddDays(dias), ModalidadeAtendimento.AcupunturaComEletro)).Atendimento;

    private Task RegraAsync(decimal valor, TipoAcompanhamento? tipo = null)
        => comissao.SalvarRegraAsync(new RegraComissaoRecuperacao { Valor = valor, Tipo = tipo, VigenteDe = Hoje.AddDays(-30) }, gestora.Login);

    [Fact]
    public async Task Fluxo_completo_assumir_voltar_guia_e_validar_com_o_valor_copiado()
    {
        var caso = await CasoAsync();
        await acompanhamento.AssumirAsync(recepcao.Id, caso.Id, caso.Versao);
        var volta = await VoltaAsync();

        var linha = Assert.Single(await comissao.ApurarAsync(Hoje, Hoje));
        Assert.Equal(volta.Id, linha.AtendimentoId);
        Assert.Equal(recepcao.Id, linha.UsuarioId);
        Assert.Equal("Ana Recepção", linha.Atendente);
        Assert.True(linha.GuiaGerada);
        Assert.Equal(SituacaoRecuperacao.SemValor, linha.Situacao); // sem regra, nada a validar

        await RegraAsync(30m);
        linha = Assert.Single(await comissao.ApurarAsync(Hoje, Hoje));
        Assert.Equal(SituacaoRecuperacao.AValidar, linha.Situacao);
        Assert.Equal(30m, linha.ValorProposto);

        Assert.Equal(1, await comissao.ValidarAsync([volta.Id], gestora.Login));
        // Reajustar depois NÃO reescreve o que foi validado.
        await RegraAsync(50m);
        linha = Assert.Single(await comissao.ApurarAsync(Hoje, Hoje));
        Assert.Equal(SituacaoRecuperacao.Validada, linha.Situacao);
        Assert.Equal(30m, linha.ValorDecidido);
        Assert.Contains(await db.Auditoria.Select(a => a.Acao).ToListAsync(), a => a == "ComissaoRecuperacaoValidada");
    }

    [Fact]
    public async Task Quem_recebe_nao_valida_a_propria_comissao()
    {
        var caso = await CasoAsync();
        await acompanhamento.AssumirAsync(recepcao.Id, caso.Id, caso.Versao);
        var volta = await VoltaAsync();
        await RegraAsync(30m);
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => comissao.ValidarAsync([volta.Id], recepcao.Login));
        Assert.Contains("é sua", erro.Message);
        Assert.Empty(await db.ComissoesRecuperacao.ToListAsync());
    }

    [Fact]
    public async Task Voltou_sem_ninguem_ter_assumido_nao_e_recuperacao_de_ninguem()
    {
        await CasoAsync();
        await VoltaAsync();
        Assert.Empty(await comissao.ApurarAsync(Hoje, Hoje));
    }

    [Fact]
    public async Task So_a_primeira_volta_fecha_o_fluxo_a_segunda_ja_e_tratamento()
    {
        var caso = await CasoAsync();
        await acompanhamento.AssumirAsync(recepcao.Id, caso.Id, caso.Versao);
        var primeira = await VoltaAsync();
        await VoltaAsync(dias: 7);
        var linha = Assert.Single(await comissao.ApurarAsync(Hoje, Hoje.AddDays(10)));
        Assert.Equal(primeira.Id, linha.AtendimentoId);
    }

    [Fact]
    public async Task Recusar_exige_motivo_e_desfazer_devolve_a_verificacao_sem_apagar()
    {
        var caso = await CasoAsync();
        await acompanhamento.AssumirAsync(recepcao.Id, caso.Id, caso.Versao);
        var volta = await VoltaAsync();
        await RegraAsync(30m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => comissao.RecusarAsync(volta.Id, "", gestora.Login));
        await comissao.RecusarAsync(volta.Id, "Paciente voltou por indicação do médico, sem contato.", gestora.Login);
        var linha = Assert.Single(await comissao.ApurarAsync(Hoje, Hoje));
        Assert.Equal(SituacaoRecuperacao.Recusada, linha.Situacao);

        await comissao.CancelarDecisaoAsync(linha.ComissaoId!.Value, "Recusa por engano, era recuperação.", gestora.Login);
        Assert.Equal(SituacaoRecuperacao.AValidar, Assert.Single(await comissao.ApurarAsync(Hoje, Hoje)).Situacao);
        Assert.Equal(1, await comissao.ValidarAsync([volta.Id], gestora.Login));
        // A recusa desfeita continua na base, marcada — a decisão não se apaga.
        Assert.Equal(2, await db.ComissoesRecuperacao.CountAsync());
        Assert.Single(await db.ComissoesRecuperacao.Where(c => c.CanceladaEm == null).ToListAsync());
    }

    [Fact]
    public async Task Sessao_estornada_sem_decisao_sai_e_validada_aparece_para_rever()
    {
        var caso = await CasoAsync();
        await acompanhamento.AssumirAsync(recepcao.Id, caso.Id, caso.Versao);
        var volta = await VoltaAsync();
        await RegraAsync(30m);
        await comissao.ValidarAsync([volta.Id], gestora.Login);
        var atendimento = await db.Atendimentos.SingleAsync(a => a.Id == volta.Id);
        atendimento.EstornadoEm = DateTime.Now;
        await db.SaveChangesAsync();
        var linha = Assert.Single(await comissao.ApurarAsync(Hoje, Hoje));
        Assert.True(linha.EstornadaDepois);
        Assert.Equal("Validada — sessão estornada depois", ComissaoRecuperacaoRegras.Selo(linha).Texto);

        await comissao.CancelarDecisaoAsync(linha.ComissaoId!.Value, "Sessão estornada pelo balcão.", gestora.Login);
        Assert.Empty(await comissao.ApurarAsync(Hoje, Hoje));
    }

    [Fact]
    public async Task Com_contato_registrado_a_linha_conta_as_tentativas()
    {
        var caso = await CasoAsync();
        await acompanhamento.AtualizarAsync(recepcao.Id, caso.Id, new(Guid.NewGuid(), caso.Versao, recepcao.Id,
            Hoje, EtapaAcompanhamento.ProntoParaAgendar, CanalContato.WhatsApp, ""));
        await VoltaAsync();
        var linha = Assert.Single(await comissao.ApurarAsync(Hoje, Hoje));
        Assert.True(linha.TeveContato);
        Assert.Equal(1, linha.Tentativas);
        Assert.Equal(recepcao.Id, linha.UsuarioId);
    }

    [Fact]
    public async Task Responsavel_padrao_da_indicacao_nao_conta_como_quem_assumiu()
    {
        // A indicação de BSV nasce com o responsável PADRÃO (configuração da gestão). Isso é
        // a fila distribuindo, não a atendente assumindo — sem um ato dela, não há comissão.
        var medico = new UsuarioSistema { Nome = "Médico", Login = "medico", Perfil = PerfilAcesso.Profissional,
            Profissional = new() { Nome = "Dr. Gustavo", Ativo = true } };
        db.Usuarios.Add(medico); await db.SaveChangesAsync();
        await acompanhamento.ConfigurarAsync(gestora.Id, medico.ProfissionalId!.Value, recepcao.Id);
        var consulta = new Agendamento { PacienteId = paciente.Id, ProfissionalId = medico.ProfissionalId, DataHora = DateTime.Today.AddDays(-2),
            ModalidadePrevista = ModalidadeAtendimento.Consulta };
        db.Agendamentos.Add(consulta); await db.SaveChangesAsync();
        await acompanhamento.IndicarBsvAsync(medico.Id, consulta.Id);
        Assert.Equal(recepcao.Id, (await acompanhamento.ListarAsync(gestora.Id)).Single(l => l.Tipo == TipoAcompanhamento.NovoBsv).ResponsavelId);
        await new AtendimentoService(repo).LancarAsync(paciente.Id, Hoje, ModalidadeAtendimento.BsvApenas);
        Assert.Empty(await comissao.ApurarAsync(Hoje, Hoje));
    }

    // ===== as regras puras =====

    [Fact]
    public void A_regra_mais_especifica_e_vigente_na_data_do_retorno_vence()
    {
        var d = new DateOnly(2026, 10, 1);
        var regras = new[]
        {
            new RegraComissaoRecuperacao { Id = 1, Valor = 20, VigenteDe = d.AddMonths(-6) },
            new RegraComissaoRecuperacao { Id = 2, Valor = 30, Tipo = TipoAcompanhamento.Recall, VigenteDe = d.AddMonths(-6) },
            new RegraComissaoRecuperacao { Id = 3, Valor = 45, Tipo = TipoAcompanhamento.Recall, Modalidade = ModalidadeAtendimento.BsvApenas, VigenteDe = d.AddMonths(-6) },
            new RegraComissaoRecuperacao { Id = 4, Valor = 99, VigenteDe = d.AddDays(1) },                    // ainda não vale
            new RegraComissaoRecuperacao { Id = 5, Valor = 99, Tipo = TipoAcompanhamento.Recall, VigenteDe = d.AddMonths(-6), Ativa = false }
        };
        Assert.Equal(3, ComissaoRecuperacaoRegras.Vigente(regras, TipoAcompanhamento.Recall, ModalidadeAtendimento.BsvApenas, d)!.Id);
        Assert.Equal(2, ComissaoRecuperacaoRegras.Vigente(regras, TipoAcompanhamento.Recall, ModalidadeAtendimento.Consulta, d)!.Id);
        Assert.Equal(1, ComissaoRecuperacaoRegras.Vigente(regras, TipoAcompanhamento.NovoBsv, ModalidadeAtendimento.BsvApenas, d)!.Id);
        Assert.Null(ComissaoRecuperacaoRegras.Vigente([], TipoAcompanhamento.Recall, ModalidadeAtendimento.Consulta, d));
    }

    [Fact]
    public void Regra_sem_valor_ou_com_vigencia_invertida_e_recusada()
    {
        Assert.NotNull(ComissaoRecuperacaoRegras.Impedimento(new() { Valor = 0, VigenteDe = Hoje }));
        Assert.NotNull(ComissaoRecuperacaoRegras.Impedimento(new() { Valor = 10, VigenteDe = Hoje, VigenteAte = Hoje.AddDays(-1) }));
        Assert.Null(ComissaoRecuperacaoRegras.Impedimento(new() { Valor = 10, VigenteDe = Hoje }));
    }
}
