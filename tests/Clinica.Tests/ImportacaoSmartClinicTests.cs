using System.IO.Compression;
using System.Text;
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
/// A importação do PACOTE do Smart Clinic (set/2026): o ZIP inteiro — carteira, prontuário
/// em texto, agenda futura, colunas sem campo. Os arquivos sintéticos abaixo têm a FORMA
/// medida na exportação real (mesmos nomes de coluna, HTML nos textos, JSON no
/// dados_medicos e no formulário, datas "aaaa-mm-dd hh:mm:ss"); nenhum dado real.
/// </summary>
public class ImportacaoSmartClinicTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ImportacaoSmartClinicService _servico;
    private static readonly DateOnly Hoje = new(2026, 9, 2);

    public ImportacaoSmartClinicTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new ImportacaoSmartClinicService(_repo, new ImportacaoPacientesService(_repo, new PacienteService(_repo), new ConvenioCatalogoService(_repo)));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static readonly Dictionary<string, ConvenioCadastro> Convenios = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PARTICULAR"] = new() { Codigo = "Particular", Nome = "Particular", Familia = Convenio.Personalizado, GeraGuia = false },
        [ImportacaoPacientesService.ConvenioEmBranco] = new() { Codigo = "Particular", Nome = "Particular", Familia = Convenio.Personalizado, GeraGuia = false }
    };

    private const string DadosMedicos = "{\"nome\": \"Ana Autora\",\"id_contratante_usuario\": \"u1\",\"codigo_conselho_profissional\": \"1\",\"codigo_estado_conselho_profissional\": \"19\",\"numero_conselho_profissional\": \"123456\",\"sigla_estado\": \"RJ\",\"sigla_conselho_profissional\": \"CRM\",\"descricao_conselho_profissional\": \"Conselho Regional de Medicina\"}";

    private static string Pacientes =>
        "id_paciente;id_contratante;nome;data_nascimento;email;cpf;rg;telefone;celular;operadora;convenio;numero_convenio;validade_convenio;cep;endereco;numero;complemento;bairro;cidade;estado;profissao;sexo;estado_civil;indicacao;login;senha;obs;nome_mae;created_at\n"
        + "p1;c1;Paciente Um;1980-03-05;um@x.com;529.982.247-25;12345;;22999991111;;PARTICULAR;;;27900-000;Rua A;10;;Centro;Macaé;RJ;Professor;F;CA;;login1;senha1;;Mãe Um;2024-01-02 10:00:00\n"
        + "p2;c1;Paciente Dois;1990-07-08;;;;;22999992222;;;;;;;;;;;;;M;;;login2;senha2;Obs antiga;;\n"
        + "p3;c1;Paciente Tres;;;;;;;;PARTICULAR;;;;;;;;;;;;;;;;;;\n";

    private static string PosOperatorio =>
        "id_pos_operatorio;id_contratante_usuario;id_paciente;data;retorno;anamnese;exame_fisico;conduta;fl_status;data_criacao;data_prontuario;id_agenda;data_visualizacao;dados_medicos;id_migracao;url_certificado_digital\n"
        + $"po1;u1;p1;2025-02-10 14:00:00;;\"Paciente refere dor lombar&nbsp;h&aacute; 3 dias<br />\r\n<br />\r\nSem febre\";\"<p>Lombar: contratura</p>\";\"<ol><li>Repouso</li><li>Calor local</li></ol>\";PR;2025-02-10 14:05:00;2025-02-10 00:00:00;;;\"{DadosMedicos.Replace("\"", "\"\"")}\";;\n"
        + $"po2;u1;p1;2025-03-01 09:00:00;;\"{new string('x', 5000)}\";;;PR;2025-03-01 09:00:00;;;;\"{DadosMedicos.Replace("\"", "\"\"")}\";;\n"
        + "po3;u1;p2;2025-03-02 09:00:00;;;;;PR;2025-03-02 09:00:00;;;;;;\n"
        + "po4;u1;p9;2025-03-03 09:00:00;;Texto de paciente que não existe;;;PR;2025-03-03 09:00:00;;;;;;\n";

    private static string FichaSoap =>
        "id_ficha_soap;data;id_contratante_usuario;id_paciente;subjetivo;objetivo;exame;avaliacao;plano;fl_status;dados_medicos;id_migracao;url_certificado_digital\n"
        + "s1;2025-04-01 10:00:00;u2;p2;<p>Cefaleia</p>;<p>PA 120x80</p>;;Cefaleia tensional;<ul><li>Analgésico</li></ul>;PR;\"{\"\"nome\"\": \"\"Bruno Sem Cadastro\"\"}\";;\n";

    private static string FichaClinica =>
        "id_ficha_clinica;data;id_contratante_usuario;id_paciente;fl_mama_aumento;fl_has;fl_diabetes;desc_alergias;fl_alergias;medicamentos;achados;tratamento;fl_cir_lipo_peq;fl_status;data_criacao;data_prontuario;id_agenda;data_visualizacao;dados_medicos;id_migracao;url_certificado_digital\n"
        + "fc1;2025-05-01 10:00:00;u1;p1;1;1;0;Dipirona;1;Losartana 50mg;IMC 27;Dieta;1;PR;2025-05-01 10:00:00;;;;;;\n";

    private static string ConsultaMulti =>
        "id_consulta_mult;estrutura;versao;id_contratante_usuario;id_paciente;conteudo;fl_status;data_criacao;data_prontuario;titulo;historico;dados_medicos;id_migracao;url_certificado_digital\n"
        + "cm1;<div></div>;1.0;u1;p1;\"[{\"\"type\"\":\"\"textarea\"\",\"\"label\"\":\"\"Queixa\"\",\"\"name\"\":\"\"f1\"\",\"\"userData\"\":[\"\"Dor no joelho\nesquerdo\"\"]},{\"\"type\"\":\"\"text\"\",\"\"label\"\":\"\"Peso\"\",\"\"name\"\":\"\"f2\"\",\"\"userData\"\":[\"\"\"\"]}]\";PR;2025-06-01 10:00:00;2025-06-01 00:00:00;CONSULTA - BÁSICA;;;;\n";

    private static string Agenda =>
        "id_agenda;id_paciente;paciente;profissional;inicio;fim;procedimento;cirurgia\n"
        + "a1;p1;Paciente Um;Dra. Ana Autora;2025-02-10 14:00:00;2025-02-10 14:30:00;Consulta;\n"
        + "a2;p1;Paciente Um;Dra. Ana Autora;2026-09-15 09:00:00;2026-09-15 09:30:00;Retorno;\n"
        + "a3;p2;Paciente Dois;Carlos Sem Cadastro;2026-09-16 10:00:00;2026-09-16 10:20:00;Consulta;\n"
        + "a4;p2;Paciente Dois;Dra. Ana Autora;2024-01-05 08:00:00;2024-01-05 08:30:00;Consulta;Retoque\n";

    private static byte[] Zip(params (string Nome, string Conteudo)[] arquivos)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (nome, conteudo) in arquivos)
            {
                var entrada = zip.CreateEntry(nome);
                using var w = entrada.Open();
                var bytes = Encoding.UTF8.GetBytes(conteudo);
                w.Write(bytes, 0, bytes.Length);
            }
        return memoria.ToArray();
    }

    private static PacoteSmartClinic PacoteCompleto() => PacoteSmartClinic.Abrir(Zip(
        (PacoteSmartClinic.Pacientes, Pacientes),
        (PacoteSmartClinic.PosOperatorio, PosOperatorio),
        (PacoteSmartClinic.FichaSoap, FichaSoap),
        (PacoteSmartClinic.FichaClinica, FichaClinica),
        (PacoteSmartClinic.ConsultaMulti, ConsultaMulti),
        (PacoteSmartClinic.Agenda, Agenda),
        ("contratante_usuario.csv", "id_contratante_usuario;nome;senha\nu1;Ana Autora;x\n"),
        ("leiame.txt", "não é csv")));

    private async Task<int> ProfissionalAsync(string nome)
    {
        var p = new Profissional { Nome = nome };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    // ------------------------------------------------------------------ o ZIP

    [Fact]
    public void Abre_o_zip_e_ignora_o_que_nao_e_csv()
    {
        var pacote = PacoteCompleto();
        pacote.Tem(PacoteSmartClinic.Pacientes).Should().BeTrue();
        pacote.Tabela(PacoteSmartClinic.PosOperatorio)!.Linhas.Should().HaveCount(4);
        pacote.Ignorados.Should().ContainKey("leiame.txt");
    }

    [Fact]
    public void Zip_sem_pacientes_e_recusado()
    {
        var acao = () => PacoteSmartClinic.Abrir(Zip((PacoteSmartClinic.Agenda, Agenda)));
        acao.Should().Throw<ArgumentException>().WithMessage("*pacientes.csv*");
    }

    // ------------------------------------------------------------------ conversões puras

    [Fact]
    public void Html_vira_texto_sem_perder_conteudo()
    {
        HtmlParaTexto.Converter("Dor&nbsp;h&aacute; 3 dias<br />\r\n<br />\r\nSem febre")
            .Should().Be("Dor há 3 dias\n\nSem febre");
        HtmlParaTexto.Converter("<p><b>Conduta</b></p><ol>\n\t<li>Repouso</li>\n\t<li>Calor</li></ol>")
            .Should().Be("Conduta\n\n• Repouso\n• Calor", "parágrafo e lista ficam separados por linha em branco");
        HtmlParaTexto.Converter("texto  simples").Should().Be("texto simples");
        HtmlParaTexto.Converter("   ").Should().BeNull();
        HtmlParaTexto.Converter("<script>x()</script>ok").Should().Be("ok");
    }

    [Fact]
    public void Formulario_da_consulta_vira_rotulo_e_resposta_e_pula_o_nao_respondido()
    {
        var json = "[{\"label\":\"Queixa\",\"userData\":[\"Dor no joelho\nesquerdo\"]},{\"label\":\"Peso\",\"userData\":[\"\"]},{\"label\":\"Sem userData\"}]";
        ComposicaoSmartClinic.TextoDoFormulario(json).Should().Be("Queixa: Dor no joelho esquerdo".Replace(" esquerdo", "\nesquerdo"));
        ComposicaoSmartClinic.TextoDoFormulario("[nao e json").Should().BeNull("JSON ilegível não inventa texto");
    }

    [Fact]
    public void Autor_sai_do_dados_medicos_com_o_conselho()
    {
        var indice = RegistroCsv.Indice(LeitorCsv.Ler("dados_medicos\n\"" + DadosMedicos.Replace("\"", "\"\"") + "\"\n"));
        var autor = ComposicaoSmartClinic.Autor(new RegistroCsv(indice, ["" + DadosMedicos]));
        autor.Should().NotBeNull();
        autor!.Nome.Should().Be("Ana Autora");
        autor.Conselho.Should().Be("CRM 123456/RJ");
        autor.Rotulo.Should().Be("Ana Autora (CRM 123456/RJ)");
    }

    [Fact]
    public void Ficha_clinica_vira_anamnese_com_os_marcados_e_os_textos()
    {
        var tabela = LeitorCsv.Ler(FichaClinica);
        var r = new RegistroCsv(RegistroCsv.Indice(tabela), tabela.Linhas[0]);
        var e = ComposicaoSmartClinic.Compor(PacoteSmartClinic.FichaClinica, r)!;
        e.HistoriaDoencaAtual.Should().Contain("Marcados na ficha: Mama aumento; HAS; Alergias; Cirurgia lipoaspiração pequena.");
        e.HistoriaDoencaAtual.Should().Contain("Alergias: Dipirona").And.Contain("Medicamentos: Losartana 50mg");
        e.ExameFisico.Should().Be("IMC 27");
        e.Conduta.Should().Be("Dieta");
        e.Data.Should().Be(new DateOnly(2025, 5, 1));
    }

    [Fact]
    public void Rotulo_de_flag_traduz_as_abreviaturas()
    {
        ComposicaoSmartClinic.RotuloDeFlag("fl_cir_lipo_gde").Should().Be("Cirurgia lipoaspiração grande");
        ComposicaoSmartClinic.RotuloDeFlag("fl_hipertensao_familiar").Should().Be("Hipertensão (familiar)");
        ComposicaoSmartClinic.RotuloDeFlag("desc_cabeca_pescoco").Should().Be("Cabeça pescoço");
        ComposicaoSmartClinic.RotuloDeFlag("fl_avc").Should().Be("AVC");
    }

    [Fact]
    public void Casa_o_autor_com_o_profissional_daqui_pelo_nome()
    {
        var lista = new List<Profissional> { new() { Id = 1, Nome = "Ana Autora da Silva" }, new() { Id = 2, Nome = "Zé" } };
        ImportacaoSmartClinicService.Casar(lista, "Dra. Ana Autora")!.Id.Should().Be(1);
        ImportacaoSmartClinicService.Casar(lista, "Carlos Outro").Should().BeNull();
        ImportacaoSmartClinicService.Casar(lista, "Zé").Should().NotBeNull("igual depois de normalizar");
        ImportacaoSmartClinicService.Casar(lista, "Ana").Should().BeNull("três letras contidas não identificam ninguém");
    }

    // ------------------------------------------------------------------ prévia e execução

    [Fact]
    public async Task Previa_do_pacote_diz_o_destino_de_cada_arquivo_sem_gravar()
    {
        await ProfissionalAsync("Ana Autora");
        var previa = await _servico.PreverAsync(PacoteCompleto(), Convenios, Hoje);

        previa.Pacientes.Criar.Should().Be(3);
        var posOp = previa.Prontuario.Single(p => p.Arquivo == PacoteSmartClinic.PosOperatorio);
        posOp.Registros.Should().Be(4);
        posOp.Novos.Should().Be(2, "po1 e po2");
        posOp.Vazios.Should().Be(1, "po3 não tem texto nenhum");
        posOp.SemPaciente.Should().Be(1, "po4 aponta para paciente que não está no pacientes.csv");
        previa.Prontuario.Single(p => p.Arquivo == PacoteSmartClinic.FichaSoap).Novos.Should().Be(1);
        previa.Prontuario.Single(p => p.Arquivo == PacoteSmartClinic.ConsultaMulti).Novos.Should().Be(1);

        previa.Agenda.Futuros.Should().Be(2);
        previa.Agenda.Passados.Should().Be(2);
        previa.Agenda.ProfissionaisReconhecidos.Should().Contain("Dra. Ana Autora");
        previa.Agenda.ProfissionaisSemCadastro.Should().Contain("Carlos Sem Cadastro");
        previa.AutoresReconhecidos.Should().Contain("Ana Autora");
        previa.AutoresSemCadastro.Should().Contain("Bruno Sem Cadastro");
        previa.Avisos.Should().Contain(a => a.Contains("contratante_usuario.csv"));

        // O histórico de visitas passadas foi para as observações da ficha, e as colunas
        // sem campo também — e a credencial NÃO.
        var um = previa.Pacientes.Linhas.Single(l => l.Nome == "Paciente Um").Ficha!;
        um.Observacoes.Should().Contain("— Dados do sistema anterior (Smart Clinic) —")
            .And.Contain("E-mail: um@x.com").And.Contain("RG: 12345").And.Contain("Profissão: Professor")
            .And.Contain("Estado civil: Casado(a)").And.Contain("Nome da mãe: Mãe Um")
            .And.Contain("Cadastrado no sistema anterior em: 02/01/2024")
            .And.Contain("— Visitas no sistema anterior (1) —").And.Contain("10/02/2025 14:00 · Consulta · Dra. Ana Autora");
        um.Observacoes.Should().NotContain("login1").And.NotContain("senha1");
        um.Endereco.Should().Be("Rua A, 10 - Centro, Macaé/RJ - CEP 27900-000");
        var dois = previa.Pacientes.Linhas.Single(l => l.Nome == "Paciente Dois").Ficha!;
        dois.Observacoes.Should().StartWith("Obs antiga").And.Contain("Retoque");

        _db.Evolucoes.Count().Should().Be(0, "a prévia não grava");
        _db.Agendamentos.Count().Should().Be(0);
        _db.Pacientes.Count().Should().Be(0);
    }

    [Fact]
    public async Task Executar_grava_fichas_prontuario_e_agenda_futura_e_a_segunda_rodada_nao_duplica()
    {
        var anaId = await ProfissionalAsync("Ana Autora");
        var previa = await _servico.PreverAsync(PacoteCompleto(), Convenios, Hoje);
        var progresso = new List<string>();
        var resultado = await _servico.ExecutarAsync(previa, "direcao", new Progress<string>(progresso.Add));

        resultado.Pacientes.Criados.Should().Be(3);
        resultado.EvolucoesCriadas.Should().Be(5, "po1, po2, s1, fc1 e cm1");
        resultado.EvolucoesSemPaciente.Should().Be(0, "po4 já ficou de fora na prévia");
        resultado.AgendamentosCriados.Should().Be(2);
        resultado.Erros.Should().BeEmpty();

        var um = _db.Pacientes.Single(p => p.Nome == "Paciente Um");
        var evolucoes = _db.Evolucoes.Where(e => e.PacienteId == um.Id).OrderBy(e => e.Data).ToList();
        evolucoes.Should().HaveCount(4);
        var po1 = evolucoes.Single(e => e.ChaveImportacao == "IMPORT:smartclinic:pos_operatorio:po1");
        po1.TextoEvolucao.Should().Be("Paciente refere dor lombar há 3 dias\n\nSem febre");
        po1.ExameFisico.Should().Be("Lombar: contratura");
        po1.Conduta.Should().Be("• Repouso\n• Calor local");
        po1.ProfissionalId.Should().Be(anaId, "o autor casou com o profissional daqui");
        po1.CriadoPor.Should().Be("Ana Autora (CRM 123456/RJ)");
        po1.CriadoEm.Should().Be(new DateTime(2025, 2, 10, 14, 5, 0), "a data de criação é a de lá");
        po1.Data.Should().Be(new DateOnly(2025, 2, 10));
        evolucoes.Single(e => e.ChaveImportacao!.EndsWith(":po2")).TextoEvolucao!.Length
            .Should().Be(5000, "texto acima de 4.000 não é cortado");
        var cm1 = evolucoes.Single(e => e.ChaveImportacao!.EndsWith(":cm1"));
        cm1.TextoEvolucao.Should().Contain("CONSULTA - BÁSICA").And.Contain("Queixa: Dor no joelho");

        var dois = _db.Pacientes.Single(p => p.Nome == "Paciente Dois");
        var s1 = _db.Evolucoes.Single(e => e.PacienteId == dois.Id);
        s1.ProfissionalId.Should().BeNull();
        s1.CriadoPor.Should().Be("Bruno Sem Cadastro", "autor sem cadastro fica pelo nome");
        s1.HipoteseDiagnostica.Should().Be("Cefaleia tensional");

        var agenda = _db.Agendamentos.OrderBy(a => a.DataHora).ToList();
        agenda.Should().HaveCount(2, "só os horários de hoje em diante");
        agenda[0].PacienteId.Should().Be(um.Id);
        agenda[0].ProfissionalId.Should().Be(anaId);
        agenda[0].DuracaoMinutos.Should().Be(30);
        agenda[0].Status.Should().Be(StatusAgendamento.Agendado);
        agenda[0].Observacoes.Should().Contain("Retorno");
        agenda[1].ProfissionalId.Should().BeNull();
        agenda[1].Observacoes.Should().Contain("Carlos Sem Cadastro");

        _db.Auditoria.Count(a => a.Acao == "ProntuarioImportado").Should().Be(1, "um lote");
        _db.Auditoria.Count(a => a.Acao == "ImportacaoSmartClinic").Should().Be(1);
        progresso.Should().Contain(p => p.Contains("fichas"));

        // Segunda rodada, mesmo pacote: nada duplica.
        var previa2 = await _servico.PreverAsync(PacoteCompleto(), Convenios, Hoje);
        previa2.Pacientes.JaImportadas.Should().Be(3);
        previa2.EvolucoesNovas.Should().Be(0);
        previa2.Prontuario.Sum(p => p.JaImportados).Should().Be(5);
        previa2.Agenda.FuturosNovos.Should().Be(0);
        previa2.Agenda.FuturosJaImportados.Should().Be(2);
        previa2.TemTrabalho.Should().BeFalse();
        var resultado2 = await _servico.ExecutarAsync(previa2, "direcao");
        resultado2.EvolucoesCriadas.Should().Be(0);
        resultado2.AgendamentosCriados.Should().Be(0);
        _db.Evolucoes.Count().Should().Be(5);
        _db.Agendamentos.Count().Should().Be(2);
    }

    [Fact]
    public async Task Ficha_que_ja_existia_recebe_o_prontuario_e_o_historico_sem_perder_as_observacoes()
    {
        var existente = new Paciente { Nome = "Paciente Um", Documento = "52998224725", Convenio = Convenio.Amil, Sexo = Sexo.Feminino, Observacoes = "Escrito no balcão" };
        _db.Pacientes.Add(existente);
        await _db.SaveChangesAsync();

        var previa = await _servico.PreverAsync(PacoteCompleto(), Convenios, Hoje);
        previa.Pacientes.Completar.Should().Be(1);
        var resultado = await _servico.ExecutarAsync(previa, "direcao");

        var ficha = _db.Pacientes.Single(p => p.Id == existente.Id);
        ficha.Convenio.Should().Be(Convenio.Amil, "o convênio da ficha é mantido");
        ficha.Observacoes.Should().StartWith("Escrito no balcão").And.Contain("Visitas no sistema anterior");
        ficha.ChaveImportacao.Should().Be("IMPORT:smartclinic:p1");
        _db.Evolucoes.Count(e => e.PacienteId == existente.Id).Should().Be(4, "o prontuário antigo entrou na ficha que já existia");
        resultado.EvolucoesSemPaciente.Should().Be(0);
    }

    [Fact]
    public async Task O_prontuario_importado_e_registro_clinico_para_a_guarda_e_a_exportacao()
    {
        var previa = await _servico.PreverAsync(PacoteCompleto(), Convenios, Hoje);
        await _servico.ExecutarAsync(previa, "direcao");
        var um = _db.Pacientes.Single(p => p.Nome == "Paciente Um");

        // O sistema não deixa apagar a ficha que tem prontuário — vale para o importado.
        (await _repo.PacienteTemRegistroClinicoAsync(um.Id)).Should().BeTrue();
        var acao = () => new PacienteService(_repo).RemoverAsync(um.Id);
        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*20 anos*");
    }

    // ------------------------------------------------------------------ as objeções da direção

    [Fact]
    public async Task Duplicata_do_sistema_antigo_entra_fundida_na_MESMA_rodada_com_o_prontuario_dela()
    {
        // p1 e p1b são a mesma pessoa (mesmo CPF) cadastrada duas vezes lá; o prontuário
        // de p1b tem de cair na ficha que p1 produz — numa rodada só.
        var pacientes = Pacientes + "p1b;c1;Paciente Um (duplicado);;;529.982.247-25;;;;;PARTICULAR;;;;;;;;;;;;;;;;;Obs da duplicata;;\n";
        var posOp = PosOperatorio + "po5;u1;p1b;2025-07-01 09:00:00;;Registro da ficha duplicada;;;PR;2025-07-01 09:00:00;;;;;;\n";
        var pacote = PacoteSmartClinic.Abrir(Zip(
            (PacoteSmartClinic.Pacientes, pacientes), (PacoteSmartClinic.PosOperatorio, posOp)));

        var previa = await _servico.PreverAsync(pacote, Convenios, Hoje);
        var dup = previa.Pacientes.Linhas.Single(l => l.Nome.Contains("duplicado"));
        dup.Destino.Should().Be(DestinoLinha.Completar);
        dup.FundeNaLinha.Should().Be(2, "a primeira ocorrência do CPF é a linha 2 do arquivo");
        previa.Pacientes.Problemas.Should().Be(0);
        previa.Prontuario.Single().SemPaciente.Should().Be(1, "po4 aponta para p9, que não existe");

        var r = await _servico.ExecutarAsync(previa, "direcao");
        r.Pacientes.Criados.Should().Be(3);
        r.Pacientes.Completados.Should().Be(1, "a duplicata completou a ficha da linha 2");
        _db.Pacientes.Count(p => p.Documento == "52998224725").Should().Be(1, "UMA ficha para a mesma pessoa");
        var um = _db.Pacientes.Single(p => p.Documento == "52998224725");
        um.Observacoes.Should().Contain("Obs da duplicata");
        _db.Evolucoes.Single(e => e.ChaveImportacao!.EndsWith(":po5")).PacienteId.Should().Be(um.Id,
            "o registro do id antigo duplicado caiu na ficha fundida");
        r.EvolucoesSemPaciente.Should().Be(0);

        // Rodar de novo: nada muda.
        var previa2 = await _servico.PreverAsync(pacote, Convenios, Hoje);
        previa2.TemTrabalho.Should().BeFalse();
    }

    [Fact]
    public async Task Convenio_nao_apontado_entra_A_DEFINIR_e_a_elegibilidade_acusa_em_vermelho()
    {
        var convenios = new Dictionary<string, ConvenioCadastro>(StringComparer.OrdinalIgnoreCase)
        {
            ["PARTICULAR"] = Convenios["PARTICULAR"],
            [ImportacaoPacientesService.ConvenioEmBranco] = ConvenioCadastro.ADefinir()
        };
        var previa = await _servico.PreverAsync(PacoteCompleto(), convenios, Hoje);
        previa.Pacientes.Problemas.Should().Be(0);
        await _servico.ExecutarAsync(previa, "direcao");

        var dois = _db.Pacientes.Single(p => p.Nome == "Paciente Dois");
        dois.ConvenioCodigo.Should().Be(ConvenioCadastro.CodigoADefinir);
        dois.ConvenioADefinir.Should().BeTrue();
        _db.Convenios.Should().Contain(c => c.Codigo == ConvenioCadastro.CodigoADefinir && !c.GeraGuia,
            "o catálogo foi semeado — é ele que dá nome ao convênio nas telas");
        CatalogoConvenios.Nome(dois.ConvenioCodigo, dois.Convenio).Should().Be("A definir (importado sem convênio)");

        var elegibilidade = new ElegibilidadeService(
            _repo, new AutorizacaoService(_repo), new ConsentimentoService(_repo), new ConsultaService(_repo));
        var resposta = await elegibilidade.ConferirAsync(dois.Id, Hoje);
        resposta.TemImpedimento.Should().BeTrue();
        resposta.Alertas.Should().Contain(a => a.Motivo == ImpedimentoElegibilidade.ConvenioADefinir
                                               && a.Urgencia == NivelUrgencia.Vermelho);

        var um = _db.Pacientes.Single(p => p.Nome == "Paciente Um");
        um.ConvenioADefinir.Should().BeFalse("PARTICULAR foi apontado");
        (await elegibilidade.ConferirAsync(um.Id, Hoje)).Alertas
            .Should().NotContain(a => a.Motivo == ImpedimentoElegibilidade.ConvenioADefinir);
    }

    [Fact]
    public async Task Equipe_cadastrada_DEPOIS_da_importacao_ganha_o_vinculo_na_rodada_seguinte()
    {
        var previa = await _servico.PreverAsync(PacoteCompleto(), Convenios, Hoje);
        previa.AutoresSemCadastro.Should().Contain("Ana Autora", "ainda não há ninguém na Equipe");
        var r1 = await _servico.ExecutarAsync(previa, "direcao");
        r1.Revinculados.Should().Be(0);
        _db.Evolucoes.Where(e => e.ChaveImportacao != null).Should().OnlyContain(e => e.ProfissionalId == null);
        _db.Agendamentos.Should().OnlyContain(a => a.ProfissionalId == null);

        var anaId = await ProfissionalAsync("Dra. Ana Autora");

        var previa2 = await _servico.PreverAsync(PacoteCompleto(), Convenios, Hoje);
        var r2 = await _servico.ExecutarAsync(previa2, "direcao");
        r2.Revinculados.Should().Be(3, "po1 e po2 (dados_medicos de Ana) + 1 horário futuro dela; fc1 e cm1 vieram sem dados_medicos");
        // AsNoTracking: o vínculo é um UPDATE em lote no banco, e o contexto do teste ainda
        // guarda as entidades como as gravou — em produção cada operação abre escopo novo.
        _db.Evolucoes.AsNoTracking().Where(e => e.CriadoPor!.StartsWith("Ana Autora")).Should().OnlyContain(e => e.ProfissionalId == anaId);
        _db.Evolucoes.AsNoTracking().Single(e => e.CriadoPor == "Bruno Sem Cadastro").ProfissionalId.Should().BeNull();
        _db.Agendamentos.AsNoTracking().Single(a => a.Observacoes!.Contains("Retorno")).ProfissionalId.Should().Be(anaId);
        _db.Agendamentos.AsNoTracking().Single(a => a.Observacoes!.Contains("Carlos")).ProfissionalId.Should().BeNull();
        _db.Evolucoes.Count().Should().Be(5, "e nada foi duplicado");
    }
}
