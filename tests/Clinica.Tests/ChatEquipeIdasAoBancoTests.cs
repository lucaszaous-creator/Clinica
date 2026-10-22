using System.Data.Common;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Clinica.Tests;

/// <summary>
/// O ORÇAMENTO de idas ao banco de cada operação do chat. Com o banco a ~250 ms do posto
/// (docs/desempenho-leituras-2026-10-15.md), é o número de idas que decide quanto a pessoa
/// espera — e a bolinha bate em todas as máquinas, o dia inteiro.
///
/// Antes desta medição (out/2026): lista 9 idas a cada 16 s E a cada mensagem de qualquer
/// pessoa; abrir uma conversa 9; reler a conversa aberta 7 a cada 4 s; enviar 7 + 7.
///
/// Um <c>SaveChanges</c> conta como UMA ida: o Npgsql manda os comandos dele num lote só (o
/// SQLite dos testes não agrupa, então contar comando por comando aqui mediria o SQLite).
/// </summary>
public sealed class ChatEquipeIdasAoBancoTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly Contador contador = new();
    private readonly ChatEquipeService chat;
    private readonly ITestOutputHelper saida;
    private readonly DateTime relogio = new(2026, 10, 21, 9, 0, 0);
    private readonly UsuarioSistema ana = new() { Nome = "Ana Souza", Login = "ana", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    private readonly UsuarioSistema paulo = new() { Nome = "Paulo Lima", Login = "paulo", Perfil = PerfilAcesso.Profissional, CriadoEm = new(2026, 1, 1) };

    public ChatEquipeIdasAoBancoTests(ITestOutputHelper saida)
    {
        this.saida = saida;
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).AddInterceptors(contador, contador.DoSave).Options);
        db.Database.EnsureCreated();
        db.Usuarios.AddRange(ana, paulo);
        db.SaveChanges();
        chat = new ChatEquipeService(db, () => relogio);
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private async Task<int> Medir(string nome, Func<Task> operacao)
    {
        db.ChangeTracker.Clear();
        contador.Zerar();
        await operacao();
        saida.WriteLine($"{nome}: {contador.Idas} idas");
        return contador.Idas;
    }

    [Fact]
    public async Task Cada_operacao_cabe_no_seu_orcamento_de_idas()
    {
        var direta = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        await chat.EnviarAsync(paulo.Id, direta, new("oi"));
        await chat.EnviarAsync(paulo.Id, ChatEquipeRegras.CanalGeral, new("bom dia"));
        await chat.SinalizarAsync(ana.Id, null);

        (await Medir("pulso (bolinha, a cada 16 s)", () => chat.PulsoAsync(ana.Id, 1, new()))).Should().Be(1);
        (await Medir("lista simples (bolinha)", () => chat.PainelAsync(ana.Id, completo: false))).Should().Be(2);
        (await Medir("lista completa (chat aberto)", () => chat.PainelAsync(ana.Id))).Should().Be(4);
        (await Medir("abrir conversa (já marca lida)", () => chat.AbrirAsync(ana.Id, direta))).Should().Be(3);
        (await Medir("reabrir sem novidade", () => chat.AbrirAsync(ana.Id, direta))).Should().Be(2, "nada a marcar");
        (await Medir("releitura sem novidade (4 s)", () => chat.AtualizarAsync(ana.Id, direta, int.MaxValue - 1, relogio))).Should().Be(2);
        (await Medir("enviar", () => chat.EnviarAsync(ana.Id, direta, new("tudo bem?")))).Should().Be(2);
        (await Medir("enviar a primeira de uma conversa", () => chat.EnviarAsync(ana.Id, ChatEquipeRegras.CanalRecepcao, new("oi recepção"))))
            .Should().BeLessThanOrEqualTo(3, "a lista de conversas ainda não tinha a linha");
        (await Medir("sinalizar presença", () => chat.SinalizarAsync(ana.Id, direta))).Should().Be(1);
        (await Medir("buscar", () => chat.BuscarAsync(ana.Id, "bom dia"))).Should().BeLessThanOrEqualTo(3);
    }

    /// <summary>Conta comandos, e um SaveChanges inteiro como um só (o lote do Npgsql).</summary>
    private sealed class Contador : DbCommandInterceptor
    {
        public int Idas { get; private set; }
        private bool salvando;
        public void Zerar() => Idas = 0;
        public SaveChangesInterceptor DoSave => new NoSave(this);

        private void Comando() { if (!salvando) Idas++; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e,
            InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Comando(); return ValueTask.FromResult(r); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e,
            InterceptionResult<int> r, CancellationToken ct = default) { Comando(); return ValueTask.FromResult(r); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e,
            InterceptionResult<object> r, CancellationToken ct = default) { Comando(); return ValueTask.FromResult(r); }

        private sealed class NoSave(Contador c) : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
            { c.Idas++; c.salvando = true; return ValueTask.FromResult(r); }
            public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int r, CancellationToken ct = default)
            { c.salvando = false; return ValueTask.FromResult(r); }
            public override Task SaveChangesFailedAsync(DbContextErrorEventData e, CancellationToken ct = default)
            { c.salvando = false; return Task.CompletedTask; }
        }
    }
}
