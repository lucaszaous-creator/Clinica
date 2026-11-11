// Dados FICTÍCIOS da prévia — nenhuma pessoa real, nenhuma operadora chamada.
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O ENVIO À OPERADORA na prévia (nov/2026): a aba "Envio pelo web service" de Guias TISS, o selo
/// de envio do painel de pendências e a credencial em Configurações precisam de guia em CADA
/// estado para serem vistos — e na prévia não há serviço de envio nem operadora. Esta semente faz
/// o papel dos dois: pede os envios pelo <see cref="FilaTissService"/> (como a tela) e grava os
/// desfechos pelo mesmo serviço (como o conector gravaria), sem rede nenhuma.
///
/// O que ela deixa:
/// <list type="bullet">
/// <item>o envio pelo web service LIGADO para a Petrobras e uma credencial de mentira (cifrada com
/// uma chave descartável, que nada abre) — é o que acende as ações de envio e a elegibilidade;</item>
/// <item>três guias conferidas, <b>prontas para envio</b>;</item>
/// <item>um envio <b>na fila</b>; um <b>recebido com glosa</b> (uma recebida, uma glosada, uma sem
/// resposta); um em que <b>não se sabe se chegou</b>; um <b>cancelado antes de sair</b>; e um
/// recebido e <b>já baixado</b>.</item>
/// </list>
/// Roda depois da semente da guia ANS (48), que cadastra o que a guia conferida precisa.
/// </summary>
public sealed class SementeEnvioTiss : ISemente
{
    public int Ordem => 49;

    private const string Petrobras = "Petrobras";
    private const string Conector = "previa-sem-conector";

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        var fila = new FilaTissService(repo);
        var faturamento = new FaturamentoService(repo);
        // A chave é de mentira (32 zeros): a credencial gravada com ela não abre em lugar nenhum.
        var credenciais = new CredenciaisTissService(repo, new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32])));
        credenciais.SalvarAsync(Petrobras, "clinica.previa", "senha-de-mentira", "Prévia").GetAwaiter().GetResult();
        credenciais.DefinirEnvioAsync(Petrobras, true, "Prévia").GetAwaiter().GetResult();
        db.ChangeTracker.Clear();

        var atendimentos = new AtendimentoService(repo);
        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(-2);
        var n = 0;

        // Uma guia de consulta da Petrobras, conferida (pronta para envio) — devolve o id dela.
        int Conferida(string nome)
        {
            n++;
            var paciente = db.Add(new Paciente
            {
                Nome = nome, Convenio = Convenio.Petrobras, ConvenioCodigo = Petrobras, Telefone = $"(21) 98222-10{n:00}",
                Carteirinha = $"PB10000{n:00}", ValidadeCarteirinha = dia.AddDays(200), Sexo = n % 2 == 0 ? Sexo.Masculino : Sexo.Feminino
            }).Entity;
            db.SaveChanges();
            var codigo = atendimentos.LancarAsync(paciente.Id, dia, ModalidadeAtendimento.Consulta, operador: "Prévia")
                .GetAwaiter().GetResult().Atendimento.Codigos.OrderBy(c => c.Ordem).First();
            faturamento.ConferirGuiaAnsAsync(codigo.Id, GuiaAnsDaPrevia.Para(codigo, paciente, dia)!, "secretaria.previa").GetAwaiter().GetResult();
            db.ChangeTracker.Clear();
            return codigo.Id;
        }

        TransacaoTiss Pedir(params int[] guias)
        {
            var t = fila.EnfileirarEnvioAsync(Petrobras, guias, "secretaria.previa").GetAwaiter().GetResult();
            db.ChangeTracker.Clear();
            return t;
        }

        // O que o conector faria: reservar, carimbar que a mensagem saiu e gravar o desfecho.
        void Sair(TransacaoTiss t)
        {
            fila.ReservarProximaAsync(Conector).GetAwaiter().GetResult();
            fila.MarcarEnvioIniciadoAsync(t.Id, "<previa/>").GetAwaiter().GetResult();
        }

        // 1) Recebido e JÁ BAIXADO: o fim feliz, com o número que a operadora devolveu.
        var baixado = Pedir(Conferida("Olívia Prates Sampaio"), Conferida("Raul Bittencourt Lemos"));
        Sair(baixado);
        fila.ConcluirAsync(baixado.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Lote recebido pela operadora.", Protocolo = "20261100017",
            Guias = baixado.Guias.Select((g, i) => new ResultadoDaGuiaTiss(g.NumeroGuiaPrestador, $"PB2026110{i + 1:000}", SituacaoGuiaTiss.Recebida)).ToList()
        }).GetAwaiter().GetResult();
        db.ChangeTracker.Clear();
        new BaixaDasGuiasEnviadasService(repo, fila, faturamento)
            .BaixarRecebidasAsync(baixado.Id, DateOnly.FromDateTime(DateTime.Today), "secretaria.previa").GetAwaiter().GetResult();
        db.ChangeTracker.Clear();

        // 2) Recebido COM GLOSA: uma recebida (aguardando a baixa), uma glosada e uma sem resposta.
        var comGlosa = Pedir(Conferida("Sabrina Queiroz Mota"), Conferida("Túlio Avelar Pinho"), Conferida("Úrsula Maia Coutinho"));
        Sair(comGlosa);
        fila.ConcluirAsync(comGlosa.Id, new ConclusaoTransacaoTiss
        {
            Resultado = "Lote protocolado.", Protocolo = "20261100018",
            Guias =
            [
                new ResultadoDaGuiaTiss(comGlosa.Guias[0].NumeroGuiaPrestador, "PB2026110101", SituacaoGuiaTiss.Recebida),
                new ResultadoDaGuiaTiss(comGlosa.Guias[1].NumeroGuiaPrestador, null, SituacaoGuiaTiss.Glosada, "1001 - Número da carteira inválido"),
                new ResultadoDaGuiaTiss(comGlosa.Guias[2].NumeroGuiaPrestador, null, SituacaoGuiaTiss.Pendente)
            ]
        }).GetAwaiter().GetResult();
        db.ChangeTracker.Clear();

        // 3) NÃO SE SABE SE CHEGOU: a mensagem saiu e a rede caiu. É o estado que a tela destaca.
        var incerto = Pedir(Conferida("Valdir Nóbrega Saldanha"));
        Sair(incerto);
        fila.FalharAsync(incerto.Id, "A operadora não respondeu (tempo esgotado).", podeTentarDeNovo: true).GetAwaiter().GetResult();
        db.ChangeTracker.Clear();

        // 4) Cancelado por quem pediu, antes de sair: a guia volta a ficar pronta para envio.
        var cancelado = Pedir(Conferida("Wanda Peixoto Arruda"));
        fila.CancelarAsync(cancelado.Id, "secretaria.previa").GetAwaiter().GetResult();
        db.ChangeTracker.Clear();

        // 5) Prontas para envio, ainda fora de qualquer pedido.
        Conferida("Xavier Dornelas Fraga");
        Conferida("Yara Camargo Bastos");

        // 6) NA FILA: o último pedido, que ninguém vai buscar — na prévia não há serviço de envio.
        Pedir(Conferida("Zélia Antunes Rocha"));
    }
}
