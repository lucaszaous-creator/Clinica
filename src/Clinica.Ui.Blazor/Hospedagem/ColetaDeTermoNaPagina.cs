using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Formularios.Termo;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O PONTO ÚNICO por onde toda coleta de termo passa nas telas Blazor (out/2026: substitui o
/// <c>ColetaDeTermo</c> do shell WPF, que abria as três janelas). São as mesmas portas — a ficha,
/// a seção de atendimento, a enfermagem e a central de documentos — e o mesmo trabalho: escolher
/// o modelo quando ele não vem decidido, resolver a sessão (perguntando só quando há dúvida) e
/// abrir a coleta. As regras moram em <see cref="ColetaDeTermoNaTela"/> (Application, com teste).
///
/// ⚠️ Quem chama deve RECARREGAR de qualquer forma, e não só no verdadeiro: abrir a coleta já
/// EMITE o termo numerado, então mesmo o "fechei sem assinar" mudou o que a tela de trás mostra.
/// </summary>
public sealed class ColetaDeTermoNaPagina(JanelasNaTela janelas, IServiceScopeFactory escopos)
{
    /// <summary>
    /// Abre a coleta do termo do PROCEDIMENTO. Modelo nulo = pergunta antes qual termo (a porta
    /// avulsa). Verdadeiro quando o paciente assinou ou recusou.
    /// </summary>
    public async Task<bool> AbrirAsync(int pacienteId, string pacienteNome, int? modeloId = null,
        int? documentoId = null, int? profissionalId = null, int? agendamentoId = null)
    {
        if (modeloId is null)
        {
            var modelo = await janelas.AbrirAsync<EscolherTermoForm, ModeloDocumento>(
                new Dictionary<string, object?> { ["PacienteNome"] = pacienteNome });
            if (modelo is null) return false;
            modeloId = modelo.Id;
        }

        var (sessaoId, rotulo, desistiu) = await ResolverSessaoAsync(pacienteId, pacienteNome, modeloId.Value, documentoId, agendamentoId);
        if (desistiu) return false;

        return await janelas.AbrirAsync<AssinaturaDoPacienteForm, bool>(new Dictionary<string, object?>
        {
            ["PacienteId"] = pacienteId,
            ["PacienteNome"] = pacienteNome,
            ["ModeloId"] = modeloId,
            ["DocumentoId"] = documentoId,
            ["ProfissionalId"] = profissionalId,
            ["AgendamentoId"] = sessaoId,
            ["SessaoDoTermo"] = rotulo
        });
    }

    /// <summary>
    /// Colhe o TERMO LGPD (parcela 89) — a MESMA coleta do termo de procedimento; o que muda é a
    /// AUSÊNCIA de modelo (o termo é montado das finalidades).
    /// </summary>
    public Task<bool> AbrirConsentimentoLgpdAsync(int pacienteId, string pacienteNome, int? documentoId = null)
        => janelas.AbrirAsync<AssinaturaDoPacienteForm, bool>(new Dictionary<string, object?>
        {
            ["PacienteId"] = pacienteId,
            ["PacienteNome"] = pacienteNome,
            ["ModeloId"] = null,
            ["DocumentoId"] = documentoId
        });

    /// <summary>
    /// A qual SESSÃO este termo pertence — os três degraus de <see cref="ColetaDeTermoNaTela.Decidir"/>.
    /// ⚠️ Falha de leitura NÃO impede colher: sem a agenda, o termo nasce avulso e a assinatura acontece.
    /// </summary>
    private async Task<(int? Sessao, string? Rotulo, bool Desistiu)> ResolverSessaoAsync(
        int pacienteId, string pacienteNome, int modeloId, int? documentoId, int? agendamentoId)
    {
        if (documentoId is not null || agendamentoId is not null)
        {
            var direta = ColetaDeTermoNaTela.Decidir(documentoId, agendamentoId, null);
            return (direta.Sessao, direta.Rotulo, false);
        }

        IReadOnlyList<SessaoParaTermo> sessoes;
        try
        {
            using var escopo = escopos.CreateScope();
            sessoes = await escopo.ServiceProvider.GetRequiredService<TermoProcedimentoService>()
                .SessoesParaTermoAsync(pacienteId, DateOnly.FromDateTime(DateTime.Today));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Coleta de termo — a agenda do paciente não pôde ser lida", ex);
            return (null, null, false);
        }

        var decisao = ColetaDeTermoNaTela.Decidir(null, null, sessoes);
        if (!decisao.Perguntar) return (decisao.Sessao, decisao.Rotulo, false);

        string nomeDoTermo;
        using (var escopo = escopos.CreateScope())
            nomeDoTermo = await NomeDoModeloAsync(escopo.ServiceProvider.GetRequiredService<TermoProcedimentoService>(), modeloId);

        // As sessões vão PRONTAS para o formulário: quem já as leu foi esta função.
        var escolha = await janelas.AbrirAsync<EscolherSessaoDoTermoForm, OpcaoSessaoTermo>(new Dictionary<string, object?>
        {
            ["Sessoes"] = sessoes,
            ["PacienteNome"] = pacienteNome,
            ["NomeDoTermo"] = nomeDoTermo
        });
        if (escolha is null) return (null, null, true);
        return (escolha.AgendamentoId, ColetaDeTermoNaTela.RotuloDaEscolha(sessoes, escolha.AgendamentoId), false);
    }

    private static async Task<string> NomeDoModeloAsync(TermoProcedimentoService termos, int modeloId)
    {
        try
        {
            var modelos = await termos.ModelosDisponiveisAsync();
            return modelos.FirstOrDefault(m => m.Id == modeloId)?.Nome ?? "Termo";
        }
        catch
        {
            // O nome do termo é enfeite do cabeçalho; falhar aqui não pode impedir a escolha.
            return "Termo";
        }
    }
}
