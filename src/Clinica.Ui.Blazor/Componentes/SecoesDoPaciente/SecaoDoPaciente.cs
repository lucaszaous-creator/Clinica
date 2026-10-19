using Microsoft.AspNetCore.Components;
using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Ui.Blazor.Componentes.SecoesDoPaciente;

/// <summary>
/// A base das SEÇÕES da tela do paciente (out/2026). Cada seção recebe o paciente aberto
/// (<see cref="ContextoDoPaciente"/>) e a tela que a hospeda (<see cref="ITelaDoPaciente"/>)
/// por cascata — a seção não conhece o rail nem a navegação, e trocar de paciente MONTA a
/// seção de novo (a tela usa a versão do contexto como <c>@key</c>), de modo que nada do
/// paciente anterior sobrevive dentro dela.
///
/// Herda a <see cref="TelaBase"/>: escopo por operação, as duas barreiras de permissão, os
/// diálogos, e o aviso de "voltou à vista" (a seção decide se relê — prontuário em edição
/// NÃO relê por baixo de quem digita).
/// </summary>
public abstract class SecaoDoPaciente : TelaBase
{
    [CascadingParameter] protected ContextoDoPaciente Contexto { get; set; } = default!;
    [CascadingParameter] protected ITelaDoPaciente Tela { get; set; } = default!;
}
