using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Infrastructure;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

public sealed partial class ModalidadeEnfermagemOpcao : ObservableObject
{
    public string Codigo { get; init; } = "";
    public string Nome { get; init; } = "";
    [ObservableProperty] private bool _selecionada;
}
