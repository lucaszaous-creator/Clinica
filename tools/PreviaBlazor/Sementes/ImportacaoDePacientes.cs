// Dados FICTÍCIOS da prévia — nenhuma pessoa, operadora contratada ou sistema real; os CPFs são de teste.
using System.IO;
using System.IO.Compression;
using System.Text;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A tela "Importar pacientes" do Gerente (out/2026, etapa 6). Ela só mostra alguma coisa depois
/// de a pessoa ESCOLHER um arquivo — e o hospedeiro da prévia (e o dos testes) não tem disco.
/// Por isso esta semente MONTA os três arquivos de exemplo, coerentes com a base já semeada,
/// e o hospedeiro os entrega por <see cref="ExemploPara"/> conforme o filtro do pedido:
/// - o CSV avulso: uma linha nova, uma que COMPLETA uma ficha que já existe (mesmo nome e
///   nascimento), uma JÁ IMPORTADA (a chave gravada aqui), uma que NÃO ENTRA (sem nome), uma com
///   aviso, e duas colunas "Telefone" (o rótulo único "Telefone (2)");
/// - o ZIP do Smart Clinic: pacientes.csv, um arquivo de prontuário e a agenda antiga (um horário
///   futuro e uma visita passada), com um convênio que não casa com nada ("definir depois");
/// - o ZIP de arquivos: um PDF que acha a ficha pelo id do sistema anterior, um de paciente que
///   não existe e uma linha do índice sem o arquivo — os três destinos que a prévia precisa DIZER.
/// Sem Id fixo: a ficha "já importada" é a primeira paciente semeada, ganhando a chave aqui.
/// </summary>
public sealed class SementeImportacaoDePacientes : ISemente
{
    public int Ordem => 97;

    /// <summary>O id, no "sistema anterior", da ficha que já foi importada.</summary>
    private const string IdJaImportado = "SC-0003";

    private static byte[]? _csv, _pacote, _anexos;

    /// <summary>
    /// O arquivo de exemplo para o filtro pedido pela tela (null quando o pedido não é desta tela,
    /// ou antes de semear). O ZIP DE ARQUIVOS é reconhecido pela frase do filtro; o resto pelo tipo.
    /// </summary>
    public static ArquivoEscolhido? ExemploPara(string filtro)
    {
        if (filtro.Contains("ZIP de arquivos", StringComparison.OrdinalIgnoreCase))
            return _anexos is null ? null : new ArquivoEscolhido("arquivos-smartclinic-exemplo.zip", _anexos);
        if (filtro.Contains("*.zip", StringComparison.OrdinalIgnoreCase))
            return _pacote is null ? null : new ArquivoEscolhido("smartclinic-exemplo.zip", _pacote);
        if (filtro.Contains("*.csv", StringComparison.OrdinalIgnoreCase))
            return _csv is null ? null : new ArquivoEscolhido("pacientes-exemplo.csv", _csv);
        return null;
    }

    public void Semear(ClinicaDbContext db)
    {
        // A ficha que JÁ entrou numa importação anterior: a chave é a que o serviço procura.
        var jaImportada = db.Set<Paciente>().Where(p => p.ChaveImportacao == null).OrderBy(p => p.Id).FirstOrDefault();
        if (jaImportada is not null)
        {
            jaImportada.ChaveImportacao = ImportacaoPacientesService.Chave(ImportacaoPacientesService.SistemaSmartClinic, IdJaImportado);
            db.SaveChanges();
        }

        // A ficha que o arquivo COMPLETA: reconhecida por nome + nascimento (sem CPF dos dois lados).
        var existente = db.Set<Paciente>()
            .Where(p => p.ChaveImportacao == null && p.DataNascimento != null && p.Documento == null)
            .OrderBy(p => p.Id).FirstOrDefault();

        _csv = Csv(jaImportada, existente);
        _pacote = Pacote(jaImportada);
        _anexos = Anexos(jaImportada);
    }

    private static byte[] Csv(Paciente? jaImportada, Paciente? existente)
    {
        var sb = new StringBuilder();
        sb.Append("Código;Nome;CPF;Telefone;Telefone;Data de nascimento;Sexo;Convênio;Carteirinha;Cidade;Email\n");
        sb.Append("SC-0101;Helena Prévia Souza;529.982.247-25;(21) 98888-1001;;12/03/1984;F;Unimed;0001234;Niterói;helena@exemplo.test\n");
        sb.Append("SC-0102;Otávio Prévia Lima;;(21) 98888-1002;(21) 3333-1002;05/11/1972;M;Particular;;Rio de Janeiro;\n");
        sb.Append("SC-0103;Marta Exemplo Dias;;(21) 98888-1003;;31/02/1990;;Plano Fictício Sul;99887766;Macaé;\n");
        if (existente is not null)
            sb.Append($"SC-0104;{existente.Nome};;;(21) 3333-1004;{existente.DataNascimento:dd/MM/yyyy};;Unimed;;Niterói;\n");
        if (jaImportada is not null)
            sb.Append($"{IdJaImportado};{jaImportada.Nome};;;;;;;;;\n");
        sb.Append("SC-0105;;;(21) 98888-1005;;;;;;;\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] Pacote(Paciente? jaImportada)
    {
        var hoje = DateTime.Today;
        var futuro = hoje.AddDays(9).AddHours(10);
        var passado = hoje.AddDays(-40).AddHours(14);

        var pacientes = new StringBuilder(
            "id_paciente;id_contratante;nome;data_nascimento;email;cpf;rg;telefone;celular;operadora;convenio;numero_convenio;validade_convenio;cep;endereco;numero;complemento;bairro;cidade;estado;profissao;sexo;estado_civil;indicacao;login;senha;obs;nome_mae;created_at\n");
        pacientes.Append("SC-0201;c1;Renata Pacote Alves;1979-04-12;renata@exemplo.test;;;;21977770201;TIM;UNIMED;0002201;;24000-000;Rua das Flores;120;ap 301;Centro;Niterói;RJ;Professora;F;;Indicação;rp;x;;Mãe Fictícia;2024-02-10 09:00:00\n");
        pacientes.Append("SC-0202;c1;Caio Pacote Nunes;1965-09-30;;;;;21977770202;;PARTICULAR;;;;;;;;Rio de Janeiro;RJ;;M;;;cn;x;Hipertenso;;2024-05-02 10:00:00\n");
        pacientes.Append("SC-0203;c1;Sofia Pacote Reis;1992-01-08;;;;;21977770203;;Saúde Exemplo Norte;7788;;;;;;;Macaé;RJ;;F;;;sr;x;;;2025-01-15 11:00:00\n");
        if (jaImportada is not null)
            pacientes.Append($"{IdJaImportado};c1;{jaImportada.Nome};;;;;;;;;;;;;;;;;;;;;;;;;;\n");

        var posOperatorio =
            "id_pos_operatorio;id_contratante_usuario;id_paciente;data;retorno;anamnese;exame_fisico;conduta;fl_status;data_criacao;data_prontuario;id_agenda;data_visualizacao;dados_medicos;id_migracao;url_certificado_digital\n"
            + "po-1;u1;SC-0201;2025-03-10 14:00:00;;Refere dor lombar há 3 dias;Contratura lombar;Repouso e calor local;PR;2025-03-10 14:05:00;;;;;;\n"
            + "po-2;u1;SC-0202;2025-04-02 09:00:00;;Retorno pós-sessão, sem queixas;;Manter o plano;PR;2025-04-02 09:00:00;;;;;;\n"
            + "po-3;u1;SC-9999;2025-04-03 09:00:00;;Registro de paciente que não está no pacote;;;PR;2025-04-03 09:00:00;;;;;;\n";

        var agenda =
            "id_agenda;id_paciente;paciente;profissional;inicio;fim;procedimento;cirurgia\n"
            + $"ag-1;SC-0201;Renata Pacote Alves;Profissional Antigo;{futuro:yyyy-MM-dd HH:mm:ss};{futuro.AddMinutes(30):yyyy-MM-dd HH:mm:ss};Consulta;\n"
            + $"ag-2;SC-0202;Caio Pacote Nunes;Profissional Antigo;{passado:yyyy-MM-dd HH:mm:ss};{passado.AddMinutes(30):yyyy-MM-dd HH:mm:ss};Consulta;\n";

        return Zip(
            ("pacientes.csv", Encoding.UTF8.GetBytes(pacientes.ToString())),
            ("pos_operatorio.csv", Encoding.UTF8.GetBytes(posOperatorio)),
            ("agenda.csv", Encoding.UTF8.GetBytes(agenda)),
            ("leiame.txt", Encoding.UTF8.GetBytes("não é CSV")));
    }

    private static byte[] Anexos(Paciente? jaImportada)
    {
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% exemplo fictício da prévia\n%%EOF\n");
        var nome = jaImportada?.Nome ?? "Paciente Exemplo";
        var indice =
            "url;id_arquivo;nome_paciente;id_paciente;titulo;data\n"
            + $"https://exemplo.test/r1.pdf;r1;{nome};{IdJaImportado};Receita #1001;2025-06-10 10:00:00\n"
            + $"https://exemplo.test/r2.pdf;r2;{nome};{IdJaImportado};Laudo de exame;2025-07-02 15:30:00\n"
            + "https://exemplo.test/r3.pdf;r3;Ninguém Cadastrado Exemplo;SC-8888;Receita #1003;2025-07-05 09:00:00\n"
            + $"https://exemplo.test/r4.pdf;r4;{nome};{IdJaImportado};Receita #1004;2025-08-01 08:00:00\n";
        return Zip(
            ("705/relacao_arquivos.csv", Encoding.UTF8.GetBytes(indice)),
            ("705/r1.pdf", pdf),
            ("705/r2.pdf", pdf),
            ("705/r3.pdf", pdf),
            ("705/sem-indice.pdf", pdf));
    }

    private static byte[] Zip(params (string Nome, byte[] Bytes)[] arquivos)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (nome, bytes) in arquivos)
            {
                var entrada = zip.CreateEntry(nome);
                using var w = entrada.Open();
                w.Write(bytes, 0, bytes.Length);
            }
        return memoria.ToArray();
    }
}
