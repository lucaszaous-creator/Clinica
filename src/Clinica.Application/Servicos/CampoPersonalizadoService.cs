using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O catálogo dos CAMPOS PERSONALIZADOS da sessão (set/2026) — ver
/// <see cref="CampoPersonalizadoProntuario"/> para o que ele é e o que ele não é.
///
/// A regra que atravessa tudo aqui: <b>aplicar COPIA</b>. O valor gravado leva o rótulo e
/// o tipo de quando foi escrito, e é por isso que a definição pode ser renomeada ou
/// desativada sem reescrever o prontuário de ninguém.
/// </summary>
public sealed class CampoPersonalizadoService
{
    private readonly IClinicaRepositorio _repo;

    public CampoPersonalizadoService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Teto de campos ATIVOS. Não é limitação técnica: é a mesma decisão do "só o que muda
    /// a conduta ganha selo" — uma folha de sessão com trinta campos extras deixa de ser
    /// preenchida, e o que se perde não é o trigésimo campo, são os doze do sistema.
    /// </summary>
    public const int MaximoAtivos = 12;

    public Task<IReadOnlyList<CampoPersonalizadoProntuario>> TodosAsync(
        CancellationToken ct = default) => _repo.CamposPersonalizadosAsync(ct);

    /// <summary>
    /// Os campos que a tela de uma sessão deve mostrar: os ATIVOS que valem para a
    /// modalidade dela, na ordem cadastrada.
    /// </summary>
    public async Task<IReadOnlyList<CampoPersonalizadoProntuario>> DaSessaoAsync(
        string? modalidadeCodigo, CancellationToken ct = default)
        => (await _repo.CamposPersonalizadosAsync(ct))
            .Where(c => c.Ativo && c.ValePara(modalidadeCodigo))
            .OrderBy(c => c.Ordem).ThenBy(c => c.Id)
            .ToList();

    /// <summary>
    /// Cria ou atualiza a definição. Objeto NOVO com o Id, como toda edição da casa: a
    /// cópia campo a campo é o lugar 3 da auditoria de linha, e é ela que apaga o que
    /// chega nulo.
    /// </summary>
    public async Task<CampoPersonalizadoProntuario> SalvarAsync(
        CampoPersonalizadoProntuario dados, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.Rotulo))
            throw new InvalidOperationException("O campo precisa de um nome — é o que o profissional lê.");

        if (dados.Tipo == TipoCampoPersonalizado.Lista && dados.OpcoesDaLista.Count == 0)
            throw new InvalidOperationException(
                "Uma lista sem opções não oferece nada para escolher — escreva as opções, uma por linha.");

        var todos = await _repo.CamposPersonalizadosAsync(ct);

        // O teto conta os ATIVOS, e só quando o campo ESTÁ ficando ativo: recusar o
        // Salvar de um campo que já existe (ou de um que está sendo desativado) travaria
        // justamente a correção que resolveria o excesso.
        if (dados.Ativo)
        {
            var ativosSemEste = todos.Count(c => c.Ativo && c.Id != dados.Id);
            if (ativosSemEste >= MaximoAtivos)
                throw new InvalidOperationException(
                    $"A clínica já tem {MaximoAtivos} campos ativos. Desative um antes de "
                    + "acrescentar outro — uma folha com campos demais deixa de ser preenchida.");
        }

        CampoPersonalizadoProntuario destino;
        if (dados.Id == 0)
        {
            destino = new CampoPersonalizadoProntuario
            {
                CriadoEm = DateTime.Now,
                CriadoPor = operador,
                Ordem = dados.Ordem == 0 ? todos.Count + 1 : dados.Ordem
            };
            await _repo.AdicionarCampoPersonalizadoAsync(destino, ct);
        }
        else
        {
            destino = await _repo.ObterCampoPersonalizadoAsync(dados.Id, ct)
                ?? throw new InvalidOperationException("Campo não encontrado.");
            destino.Ordem = dados.Ordem;
        }

        destino.Rotulo = dados.Rotulo.Trim();
        destino.Tipo = dados.Tipo;
        destino.Opcoes = Limpar(dados.Opcoes);
        destino.Ajuda = Limpar(dados.Ajuda);
        destino.ModalidadeCodigo = Limpar(dados.ModalidadeCodigo);
        destino.Ativo = dados.Ativo;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = dados.Id == 0 ? "CampoPersonalizadoCriado" : "CampoPersonalizadoAlterado",
            Detalhe = $"{destino.Rotulo} ({destino.Tipo})" + (destino.Ativo ? "" : " — desativado")
        }, ct);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>
    /// Os valores que a sessão deve gravar, prontos para o
    /// <see cref="ProntuarioService.SalvarAsync"/> — rótulo e tipo COPIADOS.
    ///
    /// Campo em branco NÃO vira linha: gravar o vazio encheria o prontuário de linhas que
    /// dizem "ninguém respondeu", e "não respondido" já é o que a ausência significa.
    /// </summary>
    public static IReadOnlyList<ValorCampoPersonalizado> Montar(
        IReadOnlyList<CampoPersonalizadoProntuario> campos,
        IReadOnlyDictionary<int, string?> respostas)
    {
        var valores = new List<ValorCampoPersonalizado>();

        foreach (var campo in campos)
        {
            if (!respostas.TryGetValue(campo.Id, out var bruto)) continue;
            var valor = Normalizar(campo, bruto);
            if (string.IsNullOrEmpty(valor)) continue;

            valores.Add(new ValorCampoPersonalizado
            {
                CampoId = campo.Id,
                Rotulo = campo.Rotulo,
                Tipo = campo.Tipo,
                Valor = valor
            });
        }

        return valores;
    }

    /// <summary>
    /// O valor como ele é GRAVADO. Número em cultura INVARIANTE de propósito: dois postos
    /// com culturas diferentes escreveriam "2,5" e "2.5" na mesma coluna, e a comparação
    /// entre sessões deixaria de existir sem nada falhar.
    ///
    /// Valor que não serve ao tipo é RECUSADO, e não descartado em silêncio: descartar
    /// deixaria o campo em branco depois de a pessoa o ter preenchido — o registro
    /// afirmaria que ninguém respondeu.
    /// </summary>
    private static string Normalizar(CampoPersonalizadoProntuario campo, string? bruto)
    {
        var texto = bruto?.Trim() ?? string.Empty;
        if (texto.Length == 0) return string.Empty;

        switch (campo.Tipo)
        {
            case TipoCampoPersonalizado.Numero:
                if (!decimal.TryParse(texto, NumberStyles.Number, new CultureInfo("pt-BR"), out var numero)
                    && !decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out numero))
                    throw new InvalidOperationException(
                        $"“{campo.Rotulo}” espera um número — confira o que foi digitado.");
                return numero.ToString(CultureInfo.InvariantCulture);

            case TipoCampoPersonalizado.Data:
                if (!DateOnly.TryParseExact(texto, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var data)
                    && !DateOnly.TryParse(texto, new CultureInfo("pt-BR"), DateTimeStyles.None, out data))
                    throw new InvalidOperationException(
                        $"“{campo.Rotulo}” espera uma data no formato dd/mm/aaaa.");
                return data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case TipoCampoPersonalizado.Lista:
                var opcao = campo.OpcoesDaLista.FirstOrDefault(
                    o => string.Equals(o, texto, StringComparison.OrdinalIgnoreCase));
                // Fora da lista é recusado: aceitar transformaria a coluna comparável num
                // texto livre com aparência de lista.
                return opcao ?? throw new InvalidOperationException(
                    $"“{texto}” não é uma das opções de “{campo.Rotulo}”.");

            case TipoCampoPersonalizado.SimNao:
                return texto.Equals("sim", StringComparison.OrdinalIgnoreCase)
                       || texto.Equals("true", StringComparison.OrdinalIgnoreCase)
                    ? "Sim"
                    : "Não";

            default:
                return texto;
        }
    }

    /// <summary>
    /// Como o valor é LIDO — o inverso exato do <see cref="Normalizar"/>. Número e data
    /// voltam em pt-BR, que é como a clínica os lê; o resto sai como está.
    ///
    /// Mora aqui porque a folha impressa, a exportação e a tela escrevem a mesma coisa, e
    /// três formatações divergiriam na primeira correção.
    /// </summary>
    public static string Exibir(ValorCampoPersonalizado valor) => valor.Tipo switch
    {
        TipoCampoPersonalizado.Numero =>
            decimal.TryParse(valor.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                ? n.ToString("0.##", new CultureInfo("pt-BR"))
                : valor.Valor,
        TipoCampoPersonalizado.Data =>
            DateOnly.TryParseExact(valor.Valor, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d)
                ? d.ToString("dd/MM/yyyy")
                : valor.Valor,
        _ => valor.Valor
    };

    /// <summary>
    /// A linha que a VERSÃO guarda — "Agulhas: 12 · Aparelho: TENS". É o que permite
    /// recuperar o que os campos diziam antes de uma correção sem uma tabela de versões
    /// própria (ver <see cref="VersaoEvolucao.CamposPersonalizados"/>). Nulo quando não há
    /// nenhum: string vazia gravada seria uma versão afirmando que a sessão TINHA campos
    /// e todos estavam em branco.
    /// </summary>
    public static string? Resumir(IEnumerable<ValorCampoPersonalizado> valores)
    {
        var partes = valores.Select(v => $"{v.Rotulo}: {Exibir(v)}").ToList();
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    private static string? Limpar(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
