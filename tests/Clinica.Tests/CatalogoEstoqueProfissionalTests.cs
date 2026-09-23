using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class CatalogoEstoqueProfissionalTests : IDisposable
{
    private readonly SqliteConnection _conexao = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly EstoqueService _estoque;
    private readonly DateOnly _dia = new(2026, 8, 10);
    public CatalogoEstoqueProfissionalTests()
    {
        _conexao.Open();
        _db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        _db.Database.EnsureCreated();
        _estoque = new(new ClinicaRepositorio(_db));
    }
    public void Dispose() { _db.Dispose(); _conexao.Dispose(); }

    [Fact]
    public async Task Compra_por_caixa_preserva_documento_conversao_e_valor_da_fatura()
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque {
            Nome = "Material descartável", Unidade = "un", UnidadeCompra = "cx", FatorCompra = 3,
            CodigoInterno = " mat-01 ", Grupo = GrupoEstoque.MaterialAssistencial,
            Uso = UsoEstoque.Ambos, EstoqueMinimo = 2, EstoqueMaximo = 9,
            Fabricante = "Fabricante", Apresentacao = "Caixa com três", CodigoBarras = "123",
            LocalArmazenamento = "Armário A", Observacoes = "Conferir apresentação"
        });
        var movimento = await _estoque.ComprarAsync(new MovimentoEstoque {
            ItemEstoqueId = item.Id, Tipo = TipoMovimentoEstoque.Entrada,
            Quantidade = 2, CustoUnitario = 10, Data = _dia, DocumentoEntrada = "NF-12"
        }, "Fornecedor", _dia.AddDays(30), emUnidadeCompra: true);
        _db.ChangeTracker.Clear();
        var salvo = (await _estoque.ObterItemAsync(item.Id))!;
        salvo.CodigoInterno.Should().Be("MAT-01");
        salvo.Apresentacao.Should().Be("Caixa com três");
        salvo.Observacoes.Should().Be("Conferir apresentação");
        movimento.Quantidade.Should().Be(6);
        movimento.CustoUnitario.Should().Be(3.3333m);
        movimento.QuantidadeInformada.Should().Be(2);
        movimento.UnidadeInformada.Should().Be("cx");
        movimento.FatorConversao.Should().Be(3);
        movimento.Fornecedor.Should().Be("Fornecedor");
        movimento.DocumentoEntrada.Should().Be("NF-12");
        (await _db.Set<LancamentoFinanceiro>().SingleAsync()).Valor.Should().Be(20);
        salvo.FatorCompra = 12;
        await _estoque.SalvarItemAsync(salvo);
        (await _estoque.MovimentosAsync(item.Id)).Single().FatorConversao.Should().Be(3);
        (await _estoque.SaldosAsync()).Single().EstoqueMaximo.Should().Be(9);
    }

    [Fact]
    public async Task Codigo_interno_normalizado_nao_pode_duplicar_item_inativo()
    {
        await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "A", CodigoInterno = "abc", Ativo = false });
        var duplicar = () => _estoque.SalvarItemAsync(new ItemEstoque { Nome = "B", CodigoInterno = " ABC " });
        await duplicar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*código interno*");
        (await _db.Set<ItemEstoque>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Medicamento_exige_lote_e_validade_mesmo_sem_marcar_as_opcoes()
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Medicamento cadastrado pela clínica", Grupo = GrupoEstoque.Medicamento });
        item.ExigirLote.Should().BeTrue(); item.ExigirValidade.Should().BeTrue();
        var semLote = () => _estoque.EntrarAsync(item.Id, 1, data: _dia, validade: _dia.AddDays(10));
        await semLote.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lote*");
        var semValidade = () => _estoque.EntrarAsync(item.Id, 1, data: _dia, lote: "A");
        await semValidade.Should().ThrowAsync<InvalidOperationException>().WithMessage("*validade*");
        await _estoque.EntrarAsync(item.Id, 1, data: _dia, lote: "A", validade: _dia.AddDays(10));
        (await _estoque.SaldosAsync()).Single().Saldo.Should().Be(1);
    }

    [Fact]
    public async Task Rotina_exige_setor_e_nao_vira_custo_de_sessao()
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Produto de limpeza", Grupo = GrupoEstoque.HigieneLimpeza, Uso = UsoEstoque.Rotina });
        await _estoque.EntrarAsync(item.Id, 10, 5, _dia);
        var dados = new MovimentoEstoque { ItemEstoqueId = item.Id, Tipo = TipoMovimentoEstoque.Saida, Quantidade = 2, Data = _dia, DestinoConsumo = DestinoConsumoEstoque.Rotina };
        var semSetor = () => _estoque.MovimentarAsync(dados);
        await semSetor.Should().ThrowAsync<InvalidOperationException>().WithMessage("*setor*");
        dados.SetorDestino = "Higienização";
        var movimento = await _estoque.MovimentarAsync(dados, "Responsável");
        movimento.SetorDestino.Should().Be("Higienização");
        movimento.AtendimentoId.Should().BeNull();
        movimento.CriadoPor.Should().Be("Responsável");
        (await _estoque.CustosDeSessaoAsync(_dia, _dia)).Should().BeEmpty();
        (await _estoque.SaldosAsync()).Single().Saldo.Should().Be(8);
    }

    [Fact]
    public async Task Material_exclusivo_de_procedimento_nao_aceita_baixa_de_rotina()
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Material clínico" });
        await _estoque.EntrarAsync(item.Id, 2, data: _dia);
        var baixar = () => _estoque.MovimentarAsync(new MovimentoEstoque { ItemEstoqueId = item.Id, Tipo = TipoMovimentoEstoque.Saida,
            Quantidade = 1, Data = _dia, DestinoConsumo = DestinoConsumoEstoque.Rotina, SetorDestino = "Recepção" });
        await baixar.Should().ThrowAsync<InvalidOperationException>();
        (await _estoque.SaldosAsync()).Single().Saldo.Should().Be(2);
    }

    [Fact]
    public async Task Conversao_nao_arredonda_saldo_silenciosamente()
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Fracionado", Unidade = "ml", UnidadeCompra = "fr", FatorCompra = 1.001m });
        var entrar = () => _estoque.MovimentarAsync(new MovimentoEstoque { ItemEstoqueId = item.Id, Tipo = TipoMovimentoEstoque.Entrada, Quantidade = 0.001m, Data = _dia }, emUnidadeCompra: true);
        await entrar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*conversão*");
        (await _estoque.MovimentosAsync(item.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Falha_na_conta_desfaz_entrada_e_historico_da_compra()
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Material", UnidadeCompra = "cx", FatorCompra = 20 });
        var comprar = () => _estoque.ComprarAsync(new MovimentoEstoque { ItemEstoqueId = item.Id,
            Tipo = TipoMovimentoEstoque.Entrada, Quantidade = 1, CustoUnitario = 0.0001m, Data = _dia },
            "Fornecedor", _dia, pago: true, forma: FormaPagamento.Dinheiro, emUnidadeCompra: true);
        await comprar.Should().ThrowAsync<ArgumentException>();
        (await _estoque.MovimentosAsync(item.Id)).Should().BeEmpty();
        (await _db.Set<LancamentoFinanceiro>().CountAsync()).Should().Be(0);
    }

    private async Task<Atendimento> AtendimentoAsync()
    {
        var paciente = new Paciente { Nome = "Paciente de teste" };
        _db.Add(paciente);
        var atendimento = new Atendimento { Paciente = paciente, Data = _dia };
        _db.Add(atendimento); await _db.SaveChangesAsync();
        return atendimento;
    }

    [Fact]
    public async Task Confirmacao_sem_material_exige_declaracao_explicita()
    {
        var atendimento = await AtendimentoAsync();
        var vazio = () => _estoque.ConfirmarConsumoProcedimentoAsync(atendimento.Id, new([]), "Médico");
        await vazio.Should().ThrowAsync<InvalidOperationException>().WithMessage("*explicitamente*");
        var conferencia = await _estoque.ConfirmarConsumoProcedimentoAsync(atendimento.Id, new([], true), "Médico");
        conferencia.SemConsumo.Should().BeTrue();
        conferencia.ConferidoPor.Should().Be("Médico");
        (await _estoque.ConferenciaDoProcedimentoAsync(atendimento.Id))!.Id.Should().Be(conferencia.Id);
    }

    [Fact]
    public async Task Falta_de_saldo_registra_relato_pendente_sem_baixa_parcial()
    {
        var atendimento = await AtendimentoAsync();
        var a = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "A" });
        var b = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "B" });
        await _estoque.EntrarAsync(a.Id, 10, data: _dia);
        var registro = await _estoque.ConfirmarConsumoProcedimentoAsync(atendimento.Id,
            new([new(a.Id, 2), new(b.Id, 1)]), "Médico");
        registro.BaixadoEm.Should().BeNull();
        registro.MotivoPendencia.Should().Contain("Saldo");
        (await _estoque.SaldosAsync()).Single(i => i.ItemId == a.Id).Saldo.Should().Be(10);
        EstoqueService.MateriaisRegistrados((await _estoque.ConferenciaDoProcedimentoAsync(atendimento.Id))!).Should().HaveCount(2);
        await _estoque.EntrarAsync(b.Id, 5, data: _dia);
        var retomado = await _estoque.ConfirmarConsumoProcedimentoAsync(atendimento.Id,
            new([new(a.Id, 2), new(b.Id, 1)]), "Gestão");
        retomado.BaixadoEm.Should().NotBeNull();
        (await _estoque.SaldosAsync()).Single(i => i.ItemId == a.Id).Saldo.Should().Be(8);
        (await _estoque.SaldosAsync()).Single(i => i.ItemId == b.Id).Saldo.Should().Be(4);
    }

    [Fact]
    public async Task Confirmacao_identica_e_recepcao_nao_repetem_consumo()
    {
        var atendimento = await AtendimentoAsync();
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Material", ExigirLote = true });
        await _estoque.EntrarAsync(item.Id, 10, 2, _dia, lote: "A");
        var pedido = new PedidoConsumoProcedimento([new(item.Id, 2, "A")]);
        var primeira = await _estoque.ConfirmarConsumoProcedimentoAsync(atendimento.Id, pedido, "Médico");
        (await _estoque.ConfirmarConsumoProcedimentoAsync(atendimento.Id, pedido, "Médico")).Id.Should().Be(primeira.Id);
        var repetir = () => _estoque.BaixarAsync(item.Id, 2, atendimento.Id, atendimento.PacienteId, _dia);
        await repetir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*já foram conferidos*");
        (await _estoque.SaldosAsync()).Single().Saldo.Should().Be(8);
        (await _estoque.CustoDoAtendimentoAsync(atendimento.Id)).Custo.Should().Be(4);
    }

    [Fact]
    public async Task Baixa_antiga_pode_ser_conferida_sem_ser_descontada_de_novo()
    {
        var atendimento = await AtendimentoAsync();
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Material" });
        await _estoque.EntrarAsync(item.Id, 10, 2, _dia);
        await _estoque.BaixarAsync(item.Id, 2, atendimento.Id, atendimento.PacienteId, _dia);
        await _estoque.ConfirmarConsumoProcedimentoAsync(atendimento.Id, new([new(item.Id, 2)]), "Médico");
        (await _estoque.SaldosAsync()).Single().Saldo.Should().Be(8);
        (await _estoque.MovimentosAsync(item.Id)).Count.Should().Be(2);
    }

    [Fact]
    public async Task Procedimento_exige_lote_quando_produto_e_rastreado()
    {
        var atendimento = await AtendimentoAsync();
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Rastreado", ExigirLote = true });
        await _estoque.EntrarAsync(item.Id, 10, 2, _dia, lote: "A");
        var gravar = () => _estoque.ConfirmarConsumoProcedimentoAsync(atendimento.Id, new([new(item.Id, 2)]), "Médico");
        await gravar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lote*");
        (await _estoque.SaldosAsync()).Single().Saldo.Should().Be(10);
    }
}
