using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Application.Modelos;

// ============================================================================================
// A ASSINATURA NA TELA (out/2026 — a autorização da direção de 19/10/2026, SÓ A CAMADA DE TELA).
//
// O que mora aqui é o que os ViewModels WPF da área congelada faziam e que é PROTOCOLO, não
// leiaute: a abertura e a conferência do A1 (EscolherCertificadoViewModel.CarregarA1Async) e a
// ORDEM das chamadas de cada caminho de assinatura (AcoesDoDocumento.AssinarAsync,
// PrescricaoInternaEdicaoViewModel.AssinarAsync, FolhaExecucaoViewModel.ValidarMedicoAsync e
// AssinarExecucaoAsync). Foi MOVIDO linha a linha, sem mudar argumento, ordem nem escopo — e
// passou a morar onde o `dotnet test` alcança (`AssinaturaNaTelaTests` fixa a sequência).
//
// ⚠️ O MOTOR continua congelado e NÃO é tocado por nada aqui: estes fluxos só CHAMAM
// AssinaturaDeDocumentoClinicoService / AssinaturaDePrescricaoService / CertificadoA1 com os
// mesmos argumentos de antes. O que muda é QUEM pergunta (a tela Blazor, pelos delegados
// `escolher` e `confirmar`), nunca o que é assinado. Ver docs/safeid-congelado.md.
// ============================================================================================

/// <summary>
/// Um certificado na lista do seletor, já com o que decide a escolha (subiu do
/// <c>EscolherCertificadoViewModel</c> WPF, sem mudança).
/// </summary>
public sealed class LinhaCertificado
{
    public required CertificadoAssinatura Certificado { get; init; }
    public required string Titular { get; init; }
    public required string Documento { get; init; }
    public required string Validade { get; init; }
    public required string Emissor { get; init; }
    public required bool Vigente { get; init; }
    public required bool EhECpf { get; init; }

    /// <summary>Certificado importado de arquivo para este ato.</summary>
    public required string Procedencia { get; init; }

    /// <summary>Vencido não se escolhe: assinar com ele produz documento inválido.</summary>
    public bool PodeEscolher => Vigente && EhECpf;

    /// <summary>Por que não dá — ao lado da linha, em vez de só depois do clique.</summary>
    public string? Impedimento => (Vigente, EhECpf) switch
    {
        (false, _) => "Fora da validade",
        (_, false) => "Não é e-CPF (não traz o CPF do titular)",
        _ => null
    };

    public static LinhaCertificado De(CertificadoAssinatura c) => new()
    {
        Certificado = c,
        Titular = c.Titular,
        Documento = c.Cpf is null ? "sem CPF no certificado" : Domain.Cpf.Formatar(c.Cpf),
        Validade = $"{c.ValidoDe:dd/MM/yyyy} a {c.ValidoAte:dd/MM/yyyy}",
        Emissor = c.Emissor,
        Vigente = c.Vigente,
        EhECpf = c.EhECpf,
        Procedencia = c.Procedencia
    };
}

/// <summary>
/// Importar o A1 individual SOMENTE para a operação atual — a metade do
/// <c>EscolherCertificadoViewModel.CarregarA1Async</c> e da leitura do arquivo do
/// <c>EscolherCertificadoWindow</c> que é protocolo (movida sem mudança de comportamento).
/// </summary>
public static class CarregamentoDoA1
{
    /// <summary>O que a tela diz enquanto confere.</summary>
    public const string Conferindo = "Conferindo seu certificado A1…";

    /// <summary>O que a tela diz depois de carregar.</summary>
    public const string Carregado =
        "A1 carregado para esta operação. Confira o titular e clique em Assinar. O arquivo não foi instalado e a senha não foi salva.";

    /// <summary>A recusa do Assinar sem certificado carregado.</summary>
    public const string CarregueAntes = "Carregue seu certificado A1 e confira o titular antes de assinar.";

    /// <summary>O filtro do diálogo de arquivo — o MESMO do WPF.</summary>
    public const string FiltroDoArquivo = "Certificado A1 (*.pfx;*.p12)|*.pfx;*.p12";

    /// <summary>
    /// A conferência do ARQUIVO antes de abrir (o code-behind do WPF fazia isto com o tamanho do
    /// stream). Nulo = serve; senão, a frase da recusa.
    /// </summary>
    public static string? RecusaDoArquivo(byte[]? arquivo)
    {
        if (arquivo is null) return "Escolha o arquivo do certificado A1.";
        if (arquivo.Length is <= 0 or > CertificadoA1.LimiteBytes) return "Escolha um arquivo A1 de até 128 KB.";
        return null;
    }

    /// <summary>
    /// Abre o A1, confere o CPF contra o profissional do LOGIN e a confiança da cadeia — exatamente
    /// o que o ViewModel WPF fazia, na mesma ordem: login → usuário e profissional ativos → abrir e
    /// exigir a confiança fora da thread da tela → o acesso não mudou no meio. Recusa com
    /// <see cref="InvalidOperationException"/> (a frase vai para a tela tal e qual); o resto é
    /// erro inesperado, e quem chama mostra a frase genérica (<see cref="FraseDaFalha"/>).
    /// </summary>
    public static async Task<CertificadoAssinatura> CarregarAsync(
        IServiceScopeFactory? escopos, byte[] arquivo, string senha,
        IConfiancaCertificadoA1? confianca = null)
    {
        if (escopos is null || !SessaoUsuario.Atual.Autenticado)
            throw new InvalidOperationException("Entre com seu usuário profissional para carregar o A1.");
        var usuarioId = SessaoUsuario.Atual.UsuarioId;
        using var escopo = escopos.CreateScope();
        var usuario = await escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>()
            .ObterUsuarioAsync(usuarioId);
        if (usuario?.Ativo != true || usuario.Profissional?.Ativo != true)
            throw new InvalidOperationException("Seu usuário precisa estar vinculado a um profissional ativo.");
        var cpf = usuario.Profissional.Cpf;
        var conferencia = confianca ?? new ConfiancaCertificadoA1();
        var certificado = await Task.Run(() =>
        {
            var c = CertificadoA1.Abrir(arquivo, senha, cpf);
            try { conferencia.Exigir(c.Certificado); return c; }
            catch { c.Dispose(); throw; }
        });
        if (SessaoUsuario.Atual.UsuarioId != usuarioId)
        {
            certificado.Dispose();
            throw new InvalidOperationException("O acesso mudou. Abra novamente a assinatura.");
        }
        return certificado;
    }

    /// <summary>A frase da falha, como o WPF a escolhia: a recusa diz o motivo; o resto, a genérica.</summary>
    public static string FraseDaFalha(Exception e)
        => e is InvalidOperationException ? e.Message : "Não foi possível carregar o A1. Confira o arquivo e tente novamente.";
}

/// <summary>
/// Assinar um documento clínico JÁ EMITIDO com o A1 — o caminho que era do
/// <c>AcoesDoDocumento.AssinarAsync</c> (shell WPF) e do
/// <c>JanelasDosProntuariosWpf.AssinarAnamneseAsync</c>. As guardas de estado e as frases são
/// as MESMAS; o ponto único das duas portas (a Blazor e a WPF que sobrou) é
/// <see cref="AssinarAsync"/>.
/// </summary>
public static class AssinaturaDeDocumentoNaTela
{
    /// <summary>
    /// Assinado não se reassina; cancelado não se assina. Todo tipo de documento clínico pode ser
    /// assinado: a lista "emitido só para impressão" (out/2026, sem A1 na clínica) saiu quando o
    /// SafeID voltou para receitas e documentos, a pedido da clínica. Assinar continua opcional —
    /// a via impressa segue valendo do jeito de antes.
    /// </summary>
    public static bool PodeAssinar(TipoDocumentoClinico tipo, bool cancelado, bool assinado)
        => !cancelado && !assinado;

    /// <summary>
    /// Por que não dá — as duas frases do WPF, antes de qualquer certificado. Nulo = pode.
    /// </summary>
    public static string? Recusa(TipoDocumentoClinico tipo, string numero, bool cancelado, bool assinado)
    {
        if (!PodeAssinar(tipo, cancelado, assinado))
            return cancelado
                ? $"O documento {numero} está cancelado e não pode ser assinado."
                : $"O documento {numero} já foi assinado digitalmente.";
        return null;
    }

    /// <summary>O assunto que o seletor de certificado mostra — o MESMO texto do WPF.</summary>
    public static string Assunto(string rotulo, string numero) => $"Assinar {rotulo.ToLowerInvariant()} {numero}";

    /// <summary>O assunto da assinatura da anamnese (a porta dos Prontuários).</summary>
    public static string AssuntoDaAnamnese(string? numero) => $"Assinar anamnese {numero}";

    /// <summary>A frase de sucesso do caminho do documento (a mesma do snackbar WPF).</summary>
    public const string Assinado = "Documento assinado. Entregue o ARQUIVO ao paciente.";

    /// <summary>
    /// A SEQUÊNCIA, movida sem mudança: escolher o certificado → (desistiu: nada é chamado) →
    /// um escopo → <c>AssinaturaDeDocumentoClinicoService.AssinarAsync(documento, certificado,
    /// usuarioId, operador)</c>. O certificado é liberado depois da assinatura, como o
    /// <c>using var certificado</c> do WPF. Nulo = a pessoa desistiu no seletor.
    /// </summary>
    /// <param name="usuarioId">O WPF passava <c>Autenticado ? UsuarioId : null</c> — quem chama passa o mesmo.</param>
    public static async Task<DocumentoAssinado?> AssinarAsync(
        IServiceScopeFactory escopos, int documentoId, string assunto,
        Func<string, Task<CertificadoAssinatura?>> escolher, int? usuarioId, string operador)
    {
        using var certificado = await escolher(assunto);
        if (certificado is null) return null;

        using var escopo = escopos.CreateScope();
        var assinaturas = escopo.ServiceProvider.GetRequiredService<AssinaturaDeDocumentoClinicoService>();
        return await assinaturas.AssinarAsync(documentoId, certificado, usuarioId, operador);
    }
}

/// <summary>O que aconteceu quando a prescrição foi liberada pelo editor.</summary>
public enum DesfechoDaLiberacao
{
    /// <summary>Há alergia e quem assina não confirmou: nada foi assinado.</summary>
    CanceladaNaAlergia,

    /// <summary>Continuidade sem A1 ligada: liberada sem assinatura digital.</summary>
    LiberadaSemAssinatura,

    /// <summary>A pessoa fechou o seletor do certificado: sair calado é o certo.</summary>
    Desistiu,

    /// <summary>Assinada com o A1 do prescritor.</summary>
    Assinada
}

/// <summary>O que a 2ª assinatura (a da enfermagem) produziu — o que a mensagem da folha lê.</summary>
public sealed record DesfechoDaAssinaturaDaExecucao(bool Desistiu, bool DocumentoArquivado, bool DocumentoUnificado)
{
    public static readonly DesfechoDaAssinaturaDaExecucao Desistencia = new(true, false, false);
}

/// <summary>
/// Os três caminhos de assinatura da PRESCRIÇÃO DE INFUSÃO — movidos linha a linha dos ViewModels
/// WPF (<c>PrescricaoInternaEdicaoViewModel.AssinarAsync</c>, <c>FolhaExecucaoViewModel.
/// ValidarMedicoAsync</c> e <c>AssinarExecucaoAsync</c>). Cada um preserva o ESCOPO de antes
/// (o editor abria um escopo para conferir e outro para assinar; a validação do médico usava
/// UM escopo para tudo) e os argumentos exatos (o editor passa <c>Autenticado ? UsuarioId : null</c>
/// para a assinatura e <c>UsuarioId</c> para a continuidade; a folha passa <c>UsuarioId</c> nos
/// dois). Quem chama passa os números que o WPF passava.
/// </summary>
public static class AssinaturaDaPrescricaoNaTela
{
    /// <summary>O título da confirmação de alergia do EDITOR.</summary>
    public const string TituloAlergiaDoEditor = "Alergia registrada";

    /// <summary>A frase quando quem prescreve não confirma a alergia.</summary>
    public const string CanceladaNaAlergia =
        "Assinatura cancelada. Corrija os itens ou revise a lista de problemas do paciente.";

    /// <summary>A mensagem da confirmação do editor, com os achados — o MESMO texto do WPF.</summary>
    public static string MensagemDeAlergiaDoEditor(ConferenciaPrescricao conferencia)
    {
        var achados = conferencia.Alertas
            .Where(a => a.Gravidade == GravidadePrescricao.Alergia)
            .Select(a => $"• {a.Item}\n  {a.Motivo}");
        return "Há item prescrito que bate com alergia registrada deste paciente:\n\n"
               + string.Join("\n\n", achados)
               + "\n\nO sistema não impede — quem decide é quem assina. Confirma "
               + "que viu os alertas e quer prosseguir?";
    }

    /// <summary>O assunto do seletor no editor.</summary>
    public static string AssuntoDoEditor(string numero, string paciente) => $"Prescrição {numero} — {paciente}";

    /// <summary>O assunto do seletor na validação do médico (folha da infusão externa).</summary>
    public static string AssuntoDaValidacao(string numero, string paciente) => $"Validar infusão {numero} · {paciente}";

    /// <summary>O assunto do seletor na 2ª assinatura (a da enfermagem).</summary>
    public static string AssuntoDaExecucao(string numero, string paciente) => $"Prescrição {numero} — execução · {paciente}";

    /// <summary>
    /// O EDITOR, depois de gravar o rascunho: conferir (um escopo) → confirmar a alergia quando
    /// há achado → [continuidade: liberar sem assinatura (outro escopo)] → escolher o certificado
    /// → assinar (outro escopo). A confirmação vem ANTES do certificado: confirmar depois de
    /// assinar é o mesmo que não confirmar.
    /// </summary>
    public static async Task<DesfechoDaLiberacao> LiberarDoEditorAsync(
        IServiceScopeFactory escopos, int prescricaoId, bool continuidadeAtiva, string assunto,
        Func<string, string, Task<bool>> confirmarAlergia,
        Func<string, Task<CertificadoAssinatura?>> escolher,
        int usuarioIdDaSessao, int? usuarioIdDaAssinatura, string operador)
    {
        var confirmouAlergia = false;

        using (var scope = escopos.CreateScope())
        {
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
            var conferencia = await servico.ConferirParaAssinaturaAsync(prescricaoId);

            if (conferencia.ExigeConfirmacao)
            {
                confirmouAlergia = await confirmarAlergia(
                    TituloAlergiaDoEditor, MensagemDeAlergiaDoEditor(conferencia));

                if (!confirmouAlergia) return DesfechoDaLiberacao.CanceladaNaAlergia;
            }
        }

        if (continuidadeAtiva)
        {
            using var scope = escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>()
                .LiberarSemAssinaturaAsync(prescricaoId, usuarioIdDaSessao, confirmouAlergia);
            return DesfechoDaLiberacao.LiberadaSemAssinatura;
        }

        using var certificado = await escolher(assunto);
        if (certificado is null) return DesfechoDaLiberacao.Desistiu;

        using (var scope = escopos.CreateScope())
        {
            var assinaturas = scope.ServiceProvider.GetRequiredService<AssinaturaDePrescricaoService>();
            await assinaturas.AssinarPrescricaoAsync(
                prescricaoId, certificado, confirmouAlergia, usuarioIdDaAssinatura, operador);
        }

        return DesfechoDaLiberacao.Assinada;
    }

    /// <summary>O título e a mensagem da confirmação na validação do médico (o MESMO texto do WPF).</summary>
    public const string TituloAlergiaDaValidacao = "Revisar alergias";
    public const string MensagemAlergiaDaValidacao =
        "Há alerta de alergia relacionado à infusão registrada. Confirma que revisou o caso antes de assinar?";

    /// <summary>
    /// A VALIDAÇÃO DO MÉDICO na folha (infusão com orientação externa), com UM escopo para tudo,
    /// como no WPF: conferir → confirmar a alergia quando há achado (sem confirmação, sai sem
    /// nada) → [continuidade: liberar sem assinatura] → escolher → assinar.
    /// </summary>
    public static async Task<DesfechoDaLiberacao> ValidarComoMedicoAsync(
        IServiceScopeFactory escopos, int prescricaoId, bool continuidadeAtiva, string assunto,
        Func<string, string, Task<bool>> confirmarAlergia,
        Func<string, Task<CertificadoAssinatura?>> escolher,
        int usuarioId, string operador)
    {
        using var scope = escopos.CreateScope();
        var conferencia = await scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>()
            .ConferirParaAssinaturaAsync(prescricaoId);
        var confirmou = conferencia.ExigeConfirmacao
            && await confirmarAlergia(TituloAlergiaDaValidacao, MensagemAlergiaDaValidacao);
        if (conferencia.ExigeConfirmacao && !confirmou) return DesfechoDaLiberacao.CanceladaNaAlergia;

        if (continuidadeAtiva)
        {
            await scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>()
                .LiberarSemAssinaturaAsync(prescricaoId, usuarioId, confirmou);
            return DesfechoDaLiberacao.LiberadaSemAssinatura;
        }

        using var certificado = await escolher(assunto);
        if (certificado is null) return DesfechoDaLiberacao.Desistiu;
        await scope.ServiceProvider.GetRequiredService<AssinaturaDePrescricaoService>().AssinarPrescricaoAsync(
            prescricaoId, certificado, confirmou, usuarioId, operador);
        return DesfechoDaLiberacao.Assinada;
    }

    /// <summary>
    /// A 2ª ASSINATURA (a da enfermagem, na folha encerrada): escolher → (desistiu: nada é
    /// chamado) → um escopo → <c>AssinarExecucaoAsync(prescrição, certificado, usuarioId,
    /// operador)</c>. Devolve o que a mensagem da folha precisa saber do arquivo.
    /// </summary>
    public static async Task<DesfechoDaAssinaturaDaExecucao> AssinarExecucaoAsync(
        IServiceScopeFactory escopos, int prescricaoId, string assunto,
        Func<string, Task<CertificadoAssinatura?>> escolher, int usuarioId, string operador)
    {
        using var certificado = await escolher(assunto);
        if (certificado is null) return DesfechoDaAssinaturaDaExecucao.Desistencia;

        using var scope = escopos.CreateScope();
        var assinaturas = scope.ServiceProvider.GetRequiredService<AssinaturaDePrescricaoService>();
        var assinada = await assinaturas.AssinarExecucaoAsync(prescricaoId, certificado, usuarioId, operador);

        var execucao = assinada.AssinaturaDaExecucao;
        var arquivado = execucao?.ArquivoId is not null;
        var unificado = arquivado && execucao!.ArquivoId == execucao.ArquivoRegistroId;
        return new DesfechoDaAssinaturaDaExecucao(false, arquivado, unificado);
    }

    /// <summary>A mensagem depois da 2ª assinatura — o MESMO texto do WPF.</summary>
    public static (string Frase, bool EhErro) MensagemDaExecucao(bool origemEnfermagem, DesfechoDaAssinaturaDaExecucao d)
        => (origemEnfermagem
                ? "Execução assinada. Aguarda a validação do médico responsável."
                : !d.DocumentoArquivado
                    ? "A assinatura foi recebida, mas o arquivo não está disponível. Avise o suporte."
                    : d.DocumentoUnificado
                        ? "Prescrição arquivada com as duas assinaturas, horários e justificativas da execução."
                        : "Prescrição arquivada com as duas assinaturas. Esta via antiga mantém os dados da execução no registro eletrônico.",
            !d.DocumentoArquivado);
}
