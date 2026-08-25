using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Cadastro da autorização de sessões — a "senha" que o convênio libera (parcela 48).
///
/// Por que ela precisava existir AQUI
/// ----------------------------------
/// O <see cref="ElegibilidadeService"/> avisa o balcão desde a parcela 26: <i>"Cota
/// esgotada — a próxima sessão vira glosa 2006"</i> e <i>"Última sessão autorizada — peça
/// a renovação da senha"</i>. E a única porta para REGISTRAR a senha nova estava no app de
/// FATURAMENTO, que é de outra pessoa e nem sempre está aberto.
///
/// É a nona ocorrência do defeito recorrente do projeto, na variante que a parcela 46 já
/// tinha corrigido para a consulta: <b>a recepção via o aviso e não tinha por onde
/// resolver</b>. Quem recebe a senha da operadora — por telefone, por portal, por e-mail —
/// é o balcão, e o momento de digitá-la é aquele, não o de faturar.
///
/// O que ela NÃO faz
/// -----------------
/// Não conta o consumo. A quantidade utilizada é apurada pelo
/// <see cref="AutorizacaoService"/> a partir dos ATENDIMENTOS dentro da vigência —
/// <see cref="QuantidadeUtilizadaManual"/> serve só para o saldo que veio de antes do
/// sistema. Deixar a recepção digitar "usadas" livremente criaria um segundo número sobre
/// o mesmo fato, e o errado seria o que a tela mostra.
/// </summary>
public partial class AutorizacaoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;

    public ObservableCollection<EntradaConvenio> Convenios { get; } = new();

    [ObservableProperty] private int? _editandoId;
    [ObservableProperty] private string? _numero;
    [ObservableProperty] private string? _convenioCodigo;
    [ObservableProperty] private DateTime _dataEmissao = DateTime.Today;
    [ObservableProperty] private DateTime _dataValidade = DateTime.Today.AddDays(90);
    [ObservableProperty] private int _quantidadeAutorizada = 10;
    [ObservableProperty] private int _quantidadeUtilizadaManual;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _encerrada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    [ObservableProperty] private bool _ocupado;

    /// <summary>
    /// Metade VISÍVEL da permissão. A que IMPEDE é o <c>Exigir</c> no Salvar.
    ///
    /// É <c>EditarPaciente</c> — o MESMO bit que já governa cadastrar paciente na
    /// Recepção (o corte da parcela 49: senha de autorização é dado administrativo do
    /// convênio, não dado de saúde). Um bit novo pareceria mais preciso e teria um efeito indesejado: quem
    /// cadastra paciente hoje deixaria de conseguir registrar a senha até alguém marcar a
    /// caixinha nova em Acessos. Versão que introduz permissão e, de quebra, tira uma
    /// função que a pessoa usava ontem vira chamado de suporte na segunda de manhã.
    /// </summary>
    public bool PodeSalvar => SessaoUsuario.Atual.Pode(Permissao.EditarPaciente);

    public string Titulo => EditandoId is null ? "Nova autorização" : "Editar autorização";

    partial void OnEditandoIdChanged(int? value) => OnPropertyChanged(nameof(Titulo));

    /// <summary>Disparado quando a autorização é gravada — a janela fecha e a ficha recarrega.</summary>
    public event Action? Salvou;

    public AutorizacaoEdicaoViewModel(IServiceScopeFactory escopos, int pacienteId)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
    }

    public async Task CarregarAsync(int? autorizacaoId, string? convenioPadrao)
    {
        Convenios.Clear();
        foreach (var c in CatalogoConvenios.Ativos)
            Convenios.Add(c);

        // O convênio do PACIENTE já vem escolhido: numa clínica com cinco operadoras, a
        // senha é quase sempre da dele, e errar o convênio faz a autorização não valer
        // para nada sem que a tela reclame de nada.
        ConvenioCodigo = convenioPadrao ?? Convenios.FirstOrDefault()?.Codigo;

        if (autorizacaoId is not int id) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
            var autorizacao = (await servico.DoPacienteAsync(_pacienteId))
                .FirstOrDefault(a => a.Id == id);

            if (autorizacao is null)
            {
                Mensagem = "Autorização não encontrada.";
                return;
            }

            EditandoId = autorizacao.Id;
            Numero = autorizacao.Numero;
            ConvenioCodigo = autorizacao.ConvenioCodigo ?? autorizacao.Convenio.ToString();
            DataEmissao = autorizacao.DataEmissao.ToDateTime(TimeOnly.MinValue);
            DataValidade = autorizacao.DataValidade.ToDateTime(TimeOnly.MinValue);
            QuantidadeAutorizada = autorizacao.QuantidadeAutorizada;
            QuantidadeUtilizadaManual = autorizacao.QuantidadeUtilizadaManual;
            Observacoes = autorizacao.Observacoes;
            Encerrada = autorizacao.Encerrada;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — autorização não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar a autorização: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Salvar()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "registrar autorização");

        if (Ocupado) return;

        // Guardas que FALAM. Guarda que volta calada é botão que não faz nada — a lição da
        // parcela 41. O serviço também valida, e é ele quem impede; aqui a mensagem chega
        // antes, ao lado do campo que está errado.
        if (QuantidadeAutorizada <= 0)
        {
            Mensagem = "A autorização precisa de ao menos uma sessão.";
            return;
        }
        if (DataValidade < DataEmissao)
        {
            Mensagem = "A validade não pode ser anterior à emissão.";
            return;
        }

        Ocupado = true;
        try
        {
            Mensagem = null;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();

            await servico.SalvarAsync(new AutorizacaoSessoes
            {
                Id = EditandoId ?? 0,
                PacienteId = _pacienteId,
                Numero = string.IsNullOrWhiteSpace(Numero) ? null : Numero.Trim(),
                ConvenioCodigo = ConvenioCodigo,
                Convenio = CatalogoConvenios.Familia(ConvenioCodigo),
                DataEmissao = DateOnly.FromDateTime(DataEmissao),
                DataValidade = DateOnly.FromDateTime(DataValidade),
                QuantidadeAutorizada = QuantidadeAutorizada,
                QuantidadeUtilizadaManual = QuantidadeUtilizadaManual,
                Observacoes = string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim(),
                Encerrada = Encerrada
            }, SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — autorização não pôde ser gravada", ex);
            Mensagem = ex.Message;
            return;
        }
        finally
        {
            Ocupado = false;
        }

        Salvou?.Invoke();
    }
}
