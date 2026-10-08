using Clinica.Desktop.Controls;
using Clinica.Application.Abstracoes;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
namespace Clinica.Clinico.ViewModels;

/// <summary>Mesmo cadastro e importação CSV, com apresentação independente de WPF.</summary>
public sealed partial class MedicamentosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private IReadOnlyList<MedicamentoCadastro> _todos = [];
    private MedicamentoCadastro? _selecionado;
    public ObservableCollection<MedicamentoCadastro> Medicamentos { get; } = [];
    [ObservableProperty] private string _busca = "";
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _principioAtivo = "";
    [ObservableProperty] private string _apresentacao = "";
    [ObservableProperty] private string _fabricante = "";
    [ObservableProperty] private bool _ativo = true;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private string _resumo = "";
    public MedicamentosViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    { _escopos = escopos; _dialogo = dialogo; _ = CarregarAsync(); }
    partial void OnBuscaChanged(string value) => Filtrar();
    [RelayCommand] private void Novo() { _selecionado = null; Nome = PrincipioAtivo = Apresentacao = Fabricante = ""; Ativo = true; }
    [RelayCommand] private void Editar(MedicamentoCadastro? item)
    { if (item is null || !Medicamentos.Contains(item)) return; _selecionado = item; Nome = item.Nome; PrincipioAtivo = item.PrincipioAtivo ?? ""; Apresentacao = item.Apresentacao ?? ""; Fabricante = item.Fabricante ?? ""; Ativo = item.Ativo; }
    [RelayCommand] private async Task CarregarAsync()
    {
        if (Ocupado) return;
        try { Ocupado = true; SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "consultar medicamentos"); using var scope = _escopos.CreateScope(); _todos = await scope.ServiceProvider.GetRequiredService<MedicamentoCatalogoService>().ListarAsync(); NaoVerificado = false; Filtrar(); }
        catch (Exception ex) { NaoVerificado = true; Mensagem = ex.Message; MensagemEhErro = true; }
        finally { Ocupado = false; }
    }
    private void Filtrar()
    {
        var compare = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        var lista = _todos.Where(m => compare.IndexOf(m.Nome + " " + m.PrincipioAtivo + " " + m.Apresentacao + " " + m.Fabricante, Busca.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0).ToArray();
        Medicamentos.Clear(); foreach (var m in lista.OrderByDescending(m => m.Codigo.StartsWith("clinica:")).ThenBy(m => m.Nome).Take(250)) Medicamentos.Add(m);
        Resumo = $"{lista.Length:N0} cadastro(s). Exibindo até 250; use a busca para refinar.";
    }
    [RelayCommand] private async Task SalvarAsync()
    {
        if (Ocupado) return;
        try
        {
            Ocupado = true; SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "salvar medicamento");
            var dados = new MedicamentoCadastro { Codigo = _selecionado?.Codigo ?? "", Nome = Nome.Trim(), PrincipioAtivo = PrincipioAtivo.Trim(), Apresentacao = Apresentacao.Trim(), Fabricante = Fabricante.Trim(), Ativo = Ativo };
            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MedicamentoCatalogoService>().SalvarAsync(dados, SessaoUsuario.Atual.UsuarioId);
            Ocupado = false; await CarregarAsync();
            _selecionado = _todos.SingleOrDefault(m => m.Nome == dados.Nome && (m.Apresentacao ?? "") == dados.Apresentacao && (m.Fabricante ?? "") == dados.Fabricante);
            Mensagem = "Cadastro salvo. A prescrição usará os dados atualizados ao abrir a tela."; MensagemEhErro = false;
        }
        catch (Exception ex) { Mensagem = ex.Message; MensagemEhErro = true; }
        finally { Ocupado = false; }
    }
    [RelayCommand] private void ExportarModelo()
    {
        var d = new SaveFileDialog { Filter = "Planilha CSV|*.csv", FileName = "medicamentos.csv" }; if (d.ShowDialog() != true) return;
        try { File.WriteAllText(d.FileName, "Nome;PrincipioAtivo;Apresentacao;Fabricante\r\n", new UTF8Encoding(true)); Mensagem = "Preencha a planilha, salve como CSV UTF-8 e use Importar CSV."; MensagemEhErro = false; }
        catch (Exception ex) { Mensagem = ex.Message; MensagemEhErro = true; }
    }
    [RelayCommand] private async Task ImportarAsync()
    {
        if (Ocupado) return;
        var d = new OpenFileDialog { Filter = "Planilha CSV|*.csv" }; if (d.ShowDialog() != true) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "importar medicamentos");
            if (new FileInfo(d.FileName).Length > 5_000_000) throw new InvalidOperationException("A planilha deve ter até 5 MB.");
            using var parser = new TextFieldParser(d.FileName, Encoding.UTF8, true) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true }; parser.SetDelimiters(";");
            var cabecalho = parser.ReadFields(); if (cabecalho is null || !cabecalho.SequenceEqual(new[] { "Nome", "PrincipioAtivo", "Apresentacao", "Fabricante" })) throw new InvalidOperationException("Use o Modelo de planilha, com as quatro colunas e separador ponto e vírgula.");
            var lista = new List<MedicamentoCadastro>();
            while (!parser.EndOfData) { var c = parser.ReadFields(); if (c is null) continue; if (c.Length != 4) throw new InvalidOperationException("Confira as quatro colunas da planilha."); lista.Add(new() { Nome = c[0], PrincipioAtivo = c[1], Apresentacao = c[2], Fabricante = c[3] }); if (lista.Count > 5000) throw new InvalidOperationException("Importe até 5.000 medicamentos por vez."); }
            if (!await DialogosDaSessao.ConfirmarAsync(_dialogo, "Importar medicamentos", $"Importar {lista.Count} medicamentos para o cadastro compartilhado?")) return;
            Ocupado = true; using var scope = _escopos.CreateScope(); await scope.ServiceProvider.GetRequiredService<MedicamentoCatalogoService>().SalvarLoteAsync(lista, SessaoUsuario.Atual.UsuarioId);
            Ocupado = false; await CarregarAsync(); Mensagem = $"{lista.Count} medicamentos importados."; MensagemEhErro = false;
        }
        catch (Exception ex) { Mensagem = ex.Message; MensagemEhErro = true; }
        finally { Ocupado = false; }
    }
}
