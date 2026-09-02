using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Importação de pacientes do sistema anterior (set/2026 — a clínica migrou do Smart
/// Clinic e recebeu a exportação da carteira). Dois passos, e a separação é o desenho:
/// <see cref="PreverAsync"/> lê o arquivo e diz, linha a linha, o que VAI acontecer, sem
/// gravar nada; <see cref="ExecutarAsync"/> grava exatamente o que a prévia mostrou.
/// Importar às cegas e conferir depois é como se cria a carteira com 300 fichas
/// duplicadas — e duplicata de paciente é a que parte o histórico em dois (parcela 57).
///
/// As regras que não são óbvias:
/// <list type="bullet">
/// <item><b>Idempotente pela chave</b> (<see cref="Paciente.ChaveImportacao"/> =
/// <c>IMPORT:{sistema}:{id de lá}</c>): o mesmo arquivo importado duas vezes — a segunda
/// exportação, a linha corrigida, a queda de conexão no meio — pula quem já entrou.</item>
/// <item><b>Quem já existe é COMPLETADO, nunca sobrescrito</b>: a ficha da base é a que a
/// clínica vem corrigindo no balcão; o arquivo preenche só o que está VAZIO nela. O
/// convênio da ficha é o que fatura, e não se troca por importação.</item>
/// <item><b>A criação passa pelo <see cref="PacienteService"/></b> — a validação do CPF e
/// a recusa de duplicata moram lá, e uma segunda cópia da regra divergiria na primeira
/// correção.</item>
/// <item><b>Convênio do arquivo é TEXTO, e cada texto aponta para UM convênio cadastrado
/// aqui</b>, escolhido pela direção na tela. Texto sem destino é problema da linha, não
/// palpite: "Unimed" no arquivo pode ser Padrão ou Intercâmbio, e a diferença é regra de
/// faturamento.</item>
/// <item><b>Nada apaga nada</b>: não há caminho de desfazer em lote. A ficha importada por
/// engano se remove uma a uma pela ficha (só se vazia), como qualquer outra.</item>
/// </list>
/// </summary>
public sealed class ImportacaoPacientesService
{
    public const string SistemaSmartClinic = "smartclinic";

    /// <summary>Rótulo da chave do mapa de convênios para a célula em branco.</summary>
    public const string ConvenioEmBranco = "(em branco)";

    private readonly IClinicaRepositorio _repo;
    private readonly PacienteService _pacientes;

    public ImportacaoPacientesService(IClinicaRepositorio repo, PacienteService pacientes)
    {
        _repo = repo;
        _pacientes = pacientes;
    }

    public static string Chave(string sistema, string idOrigem)
        => $"IMPORT:{sistema}:{idOrigem.Trim()}";

    /// <summary>Os textos DISTINTOS da coluna de convênio, na ordem em que aparecem — é a
    /// lista que a direção mapeia. Vazio quando a coluna não foi mapeada.</summary>
    public static IReadOnlyList<string> ConveniosDoArquivo(TabelaImportada tabela, MapeamentoImportacao mapa)
    {
        if (mapa.ColunaDe(CampoImportacao.Convenio) is not { } col) return [];
        return tabela.Linhas
            .Select(l => ChaveConvenio(l[col]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ChaveConvenio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? ConvenioEmBranco : texto.Trim();

    /// <summary>
    /// Lê o arquivo com o mapeamento e responde, linha a linha, o que a execução faria.
    /// NÃO grava nada.
    /// </summary>
    /// <param name="colunasSemCampoNasObservacoes">
    /// Guardar nas OBSERVAÇÕES da ficha, como "rótulo: valor", toda coluna do arquivo que
    /// não tem campo aqui (e-mail, RG, profissão, nome da mãe…). É o "não perder nada" da
    /// migração: a ficha não tem o campo, mas a informação fica legível onde a recepção lê.
    /// Credencial e coluna interna do sistema antigo (login, senha, ids, foto) nunca entram
    /// — ver <see cref="ColunaInternaDoSistemaAnterior"/>.
    /// </param>
    public async Task<PreviaImportacao> PreverAsync(
        TabelaImportada tabela,
        MapeamentoImportacao mapa,
        IReadOnlyDictionary<string, ConvenioCadastro> convenios,
        string sistema = SistemaSmartClinic,
        CancellationToken ct = default,
        bool colunasSemCampoNasObservacoes = false)
    {
        if (mapa.ColunaDe(CampoImportacao.Nome) is null)
            throw new ArgumentException("Diga qual coluna do arquivo tem o NOME do paciente.");
        if (string.IsNullOrWhiteSpace(sistema))
            throw new ArgumentException("Informe o sistema de origem.");

        var fichas = await _repo.FichasResumidasAsync(ct);
        var porChave = fichas.Where(f => f.ChaveImportacao is not null)
            .ToDictionary(f => f.ChaveImportacao!, StringComparer.Ordinal);
        var porCpf = new Dictionary<string, FichaResumida>(StringComparer.Ordinal);
        foreach (var f in fichas)
        {
            var d = Cpf.Normalizar(f.Documento);
            if (d.Length == 11) porCpf.TryAdd(d, f);
        }
        var porNome = fichas
            .GroupBy(f => SugestorDeMapeamento.Normalizar(f.Nome))
            .ToDictionary(g => g.Key, g => g.ToList());

        var avisosGerais = new List<string>();
        if (!mapa.Tem(CampoImportacao.IdOrigem))
            avisosGerais.Add("Sem a coluna de ID do sistema anterior, importar o mesmo arquivo de novo "
                             + "só reconhece quem tem CPF — as outras fichas seriam criadas de novo.");
        if (!mapa.Tem(CampoImportacao.Cpf))
            avisosGerais.Add("Sem a coluna de CPF, quem já está cadastrado aqui só é reconhecido "
                             + "por nome e data de nascimento.");
        if (!mapa.Tem(CampoImportacao.Convenio))
            avisosGerais.Add("Sem a coluna de convênio, toda ficha precisa de um convênio escolhido "
                             + "no passo 2 (a linha \"(em branco)\").");

        var chavesNoArquivo = new Dictionary<string, int>(StringComparer.Ordinal);
        var cpfsNoArquivo = new Dictionary<string, int>(StringComparer.Ordinal);
        var linhas = new List<LinhaPrevia>();
        var semSexo = 0;
        var comAlergiaAnotada = 0;

        // As colunas que vão para as observações: as que nenhum campo lê e que não são
        // internas do sistema antigo.
        var colunasMapeadas = CamposImportacao.Todos
            .Select(mapa.ColunaDe).Where(i => i is not null).Select(i => i!.Value).ToHashSet();
        var colunasExtras = colunasSemCampoNasObservacoes
            ? Enumerable.Range(0, tabela.Colunas.Count)
                .Where(i => !colunasMapeadas.Contains(i) && !ColunaInternaDoSistemaAnterior(tabela.Colunas[i]))
                .ToArray()
            : [];

        for (var i = 0; i < tabela.Linhas.Count; i++)
        {
            var numero = i + 2; // 1 é o cabeçalho — é o número que a pessoa vê no Excel
            var l = tabela.Linhas[i];
            string? Campo(CampoImportacao c) =>
                mapa.ColunaDe(c) is { } col && col < l.Length && !string.IsNullOrWhiteSpace(l[col])
                    ? l[col].Trim() : null;

            var nome = Campo(CampoImportacao.Nome);
            var cpfBruto = Campo(CampoImportacao.Cpf);
            var cpfFormatado = cpfBruto is null ? null : Cpf.Formatar(cpfBruto);
            var convenioTexto = mapa.Tem(CampoImportacao.Convenio) ? ChaveConvenio(Campo(CampoImportacao.Convenio)) : ConvenioEmBranco;
            var avisos = new List<string>();

            LinhaPrevia Problema(string detalhe) =>
                new(numero, nome ?? "(sem nome)", cpfFormatado, convenioTexto, DestinoLinha.Problema, detalhe, avisos);

            if (nome is null)
            {
                linhas.Add(Problema("Linha sem nome."));
                continue;
            }
            if (nome.Length > 200) nome = nome[..200];

            // ---- chave de idempotência ----
            string? chave = null;
            var id = Campo(CampoImportacao.IdOrigem);
            if (id is not null)
            {
                chave = Chave(sistema, id);
                if (chavesNoArquivo.TryGetValue(chave, out var outra))
                {
                    linhas.Add(Problema($"O ID \"{id}\" se repete no arquivo (já apareceu na linha {outra})."));
                    continue;
                }
                chavesNoArquivo[chave] = numero;

                if (porChave.TryGetValue(chave, out var ja))
                {
                    linhas.Add(new LinhaPrevia(numero, nome, cpfFormatado, convenioTexto,
                        DestinoLinha.JaImportada,
                        $"Já entrou numa importação anterior como \"{ja.Nome}\" (ficha #{ja.Id}).", avisos)
                    { PacienteExistenteId = ja.Id, Chave = chave });
                    continue;
                }
            }

            // ---- CPF ----
            string? cpf = null;
            if (cpfBruto is not null)
            {
                if (!Cpf.Valido(cpfBruto))
                {
                    linhas.Add(Problema($"CPF inválido: \"{cpfBruto}\". Corrija no arquivo ou apague a célula."));
                    continue;
                }
                cpf = Cpf.Normalizar(cpfBruto);
                if (cpfsNoArquivo.TryGetValue(cpf, out var outra))
                {
                    linhas.Add(Problema($"CPF repetido no arquivo (já apareceu na linha {outra}): o sistema antigo tem "
                                        + $"esta pessoa duas vezes. A linha {outra} entra; esta fica de fora — numa segunda "
                                        + "importação ela completa a ficha que entrou."));
                    continue;
                }
                cpfsNoArquivo[cpf] = numero;
            }

            // ---- convênio ----
            if (!convenios.TryGetValue(convenioTexto, out var cadastro) || cadastro is null)
            {
                linhas.Add(Problema(convenioTexto == ConvenioEmBranco
                    ? "Convênio em branco no arquivo — escolha no passo 2 qual convênio recebe essas linhas."
                    : $"Convênio \"{convenioTexto}\" ainda não aponta para um convênio cadastrado aqui (passo 2)."));
                continue;
            }

            // ---- os demais campos ----
            var nascimento = LerData(Campo(CampoImportacao.DataNascimento), "data de nascimento", avisos, nascimento: true);
            var validade = LerData(Campo(CampoImportacao.ValidadeCarteirinha), "validade da carteirinha", avisos);
            var sexo = LerSexo(Campo(CampoImportacao.Sexo), out var avisoSexo);
            if (avisoSexo is not null) avisos.Add(avisoSexo);
            else if (Campo(CampoImportacao.Sexo) is null) semSexo++;

            // Celular primeiro (é o que vai para o WhatsApp); o outro número entra só quando
            // o celular falta — e, quando os dois existem, o segundo não se perde: vai para
            // as observações.
            var telefone = LerTelefone(Campo(CampoImportacao.Telefone));
            var outroTelefone = LerTelefone(Campo(CampoImportacao.TelefoneAlternativo));
            var observacoes = Campo(CampoImportacao.Observacoes);
            if (telefone is null) telefone = outroTelefone;
            else if (outroTelefone is not null && Telefone.Normalizar(outroTelefone) != Telefone.Normalizar(telefone))
                observacoes = Acrescentar(observacoes, $"Outro telefone: {outroTelefone}");

            var endereco = MontarEndereco(
                Campo(CampoImportacao.Endereco), Campo(CampoImportacao.EnderecoNumero),
                Campo(CampoImportacao.EnderecoComplemento), Campo(CampoImportacao.Bairro),
                Campo(CampoImportacao.Cidade), Campo(CampoImportacao.Estado), Campo(CampoImportacao.Cep));
            var carteirinha = Cortar(Campo(CampoImportacao.Carteirinha), 40);
            var (origem, indicadoPor, observacaoOrigem) = LerOrigem(Campo(CampoImportacao.Origem));
            if (observacaoOrigem is not null) observacoes = Acrescentar(observacoes, observacaoOrigem);

            if (colunasExtras.Length > 0)
            {
                var bloco = BlocoDeColunasExtras(tabela, l, colunasExtras, sistema);
                if (bloco is not null) observacoes = Acrescentar(observacoes, bloco);
                if (colunasExtras.Any(i => SugestorDeMapeamento.Normalizar(tabela.Colunas[i]) is "alergia" or "alergias"
                                           && i < l.Length && !string.IsNullOrWhiteSpace(l[i])))
                    comAlergiaAnotada++;
            }

            // ---- já existe? ----
            FichaResumida? existente = null;
            if (cpf is not null && porCpf.TryGetValue(cpf, out var porDoc))
            {
                existente = porDoc;
            }
            else if (porNome.TryGetValue(SugestorDeMapeamento.Normalizar(nome), out var homonimos))
            {
                // Nome igual E nascimento igual é a mesma pessoa; nome igual sozinho é
                // homônimo até prova em contrário — cria e AVISA, porque fundir duas
                // pessoas num prontuário é pior do que duas fichas para conferir.
                var mesma = nascimento is not null
                    ? homonimos.FirstOrDefault(h => h.DataNascimento == nascimento)
                    : null;
                if (mesma is not null)
                {
                    existente = mesma;
                    avisos.Add("Reconhecida pelo nome e pela data de nascimento (não há CPF para comparar).");
                }
                else
                    avisos.Add($"Há ficha com o mesmo nome (#{homonimos[0].Id}) — confira depois se não é a mesma pessoa.");
            }

            if (existente is not null)
            {
                if (existente.ChaveImportacao is not null && chave is not null && existente.ChaveImportacao != chave)
                    avisos.Add("A ficha já veio de outra importação; a chave dela é mantida.");

                var ficha = new Paciente
                {
                    Nome = nome, Documento = cpf, Telefone = telefone, DataNascimento = nascimento,
                    Endereco = endereco, Carteirinha = carteirinha, ValidadeCarteirinha = validade,
                    Convenio = cadastro.Familia, ConvenioCodigo = cadastro.Codigo, Sexo = sexo,
                    Observacoes = observacoes, Origem = origem, IndicadoPor = indicadoPor,
                    ChaveImportacao = chave
                };
                linhas.Add(new LinhaPrevia(numero, nome, cpfFormatado, convenioTexto, DestinoLinha.Completar,
                    $"Já cadastrada como \"{existente.Nome}\" (ficha #{existente.Id}): só os campos vazios dela serão preenchidos; o convênio da ficha é mantido.",
                    avisos)
                { Ficha = ficha, PacienteExistenteId = existente.Id, Chave = chave });
                continue;
            }

            var nova = new Paciente
            {
                Nome = nome, Documento = cpf, Telefone = telefone, DataNascimento = nascimento,
                Endereco = endereco, Carteirinha = carteirinha, ValidadeCarteirinha = validade,
                Convenio = cadastro.Familia, ConvenioCodigo = cadastro.Codigo, Sexo = sexo,
                Observacoes = observacoes, Origem = origem, IndicadoPor = indicadoPor,
                ChaveImportacao = chave
            };
            linhas.Add(new LinhaPrevia(numero, nome, cpfFormatado, convenioTexto, DestinoLinha.Criar,
                $"Ficha nova · {cadastro.Nome}", avisos)
            { Ficha = nova, Chave = chave });
        }

        // O sexo em branco é AVISO GERAL com a contagem, não uma linha de aviso por ficha:
        // na exportação real do Smart Clinic eram 1.712 de 2.238, e 1.712 avisos iguais
        // escondem os poucos que importam (data impossível, homônimo).
        if (semSexo > 0)
            avisosGerais.Add(semSexo == 1
                ? "1 ficha sem sexo no arquivo: fica como Masculino — confira na ficha."
                : $"{semSexo} fichas sem sexo no arquivo: ficam como Masculino — confira nas fichas.");
        if (comAlergiaAnotada > 0)
            avisosGerais.Add($"{comAlergiaAnotada} ficha(s) com alergia anotada no sistema anterior: o texto vai para as "
                             + "observações — registre na lista de problemas da ficha para o alerta de prescrição valer.");

        return new PreviaImportacao(sistema, linhas, avisosGerais);
    }

    /// <summary>
    /// O endereço da FICHA é uma linha só (é o que a receita imprime — art. 35 da Lei
    /// 5.991/1973); o Smart Clinic exporta em sete partes. Junta o que existe, na ordem
    /// em que se escreve num envelope, e pula o que está vazio — "Rua X, , - , /RJ" não é
    /// endereço. Nulo quando não há parte nenhuma.
    /// </summary>
    public static string? MontarEndereco(
        string? logradouro, string? numero, string? complemento, string? bairro,
        string? cidade, string? estado, string? cep)
    {
        string? L(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
        logradouro = L(logradouro); numero = L(numero); complemento = L(complemento);
        bairro = L(bairro); cidade = L(cidade); estado = L(estado); cep = L(cep);

        var sb = new StringBuilder();
        if (logradouro is not null) sb.Append(logradouro);
        if (numero is not null) sb.Append(sb.Length > 0 ? ", " : "").Append(numero);
        if (complemento is not null) sb.Append(sb.Length > 0 ? " " : "").Append(complemento);
        if (bairro is not null) sb.Append(sb.Length > 0 ? " - " : "").Append(bairro);
        var cidadeUf = cidade is not null && estado is not null ? $"{cidade}/{estado}" : cidade ?? estado;
        if (cidadeUf is not null) sb.Append(sb.Length > 0 ? ", " : "").Append(cidadeUf);
        if (cep is not null) sb.Append(sb.Length > 0 ? " - " : "").Append("CEP ").Append(cep);

        return sb.Length == 0 ? null : Cortar(sb.ToString(), 300);
    }

    private static string? Acrescentar(string? observacoes, string linha)
        => string.IsNullOrWhiteSpace(observacoes) ? linha : $"{observacoes}\n{linha}";

    /// <summary>
    /// Coluna do sistema anterior que NÃO deve ir para as observações: credencial (login,
    /// senha), arquivo (foto), ids e carimbos internos, flags de opt-in vazias. Nome
    /// normalizado (sem acento, sem sublinhado).
    /// </summary>
    public static bool ColunaInternaDoSistemaAnterior(string coluna)
    {
        var n = SugestorDeMapeamento.Normalizar(coluna);
        return n is "login" or "senha" or "password" or "foto" or "fotoredimensionada" or "thumb"
            or "idcontratante" or "idmigracao" or "idlegado" or "sequencial" or "datavisualizacao"
            or "saldo" or "pais" or "whatsappoptin" or "whatsappoptinat" or "whatsappoptinsource"
            or "whatsappoptoutat" or "whatsappoptoutreason";
    }

    /// <summary>
    /// "rótulo: valor" para cada coluna extra preenchida, com o cabeçalho que diz de onde
    /// veio. Códigos conhecidos são traduzidos (estado civil "CA" → "Casado(a)").
    /// </summary>
    public static string? BlocoDeColunasExtras(
        TabelaImportada tabela, string[] linha, IReadOnlyList<int> colunas, string sistema)
    {
        var itens = new List<string>();
        foreach (var i in colunas)
        {
            if (i >= linha.Length || string.IsNullOrWhiteSpace(linha[i])) continue;
            var nome = SugestorDeMapeamento.Normalizar(tabela.Colunas[i]);
            var valor = linha[i].Trim();
            if (nome == "estadocivil") valor = EstadoCivil(valor);
            else if (nome == "createdat" || nome == "datacadastro")
            {
                var avisos = new List<string>();
                valor = LerData(valor, "data", avisos)?.ToString("dd/MM/yyyy") ?? valor;
            }
            itens.Add($"{RotuloDeColuna(tabela.Colunas[i])}: {valor}");
        }
        if (itens.Count == 0) return null;
        return $"— Dados do sistema anterior ({NomeDoSistema(sistema)}) —\n{string.Join("\n", itens)}";
    }

    public static string NomeDoSistema(string sistema)
        => sistema == SistemaSmartClinic ? "Smart Clinic" : sistema;

    private static string EstadoCivil(string codigo) => codigo.Trim().ToUpperInvariant() switch
    {
        "CA" => "Casado(a)", "SO" => "Solteiro(a)", "VI" => "Viúvo(a)", "DI" => "Divorciado(a)",
        "SE" => "Separado(a)", "UE" => "União estável", var outro => outro
    };

    /// <summary>Rótulo legível para uma coluna do arquivo — a tabela cobre o que o Smart
    /// Clinic exporta; o resto é o nome da coluna com espaços e inicial maiúscula.</summary>
    public static string RotuloDeColuna(string coluna)
    {
        switch (SugestorDeMapeamento.Normalizar(coluna))
        {
            case "email": return "E-mail";
            case "rg": return "RG";
            case "profissao": return "Profissão";
            case "estadocivil": return "Estado civil";
            case "nomemae": return "Nome da mãe";
            case "nomepai": return "Nome do pai";
            case "naturalidade": return "Naturalidade";
            case "conjuge": return "Cônjuge";
            case "outrodoc": return "Outro documento";
            case "tags": return "Etiquetas";
            case "createdat": case "datacadastro": return "Cadastrado no sistema anterior em";
            case "auditoria": return "Histórico de edições no sistema anterior";
            case "codigoclienteomie": return "Código no Omie";
            case "preferenciacontato": return "Preferência de contato";
            case "telemergencia": return "Telefone de emergência";
            case "operadora": return "Operadora / outro número";
            case "telefone2": return "Telefone 2";
            case "indicacaoemail": return "E-mail de quem indicou";
            case "alergia": case "alergias": return "Alergia (anotada no sistema anterior)";
            case "numeroconvenio": return "Número no convênio";
            case "validadeconvenio": return "Validade do convênio";
        }
        var texto = coluna.Trim().Replace('_', ' ');
        return texto.Length == 0 ? coluna : char.ToUpperInvariant(texto[0]) + texto[1..];
    }

    /// <summary>
    /// Grava o que a prévia mostrou. Linha a linha, pelo <see cref="PacienteService"/>:
    /// uma linha que falhe (o CPF entrou na base por outra máquina entre a prévia e o
    /// clique) vira ERRO na lista e as demais seguem — e a chave de importação garante
    /// que rodar de novo só pega o que faltou.
    ///
    /// ⚠️ A trilha de cada ficha é gravada DEPOIS do ato, num segundo Salvar — e isso é uma
    /// exceção declarada à regra 7 do compromisso ("auditoria no MESMO SaveChanges"). A
    /// primeira versão acrescentava o <see cref="EventoAuditoria"/> ao contexto ANTES de
    /// chamar o <see cref="PacienteService"/>; quando ele RECUSAVA a ficha (CPF que entrou
    /// por outra porta), a linha "PacienteImportado" ficava pendurada no contexto e saía
    /// gravada junto da ficha SEGUINTE — trilha afirmando uma importação que não houve, que
    /// é a garantia aparente na tabela que existe para responder "quem fez isso?". Com a
    /// ordem invertida a linha só existe quando a ficha existe, e leva o id dela.
    /// </summary>
    public async Task<ResultadoImportacao> ExecutarAsync(
        PreviaImportacao previa, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador))
            throw new ArgumentException("Informe quem está importando.");

        // Reconfere as chaves NO INSTANTE de gravar: a prévia pode ter ficado aberta
        // enquanto outra máquina importava o mesmo arquivo.
        var chavesJa = (await _repo.FichasResumidasAsync(ct))
            .Where(f => f.ChaveImportacao is not null)
            .Select(f => f.ChaveImportacao!)
            .ToHashSet(StringComparer.Ordinal);

        int criados = 0, completados = 0, pulados = previa.JaImportadas;
        var erros = new List<string>();

        foreach (var linha in previa.Linhas)
        {
            ct.ThrowIfCancellationRequested();
            if (linha.Ficha is null) continue;
            if (linha.Chave is not null && chavesJa.Contains(linha.Chave))
            {
                pulados++;
                continue;
            }

            try
            {
                if (linha.EhCriar)
                {
                    var nova = await _pacientes.SalvarNovoAsync(linha.Ficha, ct: ct);
                    await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
                    {
                        Operador = operador,
                        Acao = "PacienteImportado",
                        PacienteId = nova.Id,
                        Detalhe = $"{nova.Nome}"
                                  + (nova.Documento is null ? "" : $" · CPF {Cpf.Formatar(nova.Documento)}")
                                  + (linha.Chave is null ? "" : $" · {linha.Chave}")
                                  + $" · linha {linha.Numero} do arquivo"
                    }, ct);
                    await _repo.SalvarAsync(ct);
                    criados++;
                }
                else if (linha.EhCompletar && linha.PacienteExistenteId is { } id)
                {
                    var atual = await _repo.ObterPacienteAsync(id, ct)
                        ?? throw new InvalidOperationException($"A ficha #{id} não existe mais.");
                    // A ficha é RASTREADA: se o serviço recusar depois de ela ter sido
                    // mexida, o que foi mexido sairia gravado no Salvar da linha seguinte.
                    // Daí o retrato de antes, para devolver no catch.
                    var antes = Retrato(atual);
                    var preenchidos = Completar(atual, linha.Ficha);
                    if (preenchidos.Count == 0)
                    {
                        pulados++;
                        continue;
                    }
                    try
                    {
                        // categoriaManual: a categoria da ficha existente não se recalcula
                        // por uma importação que nem toca o convênio dela.
                        await _pacientes.AtualizarAsync(atual, categoriaManual: true, ct);
                    }
                    catch
                    {
                        Restaurar(atual, antes);
                        throw;
                    }
                    await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
                    {
                        Operador = operador,
                        Acao = "PacienteCompletadoPorImportacao",
                        PacienteId = id,
                        Detalhe = $"{atual.Nome} · preenchidos: {string.Join(", ", preenchidos)}"
                                  + (linha.Chave is null ? "" : $" · {linha.Chave}")
                                  + $" · linha {linha.Numero} do arquivo"
                    }, ct);
                    await _repo.SalvarAsync(ct);
                    completados++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                erros.Add($"Linha {linha.Numero} ({linha.Nome}): {ex.Message}");
            }
            if (linha.Chave is not null) chavesJa.Add(linha.Chave);
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "ImportacaoPacientes",
            Detalhe = $"Sistema {previa.Sistema}: {criados} ficha(s) nova(s), {completados} completada(s), "
                      + $"{pulados} já existente(s), {previa.Problemas} linha(s) com problema, {erros.Count} erro(s) na gravação."
        }, ct);
        await _repo.SalvarAsync(ct);

        return new ResultadoImportacao(criados, completados, pulados, erros);
    }

    /// <summary>
    /// Preenche na ficha da base SÓ o que está vazio. Devolve o nome dos campos
    /// preenchidos — vazio quer dizer "nada a fazer", e a linha é pulada sem trilha.
    /// </summary>
    public static IReadOnlyList<string> Completar(Paciente atual, Paciente doArquivo)
    {
        var feitos = new List<string>();

        if (string.IsNullOrWhiteSpace(atual.Documento) && doArquivo.Documento is not null)
        { atual.Documento = doArquivo.Documento; feitos.Add("CPF"); }
        if (string.IsNullOrWhiteSpace(atual.Telefone) && doArquivo.Telefone is not null)
        { atual.Telefone = doArquivo.Telefone; feitos.Add("telefone"); }
        if (atual.DataNascimento is null && doArquivo.DataNascimento is not null)
        { atual.DataNascimento = doArquivo.DataNascimento; feitos.Add("nascimento"); }
        if (string.IsNullOrWhiteSpace(atual.Endereco) && doArquivo.Endereco is not null)
        { atual.Endereco = doArquivo.Endereco; feitos.Add("endereço"); }
        if (string.IsNullOrWhiteSpace(atual.Carteirinha) && doArquivo.Carteirinha is not null)
        { atual.Carteirinha = doArquivo.Carteirinha; feitos.Add("carteirinha"); }
        if (atual.ValidadeCarteirinha is null && doArquivo.ValidadeCarteirinha is not null)
        { atual.ValidadeCarteirinha = doArquivo.ValidadeCarteirinha; feitos.Add("validade"); }
        if (atual.Origem is null && doArquivo.Origem is not null)
        {
            atual.Origem = doArquivo.Origem;
            atual.IndicadoPor ??= doArquivo.IndicadoPor;
            feitos.Add("origem");
        }
        // Observações se ACRESCENTAM, nunca substituem: o que a recepção escreveu fica, e
        // o que o arquivo traz (dados sem campo, histórico de visitas) entra abaixo.
        if (doArquivo.Observacoes is not null
            && (string.IsNullOrWhiteSpace(atual.Observacoes)
                || !atual.Observacoes.Contains(doArquivo.Observacoes, StringComparison.Ordinal)))
        {
            atual.Observacoes = string.IsNullOrWhiteSpace(atual.Observacoes)
                ? doArquivo.Observacoes
                : $"{atual.Observacoes}\n\n{doArquivo.Observacoes}";
            feitos.Add("observações");
        }
        if (atual.ChaveImportacao is null && doArquivo.ChaveImportacao is not null)
        { atual.ChaveImportacao = doArquivo.ChaveImportacao; feitos.Add("chave de importação"); }

        return feitos;
    }

    private static Paciente Retrato(Paciente p) => new()
    {
        Documento = p.Documento, Telefone = p.Telefone, DataNascimento = p.DataNascimento,
        Endereco = p.Endereco, Carteirinha = p.Carteirinha, ValidadeCarteirinha = p.ValidadeCarteirinha,
        Origem = p.Origem, IndicadoPor = p.IndicadoPor, Observacoes = p.Observacoes,
        ChaveImportacao = p.ChaveImportacao
    };

    private static void Restaurar(Paciente p, Paciente antes)
    {
        p.Documento = antes.Documento; p.Telefone = antes.Telefone; p.DataNascimento = antes.DataNascimento;
        p.Endereco = antes.Endereco; p.Carteirinha = antes.Carteirinha; p.ValidadeCarteirinha = antes.ValidadeCarteirinha;
        p.Origem = antes.Origem; p.IndicadoPor = antes.IndicadoPor; p.Observacoes = antes.Observacoes;
        p.ChaveImportacao = antes.ChaveImportacao;
    }

    // ---------------------------------------------------------------- leitura dos campos

    private static readonly string[] FormatosData =
        ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "dd/MM/yy", "d/M/yy"];

    /// <summary>Data em qualquer forma comum; o que não se lê vira AVISO e o campo fica vazio —
    /// recusar a linha por uma data mal digitada deixaria a pessoa fora do sistema.</summary>
    public static DateOnly? LerData(string? texto, string rotulo, List<string> avisos, bool nascimento = false)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var soData = texto.Trim().Split(' ', 'T')[0];
        // "0000-00-00" é o vazio de banco MySQL, não uma data mal digitada: silêncio.
        if (soData.All(c => c == '0' || c == '-' || c == '/')) return null;
        if (!DateOnly.TryParseExact(soData, FormatosData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            avisos.Add($"{rotulo} \"{texto}\" não foi entendida e ficou em branco.");
            return null;
        }
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (nascimento && (d > hoje || d.Year < 1900))
        {
            avisos.Add($"{rotulo} \"{texto}\" é impossível e ficou em branco.");
            return null;
        }
        return d;
    }

    /// <summary>Em branco devolve o padrão SEM aviso por linha (quem conta é a prévia, no
    /// aviso geral); valor não reconhecido devolve o aviso.</summary>
    public static Sexo LerSexo(string? texto, out string? aviso)
    {
        aviso = null;
        var t = SugestorDeMapeamento.Normalizar(texto ?? string.Empty);
        if (t.StartsWith('f') || t == "mulher") return Sexo.Feminino;
        if (t.StartsWith('m') || t.StartsWith('h')) return Sexo.Masculino;
        if (t.Length > 0) aviso = $"Sexo \"{texto}\" não foi entendido — confira na ficha.";
        return Sexo.Masculino;
    }

    public static string? LerTelefone(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        // O arquivo pode trazer dois números na mesma célula; fica o primeiro.
        var primeiro = texto.Split('/', ';', '|')[0].Trim();
        return Cortar(Telefone.Formatar(primeiro), 30);
    }

    /// <summary>Texto livre do sistema anterior para o enum de origem. O que não casa vira
    /// <see cref="OrigemPaciente.Outro"/> com o texto preservado nas observações — o
    /// relatório de origem conta a resposta, e a frase original não se perde.</summary>
    public static (OrigemPaciente? Origem, string? IndicadoPor, string? Observacao) LerOrigem(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null, null);
        var t = SugestorDeMapeamento.Normalizar(texto);
        if (t.Contains("indic"))
        {
            var partes = texto.Split(':', '-', '–');
            var quem = partes.Length > 1 ? string.Join(" ", partes.Skip(1)).Trim() : null;
            return (OrigemPaciente.Indicacao, string.IsNullOrWhiteSpace(quem) ? null : quem, null);
        }
        if (t.Contains("encaminh") || t.Contains("medic")) return (OrigemPaciente.Encaminhamento, null, null);
        if (t.Contains("instagram") || t.Contains("facebook") || t.Contains("rede") || t.Contains("social"))
            return (OrigemPaciente.RedesSociais, null, null);
        if (t.Contains("google") || t.Contains("internet") || t.Contains("site") || t.Contains("busca"))
            return (OrigemPaciente.Internet, null, null);
        if (t.Contains("fachada") || t.Contains("placa") || t.Contains("passou") || t.Contains("vizinh"))
            return (OrigemPaciente.Fachada, null, null);
        if (t.Contains("conven") || t.Contains("plano") || t.Contains("operadora"))
            return (OrigemPaciente.Convenio, null, null);
        if (t.Contains("campanha") || t.Contains("promo") || t.Contains("recall"))
            return (OrigemPaciente.Campanha, null, null);
        return (OrigemPaciente.Outro, null, $"Origem informada no sistema anterior: {texto.Trim()}");
    }

    private static string? Cortar(string? texto, int max)
        => texto is null ? null : texto.Length <= max ? texto : texto[..max];
}
