using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Catálogo de convênios: lê/grava as entradas no banco e mantém o cache em memória
/// (<see cref="CatalogoConvenios"/>) que serve nome/família de forma síncrona às telas.
/// Garante que os quatro convênios embutidos — e o PARTICULAR — sempre existam.
/// </summary>
public sealed class ConvenioCatalogoService
{
    private readonly IClinicaRepositorio _repo;

    public ConvenioCatalogoService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Todas as entradas (embutidos garantidos + variantes), ordenadas por nome.</summary>
    public async Task<IReadOnlyList<ConvenioCadastro>> ListarAsync(CancellationToken ct = default)
    {
        var salvos = (await _repo.ConveniosAsync(ct)).ToDictionary(c => c.Codigo, StringComparer.OrdinalIgnoreCase);

        // Garante os 4 embutidos mesmo em bancos anteriores ao catálogo.
        foreach (var familia in Enum.GetValues<Convenio>())
        {
            var codigo = familia.ToString();
            if (!salvos.ContainsKey(codigo))
                salvos[codigo] = new ConvenioCadastro
                {
                    Codigo = codigo,
                    Nome = ConvenioInfo.NomeExibicaoPadrao(familia),
                    Familia = familia,
                    Ativo = true,
                    // O embutido que ainda não existe no banco nasce já com o formato do
                    // número da guia da própria família (parcela 45). Deixá-lo em
                    // "sem validação" faria a crítica sumir justamente na base que nunca
                    // abriu a tela de Configurações — que é a maioria delas.
                    FormatoNumeroGuia = RegraNumeroGuia.PadraoDaFamilia(familia)
                };
        }

        // ===== O PARTICULAR É GARANTIDO, como os embutidos (set/2026) =====
        //
        // Ele não vem de migration nenhuma: até aqui, "particular" só existia se alguém
        // criasse a linha à mão no app de FATURAMENTO, e por isso o fluxo do particular
        // não tinha por onde começar (ver `ConvenioCadastro.Particular`). Garantido aqui,
        // ele aparece em TODA porta que lista convênios — a janela que pergunta o convênio
        // do paciente, a ficha, o cadastro do faturamento, o mapeamento da importação —
        // em vez de só naquela em que alguém lembrasse de construí-lo.
        //
        // Só é acrescentado quando não existe: a clínica que já criou o dela (com outro
        // nome, ou com o formato do número preenchido) continua com a linha dela.
        //
        // ⚠️ "Não existe" é pelo código E pelo NOME (nov/2026). A clínica tinha criado o
        // "Particular" dela à mão, com outro código, antes de esta garantia existir — e a
        // garantia, olhando só o código, punha um SEGUNDO "Particular" na lista. A
        // conferência de nomes repetidos do "Salvar configurações" então recusava TODA
        // gravação do catálogo, inclusive a de convênios que nada tinham a ver (a Sul
        // América não pôde ser configurada). A linha da clínica vence: é a que tem os
        // pacientes dela.
        var nomeParticular = ConvenioCadastro.Particular().Nome;
        if (!salvos.ContainsKey(ConvenioCadastro.CodigoParticular)
            && !salvos.Values.Any(c => string.Equals(c.Nome?.Trim(), nomeParticular, StringComparison.OrdinalIgnoreCase)))
            salvos[ConvenioCadastro.CodigoParticular] = ConvenioCadastro.Particular();

        return salvos.Values.OrderBy(c => c.Nome).ToList();
    }

    /// <summary>Recarrega o cache em memória a partir do banco (chamar no start e após salvar).</summary>
    public async Task RecarregarCacheAsync(CancellationToken ct = default)
    {
        var lista = await ListarAsync(ct);
        CatalogoConvenios.Atualizar(lista.Select(c => new EntradaConvenio(
            c.Codigo, c.Nome, c.Familia, c.Ativo,
            c.Familia == Convenio.Personalizado ? c.ParaConfig() : null,
            c.FormatoNumeroGuia,
            c.RegistroAnsOperadora,
            c.GeraGuia)));
    }

    public async Task SalvarAsync(IEnumerable<ConvenioCadastro> convenios, CancellationToken ct = default)
    {
        foreach (var c in convenios)
        {
            c.Codigo = c.Codigo.Trim();
            c.Nome = string.IsNullOrWhiteSpace(c.Nome) ? ConvenioInfo.NomeExibicaoPadrao(c.Familia) : c.Nome.Trim();
            await _repo.SalvarConvenioAsync(c, ct);
        }
        await _repo.SalvarAsync(ct);
        await RecarregarCacheAsync(ct);
    }

    /// <summary>Convênio embutido: o código coincide com uma família e a entrada não pode ser excluída.</summary>
    public static bool EhEmbutido(string codigo) => Enum.TryParse<Convenio>(codigo, ignoreCase: false, out _);

    /// <summary>
    /// Exclui uma variante do catálogo. Recusa embutidos e convênios com paciente cadastrado —
    /// nesses casos o caminho é desativar (o histórico é preservado).
    /// </summary>
    public async Task<(bool Ok, string Mensagem)> ExcluirAsync(string codigo, CancellationToken ct = default)
    {
        if (EhEmbutido(codigo))
            return (false, "Convênios embutidos não podem ser excluídos. Desative-o para ocultá-lo dos cadastros.");

        var salvo = (await _repo.ConveniosAsync(ct))
            .FirstOrDefault(c => string.Equals(c.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (salvo is null)
            return (true, "Convênio removido."); // nunca chegou a ser salvo — nada a excluir no banco

        if (await _repo.ConvenioEmUsoAsync(salvo.Codigo, ct))
            return (false, $"Há pacientes cadastrados em \"{salvo.Nome}\". Desative o convênio em vez de excluir — o histórico é preservado.");

        await _repo.ExcluirConvenioAsync(salvo.Codigo, ct);
        await _repo.SalvarAsync(ct);
        await RecarregarCacheAsync(ct);
        return (true, $"Convênio \"{salvo.Nome}\" excluído.");
    }

    // ===================== JUNTAR DOIS CONVÊNIOS (nov/2026) =====================
    //
    // Nasceu dos dois "Particular" da clínica: o criado à mão e o que o catálogo garantia,
    // cada um com parte dos pacientes. Juntar põe tudo no que FICA e aposenta o que SAI.
    //
    // O que se move é o que decide o FUTURO: o convênio de cada paciente, a tabela de preço
    // e as autorizações ainda utilizáveis. O que NÃO se move é o passado — financeiro,
    // repasse, tributo e lote TISS guardam o código que valia quando foram lançados, e
    // reescrevê-los mudaria relatórios já fechados. Por isso o que sai não é apagado: fica
    // DESATIVADO e renomeado ("… (juntado em …)"), e o histórico continua com nome legível.

    /// <summary>O que aponta hoje para o convênio — os números que a tela mostra antes de juntar.</summary>
    public async Task<UsoDoConvenio> UsoAsync(string codigo, DateOnly hoje, CancellationToken ct = default)
    {
        var (pacientes, precos, autorizacoes) = await _repo.ContarUsoDoConvenioAsync(codigo, hoje, ct);
        return new UsoDoConvenio(pacientes, precos, autorizacoes);
    }

    /// <summary>
    /// Junta <paramref name="origem"/> em <paramref name="destino"/>: pacientes e autorizações
    /// utilizáveis passam para o destino; os preços passam só se o destino ainda não tiver
    /// tabela ativa (senão a do destino vence e a da origem fica onde está). A origem fica
    /// desativada e renomeada. Tudo — e a auditoria — num SaveChanges só.
    /// Recusa (InvalidOperationException, com a frase para a tela): mesmo convênio, origem
    /// embutida, "a definir" de qualquer lado, destino desativado ou inexistente.
    /// </summary>
    public async Task<ResultadoJuncaoConvenios> JuntarAsync(
        string origem, string destino, string? operador, DateOnly hoje, CancellationToken ct = default)
    {
        if (string.Equals(origem?.Trim(), destino?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Escolha OUTRO convênio para receber os pacientes.");

        var lista = await ListarAsync(ct);
        var sai = lista.FirstOrDefault(c => string.Equals(c.Codigo, origem, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("O convênio que sairia não existe mais no catálogo.");
        var fica = lista.FirstOrDefault(c => string.Equals(c.Codigo, destino, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("O convênio escolhido para ficar não existe mais no catálogo.");

        if (EhEmbutido(sai.Codigo))
            throw new InvalidOperationException(
                $"\"{sai.Nome}\" é um convênio embutido e não pode sair do catálogo. Junte o outro nele.");
        if (EhADefinir(sai) || EhADefinir(fica))
            throw new InvalidOperationException(
                "\"A definir\" é a pergunta que a recepção ainda precisa responder, ficha a ficha — "
                + "ele não se junta a convênio nenhum.");
        if (!fica.Ativo)
            throw new InvalidOperationException(
                $"\"{fica.Nome}\" está desativado. Ative-o antes de juntar os pacientes nele.");

        // O destino pode ser o particular GARANTIDO, que só existe na leitura: grava-o antes,
        // senão os pacientes apontariam para um código sem linha no banco.
        var gravados = await _repo.ConveniosAsync(ct);
        if (!gravados.Any(c => string.Equals(c.Codigo, fica.Codigo, StringComparison.OrdinalIgnoreCase)))
            await _repo.SalvarConvenioAsync(fica, ct);

        var pacientes = await _repo.PacientesDoConvenioAsync(sai.Codigo, ct);
        foreach (var p in pacientes)
        {
            p.ConvenioCodigo = fica.Codigo;
            p.Convenio = fica.Familia;
        }

        var autorizacoes = await _repo.AutorizacoesUtilizaveisDoConvenioAsync(sai.Codigo, hoje, ct);
        foreach (var a in autorizacoes)
        {
            a.ConvenioCodigo = fica.Codigo;
            a.Convenio = fica.Familia;
        }

        var precosDoDestino = await _repo.PrecosDoConvenioAsync(fica.Codigo, ct);
        var precosDaOrigem = await _repo.PrecosDoConvenioAsync(sai.Codigo, ct);
        var precosMovidos = 0;
        var precosMantidos = 0;
        if (precosDoDestino.Any(p => p.Ativo))
            precosMantidos = precosDaOrigem.Count(p => p.Ativo);
        else
            foreach (var p in precosDaOrigem)
            {
                p.ConvenioCodigo = fica.Codigo;
                if (p.Ativo) precosMovidos++;
            }

        var nomeAntigo = sai.Nome;
        var sufixo = $" (juntado em {fica.Nome.Trim()}, {hoje.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)})";
        sai.Nome = (nomeAntigo.Trim().Length + sufixo.Length <= 80
            ? nomeAntigo.Trim() + sufixo
            : nomeAntigo.Trim()[..Math.Max(1, 80 - sufixo.Length)] + sufixo);
        sai.Ativo = false;
        await _repo.SalvarConvenioAsync(sai, ct);

        var resultado = new ResultadoJuncaoConvenios(
            nomeAntigo, fica.Nome, pacientes.Count, precosMovidos, precosMantidos, autorizacoes.Count);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ConveniosJuntados",
            Detalhe = $"\"{nomeAntigo}\" ({sai.Codigo}) juntado em \"{fica.Nome}\" ({fica.Codigo}): "
                      + resultado.Resumo()
        }, ct);

        await _repo.SalvarAsync(ct);
        await RecarregarCacheAsync(ct);
        return resultado;
    }

    private static bool EhADefinir(ConvenioCadastro c)
        => string.Equals(c.Codigo, ConvenioCadastro.CodigoADefinir, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Quantos pacientes, preços ativos e autorizações utilizáveis apontam para um convênio.</summary>
public sealed record UsoDoConvenio(int Pacientes, int Precos, int Autorizacoes);

/// <summary>O que a junção moveu — a frase da notificação e da auditoria.</summary>
public sealed record ResultadoJuncaoConvenios(
    string Saiu, string Ficou, int Pacientes, int PrecosMovidos, int PrecosMantidos, int Autorizacoes)
{
    public string Resumo()
    {
        var partes = new List<string>
        {
            Pacientes == 1 ? "1 paciente movido" : $"{Pacientes} pacientes movidos",
            PrecosMovidos == 1 ? "1 preço movido" : $"{PrecosMovidos} preços movidos",
            Autorizacoes == 1 ? "1 autorização movida" : $"{Autorizacoes} autorizações movidas"
        };
        if (PrecosMantidos > 0)
            partes.Add($"{PrecosMantidos} preço(s) do antigo ficaram nele, porque \"{Ficou}\" já tem tabela própria");
        return string.Join("; ", partes) + ".";
    }
}
