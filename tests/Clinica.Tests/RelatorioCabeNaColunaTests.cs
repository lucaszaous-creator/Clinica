using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O QUE O RELATÓRIO DE EVOLUÇÃO ESCREVE CABE NA COLUNA QUE O RECEBE (set/2026 — a clínica
/// levou <i>"Não foi possível gravar. O banco respondeu: 22001: value too long for type
/// character varying(1000)"</i> ao imprimir a ficha de uma sessão).
///
/// ⚠️ ESTES TESTES NÃO PODIAM EXISTIR EM SQLITE DO JEITO ÓBVIO: o SQLite <b>ignora</b> o
/// tamanho declarado de uma coluna de texto, então gravar não falha aqui — falha na
/// clínica, que roda Postgres. É a mesma família do <c>xmin</c> e das datas com fuso
/// (parcela 67): <i>"só o Postgres pega" quer dizer "só a clínica pega"</i>.
///
/// A saída é a mesma daquela rodada: <b>medir o que o serviço PRODUZ contra o teto que o
/// MODELO do EF declara</b>. O teto é lido do modelo, nunca escrito à mão — quem um dia
/// reapertar a coluna faz este teste falhar no mesmo commit.
///
/// A CAUSA é a cópia que ficou para trás: a importação do Smart Clinic alargou os quatro
/// textos longos da <see cref="Evolucao"/> para <c>text</c> (há 88 registros acima de 4.000
/// caracteres, o maior com 11.221) e <see cref="ItemDocumento.Detalhe"/>, que recebe NOVE
/// deles concatenados, ficou em <c>varchar(1000)</c>.
/// </summary>
public class RelatorioCabeNaColunaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly DocumentoClinicoService _documentos;
    private readonly DocumentosClinicosPdfService _pdfs;

    private static readonly DateOnly Dia = new(2026, 8, 12);

    public RelatorioCabeNaColunaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _documentos = new DocumentoClinicoService(_repo, _prontuario, new ConsentimentoService(_repo));
        _pdfs = new DocumentosClinicosPdfService(_repo);
    }

    /// <summary>O teto declarado de uma coluna QUALQUER, lido do modelo do EF.</summary>
    private int? TetoDe(Type entidade, string propriedade)
        => _db.Model.FindEntityType(entidade)!.FindProperty(propriedade)!.GetMaxLength();

    /// <summary>Um texto exatamente do tamanho que a coluna de ORIGEM aceita.</summary>
    private string Cheio(Type entidade, string propriedade, char letra)
        => new(letra, TetoDe(entidade, propriedade)
            ?? throw new InvalidOperationException(
                $"{entidade.Name}.{propriedade} não tem teto — o teste precisa de outro limite"));

    /// <summary>
    /// O teto DECLARADO da coluna, lido do modelo do EF. Nulo quer dizer "sem teto"
    /// (<c>text</c>), que é a resposta certa para texto clínico de tamanho imprevisível.
    /// </summary>
    private int? TetoDe(string propriedade)
        => _db.Model.FindEntityType(typeof(ItemDocumento))!
            .FindProperty(propriedade)!.GetMaxLength();

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private void Cabe(string? valor, string propriedade)
    {
        if (valor is null) return;
        if (TetoDe(propriedade) is not { } teto) return;

        valor.Length.Should().BeLessThanOrEqualTo(teto,
            $"o Postgres RECUSA a gravação inteira (22001) quando `ItemDocumento.{propriedade}` "
            + $"passa de {teto} — e quem vê é quem clicou em Imprimir esta sessão");
    }

    /// <summary>
    /// ⚠️ O CASO DA CLÍNICA: a sessão importada do sistema antigo. O prontuário do Smart
    /// Clinic vem em texto corrido, e uma consulta de verdade passa de 1.000 caracteres com
    /// folga — a evolução sozinha já é `text` justamente por isso.
    /// </summary>
    [Fact]
    public async Task Sessao_com_texto_longo_cabe_no_Detalhe_do_item()
    {
        var pacienteId = await PacienteAsync();

        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = Dia,
            QueixaPrincipal = new string('q', 900),
            // Os quatro `text` da entidade: é deles que vêm os 11.221 caracteres do maior
            // registro importado.
            HistoriaDoencaAtual = new string('h', 3000),
            ExameFisico = new string('e', 2000),
            Conduta = new string('c', 1500),
            TextoEvolucao = new string('t', 2500)
        }, "dra. ana");

        var doc = await _documentos.EmitirRelatorioEvolucaoAsync(
            pacienteId, inicio: Dia, fim: Dia, operador: "dra. ana");

        doc.Itens.Should().HaveCount(1);
        Cabe(doc.Itens[0].Detalhe, nameof(ItemDocumento.Detalhe));
    }

    /// <summary>
    /// Mesmo SEM nenhum campo `text`, os limitados já somam mais que mil: queixa (1000) +
    /// hipótese (1000) + plano (1000) + encaminhamento (600). O estouro não depende da
    /// importação — depende de alguém escrever a sessão inteira.
    /// </summary>
    [Fact]
    public async Task So_os_campos_com_teto_ja_passam_de_mil()
    {
        var pacienteId = await PacienteAsync();

        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = Dia,
            QueixaPrincipal = new string('q', 1000),
            HipoteseDiagnostica = new string('d', 1000),
            PlanoTerapeutico = new string('p', 1000),
            Encaminhamento = new string('n', 600)
        }, "dra. ana");

        var doc = await _documentos.EmitirRelatorioEvolucaoAsync(
            pacienteId, inicio: Dia, fim: Dia, operador: "dra. ana");

        Cabe(doc.Itens[0].Detalhe, nameof(ItemDocumento.Detalhe));
    }

    /// <summary>
    /// A PASSAGEM DE ENFERMAGEM entra no mesmo papel (parcela 78), e o <c>Texto</c> dela
    /// sozinho é `varchar(4000)`.
    /// </summary>
    [Fact]
    public async Task Passagem_de_enfermagem_longa_cabe_no_Detalhe()
    {
        var pacienteId = await PacienteAsync();

        _db.EvolucoesEnfermagem.Add(new EvolucaoEnfermagem
        {
            PacienteId = pacienteId,
            Data = Dia,
            Hora = new TimeOnly(14, 20),
            Texto = new string('x', 3500),
            AutorNome = "Joana Técnica",
            AutorConselho = "COREN-SP 999999"
        });
        await _db.SaveChangesAsync();

        var doc = await _documentos.EmitirRelatorioEvolucaoAsync(
            pacienteId, inicio: Dia, fim: Dia, operador: "joana");

        Cabe(doc.Itens[0].Detalhe, nameof(ItemDocumento.Detalhe));
    }

    /// <summary>
    /// ⚠️ O SEGUNDO ESTOURO DA MESMA LINHA, e ele não tem nada a ver com o prontuário: a
    /// coluna <c>Quantidade</c> nasceu para "1 caixa" (60) e recebe QUEM ASSINA — o
    /// <c>Profissional.Rotulo</c>, que é o Nome, de até 120. Nome comprido de brasileiro
    /// passa de 60 sem esforço nenhum.
    /// </summary>
    [Fact]
    public async Task Nome_comprido_de_profissional_cabe_na_Quantidade()
    {
        var pacienteId = await PacienteAsync();

        // ⚠️ O tamanho sai do teto que o MODELO declara para `Profissional.Nome`, nunca
        // de um número escrito aqui: alargar a origem sem alargar o destino é exatamente
        // o defeito que este arquivo existe para pegar, e um literal deixaria o teste
        // verde no commit que o recria.
        var prof = new Profissional { Nome = Cheio(typeof(Profissional), "Nome", 'N') };
        _db.Profissionais.Add(prof);
        await _db.SaveChangesAsync();

        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            ProfissionalId = prof.Id,
            Data = Dia,
            QueixaPrincipal = "dor lombar"
        }, "dra. ana");

        var doc = await _documentos.EmitirRelatorioEvolucaoAsync(
            pacienteId, inicio: Dia, fim: Dia, operador: "dra. ana");

        Cabe(doc.Itens[0].Quantidade, nameof(ItemDocumento.Quantidade));
    }

    /// <summary>
    /// E o mesmo pela enfermagem, onde o valor é <c>Nome (COREN)</c> — 120 + 60 mais os
    /// parênteses. É este par que fixa o teto de 200 da coluna.
    /// </summary>
    [Fact]
    public async Task Autor_de_enfermagem_com_nome_e_conselho_cabe_na_Quantidade()
    {
        var pacienteId = await PacienteAsync();

        _db.EvolucoesEnfermagem.Add(new EvolucaoEnfermagem
        {
            PacienteId = pacienteId,
            Data = Dia,
            Hora = new TimeOnly(9, 5),
            Texto = "sinais vitais aferidos",
            AutorNome = Cheio(typeof(EvolucaoEnfermagem), "AutorNome", 'N'),
            AutorConselho = Cheio(typeof(EvolucaoEnfermagem), "AutorConselho", 'C')
        });
        await _db.SaveChangesAsync();

        var doc = await _documentos.EmitirRelatorioEvolucaoAsync(
            pacienteId, inicio: Dia, fim: Dia, operador: "joana");

        Cabe(doc.Itens[0].Quantidade, nameof(ItemDocumento.Quantidade));
    }

    /// <summary>
    /// A ANAMNESE monta o <c>Detalhe</c> com o que o prontuário já sabe — e a história da
    /// doença atual dela sai do MESMO campo `text` da sessão.
    /// </summary>
    [Fact]
    public async Task Anamnese_com_historia_longa_cabe_no_Detalhe()
    {
        var pacienteId = await PacienteAsync();

        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = Dia,
            QueixaPrincipal = "dor lombar",
            HistoriaDoencaAtual = new string('h', 4000)
        }, "dra. ana");

        var doc = await _documentos.EmitirAnamneseAsync(pacienteId, operador: "dra. ana");

        foreach (var item in doc.Itens)
            Cabe(item.Detalhe, nameof(ItemDocumento.Detalhe));
    }

    /// <summary>
    /// ⚠️ ALARGAR A COLUNA NÃO PODE SÓ MUDAR O LUGAR DA FALHA. O `Detalhe` do relatório é
    /// desenhado numa CÉLULA de tabela, e a lição da parcela 68 vale aqui inteira: teste
    /// que prova que a gravação passa não prova que a FOLHA SAI. O tamanho é o do maior
    /// registro que a importação do Smart Clinic trouxe — 11.221 caracteres.
    /// </summary>
    [Fact]
    public async Task A_folha_da_sessao_mais_longa_da_importacao_SAI()
    {
        var pacienteId = await PacienteAsync();

        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = Dia,
            EvaAntes = 8,
            EvaDepois = 3,
            QueixaPrincipal = "dor lombar",
            // Palavras de verdade: uma parede de 11 mil letras sem espaço não quebra linha
            // e mediria outra coisa — o que a clínica tem é texto corrido.
            TextoEvolucao = string.Join(' ',
                Enumerable.Repeat("evolução do paciente conforme registro importado", 1200))[..11221]
        }, "dra. ana");

        var doc = await _documentos.EmitirRelatorioEvolucaoAsync(
            pacienteId, inicio: Dia, fim: Dia, operador: "dra. ana");

        var pdf = _pdfs.Gerar((await _documentos.ObterAsync(doc.Id))!);

        pdf.Should().NotBeNull();
        pdf.Length.Should().BeGreaterThan(2000,
            "a folha com 11 mil caracteres de evolução tem de sair — alargar a coluna não "
            + "pode ter só empurrado a falha da gravação para o desenho da tabela");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
