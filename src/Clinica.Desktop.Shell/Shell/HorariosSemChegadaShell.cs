using System.Collections.ObjectModel;
using System.Windows.Threading;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell;

/// <summary>
/// OS HORÁRIOS SEM CHEGADA NO SINO (nov/2026 — conclusão automática por profissional; regra 3 da
/// direção: o que pede ação vira aviso). O profissional com a regra ligada tem o horário com chegada
/// concluído sozinho; o que ficou SEM chegada, passado o prazo, ninguém sabe se aconteceu — e é a
/// recepção quem confirma falta ou presença. A regra e as frases moram em
/// <see cref="ConclusaoPorProfissional"/> (Application, testada) e a leitura em
/// <see cref="Clinica.Infrastructure.ConclusaoAutomaticaService.SemChegadaAsync"/>; aqui só se lê e se desenha.
/// Mesmo desenho dos avisos do @SemDor (<c>AvisosDoSemDorShell</c>).
/// </summary>
public sealed partial class ShellViewModel
{
    private DispatcherTimer? _relogioSemChegada;
    private bool _semChegadaEmCena, _consultandoSemChegada;
    private int _geracaoSemChegada;

    public ObservableCollection<HorariosSemChegada> HorariosSemChegada { get; } = [];

    public string SituacaoSemChegada { get; private set; } = "";

    /// <summary>Quem mexe na agenda (a recepção e a direção): é quem confirma falta ou presença.</summary>
    public bool PodeVerSemChegada => _sessaoClinica is { Autenticado: true } sessao && sessao.Pode(Permissao.EditarAgenda);

    private void IniciarSemChegada()
    {
        if (_semChegadaEmCena || !PodeVerSemChegada) return;
        _semChegadaEmCena = true;
        // 5 min: o prazo é de horas; ler mais vezes não traz notícia.
        _relogioSemChegada ??= new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _relogioSemChegada.Tick -= BatidaSemChegada; _relogioSemChegada.Tick += BatidaSemChegada;
        _relogioSemChegada.Start(); _ = AtualizarSemChegadaAsync();
    }

    private void PararSemChegada()
    {
        _semChegadaEmCena = false; ++_geracaoSemChegada; _relogioSemChegada?.Stop();
    }

    private void BatidaSemChegada(object? sender, EventArgs e) => _ = AtualizarSemChegadaAsync();

    public async Task AtualizarSemChegadaAsync()
    {
        if (!_semChegadaEmCena || _consultandoSemChegada || !PodeVerSemChegada) return;
        var g = _geracaoSemChegada; var usuario = _sessaoClinica!.UsuarioId; _consultandoSemChegada = true;
        try
        {
            using var scope = _servicos.CreateScope();
            var linhas = await scope.ServiceProvider.GetRequiredService<Clinica.Infrastructure.ConclusaoAutomaticaService>()
                .SemChegadaAsync(Clinica.Infrastructure.ConclusaoAutomaticaService.Agora);
            if (!_semChegadaEmCena || g != _geracaoSemChegada || usuario != _sessaoClinica.UsuarioId) return;
            // Entre o Clear e o último Add não há await.
            HorariosSemChegada.Clear();
            foreach (var l in linhas) HorariosSemChegada.Add(l);
            SituacaoSemChegada = "";
            SemChegadaMudou(linhas.Select(l => l.Chave));
        }
        catch (Exception ex)
        {
            if (_semChegadaEmCena && g == _geracaoSemChegada)
            {
                // Terceiro estado: lista vazia por falha não pode parecer "todos chegaram".
                SituacaoSemChegada = "Não foi possível ler os horários sem chegada. Confira a agenda.";
                NotificarSino();
                Clinica.Application.Diagnostico.Registrar("Horários sem chegada no sino", ex);
            }
        }
        finally { _consultandoSemChegada = false; }
    }

    /// <summary>Marca a leitura e abre a agenda: é lá que a recepção registra a falta ou a chegada.</summary>
    public void AbrirSemChegada(HorariosSemChegada aviso)
    {
        if (!PodeVerSemChegada) return;
        LerAviso(aviso.Chave);
        AvisosAbertos = false;
        if (!IrPara(ChavesSuite.Agenda, false))
            Snackbar?.Info($"{aviso.Frase} Abra a agenda de {aviso.MaisAntigo:dd/MM} em diante.");
    }
}
