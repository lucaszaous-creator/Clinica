using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>O que a emissão conseguiu fazer, já na frase que a tela mostra.</summary>
public sealed record ResultadoFicha(string Frase, bool EhErro);

/// <summary>
/// A FICHA DO ATENDIMENTO — o papel que o paciente leva embora (parcela 78), num PONTO
/// ÚNICO (set/2026).
///
/// Por que ela subiu para cá
/// -------------------------
/// A emissão existia em TRÊS cópias — <c>AtendimentoViewModel</c>,
/// <c>AtendimentoEnfermagemViewModel</c> e <c>EnfermagemViewModel</c> —, e elas JÁ tinham
/// divergido em três pontos, cada um deles uma regra que só existia numa delas:
///
/// <list type="number">
///   <item><b>A guarda de paciente</b>: só a da enfermagem DIZIA por que não dava
///   ("Abra um paciente…"). As outras duas voltavam caladas — o botão que não faz nada
///   da parcela 41, em duas das três.</item>
///   <item><b>A recusa de permissão</b>: numa ela virava frase na tela; nas outras subia
///   como exceção até a rede do dispatcher.</item>
///   <item><b>A guarda do rascunho não gravado</b> — a mais cara: só a do médico impedia
///   imprimir com texto digitado e não salvo. A da enfermagem, que TAMBÉM tem rascunho,
///   entregava ao paciente um papel sem o que a técnica acabara de escrever.</item>
/// </list>
///
/// Agora seria uma QUARTA cópia (a sessão aberta a partir do prontuário), e a lição do
/// projeto é a mesma desde a parcela 42: duas definições da mesma regra divergem na
/// primeira correção, e a que ninguém lembra de ajustar é a que fica errada.
///
/// ⚠️ Ela NUNCA lança — toda falha vira frase, como o <see cref="EntregaAoPaciente"/>.
/// Aqui isso não é conforto: a recusa de permissão e a de rascunho precisam APARECER na
/// tela para a pessoa saber o que fazer, e exceção que sobe até o dispatcher vira "o
/// sistema quebrou" em vez de "salve a sessão antes".
/// </summary>
public static class FichaDoAtendimento
{
    /// <summary>
    /// Emite o relatório de evolução recortado em UM DIA, gera o PDF e o abre.
    /// </summary>
    /// <param name="dia">
    /// O dia do FATO, nunca hoje. As portas abrem sessão de dias passados (a lista do
    /// prontuário, a dívida de registro, a Minha semana), e recortar em hoje devolveria
    /// "não há registro no prontuário para relatar neste período" numa sessão que existe.
    /// </param>
    /// <param name="temRascunhoNaoGravado">
    /// A tela tem texto digitado que ainda não está no prontuário. Parâmetro
    /// OBRIGATÓRIO de propósito: com valor padrão, a tela nova nasceria sem responder à
    /// pergunta e o defeito voltaria calado — o compilador achando os chamadores é o que
    /// faz cada porta decidir explicitamente (a lição da autoria da fila, parcela 69).
    /// Lista não tem rascunho e passa <c>false</c>; tela de escrita passa o que ela sabe.
    /// </param>
    /// <param name="contextoDoLog">
    /// Quem chamou, para o log dizer de qual tela veio — é a única coisa que as três
    /// cópias tinham direito de manter diferente.
    /// </param>
    public static async Task<ResultadoFicha> EmitirAsync(
        IServiceScopeFactory escopos, int pacienteId, DateOnly dia,
        bool temRascunhoNaoGravado, string contextoDoLog)
    {
        // Guarda que DIZ por que não dá, em vez de voltar calada (parcela 41). Ela é a
        // segunda barreira: a primeira é o `IsEnabled` do botão em cada tela.
        if (pacienteId == 0)
            return new ResultadoFicha(
                "Escolha um paciente para emitir a ficha do atendimento.", true);

        // ⚠️ A pergunta é "há rascunho?", NUNCA "a sessão está em branco" — confundir as
        // duas é o defeito da parcela 74: a sessão de acupuntura mais comum da casa
        // (EVA 8→3, pontos no mapa, nenhuma linha de texto) está "em branco" para efeito
        // de encerrar e tem MUITO o que gravar.
        //
        // Emitir é um FATO: o papel é numerado, fica na lista do paciente e não se apaga
        // — cancela-se com motivo. Imprimir o que ainda não está no prontuário entregaria
        // ao paciente uma versão que o prontuário não tem.
        if (temRascunhoNaoGravado)
            return new ResultadoFicha(
                "Salve a sessão antes de imprimir a ficha — o papel sai do que está "
                + "gravado no prontuário, e o que você digitou ainda não está.", true);

        try
        {
            // A ficha IMPRIME o prontuário, não o escreve: o bit é o de LER.
            SessaoUsuario.Atual.Exigir(
                Permissao.VerProntuario, "imprimir a ficha do atendimento");

            byte[] pdf;
            string numero;
            using (var escopo = escopos.CreateScope())
            {
                var servicos = escopo.ServiceProvider;

                // SEQUENCIAL, nunca WhenAll: é o mesmo DbContext do escopo (parcela 74).
                var documento = await servicos.GetRequiredService<DocumentoClinicoService>()
                    .EmitirRelatorioEvolucaoAsync(
                        pacienteId, SessaoUsuario.Atual.ProfissionalId,
                        inicio: dia, fim: dia,
                        operador: SessaoUsuario.Atual.Operador);

                numero = documento.Numero;

                pdf = await servicos.GetRequiredService<DocumentosClinicosPdfService>()
                    .GerarAsync(
                        documento.Id,
                        await servicos.GetRequiredService<ParametrosService>()
                            .ObterPrestadorAsync());

                // ⚠️ A trilha que faltava nas três cópias (ponto 4 do compromisso, e a
                // lição da parcela 60): a ficha é dado de saúde SAINDO para um arquivo no
                // disco, que é o caminho mais fácil de ele sair da clínica e o que uma
                // investigação procura. `RegistrarAsync` não lança e loga por dentro, então
                // ela entra no mesmo escopo sem poder derrubar a emissão.
                await servicos.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ExportacaoClinica);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf,
                ImpressaoPdf.NomeSeguro($"Ficha-do-atendimento-{numero.Replace('/', '-')}.pdf"));

            // ⚠️ Falha ao ABRIR o leitor não desfaz a emissão: o documento existe, está
            // numerado e está na lista do paciente. A frase diz isso — senão a pessoa
            // emite de novo e a clínica fica com dois papéis do mesmo atendimento.
            return erro is null
                ? new ResultadoFicha(
                    $"Ficha {numero} emitida — ela fica na lista de documentos do paciente.",
                    false)
                : new ResultadoFicha(
                    $"Ficha {numero} emitida e gravada, mas o leitor de PDF não abriu: {erro}",
                    true);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar($"{contextoDoLog} — ficha do atendimento não pôde ser emitida", ex);
            return new ResultadoFicha(ex.Message, true);
        }
    }
}
