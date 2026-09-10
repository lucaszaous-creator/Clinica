using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Um documento clínico JÁ EMITIDO, como os atos precisam vê-lo.
///
/// É o mínimo que os seis atos consultam, e não a linha de nenhuma das telas: cada uma tem
/// a sua (a central lista nove folhas, inclusive as financeiras; a ficha e o Consultório
/// listam só as clínicas). O que atravessa é o documento, não o leiaute.
///
/// ⚠️ As duas PERMISSÕES são resolvidas aqui, do catálogo, e não recebidas: é a regra da
/// parcela 59 — receita pede <c>Prescrever</c>, declaração de comparecimento pede só a
/// ficha —, e três telas resolvendo o mesmo bit por conta própria foi exatamente como
/// nasceram as divergências que este arquivo existe para acabar.
/// </summary>
public sealed record DocumentoNaTela
{
    public required int DocumentoId { get; init; }

    /// <summary>
    /// Dono do documento. A entrega precisa do telefone dele, e a 2ª via precisa registrar
    /// de QUEM é o prontuário que saiu em PDF. Nulo só na folha financeira, cujo
    /// destinatário nem sempre é paciente do sistema.
    /// </summary>
    public int? PacienteId { get; init; }

    public required string Numero { get; init; }

    /// <summary>O tipo, que é quem decide os bits e as frases.</summary>
    public required TipoDocumentoClinico Tipo { get; init; }

    /// <summary>
    /// Como a TELA chama este papel ("Receituário" na central, "Receita" na ficha — o
    /// rótulo da central segue o mockup aprovado, e não o enum). Vem de fora de propósito:
    /// unificar o vocabulário das três telas é outra decisão, e ela é do cliente.
    /// </summary>
    public required string Rotulo { get; init; }

    public required bool Cancelado { get; init; }

    /// <summary>Selado com certificado ICP-Brasil.</summary>
    public required bool Assinado { get; init; }

    /// <summary>O link público está no ar HOJE.</summary>
    public bool LinkNoAr { get; init; }

    /// <summary>
    /// Já teve link algum dia — ou seja, tem token e um PDF assinado que o carrega no QR.
    /// É o que separa "dá para republicar" de "nunca houve link": republicar reusa o MESMO
    /// token, então o QR que o paciente já tem impresso volta a funcionar.
    /// </summary>
    public bool JaTeveLink { get; init; }

    /// <summary>O acesso para LER este papel (parcela 59).</summary>
    public Permissao AcessoParaVer => CentralDocumentosService.AcessoParaVer(Tipo);

    /// <summary>O acesso para assinar, enviar, republicar ou cancelar.</summary>
    public Permissao AcessoParaMexer => CentralDocumentosService.AcessoParaEmitir(Tipo);

    public string NomeArquivo => $"{Rotulo}-{Numero.Replace('/', '-')}.pdf";

    /// <summary>
    /// O mesmo sufixo que o serviço de assinatura grava, para a pasta de entregas não
    /// guardar duas versões do mesmo número com nomes diferentes.
    /// </summary>
    public string NomeArquivoAssinado => $"{Rotulo}-{Numero.Replace('/', '-')}-assinado.pdf";

    /// <summary>Assinado não se reassina (dois arquivos válidos do mesmo ato); cancelado não se assina.</summary>
    public bool PodeAssinar => !Cancelado && !Assinado;

    /// <summary>Só o ASSINADO se entrega como arquivo — sem assinatura, o que vale é a via de caneta.</summary>
    public bool PodeEnviar => Assinado && !Cancelado;

    // ===== A metade VISÍVEL de cada ato: o estado E o bit daquele TIPO de papel =====
    //
    // ⚠️ Estado SEM o bit deixa o botão aceso e o clique estoura no `Exigir` (parcela 41);
    // bit sem o estado oferece assinar o que já está assinado. As duas metades andam juntas,
    // e é a MESMA resolução do `Exigir` que os atos fazem — sem isso elas discordam, que é
    // o corredor sem saída da parcela 69 (o Consultório apagava o botão por
    // `EditarProntuario` e o comando exigia o bit do tipo).

    /// <summary>Oferecer "Assinar com o e-CPF" nesta linha.</summary>
    public bool OferecerAssinar => PodeAssinar && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    /// <summary>Oferecer "Enviar ao paciente".</summary>
    public bool OferecerEnviar => PodeEnviar && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    /// <summary>Oferecer "Cancelar" — cancelar duas vezes não existe.</summary>
    public bool OferecerCancelar => !Cancelado && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    /// <summary>
    /// Oferecer "Renovar o link": só para o que JÁ teve link e saiu do ar. Documento
    /// cancelado não volta — receita cancelada baixável é a pior espécie de arquivo no ar.
    /// </summary>
    public bool OferecerRenovarLink => JaTeveLink && !Cancelado && !LinkNoAr
        && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    /// <summary>
    /// Oferecer "Tirar do ar" — o inverso exato, e por isso vale INCLUSIVE para o cancelado:
    /// o cancelamento já despublica, mas se aquela remoção falhou (o provedor fora do ar na
    /// hora) o arquivo continua acessível, e é justamente aí que o botão precisa existir.
    /// </summary>
    public bool OferecerTirarDoAr => LinkNoAr && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    /// <summary>
    /// O documento recém-EMITIDO, para a tela imprimi-lo sem esperar a lista recarregar.
    ///
    /// A emissão montada (relatório de evolução, anamnese) imprime na hora, e pela linha da
    /// lista a impressão simplesmente não aconteceria quando a folha não estivesse nela —
    /// em silêncio, depois de o número ter sido gasto.
    /// </summary>
    public static DocumentoNaTela De(DocumentoClinico d, string? rotulo = null) => new()
    {
        DocumentoId = d.Id,
        PacienteId = d.PacienteId,
        Numero = d.Numero,
        Tipo = d.Tipo,
        Rotulo = rotulo ?? TipoDocumentoInfo.Rotular(d.Tipo),
        Cancelado = d.Cancelado,
        Assinado = d.AssinadoEletronicamente,
        LinkNoAr = d.LinkNoAr(DateOnly.FromDateTime(DateTime.Today)),
        JaTeveLink = !string.IsNullOrWhiteSpace(d.TokenPublicacao)
    };
}

/// <summary>
/// O que o ato fez, na frase que a tela mostra.
///
/// O CANAL é decidido aqui dentro (snackbar para confirmação passageira, inline para o que
/// precisa ficar na tela) — foi por não ser que uma tela dizia "Documento assinado" no
/// snackbar e a outra não dizia nada.
/// </summary>
/// <param name="Mudou">A lista precisa ser relida: o documento mudou de estado.</param>
/// <param name="Inline">Frase para a mensagem da tela; nula limpa a que estava lá.</param>
/// <param name="EhErro">Pinta a mensagem de erro.</param>
/// <param name="Silencioso">
/// A pessoa DESISTIU num diálogo. A tela não mexe em mensagem nenhuma — sair calado é o
/// certo aqui, e é a exceção prevista pela regra do "botão que não faz nada" (parcela 41).
/// </param>
public sealed record ResultadoAcaoDocumento(
    bool Mudou = false, string? Inline = null, bool EhErro = false, bool Silencioso = false)
{
    public static readonly ResultadoAcaoDocumento Desistiu = new(Silencioso: true);

    public static ResultadoAcaoDocumento Erro(string frase) => new(Inline: frase, EhErro: true);
}

/// <summary>
/// Os SEIS atos sobre um documento clínico já emitido, num ponto único (set/2026): 2ª via,
/// assinar com e-CPF, entregar o arquivo ao paciente, republicar o link, tirar do ar e
/// cancelar com motivo.
///
/// O problema que isto resolve
/// ---------------------------
/// Eram QUATRO cópias — a central de documentos e o "Receituário" da Recepção, a aba
/// Documentos da ficha do paciente e as Prescrições do Consultório —, e elas JÁ TINHAM
/// divergido, cada uma com um subconjunto diferente dos atos:
///
/// <list type="bullet">
/// <item>a CENTRAL, que é a tela que a direção nomeia como "a tela de Documentos", não
/// assinava e não enviava: emitia a receita e não havia como selá-la nem entregá-la;</item>
/// <item>o RECEITUÁRIO e a FICHA assinavam e enviavam, e não mexiam no link publicado;</item>
/// <item>só o CONSULTÓRIO tinha os seis.</item>
/// </list>
///
/// Nada falhava em lugar nenhum: cada tela fazia corretamente o que sabia fazer, e a
/// pessoa descobria o buraco com o paciente na frente, indo procurar outra tela para
/// terminar o trabalho. É o defeito recorrente do projeto na variante "capacidade que
/// existe numa porta só", agora DENTRO de um módulo — e o comentário da ficha dizia, por
/// escrito, que os comandos dela eram "os MESMOS da tela irmã … duas versões divergiriam
/// na primeira correção". Eram duas versões. Comentário que afirma que duas cópias são a
/// mesma coisa é a segunda definição se anunciando.
///
/// ⚠️ Aqui moram só os atos do documento CLÍNICO. A folha financeira (recibo, orçamento)
/// tem outro PDF e outro serviço, e continua na central — que é a única tela que a lista.
/// </summary>
public static class AcoesDoDocumento
{
    /// <summary>
    /// 2ª via: reimprime o que foi EMITIDO, nunca o que o prontuário diz hoje — a via que
    /// sai agora tem de ser idêntica à que o paciente levou (a regra mora no
    /// <see cref="DocumentosClinicosPdfService"/>, que devolve os bytes guardados quando o
    /// documento está assinado).
    ///
    /// ⚠️ Registra a TRILHA DE LEITURA: receita, atestado, relatório e anamnese em PDF no
    /// disco são dado de saúde SAINDO do sistema (parcela 60/62). A central já fazia isso e
    /// as outras três dependiam do registro que a tela faz na TROCA de paciente — que cobre
    /// por acidente, pela janela de silêncio da mesma origem. Aqui o registro acompanha o
    /// ato, e não a tela.
    /// </summary>
    public static async Task<ResultadoAcaoDocumento> ImprimirAsync(
        DocumentoNaTela doc, IServiceScopeFactory escopos)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(doc.AcessoParaVer, $"abrir {doc.Rotulo.ToLowerInvariant()}");

            byte[] pdf;
            using (var escopo = escopos.CreateScope())
            {
                var pdfs = escopo.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var parametros = escopo.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.GerarAsync(doc.DocumentoId, await parametros.ObterPrestadorAsync());

                // SEQUENCIAL, nunca WhenAll: é o mesmo DbContext do escopo (parcela 74).
                if (doc.PacienteId is { } id)
                    await escopo.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                        .RegistrarAsync(id, SessaoUsuario.Atual.Operador,
                            OrigemAcessoProntuario.Documento);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro(doc.NomeArquivo));

            return new ResultadoAcaoDocumento(Inline: erro, EhErro: erro is not null);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                $"Documento {doc.Numero} — segunda via não pôde ser gerada", ex);

            // O documento ESTÁ emitido: dizer só "falhou" faria a pessoa emitir de novo e
            // ficar com dois papéis numerados para o mesmo ato.
            return ResultadoAcaoDocumento.Erro(
                $"O documento {doc.Numero} está emitido, mas o PDF não pôde ser gerado: {ex.Message}");
        }
    }

    /// <summary>
    /// Assina um documento já emitido com o certificado ICP-Brasil (parcela 43).
    ///
    /// Existe porque a emissão e a assinatura nem sempre acontecem no mesmo minuto: o
    /// atestado sai às 9h e o token está na bolsa. Sem esta porta, a única saída seria
    /// cancelar e emitir outro — gastar um número de documento para resolver um problema
    /// de logística.
    ///
    /// O arquivo assinado é <b>salvo e aberto</b>, nunca mandado para a impressora: a
    /// assinatura vive nos BYTES, e a via impressa de um documento assinado é CÓPIA.
    /// </summary>
    public static async Task<ResultadoAcaoDocumento> AssinarAsync(
        DocumentoNaTela doc, IServiceScopeFactory escopos, ISnackbarService snackbar)
    {
        // Guarda de ESTADO: diz por que não dá, em vez de voltar calada — o botão já está
        // apagado, e quem chega aqui por atalho merece a frase.
        if (!doc.PodeAssinar)
            return ResultadoAcaoDocumento.Erro(doc.Cancelado
                ? $"O documento {doc.Numero} está cancelado e não pode ser assinado."
                : $"O documento {doc.Numero} já foi assinado digitalmente.");

        try
        {
            // O bit do TIPO, nunca um fixo (parcela 60): com `Prescrever` fixo as duas
            // barreiras DISCORDAVAM sobre que ato é aquele — em quatro dos oito tipos a
            // pessoa atravessava a porta, fazia o trabalho e levava a recusa no fim; e
            // quem tinha `Prescrever` sem o bit do tipo passava direto.
            SessaoUsuario.Atual.Exigir(doc.AcessoParaMexer, "assinar documento clínico");

            var certificado = EscolherCertificadoWindow.Perguntar(
                $"Assinar {doc.Rotulo.ToLowerInvariant()} {doc.Numero}",
                JanelaDona.Atual(), escopos);

            if (certificado is null) return ResultadoAcaoDocumento.Desistiu;

            DocumentoAssinado assinado;
            using (var escopo = escopos.CreateScope())
            {
                var assinaturas = escopo.ServiceProvider
                    .GetRequiredService<AssinaturaDeDocumentoClinicoService>();

                assinado = await assinaturas.AssinarAsync(
                    doc.DocumentoId, certificado,
                    SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                    SessaoUsuario.Atual.Operador);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                assinado.Pdf, ImpressaoPdf.NomeSeguro(assinado.NomeArquivo));

            if (erro is not null)
                return new ResultadoAcaoDocumento(
                    Mudou: true,
                    Inline: $"{erro} O documento foi assinado e está guardado no sistema.",
                    EhErro: true);

            snackbar.Sucesso("Documento assinado. Entregue o ARQUIVO ao paciente.");
            return new ResultadoAcaoDocumento(Mudou: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                $"Documento {doc.Numero} — assinatura não pôde ser concluída", ex);
            return ResultadoAcaoDocumento.Erro(ex.Message);
        }
    }

    /// <summary>
    /// Entrega o ARQUIVO assinado ao paciente pelo WhatsApp (parcela 43, 2ª rodada).
    ///
    /// A assinatura vive nos bytes: quem sai só com o papel leva um documento sem a
    /// garantia que o sistema produziu, e a farmácia recusa — com razão. Ver
    /// <see cref="EntregaAoPaciente"/>, inclusive por que o anexo não é automático.
    /// </summary>
    public static async Task<ResultadoAcaoDocumento> EnviarAsync(
        DocumentoNaTela doc, IServiceScopeFactory escopos)
    {
        try
        {
            // Dado de saúde SAINDO para fora — a barreira que a parcela 60 passou a cobrar
            // no export, e que faltava em duas das quatro cópias deste ato.
            SessaoUsuario.Atual.Exigir(doc.AcessoParaMexer, "enviar documento clínico");

            if (!doc.PodeEnviar)
                return ResultadoAcaoDocumento.Erro(doc.Cancelado
                    ? $"O documento {doc.Numero} está cancelado."
                    : $"O documento {doc.Numero} ainda não foi assinado digitalmente. "
                      + "Sem assinatura, o que vale é a via impressa e assinada à caneta — "
                      + "assine antes de enviar o arquivo.");

            byte[] pdf;
            Paciente? paciente = null;
            string? nomeClinica;

            using (var escopo = escopos.CreateScope())
            {
                var pdfs = escopo.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var parametros = escopo.ServiceProvider.GetRequiredService<ParametrosService>();

                // Os BYTES GUARDADOS, porque o documento está assinado — é a regra que mora
                // dentro do GerarAsync, e é o que faz o arquivo continuar válido.
                pdf = await pdfs.GerarAsync(doc.DocumentoId);

                if (doc.PacienteId is { } id)
                    paciente = await escopo.ServiceProvider
                        .GetRequiredService<PacienteService>().ObterComHistoricoAsync(id);

                var prestador = await parametros.ObterPrestadorAsync();
                nomeClinica = prestador.NomeFantasia ?? prestador.RazaoSocial;
            }

            var entrega = EntregaAoPaciente.Entregar(
                pdf, doc.NomeArquivoAssinado, paciente?.Telefone,
                paciente?.Nome ?? "paciente", doc.Rotulo, nomeClinica);

            return new ResultadoAcaoDocumento(Inline: entrega.Frase, EhErro: entrega.EhErro);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                $"Documento {doc.Numero} — não pôde ser entregue ao paciente", ex);
            return ResultadoAcaoDocumento.Erro(ex.Message);
        }
    }

    /// <summary>
    /// Põe o link vencido DE VOLTA no ar, reusando o MESMO token (parcela 53) — o QR já
    /// impresso pelo paciente volta a funcionar. Token novo obrigaria a emitir outro
    /// documento, porque o endereço está selado dentro do PDF que ele levou.
    /// </summary>
    public static async Task<ResultadoAcaoDocumento> RenovarLinkAsync(
        DocumentoNaTela doc, IServiceScopeFactory escopos, ISnackbarService snackbar)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(
                doc.AcessoParaMexer, $"republicar o link de {doc.Rotulo.ToLowerInvariant()}");

            if (doc.Cancelado)
                return ResultadoAcaoDocumento.Erro($"{doc.Numero} está cancelado e não volta ao ar.");

            using var escopo = escopos.CreateScope();
            var resultado = await escopo.ServiceProvider
                .GetRequiredService<PublicacaoDocumentoService>().RenovarAsync(doc.DocumentoId);

            // ⚠️ `NaoSeAplica` devolve os três campos NULOS — é o caso de a publicação estar
            // desligada, e "não aconteceu nada" precisa de frase própria, que diga onde se
            // liga. Com o Erro nulo a tela ficaria muda depois do clique. A frase é a que a
            // central já tinha; a cópia do Consultório dizia só "não foi possível
            // republicar", e ao unificar é a MELHOR das duas que vale.
            if (!resultado.Publicou)
                return ResultadoAcaoDocumento.Erro(resultado.Erro
                    ?? "A publicação está desligada: cadastre o domínio da clínica em "
                       + "Configurações → Publicação.");

            // Snackbar, e não inline: o fato DURÁVEL volta na própria lista, que passa a
            // dizer "link no ar até 09/10" na linha.
            snackbar.Sucesso(
                $"{doc.Numero} de volta ao ar até {resultado.Ate:dd/MM/yyyy}. "
                + "O QR já impresso volta a funcionar.");

            return new ResultadoAcaoDocumento(Mudou: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                $"Documento {doc.Numero} — link não pôde ser renovado", ex);
            return ResultadoAcaoDocumento.Erro(ex.Message);
        }
    }

    /// <summary>
    /// O par do renovar: tira do ar AGORA um link publicado. Receita publicada por engano —
    /// o paciente errado, o documento errado — não espera o prazo de 30 ou 180 dias.
    /// </summary>
    public static async Task<ResultadoAcaoDocumento> TirarDoArAsync(
        DocumentoNaTela doc, IServiceScopeFactory escopos, IDialogoService dialogo,
        ISnackbarService snackbar)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(
                doc.AcessoParaMexer, $"tirar do ar o link de {doc.Rotulo.ToLowerInvariant()}");

            if (!doc.LinkNoAr)
                return ResultadoAcaoDocumento.Erro($"{doc.Numero} não tem link no ar para tirar.");

            if (!dialogo.Confirmar("Tirar o link do ar",
                    $"Tirar do ar o link de {doc.Numero}?\n\n"
                    + "O QR impresso que o paciente levou para a farmácia para de abrir "
                    + "imediatamente. O documento e a assinatura continuam guardados, e o "
                    + "link pode voltar depois pelo \"Renovar link\"."))
                return ResultadoAcaoDocumento.Desistiu;

            using var escopo = escopos.CreateScope();
            var documentos = escopo.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            if (await documentos.ObterAsync(doc.DocumentoId) is not { } documento)
                return ResultadoAcaoDocumento.Erro($"{doc.Numero} não foi encontrado.");

            var saiu = await escopo.ServiceProvider
                .GetRequiredService<PublicacaoDocumentoService>()
                .DespublicarAsync(documento, SessaoUsuario.Atual.Operador);

            // O provedor recusou a remoção: o arquivo CONTINUA no ar. Dizer "saiu" aqui seria
            // a pior mentira desta tela — a pessoa concluiria que resolveu.
            if (!saiu)
                return ResultadoAcaoDocumento.Erro(
                    $"{doc.Numero} NÃO saiu do ar: o armazenamento recusou a remoção. "
                    + "O link continua acessível. Tente de novo em instantes; persistindo, "
                    + "o caminho do arquivo está no log de erros.");

            snackbar.Sucesso($"{doc.Numero} saiu do ar. O documento continua guardado.");
            return new ResultadoAcaoDocumento(Mudou: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                $"Documento {doc.Numero} — link não pôde ser tirado do ar", ex);
            return ResultadoAcaoDocumento.Erro(ex.Message);
        }
    }

    /// <summary>
    /// Cancela com MOTIVO. Não apaga: o número continua queimado e a linha continua na
    /// lista, marcada — a via que o paciente levou não desaparece por ser apagada do
    /// sistema (ponto 1 do compromisso de conformidade).
    /// </summary>
    public static async Task<ResultadoAcaoDocumento> CancelarAsync(
        DocumentoNaTela doc, IServiceScopeFactory escopos, IDialogoService dialogo,
        ISnackbarService snackbar)
    {
        if (doc.Cancelado)
            return ResultadoAcaoDocumento.Erro($"{doc.Numero} já está cancelado.");

        try
        {
            // Cancelar é do mesmo peso de emitir, e cobra o mesmo bit do TIPO.
            SessaoUsuario.Atual.Exigir(doc.AcessoParaMexer, "cancelar documento clínico");

            var motivo = dialogo.PerguntarTexto(
                "Cancelar documento",
                $"Por que o(a) {doc.Rotulo.ToLowerInvariant()} {doc.Numero} está sendo cancelado? "
                + "Ele continua na lista, marcado como cancelado — a via impressa não desaparece "
                + "por ser apagada do sistema.");

            // Motivo OBRIGATÓRIO: `PerguntarTexto` devolve nulo quando a pessoa desiste, e
            // aqui o vazio não é resposta (checagem 39).
            if (string.IsNullOrWhiteSpace(motivo)) return ResultadoAcaoDocumento.Desistiu;

            using var escopo = escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<DocumentoClinicoService>()
                .CancelarAsync(doc.DocumentoId, motivo, SessaoUsuario.Atual.Operador);

            snackbar.Info("Documento cancelado.");
            return new ResultadoAcaoDocumento(Mudou: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                $"Documento {doc.Numero} — não pôde ser cancelado", ex);
            return ResultadoAcaoDocumento.Erro(ex.Message);
        }
    }
}
