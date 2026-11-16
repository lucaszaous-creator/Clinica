using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A consulta de elegibilidade a partir da agenda (nov/2026): quem dá para consultar no dia, por
/// que os outros não, o sinal de quem já foi consultado hoje e a busca pelo CPF. Banco SQLite em
/// memória, com dados FICTÍCIOS; nada vai à operadora (a consulta em si é a fila, já testada).
/// </summary>
public sealed class ElegibilidadeNaAgendaServiceTests : IDisposable
{
    private const string Petrobras = "Petrobras";
    private const string Amil = "Amil";
    private const string Unimed = "UnimedPadrao";
    private static readonly DateOnly Dia = new(2026, 11, 10);

    private readonly TissBancoDeTeste _b = new();

    public void Dispose() => _b.Dispose();

    private ElegibilidadeNaAgendaService Servico() => new(_b.Repo, _b.Credenciais(), () => _b.Agora);

    private async Task ConveniosAsync(bool credencialPetrobras = true, bool credencialAmil = false)
    {
        _b.Db.Convenios.AddRange(
            new ConvenioCadastro { Codigo = Petrobras, Nome = "Saúde Petrobras", Familia = Convenio.Petrobras },
            new ConvenioCadastro { Codigo = Amil, Nome = "Amil", Familia = Convenio.Amil },
            new ConvenioCadastro { Codigo = Unimed, Nome = "Unimed", Familia = Convenio.UnimedPadrao });
        await _b.Db.SaveChangesAsync();
        if (credencialPetrobras) await _b.Credenciais().SalvarAsync(Petrobras, "login.teste", "senha-ficticia", TissBancoDeTeste.Operador);
        if (credencialAmil) await _b.Credenciais().SalvarAsync(Amil, "000123", "senha-ficticia", TissBancoDeTeste.Operador);
    }

    private async Task<Paciente> PacienteAsync(string nome, Convenio familia, string codigo, string? carteira, string? cpf = null)
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Carteirinha = carteira,
            ValidadeCarteirinha = carteira is null ? null : new DateOnly(2027, 12, 31), Documento = cpf
        };
        _b.Db.Pacientes.Add(p);
        await _b.Db.SaveChangesAsync();
        return p;
    }

    private async Task<Agendamento> MarcarAsync(Paciente p, int hora, bool particular = false, StatusAgendamento status = StatusAgendamento.Agendado)
    {
        var a = new Agendamento
        {
            PacienteId = p.Id, DataHora = Dia.ToDateTime(new TimeOnly(hora, 0)), ModalidadePrevista = ModalidadeAtendimento.Consulta,
            Particular = particular, Status = status, CriadoPor = "recepcao.teste"
        };
        _b.Db.Agendamentos.Add(a);
        await _b.Db.SaveChangesAsync();
        return a;
    }

    // ---------- os convênios consultáveis ----------

    [Fact]
    public async Task So_entra_convenio_com_elegibilidade_no_catalogo_e_credencial_gravada()
    {
        await ConveniosAsync(credencialPetrobras: true, credencialAmil: false);

        (await Servico().ConveniosComConsultaAsync()).Select(c => c.Codigo).Should().Equal(Petrobras);

        await _b.Credenciais().SalvarAsync(Amil, "000123", "senha-ficticia", TissBancoDeTeste.Operador);
        (await Servico().ConveniosComConsultaAsync()).Select(c => c.Codigo).Should().Equal(Amil, Petrobras);

        // Unimed não está no catálogo: nem com credencial gravada ela entra.
        await _b.Credenciais().SalvarAsync(Unimed, "x", "y", TissBancoDeTeste.Operador);
        (await Servico().ConveniosComConsultaAsync()).Select(c => c.Codigo).Should().NotContain(Unimed);
    }

    [Fact]
    public async Task Convenio_inativo_nao_entra()
    {
        await ConveniosAsync();
        _b.Db.Convenios.Single(c => c.Codigo == Petrobras).Ativo = false;
        await _b.Db.SaveChangesAsync();

        (await Servico().ConveniosComConsultaAsync()).Should().BeEmpty();
    }

    // ---------- a agenda do dia ----------

    [Fact]
    public async Task A_agenda_do_dia_traz_quem_da_para_consultar_e_o_motivo_dos_outros()
    {
        await ConveniosAsync(credencialPetrobras: true, credencialAmil: false);
        var ana = await PacienteAsync("Ana Teste", Convenio.Petrobras, Petrobras, "PB0001 23");
        var bia = await PacienteAsync("Bia Teste", Convenio.Petrobras, Petrobras, null);
        var caio = await PacienteAsync("Caio Teste", Convenio.Amil, Amil, "AM0001");
        var davi = await PacienteAsync("Davi Teste", Convenio.UnimedPadrao, Unimed, "UN0001");
        var eva = await PacienteAsync("Eva Teste", Convenio.Petrobras, Petrobras, "PB0002");
        var fabio = await PacienteAsync("Fábio Teste", Convenio.Petrobras, Petrobras, "PB0003");
        await MarcarAsync(ana, 8);
        await MarcarAsync(bia, 9);
        await MarcarAsync(caio, 10);
        await MarcarAsync(davi, 11);
        await MarcarAsync(eva, 12, particular: true);
        await MarcarAsync(fabio, 13, status: StatusAgendamento.Cancelado);

        var painel = await Servico().PrepararAsync(Dia);

        painel.Convenios.Select(c => c.Codigo).Should().Equal(Petrobras);
        painel.Pacientes.Select(p => p.Paciente).Should().Equal("Ana Teste", "Bia Teste", "Caio Teste", "Davi Teste", "Eva Teste");
        var porNome = painel.Pacientes.ToDictionary(p => p.Paciente);
        porNome["Ana Teste"].Impedimento.Should().BeNull();
        porNome["Ana Teste"].Carteira.Should().Be("PB000123", "os espaços da carteirinha saem, como no pedido da ficha");
        porNome["Ana Teste"].ConvenioCodigo.Should().Be(Petrobras);
        porNome["Bia Teste"].Impedimento.Should().Be(ElegibilidadeNaAgendaService.SemCarteirinha);
        porNome["Caio Teste"].Impedimento.Should().Be(ElegibilidadeNaAgendaService.SemConsultaAutomatica, "a Amil sem credencial não consulta");
        porNome["Davi Teste"].Impedimento.Should().Be(ElegibilidadeNaAgendaService.SemConsultaAutomatica);
        porNome["Eva Teste"].Impedimento.Should().Be(ElegibilidadeNaAgendaService.Particular);
    }

    [Fact]
    public async Task O_agendamento_consultado_hoje_tem_o_sinal_e_nao_sei_nunca_vira_nao_elegivel()
    {
        await ConveniosAsync();
        var ana = await PacienteAsync("Ana Teste", Convenio.Petrobras, Petrobras, "PB0001");
        var bia = await PacienteAsync("Bia Teste", Convenio.Petrobras, Petrobras, "PB0002");
        var caio = await PacienteAsync("Caio Teste", Convenio.Petrobras, Petrobras, "PB0003");
        var davi = await PacienteAsync("Davi Teste", Convenio.Petrobras, Petrobras, "PB0004");
        var eva = await PacienteAsync("Eva Teste", Convenio.Petrobras, Petrobras, "PB0005");
        foreach (var (p, h) in new[] { (ana, 8), (bia, 9), (caio, 10), (davi, 11), (eva, 12) }) await MarcarAsync(p, h);

        var fila = _b.Fila();
        // O papel do conector: pede, reserva e grava o desfecho (cada pedido é reservado logo após entrar).
        async Task<TransacaoTiss> Pedir(string carteira) => await fila.EnfileirarElegibilidadeAsync(Petrobras, carteira, null, null, TissBancoDeTeste.Operador);
        async Task<int> PedirEReservar(string carteira) { await Pedir(carteira); return (await fila.ReservarProximaAsync("conector-de-teste"))!.Id; }
        await fila.ConcluirAsync(await PedirEReservar("PB0001"), new ConclusaoTransacaoTiss { Resultado = "Elegível." });
        await fila.ConcluirAsync(await PedirEReservar("PB0002"), new ConclusaoTransacaoTiss { Resultado = "Não elegível: 1016 - BENEFICIARIO SUSPENSO" });
        await fila.RecusarAsync(await PedirEReservar("PB0003"), "sem resposta da operadora (rede)");
        await Pedir("PB0004"); // continua na fila

        var painel = (await Servico().PrepararAsync(Dia)).Pacientes.ToDictionary(p => p.Paciente);

        painel["Ana Teste"].ConsultaDeHoje.Should().Be(new SeloDaConsultaDeHoje("Elegível hoje", "sucesso"));
        painel["Bia Teste"].ConsultaDeHoje.Should().Be(new SeloDaConsultaDeHoje("Não elegível hoje", "perigo"));
        painel["Caio Teste"].ConsultaDeHoje.Should().Be(new SeloDaConsultaDeHoje("Sem resposta hoje", "aviso"), "falha não é não elegível");
        painel["Davi Teste"].ConsultaDeHoje.Should().Be(new SeloDaConsultaDeHoje("Na fila", "info"));
        painel["Eva Teste"].ConsultaDeHoje.Should().BeNull("ninguém consultou");
    }

    [Fact]
    public async Task Consulta_de_ontem_nao_marca_o_agendamento_de_hoje()
    {
        await ConveniosAsync();
        var ana = await PacienteAsync("Ana Teste", Convenio.Petrobras, Petrobras, "PB0001");
        await MarcarAsync(ana, 8);
        var ontem = _b.Agora;
        _b.Agora = ontem.AddDays(-1);
        await _b.Fila().EnfileirarElegibilidadeAsync(Petrobras, "PB0001", null, null, TissBancoDeTeste.Operador);
        var t = (await _b.Fila().ReservarProximaAsync("conector-de-teste"))!;
        await _b.Fila().ConcluirAsync(t.Id, new ConclusaoTransacaoTiss { Resultado = "Elegível." });
        _b.Agora = ontem;

        (await Servico().PrepararAsync(Dia)).Pacientes.Single().ConsultaDeHoje.Should().BeNull();
    }

    [Fact]
    public void O_limite_do_dia_e_na_fila_e_nao_falha()
        => ElegibilidadeNaAgendaService.Selo(new ElegibilidadeNaTela(FilaTiss.FraseLimiteDiario, "atencao", true, true))
            .Should().Be(new SeloDaConsultaDeHoje("Na fila (limite do dia)", "aviso"));

    // ---------- o CPF ----------

    [Fact]
    public async Task O_CPF_acha_a_ficha_e_a_consulta_usa_a_carteirinha_dela()
    {
        await ConveniosAsync();
        var ana = await PacienteAsync("Ana Teste", Convenio.Petrobras, Petrobras, "PB0001", cpf: "529.982.247-25");

        var busca = await Servico().ProcurarPorCpfAsync("52998224725", Petrobras);

        busca.Frase.Should().BeNull();
        busca.Unico.Should().Be(new PacienteDoCpf(ana.Id, "Ana Teste", Petrobras, ana.ConvenioNome, "PB0001", new DateOnly(2027, 12, 31)));
    }

    [Theory]
    [InlineData("123", ElegibilidadeNaAgendaService.CpfInvalido)]
    [InlineData("111.111.111-11", ElegibilidadeNaAgendaService.CpfInvalido)]
    [InlineData("390.533.447-05", ElegibilidadeNaAgendaService.CpfNaoEncontrado)]
    public async Task CPF_invalido_ou_sem_ficha_diz_o_que_fazer(string cpf, string frase)
    {
        await ConveniosAsync();
        await PacienteAsync("Ana Teste", Convenio.Petrobras, Petrobras, "PB0001", cpf: "529.982.247-25");

        var busca = await Servico().ProcurarPorCpfAsync(cpf, Petrobras);

        busca.Unico.Should().BeNull();
        busca.Frase.Should().Be(frase);
    }

    [Fact]
    public async Task CPF_de_outro_convenio_sem_carteirinha_ou_em_duas_fichas_nao_consulta_e_diz_por_que()
    {
        await ConveniosAsync();
        await PacienteAsync("Ana Teste", Convenio.UnimedPadrao, Unimed, "UN0001", cpf: "529.982.247-25");
        await PacienteAsync("Bia Teste", Convenio.Petrobras, Petrobras, null, cpf: "390.533.447-05");
        await PacienteAsync("Caio Teste", Convenio.Petrobras, Petrobras, "PB0003", cpf: "153.509.460-56");
        await PacienteAsync("Caio Teste (duplicado)", Convenio.Petrobras, Petrobras, "PB0004", cpf: "153.509.460-56");

        var outro = await Servico().ProcurarPorCpfAsync("529.982.247-25", Petrobras);
        var semCarteirinha = await Servico().ProcurarPorCpfAsync("390.533.447-05", Petrobras);
        var duplicado = await Servico().ProcurarPorCpfAsync("153.509.460-56", Petrobras);

        outro.Unico.Should().BeNull();
        outro.Frase.Should().Contain("outro convênio");
        semCarteirinha.Unico.Should().BeNull();
        semCarteirinha.Frase.Should().Be(ElegibilidadeNaAgendaService.SemCarteirinha);
        duplicado.Unico.Should().BeNull();
        duplicado.Frase.Should().Contain("mais de um paciente");
    }

    [Fact]
    public void O_padrao_TISS_4_01_00_nao_tem_CPF_na_verificacao_de_elegibilidade()
    {
        // A razão de o CPF só apontar a ficha: ct_elegibilidadeVerifica pede a carteira e não tem CPF do beneficiário.
        var pasta = Path.Combine(AppContext.BaseDirectory, "Tiss", "xsd-4.01.00");
        var tipos = System.Xml.Linq.XDocument.Load(Path.Combine(pasta, "tissComplexTypesV4_01_00.xsd"));
        System.Xml.Linq.XNamespace xs = "http://www.w3.org/2001/XMLSchema";
        var verifica = tipos.Descendants(xs + "complexType").Single(t => (string?)t.Attribute("name") == "ct_elegibilidadeVerifica");
        var campos = verifica.Element(xs + "sequence")!.Elements(xs + "element").Select(e => (string?)e.Attribute("name")).ToList();

        campos.Should().Contain("numeroCarteira");
        campos.Should().NotContain(c => c != null && c.Contains("cpf", StringComparison.OrdinalIgnoreCase));
        ((string?)verifica.Element(xs + "sequence")!.Elements(xs + "element").Single(e => (string?)e.Attribute("name") == "numeroCarteira")
            .Attribute("minOccurs")).Should().BeNull("a carteira é obrigatória");
    }
}
