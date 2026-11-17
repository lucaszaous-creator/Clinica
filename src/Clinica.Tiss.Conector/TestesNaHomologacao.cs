using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Tiss.Conector;

/// <summary>O relógio dos comandos (a hora da clínica); o teste o troca para fixar o dia da janela.</summary>
public sealed record RelogioDoConector(Func<DateTime> Agora);

/// <summary>O que um teste na homologação devolve: deu para ir e voltar, e as linhas para o terminal.</summary>
public sealed record ResultadoDoTesteTiss(bool Foi, IReadOnlyList<string> Linhas);

/// <summary>A guia fictícia do <c>testar-lote --exemplo</c>: só a carteira é real (a homologação pede uma válida).</summary>
public sealed record GuiaDeExemploTiss(string Carteira, string? Senha = null, string? Token = null,
    string Procedimento = "10101012", decimal Valor = 1.00m, string? CpfProfissional = null);

/// <summary>
/// OS TESTES NA HOMOLOGAÇÃO (nov/2026, Amil): elegibilidade, lote e situação do protocolo, cada um UMA
/// ida, pelos comandos <c>testar-*</c> do servidor. São o ÚNICO caminho de um lote nesta fase, e por
/// isso nascem cercados:
/// <list type="bullet">
/// <item>só rodam com o convênio em HOMOLOGAÇÃO, e conferem que o endereço resolvido é o de homologação
/// do catálogo — um teste nunca cai em produção;</item>
/// <item>funcionam com o envio do convênio desligado (não passam pela fila nem pelas chaves de envio);</item>
/// <item>não marcam guia como enviada, não criam transação, não tocam cobrança: só LEEM a guia
/// conferida e acrescentam UMA linha à trilha, com o resultado (sem carteira e sem senha).</item>
/// </list>
/// </summary>
public sealed class TestesNaHomologacao(
    IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais,
    TissWebserviceCliente transporte, Func<DateTime> agora)
{
    public static string FraseSoEmHomologacao(string convenio)
        => $"{convenio} está em PRODUÇÃO: os testes só rodam na homologação. Use antes: comando.sh ambiente {convenio} homologacao --operador NOME.";

    private sealed record Destino(RemetenteTissWs Remetente, Uri Endereco, string SenhaMd5, CodificacaoTissWs Codificacao, OperadoraTiss Operadora);

    private async Task<(Destino? Destino, string? Recusa)> PrepararAsync(string convenio, TipoTransacaoTiss tipo, CancellationToken ct)
    {
        if (!await credenciais.EmHomologacaoAsync(convenio, ct)) return (null, FraseSoEmHomologacao(convenio));
        var operadora = await credenciais.OperadoraAsync(convenio, ct);
        if (operadora?.EnderecoHomologacao is not { } homologacao)
            return (null, $"O sistema não conhece a homologação de {convenio}.");
        var credencial = await credenciais.AbrirParaEnvioAsync(convenio, ct);
        if (credencial is null)
            return (null, $"Não há login do web service cadastrado para {convenio}: comando.sh credencial {convenio} --senha-na-entrada --operador NOME.");
        var dados = await DadosDoConvenio.LerAsync(repo, parametros, credenciais, convenio, tipo, credencial, ct);
        if (dados.Pendencia is { } pendencia) return (null, pendencia);
        // A guarda que importa: o endereço que vai ser usado é o de HOMOLOGAÇÃO, nunca outro.
        if (dados.Endereco != homologacao)
            return (null, $"O endereço resolvido não é o da homologação ({dados.Endereco?.Host}). Nada foi enviado.");
        return (new Destino(dados.Remetente(credencial), homologacao, credencial.SenhaMd5, OperadorasTiss.CodificacaoDe(operadora), operadora), null);
    }

    /// <summary>Uma consulta de elegibilidade na homologação (padrão da operadora para elegibilidade).</summary>
    public async Task<ResultadoDoTesteTiss> ElegibilidadeAsync(string convenio, string carteira, string operador, string? soapAction = null, CancellationToken ct = default)
    {
        var (d, recusa) = await PrepararAsync(convenio, TipoTransacaoTiss.Elegibilidade, ct);
        if (d is null) return Recusado(recusa!);
        string envelope;
        try
        {
            envelope = TissElegibilidadeWs.MontarEnvelope(new PedidoElegibilidadeTiss
            {
                RegistroAnsOperadora = d.Remetente.RegistroAnsOperadora, CodigoPrestadorNaOperadora = d.Remetente.CodigoPrestadorNaOperadora,
                NumeroCarteira = carteira.Trim(), Login = d.Remetente.Login, SenhaMd5 = d.SenhaMd5,
                Sequencial = Sequencial(), Momento = agora()
            });
        }
        catch (ArgumentException ex) { return Recusado("Dados recusados antes do envio: " + ex.Message); }

        var bruta = await Enviar(d, envelope, soapAction, ct);
        var linhas = Cabecalho("elegibilidade", d, OperadorasTiss.VersaoPadrao);
        string desfecho;
        if (bruta.Corpo is null) desfecho = Ocultar(bruta.Falha ?? "sem resposta", d);
        else desfecho = TissElegibilidadeWs.LerResposta(bruta.Corpo).Resumo;
        linhas.AddRange(Resposta(bruta, desfecho, d));
        await TrilhaAsync(operador, $"Teste de elegibilidade na homologação ({convenio}): {Curto(desfecho)}", ct);
        return new(bruta.Corpo is not null, linhas);
    }

    /// <summary>
    /// UM lote na homologação, com as guias conferidas indicadas (lidas, nunca alteradas) ou com uma
    /// guia fictícia (<paramref name="exemplo"/>), no padrão da operadora para lote (Amil: 4.03.00).
    /// </summary>
    public async Task<ResultadoDoTesteTiss> LoteAsync(string convenio, IReadOnlyList<int> guias, GuiaDeExemploTiss? exemplo,
        string operador, string? soapAction = null, CancellationToken ct = default, bool mesmoAssim = false)
    {
        if ((guias.Count == 0) == (exemplo is null))
            return Recusado("Informe as guias (--guias ID[,ID…]) OU --exemplo --carteira N, um dos dois.");
        if (guias.Count > TransacaoTissLimites.GuiasPorLote)
            return Recusado($"Um lote leva até {TransacaoTissLimites.GuiasPorLote} guias.");
        var (d, recusa) = await PrepararAsync(convenio, TipoTransacaoTiss.EnvioGuia, ct);
        if (d is null) return Recusado(recusa!);
        // A janela vale também na homologação (nov/2026: a Amil recusou com 3091 fora do 1º ao 5º dia
        // útil). Avisar antes de chamar economiza uma ida que já se sabe recusada.
        if (d.Operadora.JanelaDeEnvio is { } janela && !PrazosTiss.DentroDaJanela(janela, agora()) && !mesmoAssim)
            return Recusado($"A {d.Operadora.Nome} só aceita lote do {janela.PrimeiroDiaUtil}º ao {janela.UltimoDiaUtil}º dia útil do mês, "
                            + $"das {janela.Abre:HH\\:mm} às {janela.Fecha:HH\\:mm}, também na homologação (recusa {FilaTiss.GlosaForaDaJanela}); "
                            + $"próxima janela: {PrazosTiss.ProximaAbertura(janela, agora()):dd/MM 'às' HH:mm}. Para chamar assim mesmo: --mesmo-assim.");

        var carimbo = agora().ToString("HHmmss", CultureInfo.InvariantCulture);
        var elementos = new List<System.Xml.Linq.XElement>();
        try
        {
            if (exemplo is not null)
                elementos.Add(TissExportService.GuiaConferida(await GuiaDeExemploAsync(d, exemplo, ct), $"HX{carimbo}"));
            else
            {
                var codigos = (await repo.GuiasConferidasParaEnvioTissAsync(guias, ct)).ToDictionary(c => c.Id);
                foreach (var id in guias.Distinct())
                {
                    if (!codigos.TryGetValue(id, out var codigo)) return Recusado($"A guia {id} não existe no sistema.");
                    if (codigo.GuiaAns is not { } ans) return Recusado($"A guia {id} não tem a conferência no padrão ANS.");
                    if (ans.RegistroAns != d.Operadora.RegistroAns) return Recusado($"A guia {id} não é da {d.Operadora.Nome} (registro ANS {ans.RegistroAns}).");
                    if (GuiaAnsNaBaixa.ModeloDe(ans) != d.Operadora.ModeloDaGuia)
                        return Recusado($"A guia {id} foi conferida como {GuiaAnsNaBaixa.ModeloDe(ans)}, e a {d.Operadora.Nome} recebe {d.Operadora.ModeloDaGuia}.");
                    // Número próprio do teste ("H" + guia + hora): nunca o mesmo de um envio de produção.
                    elementos.Add(TissExportService.GuiaConferida(codigo, $"H{id}T{carimbo}"));
                }
            }
        }
        catch (ArgumentException ex) { return Recusado("Guia recusada antes do envio: " + ex.Message); }

        string envelope;
        try
        {
            envelope = TissLoteGuiasWs.MontarEnvelope(new PedidoLoteGuiasTiss
            {
                Remetente = d.Remetente, Sequencial = Sequencial(), Momento = agora(),
                NumeroLote = "8" + agora().ToString("MMddHHmmss", CultureInfo.InvariantCulture), Guias = elementos
            });
        }
        catch (ArgumentException ex) { return Recusado("Lote recusado antes do envio: " + ex.Message); }

        var bruta = await Enviar(d, envelope, soapAction, ct);
        var linhas = Cabecalho($"lote de {elementos.Count} guia(s){(exemplo is null ? "" : " (fictícia)")}", d, d.Remetente.VersaoTiss);
        string desfecho;
        string? protocolo = null;
        if (bruta.Corpo is null) desfecho = Ocultar(bruta.Falha ?? "sem resposta", d);
        else
        {
            var lida = TissLoteGuiasWs.LerResposta(bruta);
            desfecho = lida.Resumo;
            protocolo = lida.Protocolo;
        }
        linhas.AddRange(Resposta(bruta, desfecho, d));
        if (protocolo is not null)
        {
            linhas.Add($"PROTOCOLO: {protocolo}");
            linhas.Add($"Situação dele: comando.sh testar-protocolo {convenio} --protocolo {protocolo} --operador NOME");
        }
        linhas.Add("Nada foi gravado nas guias nem na fila: é teste.");
        await TrilhaAsync(operador, $"Lote de TESTE na homologação ({convenio}), {elementos.Count} guia(s){(exemplo is null ? "" : " fictícia")}: "
                                    + (protocolo is null ? Curto(desfecho) : $"protocolo {protocolo}"), ct);
        return new(protocolo is not null, linhas);
    }

    /// <summary>A situação de um protocolo na homologação.</summary>
    public async Task<ResultadoDoTesteTiss> ProtocoloAsync(string convenio, string protocolo, string operador, string? soapAction = null, CancellationToken ct = default)
    {
        var (d, recusa) = await PrepararAsync(convenio, TipoTransacaoTiss.StatusProtocolo, ct);
        if (d is null) return Recusado(recusa!);
        string envelope;
        try
        {
            envelope = TissStatusProtocoloWs.MontarEnvelope(new PedidoStatusProtocoloTiss
            {
                Remetente = d.Remetente, Sequencial = Sequencial(), Momento = agora(), NumeroProtocolo = protocolo.Trim()
            });
        }
        catch (ArgumentException ex) { return Recusado("Dados recusados antes do envio: " + ex.Message); }

        var bruta = await Enviar(d, envelope, soapAction, ct);
        var linhas = Cabecalho($"situação do protocolo {protocolo.Trim()}", d, d.Remetente.VersaoTiss);
        var desfecho = bruta.Corpo is null ? Ocultar(bruta.Falha ?? "sem resposta", d) : TissStatusProtocoloWs.LerResposta(bruta).Resumo;
        linhas.AddRange(Resposta(bruta, desfecho, d));
        await TrilhaAsync(operador, $"Teste de situação do protocolo na homologação ({convenio}): {Curto(desfecho)}", ct);
        return new(bruta.Corpo is not null, linhas);
    }

    // ==================== peças ====================

    /// <summary>
    /// A guia fictícia: o contratado é a clínica (código, CNES e nome dos dados do prestador), o
    /// profissional é de MENTIRA (a homologação pode recusá-lo — é o que o teste quer ver) e a carteira
    /// é a informada. Fica em memória: nada é gravado.
    /// </summary>
    private async Task<CodigoFaturamento> GuiaDeExemploAsync(Destino d, GuiaDeExemploTiss e, CancellationToken ct)
    {
        var prestador = await parametros.ObterPrestadorAsync(ct);
        var hoje = DateOnly.FromDateTime(agora());
        var sp = d.Operadora.ModeloDaGuia == ModeloDaGuiaTiss.SpSadt;
        return new CodigoFaturamento
        {
            GuiaAns = new GuiaTissConsulta
            {
                RegistroAns = d.Operadora.RegistroAns, NumeroCarteira = e.Carteira.Trim(),
                CodigoPrestadorNaOperadora = d.Remetente.CodigoPrestadorNaOperadora,
                NomeContratado = prestador.RazaoSocial ?? prestador.NomeFantasia ?? "Clínica",
                Cnes = string.IsNullOrWhiteSpace(prestador.Cnes) ? GuiaAnsNaBaixa.CnesDeQuemNaoTem : prestador.Cnes.Trim(),
                NomeProfissional = "PROFISSIONAL DE TESTE", ConselhoProfissional = "06", NumeroConselho = "000000",
                UfConselho = "33", Cbos = "225125",
                IndicacaoAcidente = GuiaAnsNaBaixa.AcidentePadrao, RegimeAtendimento = GuiaAnsNaBaixa.RegimePadrao,
                DataAtendimento = hoje, CodigoTabela = GuiaAnsNaBaixa.TabelaPadrao, TipoConsulta = GuiaAnsNaBaixa.TipoConsultaPrimeira,
                CodigoProcedimento = e.Procedimento, ValorProcedimento = e.Valor,
                ModeloGuia = sp ? GuiaAnsNaBaixa.ModeloGuiaSpSadt : null,
                Senha = e.Senha, DataAutorizacao = hoje,
                FormaConfirmacao = string.IsNullOrWhiteSpace(e.Token) ? GuiaAnsNaBaixa.ConfirmacaoNenhuma : GuiaAnsNaBaixa.ConfirmacaoToken,
                CodigoValidacao = e.Token,
                // A equipe (Amil): o profissional pelo CPF, quando --cpf-profissional é dado.
                EquipeCpf = Cpf.Valido(Cpf.Normalizar(e.CpfProfissional)) ? Cpf.Normalizar(e.CpfProfissional) : null,
                GrauParticipacao = Cpf.Valido(Cpf.Normalizar(e.CpfProfissional)) ? DominiosTiss.GrauClinico : null,
                // Sem token: "03 - instabilidade de sistemas" é o motivo menos falso para um teste.
                AusenciaCodigoValidacao = string.IsNullOrWhiteSpace(e.Token) ? "03" : null,
                TipoAtendimento = DominiosTiss.TipoAtendimentoConsulta, CaraterAtendimento = "1",
                DescricaoProcedimento = "Consulta em consultório"
            }
        };
    }

    private async Task<RespostaBrutaTissWs> Enviar(Destino d, string envelope, string? soapAction, CancellationToken ct)
        => await transporte.EnviarAsync(d.Endereco,
            TissWebserviceCliente.ComDeclaracao(envelope, TissWebserviceCliente.NomeDa(d.Codificacao)), ct, d.Codificacao, soapAction);

    private static List<string> Cabecalho(string oQue, Destino d, string versao)
        => [$"HOMOLOGAÇÃO {d.Operadora.Nome}: {oQue}, TISS {versao}, em {d.Endereco}"];

    private static IEnumerable<string> Resposta(RespostaBrutaTissWs bruta, string desfecho, Destino d)
    {
        yield return $"HTTP {bruta.StatusHttp?.ToString(CultureInfo.InvariantCulture) ?? "-"} · {Ocultar(desfecho, d)}";
        // Página que não é SOAP (erro do gateway): o trecho diz quem respondeu.
        if (bruta.Corpo is { } corpo && bruta.StatusHttp >= 400 && !TissWebserviceCliente.PareceSoap(corpo))
            yield return "Corpo: " + Ocultar(TissWebserviceCliente.Trecho(corpo), d);
    }

    private static string Ocultar(string texto, Destino d) => ExecutorDeTransacoesTiss.Ocultar(texto, [d.SenhaMd5]);

    private async Task TrilhaAsync(string operador, string detalhe, CancellationToken ct)
    {
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "servidor" : operador.Trim(),
            Acao = "TesteTissHomologacao", Detalhe = Curto(detalhe, 400)
        }, ct);
        await repo.SalvarAsync(ct);
    }

    private string Sequencial() => agora().ToString("yyMMddHHmmss", CultureInfo.InvariantCulture);

    private static string Curto(string texto, int maximo = 200) => texto.Length <= maximo ? texto : texto[..maximo] + "…";

    private static ResultadoDoTesteTiss Recusado(string frase) => new(false, ["Nada foi enviado. " + frase]);
}
