using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;
using Clinica.Domain.Medidas;

namespace Clinica.Application.Servicos;

/// <summary>
/// Medidas clínicas seriadas: peso, altura, cintura, pressão, glicemia, HbA1c (parcela 37).
///
/// Por que existe
/// --------------
/// A parcela 36 deu número às cinco especialidades pelas escalas e deixou de fora o número
/// mais básico. A endocrinologia pesa o paciente toda consulta, a geriatria acompanha
/// perda de peso e as duas escreviam "PA 140/90" em texto livre na evolução — que não
/// desenha curva, não compara consulta com consulta e não vai para o relatório. Pior: o
/// FINDRISC, aplicado desde a parcela 36, PERGUNTA o IMC e a circunferência de cintura, o
/// paciente responde e nada disso ficava registrado; o escore era gravado e os dados que o
/// produziram evaporavam.
///
/// As regras
/// ---------
/// - <b>Tudo o que descreve o tipo é COPIADO na colheita</b> (nome, unidade, faixa,
///   interpretação), como a escala aplicada e o preço por convênio. Mudar o corte do IMC
///   amanhã não reescreve a leitura da consulta de hoje.
/// - <b>O IMC não se digita — é derivado</b>, do peso com a altura mais recente. E a data
///   dessa altura vai junto: um IMC calculado com altura de três anos atrás continua sendo
///   a melhor leitura disponível, desde que quem lê saiba disso.
/// - <b>A única recusa é a implausibilidade</b> (2500 kg é dedo escorregado no teclado).
///   Valor anormal ENTRA: recusá-lo faria o sistema esconder justamente o paciente que
///   precisa de atenção.
/// - <b>Meia pressão arterial não existe.</b> Tipo com par exige os dois números.
/// - <b>Faixa ausente e faixa normal são coisas diferentes.</b> O peso isolado não tem
///   leitura publicada — 80 kg é obesidade em uma pessoa e magreza em outra —, e inventar
///   "normal" para ele faria a tela afirmar o que ninguém apurou.
/// </summary>
public sealed class MedidaClinicaService
{
    private readonly IClinicaRepositorio _repo;

    public MedidaClinicaService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>O que a tela oferece para digitar — sem as derivadas.</summary>
    public static IReadOnlyList<TipoMedida> Registraveis => CatalogoMedidas.Registraveis;

    /// <summary>O tipo de um código, ou null quando ele saiu do catálogo.</summary>
    public static TipoMedida? Tipo(string? codigo) => CatalogoMedidas.Obter(codigo);

    /// <summary>Colheitas do paciente, da mais recente para a mais antiga.</summary>
    public Task<IReadOnlyList<MedidaClinica>> DoPacienteAsync(
        int pacienteId, string? tipoCodigo = null, CancellationToken ct = default)
        => _repo.MedidasDoPacienteAsync(pacienteId, tipoCodigo, ct);

    public Task<MedidaClinica?> ObterAsync(int medidaId, CancellationToken ct = default)
        => _repo.ObterMedidaAsync(medidaId, ct);

    /// <summary>
    /// Registra uma colheita, copiando do catálogo tudo o que descreve o tipo.
    /// </summary>
    public async Task<MedidaClinica> RegistrarAsync(
        MedidaClinica dados, string? operador = null, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(dados.PacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        var tipo = CatalogoMedidas.Obter(dados.TipoCodigo)
            ?? throw new InvalidOperationException(
                $"Medida desconhecida: “{dados.TipoCodigo}”.");

        // O IMC é leitura, não colheita. Deixá-lo ser gravado criaria um terceiro número
        // livre para contradizer o peso e a altura que aparecem na linha de cima.
        if (tipo.Derivada)
            throw new InvalidOperationException(
                $"{tipo.Nome} é calculado do peso e da altura — não se digita.");

        if (dados.Valor < tipo.Minimo || dados.Valor > tipo.Maximo)
            throw new InvalidOperationException(
                $"{tipo.Nome} entre {tipo.Minimo.ToString("0.##", Cultura)} e "
                + $"{tipo.Maximo.ToString("0.##", Cultura)} {tipo.Unidade}. "
                + "O limite é de plausibilidade, não de normalidade.");

        if (tipo.TemSegundoValor)
        {
            if (dados.ValorSecundario is not { } segundo)
                throw new InvalidOperationException(
                    $"{tipo.Nome} precisa dos dois valores: {tipo.RotuloSegundoValor} está em branco.");

            if (segundo < tipo.Minimo || segundo > tipo.Maximo)
                throw new InvalidOperationException(
                    $"{tipo.RotuloSegundoValor} entre {tipo.Minimo.ToString("0.##", Cultura)} e "
                    + $"{tipo.Maximo.ToString("0.##", Cultura)} {tipo.Unidade}.");

            // Diastólica maior que a sistólica é inversão de campo, não paciente raro.
            if (segundo > dados.Valor)
                throw new InvalidOperationException(
                    $"{tipo.RotuloSegundoValor} ({segundo.ToString("0.##", Cultura)}) não pode ser "
                    + $"maior que o primeiro valor ({dados.Valor.ToString("0.##", Cultura)}) — "
                    + "confira se os campos não foram trocados.");
        }
        else if (dados.ValorSecundario is not null)
        {
            throw new InvalidOperationException($"{tipo.Nome} tem um valor só.");
        }

        if (dados.Data > DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException("A medida não pode ser de uma data futura.");

        var medida = new MedidaClinica
        {
            PacienteId = dados.PacienteId,
            ProfissionalId = dados.ProfissionalId,
            EvolucaoId = dados.EvolucaoId,
            Data = dados.Data,
            TipoCodigo = tipo.Codigo,
            // COPIADO, não referenciado: renomear o tipo amanhã não reescreve o que se
            // colheu hoje. É a regra da avaliação clínica e do preço por convênio.
            TipoNome = tipo.Nome,
            Unidade = tipo.Unidade,
            Valor = dados.Valor,
            ValorSecundario = dados.ValorSecundario,
            Observacoes = Limpar(dados.Observacoes),
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        AplicarFaixa(medida, tipo, paciente.Sexo);

        await _repo.AdicionarMedidaAsync(medida, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "MedidaRegistrada",
            Detalhe = $"{tipo.Nome} {tipo.Formatar(medida.Valor, medida.ValorSecundario)} "
                      + $"em {medida.Data:dd/MM/yyyy}",
            PacienteId = medida.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return medida;
    }

    /// <summary>
    /// Apaga uma colheita. Medida errada é medida errada — diferente da NC e do documento
    /// clínico, ela não descreve uma decisão que alguém tomou, e mantê-la na série faria a
    /// curva mentir para sempre por causa de um dedo no teclado. O rastro fica na auditoria.
    /// </summary>
    public async Task ExcluirAsync(
        int medidaId, string? operador = null, CancellationToken ct = default)
    {
        var medida = await _repo.ObterMedidaAsync(medidaId, ct)
            ?? throw new InvalidOperationException("Medida não encontrada.");

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "MedidaExcluida",
            Detalhe = $"{medida.TipoNome} {medida.ValorFormatado} em {medida.Data:dd/MM/yyyy}",
            PacienteId = medida.PacienteId
        }, ct);

        await _repo.RemoverMedidaAsync(medidaId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// A série de um tipo, em ordem CRONOLÓGICA (o repositório devolve do mais novo para o
    /// mais velho; a curva precisa do contrário).
    ///
    /// Pedir a série do IMC não devolve vazio: ela é MONTADA, ponto a ponto, de cada peso
    /// com a altura vigente naquela data.
    /// </summary>
    public async Task<SerieMedida> SerieAsync(
        int pacienteId, string tipoCodigo, CancellationToken ct = default)
    {
        var tipo = CatalogoMedidas.Obter(tipoCodigo)
            ?? throw new InvalidOperationException($"Medida desconhecida: “{tipoCodigo}”.");

        if (tipo.Codigo == CatalogoMedidas.Imc)
            return await SerieImcAsync(pacienteId, tipo, ct);

        var medidas = await _repo.MedidasDoPacienteAsync(pacienteId, tipo.Codigo, ct);

        var pontos = medidas
            .OrderBy(m => m.Data).ThenBy(m => m.Id)
            .Select(m => new PontoMedida(
                m.Data, m.Valor, m.ValorSecundario, m.FaixaNome, m.FaixaGravidade))
            .ToList();

        return new SerieMedida(tipo.Codigo, tipo.Nome, tipo.Unidade, pontos);
    }

    /// <summary>
    /// O cartão de abertura do paciente: a última colheita de cada tipo e o IMC derivado.
    /// </summary>
    public async Task<ResumoMedidas> ResumoAsync(int pacienteId, CancellationToken ct = default)
    {
        var todas = await _repo.MedidasDoPacienteAsync(pacienteId, null, ct);

        // A lista já vem da mais recente para a mais antiga, então a primeira de cada tipo
        // é a última colheita dele.
        var ultimas = todas
            .GroupBy(m => m.TipoCodigo)
            .Select(g => g.First())
            .OrderByDescending(m => m.Data)
            .ToList();

        var peso = ultimas.FirstOrDefault(m => m.TipoCodigo == CatalogoMedidas.Peso);
        if (peso is null)
            return new ResumoMedidas(ultimas, null, null, GravidadeFaixa.Normal, null, null);

        var altura = AlturaVigente(todas, peso.Data);
        var imc = CatalogoMedidas.CalcularImc(peso.Valor, altura?.Valor);
        if (imc is null)
            return new ResumoMedidas(ultimas, null, null, GravidadeFaixa.Normal, null, null);

        var faixa = CatalogoMedidas.Obter(CatalogoMedidas.Imc)?.FaixaDe(imc.Value, null);
        return new ResumoMedidas(
            ultimas, imc, faixa?.Nome, faixa?.Gravidade ?? GravidadeFaixa.Normal,
            peso.Data, altura!.Data);
    }

    /// <summary>
    /// A curva do IMC, montada de cada peso com a altura vigente naquela data.
    ///
    /// "Vigente" é a altura mais recente ATÉ o dia do peso. Quando não há nenhuma antes —
    /// o caso comum, porque o adulto é pesado toda consulta e medido uma vez, muitas vezes
    /// depois —, cai para a PRIMEIRA altura registrada. A alternativa seria devolver curva
    /// vazia justamente para quem tem os dois dados, e altura de adulto não muda de um
    /// semestre para o outro. Em criança muda, e é por isso que a data da altura usada
    /// aparece no resumo em vez de ficar implícita.
    /// </summary>
    private async Task<SerieMedida> SerieImcAsync(
        int pacienteId, TipoMedida tipo, CancellationToken ct)
    {
        var todas = await _repo.MedidasDoPacienteAsync(pacienteId, null, ct);

        var pesos = todas
            .Where(m => m.TipoCodigo == CatalogoMedidas.Peso)
            .OrderBy(m => m.Data).ThenBy(m => m.Id)
            .ToList();

        var pontos = new List<PontoMedida>();
        foreach (var p in pesos)
        {
            var altura = AlturaVigente(todas, p.Data);
            if (CatalogoMedidas.CalcularImc(p.Valor, altura?.Valor) is not { } imc) continue;

            var faixa = tipo.FaixaDe(imc, null);
            pontos.Add(new PontoMedida(
                p.Data, imc, null, faixa?.Nome, faixa?.Gravidade ?? GravidadeFaixa.Normal,
                Derivado: true));
        }

        return new SerieMedida(tipo.Codigo, tipo.Nome, tipo.Unidade, pontos);
    }

    private static MedidaClinica? AlturaVigente(
        IReadOnlyList<MedidaClinica> todas, DateOnly ate)
    {
        var alturas = todas
            .Where(m => m.TipoCodigo == CatalogoMedidas.Altura)
            .OrderBy(m => m.Data).ThenBy(m => m.Id)
            .ToList();

        if (alturas.Count == 0) return null;

        return alturas.LastOrDefault(a => a.Data <= ate) ?? alturas[0];
    }

    /// <summary>
    /// Copia a leitura publicada para dentro da colheita. Sem faixa no catálogo, os campos
    /// ficam nulos — "não há leitura publicada" não é "está normal".
    /// </summary>
    private static void AplicarFaixa(MedidaClinica medida, TipoMedida tipo, Sexo sexo)
    {
        var faixa = tipo.FaixaDe(medida.Valor, sexo);
        medida.FaixaNome = faixa?.Nome;
        medida.FaixaInterpretacao = faixa?.Interpretacao;
        medida.FaixaGravidade = faixa?.Gravidade ?? GravidadeFaixa.Normal;
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// pt-BR fixo nas mensagens: elas citam limites com decimal, e a cultura da máquina
    /// faria a mesma clínica ler "0.5" num posto e "0,5" no outro.
    /// </summary>
    private static readonly System.Globalization.CultureInfo Cultura = new("pt-BR");
}
