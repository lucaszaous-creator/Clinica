using System.Text.Json;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Financeiro.ViewModels;
using Clinica.Financeiro.Web;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Contratos e ciclo completo de diálogos web sobre o SQLite sintético do harness.</summary>
public static class DialogosWebQa
{
    public static async Task Executar(IServiceProvider services)
    {
        var escopos = services.GetRequiredService<IServiceScopeFactory>();
        var snackbar = services.GetRequiredService<ISnackbarService>();
        var dialogo = services.GetRequiredService<IDialogoService>();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Exigir(db.Database.IsSqlite() && new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource == ":memory:",
            "QA de diálogos exige SQLite em memória.");
        var erros = DialogosFinanceiroController.ValidarRegistro();
        Exigir(erros.Count == 0, "Contrato de formulário inválido: " + string.Join("; ", erros));
        var original = await db.Set<UsuarioSistema>().AsNoTracking().SingleAsync(u => u.Id == SessaoUsuario.Atual.UsuarioId);
        var item = await db.ItensEstoque.FirstAsync();
        var paciente = new Paciente { Nome = "Paciente sintético QA dos diálogos" };
        var pacote = new PacotePaciente { Paciente = paciente, Nome = "Pacote sintético QA", SessoesContratadas = 5, Valor = 100, DataCompra = DateOnly.FromDateTime(DateTime.Today) };
        db.Add(pacote);
        db.Add(new PacoteCatalogo { Nome = "Catálogo sintético QA", SessoesIncluidas = 5, Valor = 100 });
        if (!await db.Set<Profissional>().AnyAsync()) db.Add(new Profissional { Nome = "Profissional sintético QA", Ativo = true });
        await db.SaveChangesAsync();
        var totalLancamentos = await db.Set<LancamentoFinanceiro>().CountAsync();
        var totalPacotes = await db.Set<PacotePaciente>().CountAsync();
        var totalCategorias = await db.Set<CategoriaFinanceira>().CountAsync();
        using var controller = new DialogosFinanceiroController();
        (string Tipo, Func<object> Vm)[] casos =
        [
            ("Categoria", () => new CategoriaEdicaoViewModel(escopos, 1)),
            ("CobrancaPix", () => new CobrancaPixViewModel(escopos, 250m, "QA-SINTETICO")),
            ("ContasFixas", () => new ContasViewModel(escopos, snackbar, dialogo)),
            ("Conta", () => new ContaEdicaoViewModel(escopos)),
            ("ExtratoEstoque", () => new ExtratoEstoqueViewModel(escopos, item.Id, item.Nome)),
            ("Lancamento", () => new LancamentoEdicaoViewModel(escopos)),
            ("ItemEstoque", () => new ItemEstoqueEdicaoViewModel(escopos, item.Id)),
            ("Orcamento", () => new OrcamentoEdicaoViewModel(escopos, DateTime.Today.Year, DateTime.Today.Month)),
            ("Recorrente", () => new RecorrenteEdicaoViewModel(escopos, 0)),
            ("MovimentoEstoque", () => new MovimentoEstoqueViewModel(escopos, item.Id, item.Nome)),
            ("RegraRepasse", () => new RegraRepasseViewModel(escopos)),
            ("Taxa", () => new TaxaEdicaoViewModel(escopos, 0)),
            ("Tributo", () => new TributoEdicaoViewModel(escopos, 0)),
            ("RegrasRepasse", () => new RepassesViewModel(escopos, snackbar, dialogo)),
            ("ValidadesEstoque", () => new EstoqueViewModel(escopos, snackbar, dialogo)),
            ("Recebimento", () => new BaixarLancamentoViewModel(escopos, new LancamentoFinanceiro { Descricao = "Conta sintética sem gravação", Valor = 100, Tipo = TipoLancamento.Entrada })),
            ("PacoteVenda", () => new PacoteVendaViewModel(escopos)),
            ("PacoteCatalogo", () => new PacoteCatalogoEdicaoViewModel(escopos)),
            ("CatalogoPacotes", () => new PacotesViewModel(escopos, snackbar, dialogo)),
            ("ConsumosPacote", () => new ConsumosPacoteViewModel(escopos, dialogo, pacote.Id, "Pacote sintético")),
            ("MateriaisProcedimento", () => new MateriaisProcedimentoViewModel([new MaterialProcedimentoLinha { ItemId = item.Id, Nome = item.Nome, Saldo = "10 un" }])),
            ("TrocaSenha", () => new DialogosTrocaSenhaViewModel(escopos))
        ];
        foreach (var caso in casos)
        {
            var vm = caso.Vm();
            var tarefa = controller.AbrirAsync(caso.Tipo, vm);
            await Pronto(controller);
            if (tarefa.IsFaulted) await tarefa;
            var atual = controller.EstadoAtual ?? throw new Exception("Não abriu " + caso.Tipo);
            Exigir(atual.Pagina.Chave == caso.Tipo && Guid.TryParse(atual.Id, out _), "Modal sem identidade válida.");
            Exigir(!atual.Pagina.NaoVerificado && !atual.Pagina.MensagemEhErro, $"Falha ao abrir {caso.Tipo}: {atual.Pagina.Mensagem}");
            foreach (var campo in atual.Pagina.Campos)
            {
                Exigir(campo.Tipo is "texto" or "textarea" or "leitura" or "selecao" or "data" or "booleano" or "senha", "Tipo de campo desconhecido.");
                if (campo.Tipo == "booleano") Exigir(campo.Valor is bool, "Checkbox perdeu tipo booleano.");
                if (campo.Tipo == "senha") Exigir(campo.Valor is null, "Senha exposta no estado.");
                if (campo.Tipo == "selecao") Exigir(campo.Opcoes.Select(o => o.Valor).Distinct().Count() == campo.Opcoes.Count, "Opções têm identificadores duplicados.");
            }
            await Rejeitar(() => controller.AtualizarCampoAsync(atual.Id, "UsuarioId", Json(123)), "Campo arbitrário aceito.");
            await Rejeitar(() => controller.ExecutarAcaoAsync(atual.Id, "GetType"), "Comando arbitrário aceito.");
            var leitura = atual.Pagina.Campos.FirstOrDefault(c => c.Tipo == "leitura");
            if (leitura is not null) await Rejeitar(() => controller.AtualizarCampoAsync(atual.Id, leitura.Chave, Json("alterado")), "Campo readonly editado.");
            var opcao = atual.Pagina.Campos.FirstOrDefault(c => c.Tipo == "selecao" && c.Visivel && c.Habilitado);
            if (opcao is not null) await Rejeitar(() => controller.AtualizarCampoAsync(atual.Id, opcao.Chave, Json("id-fora-da-colecao")), "Opção estrangeira aceita.");
            var data = atual.Pagina.Campos.FirstOrDefault(c => c.Tipo == "data" && c.Visivel && c.Habilitado);
            if (data is not null) await Rejeitar(() => controller.AtualizarCampoAsync(atual.Id, data.Chave, Json("2026-99-99")), "Data inválida aceita.");
            var booleano = atual.Pagina.Campos.FirstOrDefault(c => c.Tipo == "booleano" && c.Visivel && c.Habilitado);
            if (booleano is not null) await controller.AtualizarCampoAsync(atual.Id, booleano.Chave, Json((bool)booleano.Valor!));
            controller.Fechar(atual.Id);
            Exigir(await tarefa == false && controller.EstadoAtual is null, "Cancelar confirmou ou deixou modal aberto.");
            await Rejeitar(() => controller.ExecutarAcaoAsync(atual.Id, "salvar"), "Modal obsoleto aceitou salvar.");
            Console.WriteLine($"DIALOGOS WEB OK {caso.Tipo}: campos, opções, comandos e cancelamento.");
        }
        Exigir(totalLancamentos == await db.Set<LancamentoFinanceiro>().CountAsync() && totalPacotes == await db.Set<PacotePaciente>().CountAsync()
            && totalCategorias == await db.Set<CategoriaFinanceira>().CountAsync(), "Abrir/cancelar formulários gravou dados.");

        var pergunta = controller.PerguntarTextoAsync("Motivo QA", "Informe o motivo", null, true);
        var perguntaId = controller.EstadoAtual!.Id;
        await controller.ExecutarAcaoAsync(perguntaId, "confirmar");
        Exigir(!pergunta.IsCompleted && controller.EstadoAtual!.Pagina.MensagemEhErro, "Pergunta obrigatória vazia foi aceita.");
        await controller.AtualizarCampoAsync(perguntaId, "Texto", Json("Motivo sintético"));
        await controller.ExecutarAcaoAsync(perguntaId, "confirmar");
        Exigir(await pergunta == "Motivo sintético", "Pergunta perdeu o texto.");
        var confirmar = controller.ConfirmarAsync("Confirmação QA", "Dados fictícios", true);
        Exigir(controller.EstadoAtual!.Pagina.Acoes.Single(a => a.Chave == "confirmar").Estilo == "perigo", "Confirmação perigosa perdeu estilo.");
        controller.Fechar(controller.EstadoAtual.Id); Exigir(!await confirmar, "Cancelar confirmação retornou true.");
        var aviso = controller.AvisoAsync("Histórico QA", "Linha 1\nLinha 2");
        controller.Fechar(controller.EstadoAtual!.Id); await aviso;

        // Uma ação de venda aguarda o filho de catálogo sem bloquear o canal do filho.
        var venda = new PacoteVendaViewModel(escopos);
        var vendaAberta = controller.AbrirAsync("PacoteVenda", venda); await Pronto(controller);
        var paiId = controller.EstadoAtual!.Id;
        var abrirFilho = controller.ExecutarAcaoAsync(paiId, "cadastrar-pacote");
        await Aguardar(() => controller.EstadoAtual?.Pagina.Chave == "PacoteCatalogo");
        var filhoId = controller.EstadoAtual!.Id;
        await Rejeitar(() => controller.ExecutarAcaoAsync(paiId, "salvar"), "Pai aceitou ação enquanto filho estava aberto.");
        controller.Fechar(filhoId); await abrirFilho;
        Exigir(controller.EstadoAtual?.Id == paiId && controller.EstadoAtual.PodeFechar, "Filho não retornou ao pai desbloqueado.");
        controller.Fechar(paiId); Exigir(await vendaAberta == false, "Cancelar pai confirmou venda.");

        // Campos de linha e anti-duplo-clique usando o mesmo VM real, com gravação controlada em memória.
        var liberar = new TaskCompletionSource<ConferenciaConsumoProcedimento>(TaskCreationOptions.RunContinuationsAsynchronously);
        var chamadas = 0;
        var materiais = new MateriaisProcedimentoViewModel([new MaterialProcedimentoLinha { ItemId = item.Id, Nome = item.Nome, Saldo = "10 un" }], _ => { chamadas++; return liberar.Task; });
        var materiaisAberto = controller.AbrirAsync("MateriaisProcedimento", materiais);
        var materialId = controller.EstadoAtual!.Id;
        var tabela = controller.EstadoAtual.Pagina.Secoes.Single().Tabelas.Single();
        var linha = tabela.Linhas.Single();
        await controller.AtualizarCampoAsync(materialId, "Quantidade", Json("2"), tabela.Chave, linha.Id);
        await controller.AtualizarCampoAsync(materialId, "Lote", Json("LOTE-QA"), tabela.Chave, linha.Id);
        Exigir(materiais.Itens.Single().Quantidade == "2" && materiais.Itens.Single().Lote == "LOTE-QA", "Campo de linha não chegou ao VM.");
        await Rejeitar(() => controller.AtualizarCampoAsync(materialId, "Nome", Json("Inválido"), tabela.Chave, linha.Id), "Linha expôs campo não permitido.");
        await controller.ExecutarAcaoAsync(materialId, "outro-lote", linha.Id, tabela.Chave);
        Exigir(materiais.Itens.Count == 2, "Outro lote não criou segunda linha.");
        var salvarMateriais = controller.ExecutarAcaoAsync(materialId, "confirmar");
        Exigir(controller.EstadoAtual!.Ocupado, "Gravação não marcou modal ocupado.");
        await Rejeitar(() => controller.ExecutarAcaoAsync(materialId, "confirmar"), "Dupla gravação foi aceita.");
        await Rejeitar(() => controller.AtualizarCampoAsync(materialId, "Busca", Json("novo")), "Editou durante gravação.");
        await Rejeitar(() => { controller.Fechar(materialId); return Task.CompletedTask; }, "Fechou durante gravação.");
        liberar.SetResult(new ConferenciaConsumoProcedimento { BaixadoEm = DateTime.Now }); await salvarMateriais;
        Exigir(chamadas == 1 && !materiais.PodeEditar, "Gravação duplicada ou registro não ficou readonly.");
        controller.Fechar(materialId); await materiaisAberto;

        await ValidarPersistencia(controller, services);
        var senhaVm = new DialogosTrocaSenhaViewModel(escopos);
        var senhaAberta = controller.AbrirAsync("TrocaSenha", senhaVm);
        var senhaId = controller.EstadoAtual!.Id;
        await controller.AtualizarCampoAsync(senhaId, "Atual", Json("segredo-ficticio-QA"));
        Exigir(controller.EstadoAtual.Pagina.Campos.All(c => c.Valor is null), "Senha serializada no estado.");
        controller.Fechar(senhaId); await senhaAberta;
        Exigir(senhaVm.Atual.Length == 0 && senhaVm.Nova.Length == 0 && senhaVm.Repetida.Length == 0, "Cancelamento reteve senha no VM.");

        // Troca de sessão invalida IDs e dispose cancela todos os aguardadores.
        using var encerrando = new DialogosFinanceiroController();
        var pendente = encerrando.PerguntarTextoAsync("Pendente", "QA", null, true);
        var pendenteId = encerrando.EstadoAtual!.Id;
        try
        {
            SessaoUsuario.Atual.Entrar(new UsuarioSistema { Id = original.Id + 10000, Nome = "Outra sessão QA", Login = "qa-outra", Perfil = PerfilAcesso.Gerente });
            await Rejeitar(() => encerrando.AtualizarCampoAsync(pendenteId, "Texto", Json("não pode")), "Outra sessão conseguiu responder.");
            await Rejeitar(() => Task.FromResult(encerrando.EstadoAtual), "Nova sessão ainda leu o formulário anterior.");
            encerrando.Dispose();
            try { await pendente; throw new Exception("Dispose não cancelou tarefa pendente."); } catch (OperationCanceledException) { }
        }
        finally { SessaoUsuario.Atual.Entrar(original); }
        Console.WriteLine("DIALOGOS WEB OK: 25 contratos, prompts, nested, IDs, readonly, materiais, dupla gravação, senha e perda de sessão.");
    }

    static async Task ValidarPersistencia(DialogosFinanceiroController controller, IServiceProvider services)
    {
        var escopos = services.GetRequiredService<IServiceScopeFactory>();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var taxa = new TaxaEdicaoViewModel(escopos, 0);
        var aberta = controller.AbrirAsync("Taxa", taxa);
        var id = controller.EstadoAtual!.Id;
        await controller.ExecutarAcaoAsync(id, "salvar");
        Exigir(!aberta.IsCompleted && controller.EstadoAtual!.Pagina.MensagemEhErro, "Validação fechou taxa incompleta.");
        await controller.AtualizarCampoAsync(id, "Adquirente", Json("Contrato QA WEB"));
        await controller.AtualizarCampoAsync(id, "Percentual", Json("2,5"));
        await controller.ExecutarAcaoAsync(id, "salvar");
        Exigir(await aberta == true, "Taxa não confirmou persistência.");
        var gravada = await db.Set<TaxaCartao>().AsNoTracking().SingleAsync(t => t.Adquirente == "Contrato QA WEB");
        Exigir(gravada.Percentual == 2.5m, "Taxa perdeu percentual.");
        var editar = new TaxaEdicaoViewModel(escopos, gravada.Id);
        aberta = controller.AbrirAsync("Taxa", editar);
        await Aguardar(() => editar.Adquirente == "Contrato QA WEB"); id = controller.EstadoAtual!.Id;
        await controller.AtualizarCampoAsync(id, "Percentual", Json("3"));
        await controller.ExecutarAcaoAsync(id, "salvar"); Exigir(await aberta == true, "Edição da taxa não concluiu.");
        Exigir((await db.Set<TaxaCartao>().AsNoTracking().SingleAsync(t => t.Id == gravada.Id)).Percentual == 3m, "Edição não preservou ID/valor.");

        var recorrente = new RecorrenteEdicaoViewModel(escopos, 0);
        aberta = controller.AbrirAsync("Recorrente", recorrente); await Pronto(controller); id = controller.EstadoAtual!.Id;
        await controller.AtualizarCampoAsync(id, "Descricao", Json("Conta fixa QA WEB"));
        await controller.AtualizarCampoAsync(id, "Valor", Json("123,45"));
        await controller.ExecutarAcaoAsync(id, "salvar"); Exigir(await aberta == true, "Conta fixa não concluiu.");
        var molde = await db.Set<LancamentoRecorrente>().AsNoTracking().SingleAsync(r => r.Descricao == "Conta fixa QA WEB");
        recorrente = new RecorrenteEdicaoViewModel(escopos, molde.Id);
        aberta = controller.AbrirAsync("Recorrente", recorrente); await Aguardar(() => recorrente.Descricao == molde.Descricao); id = controller.EstadoAtual!.Id;
        await Rejeitar(() => controller.AtualizarCampoAsync(id, "PrimeiroVencimento", Json("2026-01-01")), "Edição alterou âncora da recorrência.");
        await controller.AtualizarCampoAsync(id, "Valor", Json("145,67"));
        await controller.ExecutarAcaoAsync(id, "salvar"); Exigir(await aberta == true, "Editar conta fixa não concluiu.");
        var alterado = await db.Set<LancamentoRecorrente>().AsNoTracking().SingleAsync(r => r.Id == molde.Id);
        Exigir(alterado.Valor == 145.67m && alterado.PrimeiroVencimento == molde.PrimeiroVencimento, "Edição de recorrência mudou âncora ou perdeu valor.");
        Console.WriteLine("DIALOGOS WEB OK: taxa e conta fixa criadas/editadas pelo controller e serviços reais.");
    }
    static JsonElement Json<T>(T valor) => JsonSerializer.SerializeToElement(valor);
    static void Exigir(bool condicao, string mensagem) { if (!condicao) throw new InvalidOperationException(mensagem); }
    static async Task Rejeitar(Func<Task> acao, string mensagem)
    {
        try { await acao(); } catch (InvalidOperationException) { return; } catch (OperationCanceledException) { return; }
        throw new Exception(mensagem);
    }
    static async Task Pronto(DialogosFinanceiroController c)
    {
        await Aguardar(() => c.EstadoAtual is { Ocupado: false });
        await Task.Delay(40);
    }
    static async Task Aguardar(Func<bool> condicao)
    {
        for (var i = 0; i < 150; i++) { if (condicao()) return; await Task.Delay(20); }
        throw new TimeoutException("Estado de formulário não ficou pronto em três segundos.");
    }
}
