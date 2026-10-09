using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.WebInfusao;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Dados fictícios, SQLite em memória e o mesmo adapter/editor usado no aplicativo.</summary>
internal static class InfusaoQa
{
    public static async Task Executar(string saida)
    {
        Console.WriteLine("Infusão QA: preparando SQLite em memória.");
        using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        var servicos = new ServiceCollection();
        servicos.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        servicos.AddScoped(_ => new ClinicaDbContext(opcoes));
        servicos.AddSingleton<SessaoUsuario>();
        using var sp = servicos.BuildServiceProvider();
        using var escopo = sp.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        await db.Database.EnsureCreatedAsync();
        Console.WriteLine("Infusão QA: banco sintético criado.");
        var paciente = new Paciente { Nome = "Paciente fictício — validação da infusão", Documento = "12345678909", Telefone = "22999990000", Convenio = Convenio.UnimedIntercambio };
        var profissional = new Profissional { Nome = "Profissional fictício", RegistroConselho = "CRM-RJ 123456", Ativo = true };
        db.AddRange(paciente, profissional); await db.SaveChangesAsync();
        var usuario = new UsuarioSistema { Nome = "Validação fictícia", Login = "qa.infusao", Perfil = PerfilAcesso.Gerente, ProfissionalId = profissional.Id, Profissional = profissional };
        var modelo = new ModeloDocumento
        {
            Nome = "Modelo demonstrativo", Tipo = TipoDocumentoClinico.Receita, ParaInfusao = true, Ativo = true,
            Corpo = "Medicamento demonstrativo do modelo",
            ConfiguracaoInfusao = new ModeloInfusao(null, null,
                new[] { ModeloInfusao.De(new ItemPrescricaoInterna { Descricao = "Medicamento demonstrativo do modelo", Dose = "1 unidade teste", Diluente = "Diluente demonstrativo", Volume = "100 mL", Via = ViaAdministracao.Endovenosa, GrupoInfusao = 1 }) }).Guardar()
        };
        db.AddRange(usuario, modelo, new MedicamentoCadastro { Codigo = "QA-001", Nome = "Medicamento demonstrativo cadastrado", Ativo = true },
            new ConfiguracaoGlobal { Chave = ContinuidadeSemAssinatura.Configuracao, Valor = "true" });
        await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        await FilaQa.Executar(sp, saida);
        await MedicoQa.Executar(sp, db, profissional, saida);
        var dialogos = new Dialogos();
        var vm = new PrescricaoInternaEdicaoViewModel(sp.GetRequiredService<IServiceScopeFactory>(), dialogos, paciente.Id, paciente.Nome, profissional.Id);
        await vm.Inicializacao;
        Console.WriteLine($"Infusão QA: ViewModel carregado, {vm.CatalogoMedicamentos.Count} medicamentos disponíveis.");
        if (vm.MensagemEhErro) throw new InvalidOperationException("Inicialização da infusão: " + vm.Mensagem);
        var adaptador = new InfusaoWebAdapter(vm, () => { });
        using var painel = new PainelClinicoWeb("infusao", adaptador.ObterEstado, adaptador.ExecutarAsync, InfusaoWebAdapter.ExigirAcesso);
        var janela = new Window { Content = painel, Width = 1180, Height = 760, Left = -30000, Top = -30000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        janela.Show();
        try
        {
            var navegador = Program.Navegador(janela);
            await Program.Esperar(navegador, "!!document.querySelector('.infusao-pagina')");
            Console.WriteLine("Infusão QA: editor WebView2 carregado.");
            async Task Clicar(string rotulo)
            {
                var seletor = "[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===" + JsonSerializer.Serialize(rotulo) + ")";
                await Program.Esperar(navegador, seletor + " && !" + seletor + ".disabled");
                await Program.Script(navegador, seletor + ".click()");
            }
            async Task Campo(string rotulo, string valor)
            {
                await Program.Script(navegador, "(()=>{const el=[...document.querySelectorAll('label.inf-campo')].find(l=>l.querySelector('span')?.textContent===" + JsonSerializer.Serialize(rotulo) + ").querySelector('input');el.focus();Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el," + JsonSerializer.Serialize(valor) + ");el.dispatchEvent(new Event('input',{bubbles:true}));})()");
                await Task.Delay(150);
            }
            await Campo("Medicamento", "demonstrativo cadastrado");
            await Program.Esperar(navegador, "document.querySelectorAll('.inf-sugestoes [role=option]').length===1");
            await Program.Script(navegador, "document.querySelector('.inf-sugestoes [role=option]').click()");
            await Program.Esperar(navegador, "document.querySelector('.inf-medicamento input').value==='Medicamento demonstrativo cadastrado'");
            if (vm.Itens.Single().Descricao != "Medicamento demonstrativo cadastrado") throw new InvalidOperationException("A sugestão não chegou ao ViewModel.");
            await Campo("Quantidade / dose", "2 unidades teste");
            if (vm.Itens.Single().Dose != "2 unidades teste") throw new InvalidOperationException("Dose não chegou ao ViewModel.");
            await Program.Esperar(navegador, "document.activeElement===document.querySelector('.inf-item-linha .inf-campo:nth-child(2) input')");
            await Task.Delay(600);
            await Program.Esperar(navegador, "document.activeElement===document.querySelector('.inf-item-linha .inf-campo:nth-child(2) input')");
            Console.WriteLine("OK infusão: digitação preserva foco e sugestão só preenche o medicamento.");

            await Program.Script(navegador, "document.querySelector('.inf-modelos summary').click()");
            await Program.Esperar(navegador, "!!document.querySelector('.inf-modelos-lista button')");
            dialogos.Confirmacao = false;
            await Program.Script(navegador, "document.querySelector('.inf-modelos-lista button').click()");
            await Task.Delay(200);
            if (vm.Itens.Single().Descricao != "Medicamento demonstrativo cadastrado") throw new InvalidOperationException("Cancelar aplicação de modelo alterou o rascunho.");
            dialogos.Confirmacao = true;
            await Program.Script(navegador, "document.querySelector('.inf-modelos-lista button').click()");
            await Program.Esperar(navegador, "document.querySelector('.inf-medicamento input').value==='Medicamento demonstrativo do modelo'");
            if (vm.Itens.Single().Dose != "1 unidade teste" || vm.Infusoes.Single().Diluente != "Diluente demonstrativo") throw new InvalidOperationException("Aplicar modelo não preencheu o preparo/dose.");
            Console.WriteLine("OK infusão: modelo preserva rascunho ao cancelar e aplica preparo/medicamentos ao confirmar.");

            await Program.Script(navegador, "(()=>{const e=document.querySelector('.inf-texto-rico');e.focus();e.textContent='Indicação fictícia';e.dispatchEvent(new Event('input',{bubbles:true}));const range=document.createRange();range.selectNodeContents(e);const s=getSelection();s.removeAllRanges();s.addRange(range);})()");
            await Clicar("Negrito");
            await Task.Delay(150);
            if (vm.Indicacao != "Indicação fictícia" || !TextoFormatado.Ler(vm.Indicacao, vm.IndicacaoFormatada).Any(t => t.Negrito)) throw new InvalidOperationException("Negrito ou texto clínico não atravessou a ponte.");
            await Clicar("Salvar rascunho");
            await Program.Esperar(navegador, "document.querySelector('.inf-aviso')?.textContent.includes('Rascunho salvo')===true");
            db.ChangeTracker.Clear();
            var gravada = await db.PrescricoesInternas.AsNoTracking().Include(p => p.Itens).SingleAsync();
            if (gravada.Situacao != SituacaoPrescricao.Rascunho || gravada.Itens.Single().Descricao != "Medicamento demonstrativo do modelo" || gravada.Indicacao != "Indicação fictícia") throw new InvalidOperationException("Rascunho não persistiu o conteúdo do editor.");
            if (!TextoFormatado.Ler(gravada.Indicacao, gravada.IndicacaoFormatada).Any(t => t.Negrito)) throw new InvalidOperationException("Formatação não foi persistida.");
            var reaberta = new PrescricaoInternaEdicaoViewModel(sp.GetRequiredService<IServiceScopeFactory>(), dialogos, paciente.Id, paciente.Nome, profissional.Id, prescricaoId: gravada.Id);
            await reaberta.Inicializacao;
            if (reaberta.MensagemEhErro || reaberta.Itens.Single().Descricao != gravada.Itens.Single().Descricao) throw new InvalidOperationException("Reabertura do rascunho perdeu conteúdo.");
            Console.WriteLine("OK infusão: editor → adapter → ViewModel → SQLite → reabertura, com texto formatado.");

            await Clicar("Salvar esta infusão como modelo");
            await Campo("Nome do modelo", "Modelo salvo no QA");
            await Clicar("Salvar modelo");
            await Program.Esperar(navegador, "document.querySelector('.inf-aviso')?.textContent.includes('Modelo salvo')===true");
            db.ChangeTracker.Clear();
            if (!await db.Set<ModeloDocumento>().AnyAsync(m => m.Nome == "Modelo salvo no QA")) throw new InvalidOperationException("Salvar modelo não persistiu.");
            await Clicar("Cancelar");

            foreach (var (largura, altura) in new[] { (1180, 760), (900, 600), (650, 650) })
            {
                janela.Width = largura; janela.Height = altura; await Task.Delay(250);
                await Program.Esperar(navegador, "document.documentElement.scrollWidth<=innerWidth+1");
                await Program.Esperar(navegador, "(()=>{const r=document.querySelector('.inf-rodape').getBoundingClientRect();return r.bottom<=innerHeight+1&&r.top>=0})()");
                await Program.Capturar(navegador, Path.Combine(saida, $"infusao-{largura}.png"));
            }
            janela.Width = 1180; janela.Height = 680; await Task.Delay(250);
            await Program.Script(navegador, "document.querySelector('.inf-grupo').scrollIntoView({block:'start'})");
            await Program.Capturar(navegador, Path.Combine(saida, "infusao-preparo.png"));
            Console.WriteLine("OK infusão: 1180/900/650 px sem vazamento horizontal, ações acessíveis no rodapé.");

            var liberada = false;
            reaberta.Fechar = () => liberada = true;
            var adaptadorReaberta = new InfusaoWebAdapter(reaberta, () => { });
            await adaptadorReaberta.ExecutarAsync(JsonSerializer.SerializeToElement(new { acao = "assinar" }));
            db.ChangeTracker.Clear();
            if (!liberada || (await db.PrescricoesInternas.AsNoTracking().SingleAsync()).Situacao != SituacaoPrescricao.Liberada)
                throw new InvalidOperationException("A liberação para enfermagem não persistiu: " + reaberta.Mensagem);
            var copia = new PrescricaoInternaEdicaoViewModel(sp.GetRequiredService<IServiceScopeFactory>(), dialogos, paciente.Id, paciente.Nome, profissional.Id);
            await copia.Inicializacao;
            var adaptadorCopia = new InfusaoWebAdapter(copia, () => { });
            await adaptadorCopia.ExecutarAsync(JsonSerializer.SerializeToElement(new { acao = "copiarUltima" }));
            if (copia.Itens.Single().Descricao != "Medicamento demonstrativo do modelo" || copia.Indicacao != "Indicação fictícia")
                throw new InvalidOperationException("Copiar última prescrição liberada perdeu conteúdo: " + copia.Mensagem);
            if (await db.PrescricoesInternas.CountAsync() != 1) throw new InvalidOperationException("Copiar última gravou prescrição sem salvar.");
            Console.WriteLine("OK infusão: liberação chega ao estado Liberada e copiar última recupera conteúdo sem gravação automática.");

            var snapshot = JsonSerializer.SerializeToElement(adaptador.ObterEstado());
            var grupoId = snapshot.GetProperty("grupos")[0].GetProperty("id").GetString();
            await Rejeitar(adaptador, new { acao = "campo", grupo = grupoId, item = "nao-existe", campo = "dose", valor = "NÃO GRAVAR" });
            await Rejeitar(adaptador, new { acao = "campo", campo = "PacienteId", valor = "999" });
            await Rejeitar(adaptador, new { acao = "campo", grupo = grupoId, campo = "via", valor = "999" });
            var quantidadeAntes = vm.Infusoes.Count;
            await adaptador.ExecutarAsync(JsonSerializer.SerializeToElement(new { acao = "removerInfusao", grupo = grupoId }));
            await Rejeitar(adaptador, new { acao = "adicionarItem", grupo = grupoId });
            if (vm.Infusoes.Count != quantidadeAntes - 1) throw new InvalidOperationException("ID removido foi reutilizado.");
            await Rejeitar(adaptador, new { acao = "assinar" });
            Console.WriteLine("OK infusão: IDs ausentes/obsoletos, via inválida e campos arbitrários rejeitados.");
        }
        finally { janela.Close(); }
        painel.Dispose();
        sp.GetRequiredService<SessaoUsuario>().Entrar(new UsuarioSistema { Id = 99999, Nome = "Enfermagem fictícia", Login = "qa.sem-permissao", Perfil = PerfilAcesso.Enfermagem });
        var negou = false;
        try { await adaptador.ExecutarAsync(JsonSerializer.SerializeToElement(new { acao = "criarInfusao" })); }
        catch (UnauthorizedAccessException) { negou = true; }
        finally { sp.GetRequiredService<SessaoUsuario>().Entrar(usuario); }
        if (!negou) throw new InvalidOperationException("Adapter permitiu edição sem permissão Prescrever.");
        Console.WriteLine("OK infusão: comando indisponível e perfil sem permissão rejeitados pelo adapter.");
    }

    private static async Task Rejeitar(InfusaoWebAdapter adaptador, object comando)
    {
        try { await adaptador.ExecutarAsync(JsonSerializer.SerializeToElement(comando)); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("A ponte aceitou um comando inválido: " + JsonSerializer.Serialize(comando));
    }

    private sealed class Dialogos : IDialogoService
    {
        public bool Confirmacao { get; set; }
        public bool Confirmar(string titulo, string mensagem) => Confirmacao;
        public bool ConfirmarPerigo(string titulo, string mensagem) => Confirmacao;
        public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
        public void Aviso(string titulo, string mensagem) { }
    }
}
