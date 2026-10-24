using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de CONFIGURAÇÕES do Gerente decide, fora do Razor (out/2026, migração para
/// Blazor). Era tudo do <c>ConfiguracoesViewModel</c>, do <c>CamposPersonalizadosViewModel</c>
/// e do <c>ModelosTermoViewModel</c> do WPF — onde o <c>dotnet test</c> não alcança. Aqui mora:
/// a leitura e a recusa de cada bloco (com as MESMAS frases do WPF), as frases de situação
/// (publicação, tela do paciente, conclusão, backup) e a regra dos SEGREDOS — a
/// senha do SMTP e a chave secreta do armazenamento nunca voltam para a tela, e campo vazio
/// no Salvar quer dizer MANTER o gravado (a regra do token do SMS, <c>ConfiguracaoSmsNaTela</c>).
/// Os prazos do faturamento e os dados da clínica não estão aqui: são os MESMOS de
/// <see cref="ConfiguracoesFaturamentoNaTela"/>, para a mesma regra não existir duas vezes.
/// </summary>
public static class ConfiguracoesDaClinicaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // ============================ Básico ============================

    /// <summary>O <c>TentarLerInteiro</c> do WPF: inteiro dentro da faixa, espaço nas pontas aceito.</summary>
    public static bool TentarLerInteiro(string? texto, int minimo, int maximo, out int valor)
        => int.TryParse(texto?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out valor)
           && valor >= minimo && valor <= maximo;

    public static string? Limpar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    // ============================ A clínica ============================

    /// <summary>
    /// O tipo deduzido da chave Pix, dito ao lado dela. Era calculado no WPF e NENHUMA tela o
    /// mostrava (a aba usava o componente do shell, sem ele) — campo calculado sem leitor. CPF
    /// e celular têm os mesmos onze dígitos: mostrar a dedução faz o erro aparecer no CADASTRO,
    /// e não no extrato do mês que vem.
    /// </summary>
    public static string TipoDaChavePix(string? chave) => string.IsNullOrWhiteSpace(chave)
        ? "Sem chave cadastrada — o sistema não consegue gerar código Pix."
        : PixService.Classificar(chave) switch
        {
            TipoChavePix.Cpf => "Reconhecida como CPF.",
            TipoChavePix.Cnpj => "Reconhecida como CNPJ.",
            TipoChavePix.Email => "Reconhecida como e-mail.",
            TipoChavePix.Telefone => "Reconhecida como telefone.",
            TipoChavePix.Aleatoria => "Reconhecida como chave aleatória.",
            _ => "Formato não reconhecido — confira antes de usar."
        };

    // ============================ Operação ============================

    /// <summary>
    /// Jornada, recall e carimbadora, com as frases do WPF. URL vazia da carimbadora é o caso
    /// NORMAL (a clínica pode não ter ACT contratada); URL escrita errada é recusada aqui,
    /// senão toda assinatura falharia depois com um erro de rede que ninguém liga a esta tela.
    /// </summary>
    public static OperacaoDaClinica? LerOperacao(string? jornada, string? recall, string? carimbadora, out string? erro)
    {
        if (!TentarLerInteiro(jornada, 1, 24 * 60, out var j))
        {
            erro = "A jornada diária vai de 1 a 1440 minutos (8 h = 480).";
            return null;
        }
        if (!TentarLerInteiro(recall, 1, 3650, out var r))
        {
            erro = "Os dias de inatividade do recall vão de 1 a 3650.";
            return null;
        }
        var url = Limpar(carimbadora);
        if (url is not null && !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            erro = "O endereço da carimbadora de tempo precisa ser uma URL completa "
                   + "(ex.: https://act.exemplo.com.br/tsa). Deixe em branco se a clínica "
                   + "não tiver ACT contratada.";
            return null;
        }
        erro = null;
        return new OperacaoDaClinica(j, r, url);
    }

    /// <summary>
    /// A chave "guia no agendamento" só é tocada quando MUDOU: ligar dispara o backfill do
    /// <c>RealizadoEm</c>, e repeti-lo a cada Salvar escreveria a tabela inteira à toa.
    /// </summary>
    public static bool GravarGuiaNoAgendamento(bool naTela, bool gravada) => naTela != gravada;

    /// <summary>LIGAR a chave é o único passo da tela que depende de uma ORDEM fora dela.</summary>
    public static bool PedeConfirmacaoAoLigarGuia(bool naTela, bool gravada) => naTela && !gravada;

    public const string PerguntaLigarGuiaNoAgendamento =
        "Com a chave ligada, marcar um horário já gera o atendimento e as guias.\n\n"
        + "Ligue SÓ DEPOIS de atualizar TODAS as máquinas da clínica: um programa antigo "
        + "confirmando presença de um horário marcado pelo novo geraria guia em dobro.\n\n"
        + "Todas as máquinas já estão atualizadas?";

    // ============================ Publicação (parcela 53) ============================

    /// <summary>
    /// Há armazenamento utilizável? Vindo do ambiente já está provado; do banco, quem decide é
    /// a MESMA regra que o serviço usa (<see cref="OpcoesArmazenamento.De"/>) — perguntar de
    /// outro jeito aqui faria a tela e a publicação discordarem sobre a mesma configuração.
    /// </summary>
    public static bool TemArmazenamento(bool vemDoAmbiente, string? endpoint, string? regiao,
        string? bucket, string? chave, string? segredo)
        => vemDoAmbiente || OpcoesArmazenamento.De(endpoint, regiao, bucket, chave, segredo) is not null;

    /// <summary>
    /// Os três estados que a tela precisa distinguir — e o do meio é o perigoso: domínio
    /// cadastrado sem armazenamento faz a clínica acreditar que está publicando.
    /// </summary>
    public static string DescreverPublicacao(string? dominio, bool temArmazenamento, int dias)
    {
        if (string.IsNullOrWhiteSpace(dominio))
            return "Publicação DESLIGADA. O QR impresso na receita aponta para o validador "
                + "do ITI, e o paciente precisa levar o arquivo até a farmácia.";

        if (!temArmazenamento)
            return "Domínio cadastrado, mas SEM armazenamento configurado: os documentos "
                + "continuam sendo assinados normalmente e o link não sobe. Preencha "
                + "endpoint, balde e credenciais abaixo — ou limpe o domínio para desligar.";

        return $"Publicação LIGADA em {dominio.Trim()}. O documento assinado fica no ar "
            + $"por {dias} dias e o farmacêutico o abre escaneando o QR.";
    }

    /// <summary>
    /// O Salvar da publicação. Domínio malformado é recusado AQUI (um QR apontando para lugar
    /// nenhum dentro de um PDF já assinado não se corrige). A chave secreta segue a regra dos
    /// segredos: vazia = MANTER a gravada — EXCETO quando endpoint, balde e chave também
    /// vieram vazios, que é "deixe os quatro em branco" (desligar), e aí ela sai junto.
    /// </summary>
    public static PublicacaoDaClinica? LerPublicacao(
        string? dominio, string? dias,
        string? endpoint, string? regiao, string? bucket, string? chave,
        string? segredoDigitado, string? segredoGravado, bool vemDoAmbiente, out string? erro)
    {
        var d = Limpar(dominio);
        if (d is not null && (!Uri.TryCreate(d, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            erro = "O domínio de publicação precisa ser um endereço https completo "
                   + "(ex.: https://documentos.suaclinica.com.br). Deixe em branco para "
                   + "desligar a publicação.";
            return null;
        }

        if (!TentarLerInteiro(dias, PublicacaoDocumento.DiasPublicadoMinimo, PublicacaoDocumento.DiasPublicadoMaximo, out var n))
        {
            erro = $"Os dias no ar vão de {PublicacaoDocumento.DiasPublicadoMinimo} a "
                   + $"{PublicacaoDocumento.DiasPublicadoMaximo}.";
            return null;
        }

        if (vemDoAmbiente)
        {
            erro = null;
            return new PublicacaoDaClinica(d, n, GravarCredenciais: false, null, null, null, null, null);
        }

        var e = Limpar(endpoint);
        var b = Limpar(bucket);
        var c = Limpar(chave);
        var digitado = string.IsNullOrWhiteSpace(segredoDigitado) ? null : segredoDigitado.Trim();
        var desligando = e is null && b is null && c is null && digitado is null;
        var s = desligando ? null : digitado ?? Limpar(segredoGravado);

        var preenchidos = new[] { e, b, c, s }.Count(x => x is not null);
        if (preenchidos is > 0 and < 4)
        {
            erro = "O armazenamento precisa de endpoint, balde, chave e segredo. Preencha "
                   + "os quatro, ou deixe os quatro em branco.";
            return null;
        }

        erro = null;
        return new PublicacaoDaClinica(d, n, GravarCredenciais: true, e, Limpar(regiao), b, c, s);
    }

    public static string FrasePublicacaoSalva(bool vemDoAmbiente) => vemDoAmbiente
        ? "Publicação salva. As credenciais não foram alteradas: elas estão vindo de "
          + "variável de ambiente desta máquina."
        : "Publicação salva.";

    // ============================ Conclusão automática ============================

    public static string SituacaoConclusao(bool automatica) => automatica
        ? "Conclusão automática ativa. Altere o prazo e salve para atualizar a regra."
        : "Configuração pendente: defina o prazo e salve as regras para ativar a conclusão automática.";

    /// <summary>O prazo em horas inteiras — a faixa é a do serviço (1 a 720), dita antes de gravar.</summary>
    public static int? LerPrazoConclusao(string? texto, out string? erro)
    {
        if (!TentarLerInteiro(texto, 1, 720, out var horas))
        {
            erro = "Informe o prazo em horas inteiras, de 1 a 720.";
            return null;
        }
        erro = null;
        return horas;
    }

    public static string FraseConclusaoSalva(int horas)
        => $"Conclusão automática ativa: {horas} horas após salvar a sessão. Pendências de enfermagem continuam bloqueando a conclusão.";

    public static string NomeDaModalidadeDeEnfermagem(string nome, bool ativa) => nome + (ativa ? "" : " (inativa)");

    public const string SemAcessoAoProntuario = "É necessário acesso ao prontuário para consultar as sessões pendentes.";

    public static string ResumoPendenciasConclusao(int quantas) => quantas == 0
        ? "Nenhuma sessão com prazo vencido pendente de conclusão."
        : $"{quantas} sessão(ões) com prazo vencido. Confira os impedimentos abaixo (até 100 por vez).";

    // ============================ Backup (parcelas 35 e 52) ============================

    /// <summary>Intervalo e cópias: inteiros de 1 em diante (o serviço também não aceita menos).</summary>
    public static (int Intervalo, int Copias)? LerPoliticaBackup(string? intervalo, string? copias, out string? erro)
    {
        if (!TentarLerInteiro(intervalo, 1, 3650, out var i) || !TentarLerInteiro(copias, 1, 999, out var c))
        {
            erro = "Informe números inteiros: de quantos em quantos dias copiar (1 ou mais) e quantas cópias guardar (1 ou mais).";
            return null;
        }
        erro = null;
        return (i, c);
    }

    public static string NomeDoArquivoDeBackup(DateOnly hoje) => $"backup-clinica-{hoje:yyyy-MM-dd}.json";

    public const string FiltroDoBackup = "Backup da clínica (*.json)|*.json";

    public static string ResumoDaCopiaAgora(ResultadoDaCopiaAgora r)
        => r.Falhou || r.Resumo is null
            ? $"Não foi possível copiar: {r.Erro ?? "a cópia não foi gravada."}"
            : $"Cópia gravada em {r.Caminho} — "
              + $"{r.Resumo.TotalLinhas.ToString("N0", PtBr)} registro(s) em "
              + $"{r.Resumo.TotalTabelas} tabela(s)."
              + (r.CopiasApagadas > 0 ? $" {r.CopiasApagadas} cópia(s) antiga(s) saíram da pasta." : string.Empty);

    public static string ResumoDoArquivoGerado(ResumoDoBackup m, string caminho)
        => $"{m.TotalLinhas.ToString("N0", PtBr)} registro(s) de {m.TotalTabelas} tabela(s) gravados em {caminho}.";

    /// <summary>Backup vazio é tecnicamente válido e praticamente inútil: a diferença é ESCRITA.</summary>
    public static string ResumoDaConferencia(ResumoDoBackup m) => m.Vazio
        ? $"ATENÇÃO: o arquivo abriu, tem {m.TotalTabelas} tabela(s) e NENHUM registro. "
          + "Ele não serve para restaurar a clínica."
        : $"Backup de {m.GeradoEm.ToString("dd/MM/yyyy HH:mm", PtBr)} — {m.TotalLinhas.ToString("N0", PtBr)} registro(s) "
          + $"em {m.TotalTabelas} tabela(s). "
          + $"Pacientes: {m.Pacientes.ToString("N0", PtBr)} · "
          + $"Evoluções: {m.Evolucoes.ToString("N0", PtBr)} · "
          + $"Lançamentos: {m.Lancamentos.ToString("N0", PtBr)}.";

    public const string PastaInacessivel =
        "A pasta escolhida não existe ou não está acessível desta máquina agora. Escolha uma "
        + "pasta de rede ou de nuvem sincronizada que esteja ligada.";

    /// <summary>
    /// A tela NÃO restaura — restaurar é ato de implantação ou de desastre, e a proteção certa
    /// contra o clique errado não é uma caixa de confirmação: é não haver botão.
    /// </summary>
    public const string SobreRestauracao =
        "Para RESTAURAR um backup, fale com o suporte. A restauração substitui a base inteira, só entra numa base "
        + "vazia e é feita junto com quem acompanha a operação — por isso ela não tem botão aqui.";

    // ============================ Campos personalizados (set/2026) ============================

    public static string OndeApareceOCampo(string? modalidadeCodigo) => string.IsNullOrWhiteSpace(modalidadeCodigo)
        ? "todas as modalidades"
        : CatalogoModalidades.Nome(modalidadeCodigo);

    /// <summary>
    /// O tipo do campo com acento: o humanizador do <c>RotulosEnum</c> devolvia "Numero" e
    /// "Sim nao" na tela WPF (o enum não tem rótulo declarado).
    /// </summary>
    public static string RotuloDoTipoDeCampo(Clinica.Domain.Entities.TipoCampoPersonalizado tipo) => tipo switch
    {
        Clinica.Domain.Entities.TipoCampoPersonalizado.Texto => "Texto",
        Clinica.Domain.Entities.TipoCampoPersonalizado.TextoLongo => "Texto longo",
        Clinica.Domain.Entities.TipoCampoPersonalizado.Numero => "Número",
        Clinica.Domain.Entities.TipoCampoPersonalizado.SimNao => "Sim / Não",
        Clinica.Domain.Entities.TipoCampoPersonalizado.Data => "Data",
        Clinica.Domain.Entities.TipoCampoPersonalizado.Lista => "Lista",
        _ => RotulosEnum.De(tipo)
    };

    public static string SituacaoDoCampo(bool ativo) => ativo ? "Ativo" : "Desativado";

    public static string LimiteDeCampos =>
        $"Até {CampoPersonalizadoService.MaximoAtivos} campos ativos — uma folha com campos "
        + "demais deixa de ser preenchida, e o que se perde não é o campo novo, são os do sistema.";

    public static string PerguntaDesativarCampo(string rotulo)
        => $"“{rotulo}” deixa de aparecer na folha da sessão.\n\n"
           + "O que já foi registrado nele CONTINUA no prontuário — desativar não apaga nada.";

    // ============================ Termos do procedimento (parcela 66) ============================

    public static string ResumoDasDeclaracoes(int quantas) => quantas switch
    {
        0 => "sem declarações",
        1 => "1 declaração",
        var n => $"{n} declarações"
    };

    public static string SituacaoDaExigencia(bool ativa, bool soValeNoDia) => (ativa, soValeNoDia) switch
    {
        (false, _) => "Desligado",
        (true, true) => "Exigido a cada sessão",
        _ => "Exigido — vale a partir da assinatura"
    };

    public static string AcaoDaExigencia(bool ativa) => ativa ? "Desligar" : "Ligar";

    /// <summary>Antes de gravar o termo: nome e texto, com as frases do WPF. Null = pode gravar.</summary>
    public static string? ConferirTermo(string? nome, string? corpo)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "Dê um nome ao termo (ex.: \"Termo do BSV\").";
        if (string.IsNullOrWhiteSpace(corpo)) return "Escreva o texto do termo — é o que o paciente vai ler e assinar.";
        return null;
    }

    public const string PerguntaCriarTermosDoBsv =
        "Serão criados os dois termos do BSV com o texto aprovado pela clínica:\n\n"
        + "• TCLE — o paciente assina UMA vez, e ele fica registrado na ficha;\n"
        + "• Termo da sessão — o paciente assina a CADA sessão.\n\n"
        + "As duas modalidades de BSV passam a exigi-los. Confirma?";

    /// <summary>A substituição lista o que está amarrado HOJE — só as exigências ativas.</summary>
    public static string PerguntaSubstituirTermosDoBsv(IEnumerable<string?> termosEmUso)
    {
        var emUso = string.Join("; ", termosEmUso.Select(t => $"\"{t ?? "termo"}\"").Distinct());
        return $"O BSV já exige termo: {emUso}.\n\n"
               + "Essas exigências serão DESLIGADAS e as modalidades de BSV "
               + "passam a exigir os dois termos novos. Os termos já assinados "
               + "não mudam — cada um guarda o texto que o paciente leu.\n\n"
               + "Substituir?";
    }

    public const string FraseTermosDoBsvCriados =
        "Termos do BSV amarrados às duas modalidades: o TCLE é assinado uma "
        + "vez e fica na ficha; o Termo da sessão é assinado a cada sessão.";

    public static string PerguntaDesligarExigencia(string termo, string modalidade)
        => $"O balcão deixa de cobrar o termo \"{termo}\" em {modalidade}. "
           + "Os termos já assinados continuam guardados. Confirma?";
}

/// <summary>O bloco "Operação" já validado.</summary>
public sealed record OperacaoDaClinica(int JornadaMinutos, int DiasInatividadeRecall, string? CarimbadoraDeTempo);

/// <summary>O bloco "Publicação" já validado. Sem <see cref="GravarCredenciais"/>, as credenciais vêm do ambiente e não se tocam.</summary>
public sealed record PublicacaoDaClinica(
    string? Dominio, int Dias, bool GravarCredenciais,
    string? Endpoint, string? Regiao, string? Bucket, string? Chave, string? Segredo);
