using System.Collections.ObjectModel;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Uma linha do PLANO DE CUIDADOS do dia — a etapa 4 da COFEN na tela de quem executa
/// (parcela 76).
/// </summary>
public sealed class LinhaCuidadoDoDia
{
    public required int CuidadoId { get; init; }

    /// <summary>"Curativo em MID — a cada 12h".</summary>
    public required string Redacao { get; init; }

    /// <summary>O que já foi registrado HOJE, uma linha por execução. Vazio quando nada.</summary>
    public required string Registro { get; init; }

    /// <summary>"aguardando", "se necessário" ou vazio — o selo que resume a linha.</summary>
    public required string Selo { get; init; }

    public required bool Pendente { get; init; }
}

/// <summary>
/// O PLANO DE CUIDADOS DE HOJE — a etapa 4 da COFEN 358/2009, na tela de quem executa.
///
/// Por que ele virou componente do shell (parcela 88)
/// --------------------------------------------------
/// Ele nasceu dentro da tela da Enfermagem (parcela 76) e agora precisa aparecer também na
/// seção <b>Atendimento de enfermagem</b> do módulo Clínico — que é onde a técnica vai
/// passar o atendimento inteiro. Copiá-lo daria DUAS definições de "o que falta executar
/// hoje", e a segunda é sempre a que ninguém lembra de ajustar: a regra do <b>se
/// necessário</b> (SOS não é trabalho atrasado), a da <b>corrigida que aparece marcada</b>
/// e a da <b>hora informada</b> não podem existir em duas cópias.
///
/// ⚠️ Ele NÃO é dono do paciente: recebe o id em <see cref="CarregarAsync"/> e nada mais.
/// Quem sabe de quem é a tela é a tela.
///
/// As três regras que ele carrega, e que são as caras de pagar
/// ------------------------------------------------------------
/// 1. <b>A HORA vem do campo, nunca do relógio.</b> A técnica executa às 14h e digita às
///    14h20; é a hora do FATO que vai para a folha, e o relógio vai ao lado em
///    <c>RegistradoEm</c> — a diferença entre os dois é o que uma auditoria procura.
/// 2. <b>Não realizado EXIGE justificativa.</b> Perguntar aqui é o que evita transformar a
///    regra do serviço num erro na cara de quem já executou o turno.
/// 3. <b>Falha SOZINHA.</b> O assunto da tela que o hospeda é a evolução; uma leitura de
///    plano que não respondeu não pode impedir a passagem de ser registrada. Mas também
///    não passa calada: vira o terceiro estado e vai para o log — "não há plano" e "não
///    consegui ler o plano" dizem coisas opostas numa tela que existe para dizer o que
///    falta fazer.
/// </summary>
public sealed partial class PlanoDeCuidadosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    /// <summary>Descarte de resposta fora de ordem: a tela troca de paciente a cada clique.</summary>
    private int _geracao;

    private int _pacienteId;

    public PlanoDeCuidadosViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;
    }

    /// <summary>
    /// Os cuidados prescritos que este paciente tem hoje, com o que já foi registrado.
    ///
    /// ⚠️ É a PORTA da etapa 4. Sem ela o serviço que registra a execução seria mais uma
    /// capacidade sem porta — o defeito recorrente do projeto —, e a enfermeira continuaria
    /// escrevendo "curativo a cada 12h" num plano que ninguém marca.
    /// </summary>
    public ObservableCollection<LinhaCuidadoDoDia> Cuidados { get; } = [];

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>A leitura do plano FALHOU — o terceiro estado. Ver a regra 3 acima.</summary>
    [ObservableProperty] private bool _naoVerificado;

    /// <summary>Há plano para mostrar. Sem ele a região SOME, em vez de gastar tela vazia.</summary>
    [ObservableProperty] private bool _temPlano;

    /// <summary>
    /// Hora sugerida para o registro. SUGESTÃO — o campo é de quem executou: ver a regra 1.
    /// </summary>
    [ObservableProperty] private string _hora = DateTime.Now.ToString("HH\\:mm");

    /// <summary>
    /// A mensagem da última ação. Quem a MOSTRA é a tela que hospeda o plano — ela já tem
    /// um lugar para mensagem, e uma segunda superfície ao lado seria a segunda resposta
    /// para a mesma pergunta.
    /// </summary>
    [ObservableProperty] private string? _mensagem;

    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando. O
    /// MESMO bit da folha de infusão: checar a execução é o mesmo ato e a mesma
    /// responsabilidade — um bit novo nasceria desligado para quem já faz isso hoje.</summary>
    public bool PodeChecar => SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao);

    /// <summary>
    /// Lê o plano do paciente para HOJE. <paramref name="pacienteId"/> zero limpa a região
    /// — é o caminho de "voltei para a lista".
    /// </summary>
    public async Task CarregarAsync(int pacienteId)
    {
        var geracao = ++_geracao;
        _pacienteId = pacienteId;

        if (pacienteId == 0)
        {
            Cuidados.Clear();
            TemPlano = false;
            NaoVerificado = false;
            Resumo = string.Empty;
            return;
        }

        try
        {
            NaoVerificado = false;

            using var escopo = _escopos.CreateScope();
            var plano = await escopo.ServiceProvider
                .GetRequiredService<ChecagemCuidadoService>()
                .PlanoDoDiaAsync(pacienteId, DateOnly.FromDateTime(DateTime.Today));

            if (geracao != _geracao) return;

            // ⚠️ Monta em lista LOCAL e só então publica: entre o `Clear()` e o último
            // `Add` não pode haver await, senão duas cargas se intercalam na coleção
            // (parcela 62).
            var linhas = plano is null
                ? new List<LinhaCuidadoDoDia>()
                : plano.Cuidados.Select(Montar).ToList();

            Cuidados.Clear();
            foreach (var l in linhas) Cuidados.Add(l);

            TemPlano = plano is not null;
            Resumo = plano is null
                ? "Nenhum plano de cuidados prescrito para este paciente."
                : $"{plano.Cuidados.Count} cuidado(s) prescrito(s) em "
                  + $"{plano.PrescritoEm:dd/MM/yyyy} por {plano.PrescritoPor} — "
                  + (plano.Pendentes == 0
                      ? "tudo registrado hoje."
                      : $"{plano.Pendentes} aguardando registro hoje.");
        }
        catch (Exception ex)
        {
            if (geracao != _geracao) return;

            Cuidados.Clear();
            // ⚠️ TemPlano fica LIGADO na falha: é o que faz a região continuar desenhada
            // para mostrar o terceiro estado. Sumindo, "não consegui ler" ficaria idêntico
            // a "não há plano", que é a distinção que este bloco existe para preservar.
            TemPlano = true;
            NaoVerificado = true;
            Resumo = "Não foi possível ler o plano de cuidados.";
            Diagnostico.Registrar("Enfermagem — plano de cuidados não pôde ser lido", ex);
        }
    }

    private static LinhaCuidadoDoDia Montar(CuidadoDoDia c) => new()
    {
        CuidadoId = c.CuidadoId,
        Redacao = c.Redacao,
        // ⚠️ A CORRIGIDA aparece marcada, nunca sumindo. O comentário de `RetificarAsync`
        // promete que "a folha mostra as duas", e promessa que o código não cumpre é o
        // defeito da parcela 67 — aqui ela seria pior que em outro lugar, porque apagar da
        // vista o registro que foi corrigido é exatamente o gesto que a auditoria de
        // enfermagem procura.
        Registro = string.Join("\n", c.Checagens.Select(Descrever)),
        Pendente = c.Pendente,
        // O "se necessário" tem selo PRÓPRIO, e não o de pendente: ele não é trabalho
        // atrasado, é a condição que não aconteceu.
        Selo = c.Pendente ? "aguardando"
             : c.SeNecessario && c.Vigentes.Count == 0 ? "se necessário"
             : string.Empty
    };

    private static string Descrever(ChecagemCuidado x)
    {
        var texto = x.Linha;
        if (!string.IsNullOrWhiteSpace(x.Justificativa)) texto += $" — {x.Justificativa}";
        if (x.EhRetificacao) texto += $" — corrige o anterior: {x.MotivoRetificacao}";
        return texto;
    }

    [RelayCommand]
    private Task MarcarFeitoAsync(LinhaCuidadoDoDia? linha)
        => RegistrarAsync(linha, SituacaoChecagem.Realizado);

    [RelayCommand]
    private Task MarcarNaoFeitoAsync(LinhaCuidadoDoDia? linha)
        => RegistrarAsync(linha, SituacaoChecagem.NaoRealizado);

    private async Task RegistrarAsync(LinhaCuidadoDoDia? linha, SituacaoChecagem situacao)
    {
        // Guarda sobre PARÂMETRO: vindo de botão de linha ela nunca dispara — é a exceção
        // que a checagem 21 reconhece.
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(
                Permissao.ChecarPrescricao, "registrar a execução de um cuidado");

            if (!TimeOnly.TryParse(Hora, out var hora))
            {
                Mensagem = $"Hora inválida (\"{Hora}\"). Escreva no formato 14:30 — "
                         + "é o horário em que o cuidado foi executado, não o de agora.";
                MensagemEhErro = true;
                return;
            }

            string? justificativa = null;
            if (situacao == SituacaoChecagem.NaoRealizado)
            {
                justificativa = _dialogo.PerguntarTexto(
                    "Por que não foi realizado?",
                    $"{linha.Redacao}\n\n"
                    + "Ex.: paciente ausente, recusou, material em falta, condição não ocorreu.");

                // Sem justificativa não se grava — e o serviço recusaria de qualquer forma.
                // Perguntar aqui é o que evita transformar a regra num erro na cara da técnica.
                if (string.IsNullOrWhiteSpace(justificativa)) return;
            }

            using (var escopo = _escopos.CreateScope())
            {
                await escopo.ServiceProvider.GetRequiredService<ChecagemCuidadoService>()
                    .ChecarAsync(
                        linha.CuidadoId, situacao, DateOnly.FromDateTime(DateTime.Today), hora,
                        Executante(), justificativa);
            }

            Mensagem = situacao == SituacaoChecagem.Realizado
                ? $"Registrado: {linha.Redacao} — {hora:HH\\:mm}."
                : $"Registrado como NÃO realizado: {linha.Redacao} — {hora:HH\\:mm}.";
            MensagemEhErro = false;

            await CarregarAsync(_pacienteId);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — execução do cuidado não pôde ser registrada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Quem está executando sai do LOGIN, nunca de um campo digitado: é o vínculo com a
    /// pessoa que dá valor ao registro, e o COREN é copiado no ato.
    /// </summary>
    private static IdentificacaoExecutante Executante() => new(
        UsuarioId: SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
        Nome: SessaoUsuario.Atual.Autenticado
            ? SessaoUsuario.Atual.Nome
            : SessaoUsuario.Atual.Operador,
        Conselho: SessaoUsuario.Atual.RegistroConselho);
}
