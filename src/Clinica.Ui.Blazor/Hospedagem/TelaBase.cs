using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Clinica.Domain.Entities;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// A base de toda tela e formulário Blazor (out/2026). Concentra o que cada uma repetiria:
/// os serviços de tela (escopo, hospedeiro, diálogos, notificações) e as duas metades da
/// permissão — <see cref="Pode"/> é a metade VISÍVEL (o botão nasce apagado) e
/// <see cref="Exigir"/> é a que IMPEDE, devolvendo falso e DIZENDO por quê em vez de
/// lançar (exceção solta num evento derruba o circuito inteiro da tela).
///
/// ⚠️ Nada de serviço Scoped da Application injetado aqui (checagem 37): a tela vive
/// enquanto a aba vive, e o `DbContext` ficaria com a fotografia da abertura. Escopo por
/// operação, sempre (<see cref="Escopo"/>).
/// </summary>
public abstract class TelaBase : ComponentBase, IDisposable
{
    [Inject] protected IServiceScopeFactory Escopos { get; set; } = default!;
    [Inject] protected IHostDaTela Host { get; set; } = default!;
    [Inject] protected Dialogos Dialogos { get; set; } = default!;
    [Inject] protected Notificacoes Notificacoes { get; set; } = default!;

    /// <summary>O aviso de "voltou à aba", vindo da Raiz (nulo na prévia).</summary>
    [CascadingParameter] protected SinalDeVolta? Sinal { get; set; }

    private bool _escutando;

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);
        if (!_escutando && Sinal is not null)
        {
            _escutando = true;
            Sinal.Voltou += AoSinal;
        }
    }

    private void AoSinal() => _ = InvokeAsync(async () =>
    {
        try { await AoVoltarAsync(); StateHasChanged(); }
        catch (Exception ex) { Clinica.Application.Diagnostico.Registrar($"Blazor — releitura ao voltar ({GetType().Name})", ex); }
    });

    /// <summary>
    /// A pessoa voltou a esta aba: releia por trás, SEM limpar o que está na tela (a regra do
    /// <c>IAtualizarAoVoltar</c>). Formulário em edição não relê — por isso o padrão é nada.
    /// </summary>
    protected virtual Task AoVoltarAsync() => Task.CompletedTask;

    /// <summary>A tela saiu de cena: carga que chega depois não escreve em nada.</summary>
    protected bool Descartada { get; private set; }

    /// <summary>Abre um escopo para UMA operação. Use com <c>using</c>.</summary>
    protected IServiceScope Escopo() => Escopos.CreateScope();

    /// <summary>A metade visível: o botão apaga quando falta o bit.</summary>
    protected static bool Pode(Permissao permissao) => SessaoUsuario.Atual.Pode(permissao);

    /// <summary>Basta UM dos bits (um OU, não um E).</summary>
    protected static bool PodeAlgum(Permissao permissoes) => SessaoUsuario.Atual.PodeAlgum(permissoes);

    /// <summary>
    /// A segunda barreira. Falso = não pode, e a pessoa já foi avisada. Use no começo de
    /// todo comando que grava: <c>if (!Exigir(Permissao.EditarFinanceiro, "lançar no caixa")) return;</c>
    /// </summary>
    protected bool Exigir(Permissao permissao, string acao)
    {
        if (SessaoUsuario.Atual.Pode(permissao)) return true;
        Notificacoes.Erro($"Seu acesso não permite {acao}. Fale com a direção da clínica.");
        return false;
    }

    /// <summary>A segunda barreira de <see cref="PodeAlgum"/>.</summary>
    protected bool ExigirAlgum(Permissao permissoes, string acao)
    {
        if (SessaoUsuario.Atual.PodeAlgum(permissoes)) return true;
        Notificacoes.Erro($"Seu acesso não permite {acao}. Fale com a direção da clínica.");
        return false;
    }

    /// <summary>
    /// Executa uma ação de LINHA (cancelar, emitir, confirmar): a falha vira notificação e
    /// vai para o log, nunca derruba a tela. Formulário com campo a corrigir NÃO usa isto —
    /// lá o erro fica inline, perto do campo.
    /// </summary>
    protected async Task<bool> ExecutarAsync(Func<Task> acao, string contextoDoLog, string? sucesso = null)
    {
        try
        {
            await acao();
            if (sucesso is not null) Notificacoes.Sucesso(sucesso);
            return true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(contextoDoLog, ex);
            Notificacoes.Erro(ex.Message);
            return false;
        }
    }

    /// <summary>Operador da ação, para a auditoria — quem fez LOGIN, nunca o usuário do Windows.</summary>
    protected static string Operador => SessaoUsuario.Atual.Operador;

    public virtual void Dispose()
    {
        Descartada = true;
        if (_escutando && Sinal is not null) Sinal.Voltou -= AoSinal;
        GC.SuppressFinalize(this);
    }
}
