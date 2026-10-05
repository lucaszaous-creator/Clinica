using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Leitura contextual do paciente da consulta, sem carregar ou alterar o rascunho.</summary>
public sealed partial class HistoricoConsultaViewModel(IServiceScopeFactory escopos, Func<int> pacienteId) : ObservableObject
{
    private IReadOnlyList<RegistroClinicoPaciente> registros = [];
    private int geracao;
    private string nomePaciente = string.Empty;
    [ObservableProperty] private bool _aberto;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private string _filtro = "Todos";
    public ObservableCollection<RegistroClinicoPaciente> Itens { get; } = [];
    public bool Vazio => !Carregando && Mensagem is null && Itens.Count == 0;

    [RelayCommand]
    private async Task AlternarAsync()
    {
        Aberto = !Aberto;
        if (Aberto) await RecarregarAsync();
    }
    [RelayCommand] private void Fechar() => Aberto = false;
    [RelayCommand] private void Filtrar(string? filtro) { Filtro = filtro ?? "Todos"; Publicar(); }

    public async Task RecarregarAsync()
    {
        int carga = ++geracao, id = pacienteId();
        registros = []; Itens.Clear(); nomePaciente = string.Empty; Mensagem = null; NaoVerificado = false; Carregando = false;
        if (id == 0 || !SessaoUsuario.Atual.Pode(Permissao.VerProntuario))
        { Mensagem = "Selecione um paciente com acesso ao prontuário."; OnPropertyChanged(nameof(Vazio)); return; }
        Carregando = true; OnPropertyChanged(nameof(Vazio));
        try
        {
            using var scope = escopos.CreateScope();
            var p = scope.ServiceProvider;
            var prontuario = p.GetRequiredService<ProntuarioService>();
            var paciente = await p.GetRequiredService<IClinicaRepositorio>().ObterPacienteAsync(id);
            var sessoes = await prontuario.DoPacienteAsync(id);
            var anexos = await prontuario.ContagemDeAnexosAsync(sessoes.Select(e => e.Id).ToList());
            var correcoes = await prontuario.ContagemDeVersoesAsync(sessoes.Select(e => e.Id).ToList());
            var enfermagem = await p.GetRequiredService<EvolucaoEnfermagemService>().DoPacienteAsync(id, limite: 200);
            var infusoes = await p.GetRequiredService<PrescricaoInternaService>().DoPacienteAsync(id, limite: 200);
            if (carga != geracao || id != pacienteId()) return;
            nomePaciente = paciente?.Nome ?? string.Empty;
            var textos = sessoes.ToDictionary(e => e.Id, e => SessaoDoProntuario.De(
                e, anexos.GetValueOrDefault(e.Id), correcoes.GetValueOrDefault(e.Id)).TextoParaCopiar(nomePaciente));
            registros = LinhaDoTempoClinica.Montar(SessaoUsuario.Atual.Efetivas, sessoes, anexos, enfermagem, infusoes, [], [])
                .SelectMany(par => par.Value)
                .Select(r => r.Natureza == NaturezaRegistroClinico.SessaoMedica
                    ? r with { TextoParaCopia = textos.GetValueOrDefault(r.Id) } : r).ToList();
            Publicar();
        }
        catch (Exception ex)
        {
            if (carga != geracao) return;
            Clinica.Application.Diagnostico.Registrar("Histórico lateral do atendimento", ex);
            NaoVerificado = true;
            Mensagem = "Não foi possível consultar o histórico. Tente atualizar novamente.";
        }
        finally { if (carga == geracao) { Carregando = false; OnPropertyChanged(nameof(Vazio)); } }
    }
    [RelayCommand] private Task AtualizarAsync() => RecarregarAsync();
    private void Publicar()
    {
        var lista = registros.Where(r => Filtro switch
        {
            "Sessões" => r.Natureza == NaturezaRegistroClinico.SessaoMedica,
            "Portal" => r.Natureza is NaturezaRegistroClinico.EvolucaoEnfermagem or NaturezaRegistroClinico.PrescricaoInterna,
            _ => true
        });
        Itens.Clear();
        // Ordem por dia e por tipo. Só compara horas dentro do mesmo tipo de registro:
        // as sessões médicas não têm hora e não podem receber uma hora inventada.
        foreach (var r in lista.OrderByDescending(r => r.Data).ThenBy(r => r.Natureza).ThenByDescending(r => r.Hora).ThenByDescending(r => r.Id)) Itens.Add(r);
        OnPropertyChanged(nameof(Vazio));
    }
    [RelayCommand]
    private void Ler(RegistroClinicoPaciente? registro)
    {
        if (registro is null || !registros.Contains(registro) || !SessaoUsuario.Atual.Pode(Permissao.VerProntuario)) return;
        if (registro.Natureza == NaturezaRegistroClinico.SessaoMedica)
        {
            var vm = new SessaoDoProntuarioViewModel(escopos, registro.Id, nomePaciente, ofereceAnexos: false);
            new SessaoDoProntuarioWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
            return;
        }
        var texto = string.Join("\n\n", new[] { $"{registro.Data:dd/MM/yyyy} {registro.HoraTexto} · {registro.Autor}", registro.Marca, registro.Titulo, registro.Detalhe }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var view = new TextBox { Text = texto, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), Padding = new Thickness(20) };
        new ConsultaContextualWindow(registro.Rotulo + " — leitura", view, "Voltar ao atendimento") { Owner = JanelaDona.Atual() }.ShowDialog();
    }
}
