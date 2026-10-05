using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

static partial class Program
{
    static EditorTextoClinico EditorDaEvolucao(DependencyObject view)
        =>Visuals(view).OfType<EditorTextoClinico>().Single(e=>e.Name=="EditorEvolucao");

    static RichTextBox CampoDoEditor(EditorTextoClinico editor)
        =>Visuals(editor).OfType<RichTextBox>().Single();

    static string TextoDoRich(RichTextBox campo)
        =>TextoFormatado.Linhas(new TextRange(campo.Document.ContentStart,campo.Document.ContentEnd).Text).TrimEnd('\n');

    static void SelecionarTrechoRich(RichTextBox campo,string trecho)
    {
        for(var cursor=campo.Document.ContentStart;cursor is not null;cursor=cursor.GetNextContextPosition(LogicalDirection.Forward))
        {
            if(cursor.GetPointerContext(LogicalDirection.Forward)!=TextPointerContext.Text) continue;
            var texto=cursor.GetTextInRun(LogicalDirection.Forward);
            int inicio=texto.IndexOf(trecho,StringComparison.Ordinal);
            if(inicio<0) continue;
            var de=cursor.GetPositionAtOffset(inicio)!;
            campo.Selection.Select(de,de.GetPositionAtOffset(trecho.Length)!);
            return;
        }
        throw new Exception("Trecho fictício não encontrado no editor: "+trecho);
    }

    static Button BotaoDeEstilo(EditorTextoClinico editor,string nome)
        =>Visuals(editor).OfType<Button>().Single(b=>AutomationProperties.GetName(b)==nome);

    static async Task AplicarPeloBotao(EditorTextoClinico editor,string nome)
    {
        editor.FocarTexto();CommandManager.InvalidateRequerySuggested();
        await EstabilizarCopia(Window.GetWindow(editor));
        var botao=BotaoDeEstilo(editor,nome);
        Check(botao.IsVisible && botao.IsEnabled,$"Ação de estilo inacessível: {nome}; visível={botao.IsVisible}; habilitado={botao.IsEnabled}; editor={editor.IsEnabled}; leitura={CampoDoEditor(editor).IsReadOnly}.");
        var peer=new ButtonAutomationPeer(botao);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        await EstabilizarCopia(Window.GetWindow(editor));
    }

    static async Task ValidarFormatacao(ServiceProvider sp,ClinicaDbContext db,UsuarioSistema usuario,Profissional profissional)
    {
        var foco=sp.GetRequiredService<PacienteEmFoco>();
        var focoAnterior=(foco.PacienteId,foco.Nome,foco.AgendamentoId,foco.AtendimentoId,foco.DataDoHorario);
        var perfilAnterior=usuario.Perfil;
        var clipboardAnterior=ComClipboard(CapturarClipboard);
        Exception? falhaOriginal=null;
        var janelas=new List<Window>();
        const string texto="Evolução fictícia com acentuação\nConduta e orientação.\n\nÚltima linha preservada.";
        try
        {
            var paciente=new Paciente{Nome="Paciente fictício da formatação",DataNascimento=new DateOnly(1985,2,14)};
            db.Add(paciente);await db.SaveChangesAsync();
            var agendamento=new Agendamento{PacienteId=paciente.Id,ProfissionalId=profissional.Id,
                DataHora=new DateTime(2026,10,5,14,0,0),Status=StatusAgendamento.Agendado,
                ModalidadePrevista=ModalidadeAtendimento.AcupunturaComEletro};
            db.Add(agendamento);await db.SaveChangesAsync();
            foco.Definir(paciente.Id,paciente.Nome,agendamento.Id,null,DateOnly.FromDateTime(agendamento.DataHora));
            usuario.Perfil=PerfilAcesso.Profissional;sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            var vm=new PacienteWorkspaceViewModel(sp,foco,ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento));
            await vm.Atendimento.CarregarAsync();
            vm.Atendimento.TextoEvolucao=texto;vm.Atendimento.EvaAntes=6;vm.Atendimento.EvaDepois=3;
            var view=new PacienteWorkspaceView{DataContext=vm};
            var janela=JanelaDeCopia(view,1366,820);janelas.Add(janela);
            System.Windows.Application.Current.MainWindow=janela;janela.Show();await EstabilizarCopia(janela);
            var editor=EditorDaEvolucao(view);var campo=CampoDoEditor(editor);
            editor.FocarTexto();await EstabilizarCopia(janela);
            Check(TextoDoRich(campo)==texto,"Carga inicial do editor mudou quebras de linha ou acentuação.");
            campo.SelectAll();ComClipboard(()=>Clipboard.SetText(texto));
            ComClipboard(()=>ApplicationCommands.Paste.Execute(null,campo));await EstabilizarCopia(janela);
            Check(TextoDoRich(campo)==texto && editor.Texto==texto,"Colar parágrafos e linha vazia alterou o texto clínico.");
            SelecionarTrechoRich(campo,"Evolução fictícia");
            await AplicarPeloBotao(editor,"Negrito");
            Check(Equals(campo.Selection.GetPropertyValue(TextElement.FontWeightProperty),FontWeights.Bold),"Botão Negrito não formatou a seleção.");
            SelecionarTrechoRich(campo,"Conduta e orientação.");
            // Executa o comando associado ao gesto do teclado sem enviar teclas à janela real do usuário.
            var atalho=campo.InputBindings.OfType<KeyBinding>().Concat(editor.InputBindings.OfType<KeyBinding>())
                .Single(b=>b.Key==Key.I && b.Modifiers==ModifierKeys.Control);
            Check(atalho.Command==EditingCommands.ToggleItalic,"Ctrl+I não está associado ao itálico.");
            EditingCommands.ToggleItalic.Execute(null,campo);await EstabilizarCopia(janela);
            Check(Equals(campo.Selection.GetPropertyValue(TextElement.FontStyleProperty),FontStyles.Italic),"Comando do atalho Ctrl+I não formatou a seleção.");
            Check(TextoDoRich(campo)==texto && vm.Atendimento.TextoEvolucao==texto,"Aplicar estilos mudou texto clínico.");
            ValidarEstilosPersistidos(texto,vm.Atendimento.TextoEvolucaoFormatado);
            CapturarCopia(janela,"evolucao-negrito-italico.png");
            Check(await vm.Atendimento.TentarSalvarAsync(),"Não salvou evolução formatada: "+vm.Atendimento.Mensagem);
            int id=vm.Atendimento.EvolucaoId;
            var salvo=await db.Evolucoes.AsNoTracking().SingleAsync(e=>e.Id==id);
            Check(salvo.TextoEvolucao==texto,"Gravação alterou texto clínico.");
            ValidarEstilosPersistidos(salvo.TextoEvolucao,salvo.TextoEvolucaoFormatado);

            var reaberto=new PacienteWorkspaceViewModel(sp,foco,ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento));
            await reaberto.Atendimento.CarregarAsync();
            var novaView=new PacienteWorkspaceView{DataContext=reaberto};janela.Content=novaView;await EstabilizarCopia(janela);
            editor=EditorDaEvolucao(novaView);campo=CampoDoEditor(editor);
            Check(TextoDoRich(campo)==texto,"Reabertura mudou texto multilinha.");
            SelecionarTrechoRich(campo,"Evolução fictícia");
            Check(Equals(campo.Selection.GetPropertyValue(TextElement.FontWeightProperty),FontWeights.Bold),"Reabertura perdeu negrito.");
            SelecionarTrechoRich(campo,"Conduta e orientação.");
            Check(Equals(campo.Selection.GetPropertyValue(TextElement.FontStyleProperty),FontStyles.Italic),"Reabertura perdeu itálico.");
            ComClipboard(()=>ApplicationCommands.Copy.Execute(null,campo));
            Check(ComClipboard(()=>Clipboard.GetText())=="Conduta e orientação.","Ctrl+C não preservou texto selecionado formatado.");

            reaberto.Atendimento.HistoricoConsulta.Aberto=true;await reaberto.Atendimento.HistoricoConsulta.RecarregarAsync();await EstabilizarCopia(janela);
            var historico=Visuals(novaView).OfType<HistoricoConsultaView>().Single();
            ValidarLeitorFormatado(historico);
            var item=reaberto.Atendimento.HistoricoConsulta.Itens.Single(r=>r.Id==id && r.Natureza==Clinica.Domain.Prontuario.NaturezaRegistroClinico.SessaoMedica);
            Check((await CopiarPorBotao(BotaoDeCopia(historico,item),true)).Contains(texto),"Cópia do histórico perdeu o texto formatado.");
            reaberto.Atendimento.HistoricoConsulta.FecharCommand.Execute(null);await EstabilizarCopia(janela);

            var prontuario=new ProntuarioClinicoViewModel(sp.GetRequiredService<IServiceScopeFactory>(),foco);await prontuario.CarregarAsync();
            var lista=new ProntuarioClinicoView{DataContext=prontuario};var janelaLista=JanelaDeCopia(lista,1100,760);
            janelas.Add(janelaLista);janelaLista.Show();await EstabilizarCopia(janelaLista);ValidarLeitorFormatado(lista);
            SelecionarECopiar(lista,"Conduta e orientação.");janelaLista.Close();

            var leituraVm=new SessaoDoProntuarioViewModel(sp.GetRequiredService<IServiceScopeFactory>(),id,paciente.Nome,false);
            await AguardarCopia(()=>!leituraVm.Carregando,"Sessão formatada não terminou de carregar.");
            var leitura=new SessaoDoProntuarioWindow(leituraVm){ShowActivated=false,ShowInTaskbar=false,
                WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
            janelas.Add(leitura);leitura.Show();await EstabilizarCopia(leitura);
            ValidarLeitorFormatado(leitura);
            SelecionarECopiar(leitura,"Evolução fictícia");
            CapturarCopia(leitura,"sessao-negrito-italico-somente-leitura.png");leitura.Close();

            SelecionarTrechoRich(campo,"Evolução fictícia");await AplicarPeloBotao(editor,"Negrito");
            SelecionarTrechoRich(campo,"Conduta e orientação.");await AplicarPeloBotao(editor,"Itálico");
            Check(TextoFormatado.Ler(editor.Texto,editor.Formato).All(t=>!t.Negrito&&!t.Italico),"Desativar os botões não removeu os estilos.");
            Check(await reaberto.Atendimento.TentarSalvarAsync(),"Não salvou remoção dos estilos.");
            var semEstilo=await db.Evolucoes.AsNoTracking().SingleAsync(e=>e.Id==id);
            Check(semEstilo.TextoEvolucao==texto && string.IsNullOrEmpty(semEstilo.TextoEvolucaoFormatado),"Remover estilos apagou texto ou manteve metadata anterior.");

            usuario.Perfil=PerfilAcesso.Enfermagem;sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            var restrito=new PacienteWorkspaceViewModel(sp,foco,ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento));
            await restrito.Atendimento.CarregarAsync();
            var viewRestrita=new AtendimentoView{DataContext=restrito.Atendimento};janela.Content=viewRestrita;await EstabilizarCopia(janela);
            var editorRestrito=EditorDaEvolucao(viewRestrita);var campoRestrito=CampoDoEditor(editorRestrito);
            Check(!restrito.Atendimento.PodeEditarProntuario && (campoRestrito.IsReadOnly||!campoRestrito.IsEnabled),"Perfil sem permissão consegue escrever a evolução.");
            Check(!BotaoDeEstilo(editorRestrito,"Negrito").IsEnabled && !BotaoDeEstilo(editorRestrito,"Itálico").IsEnabled,
                "Perfil sem permissão consegue formatar a evolução.");
            File.WriteAllText(Path.Combine(output,"cenarios-formatacao.json"),JsonSerializer.Serialize(new{
                botaoNegrito=true,comandoAtalhoItalico=true,salvarReabrir=true,textoMultilinhaEAcentos=true,
                copiarTrecho=true,sessaoSomenteLeitura=true,removerEstilos=true,permissoes=true},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS: negrito/itálico no editor, comando do atalho, salvar/reabrir, texto multilinha, cópia, remoção e permissões.");
        }
        catch(Exception ex){falhaOriginal=ex;throw;}
        finally
        {
            try{ComClipboard(()=>{if(clipboardAnterior is null)Clipboard.Clear();else Clipboard.SetDataObject(clipboardAnterior,true);});}
            catch(Exception)when(falhaOriginal is not null){Console.Error.WriteLine("Aviso: não foi possível restaurar o clipboard após a falha do cenário.");}
            finally
            {
                foreach(var janela in janelas.Where(w=>w.IsLoaded).Reverse())janela.Close();
                usuario.Perfil=perfilAnterior;sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
                if(focoAnterior.PacienteId is {} anterior)foco.Definir(anterior,focoAnterior.Nome,focoAnterior.AgendamentoId,focoAnterior.AtendimentoId,focoAnterior.DataDoHorario);
                else foco.Limpar();
            }
        }
    }

    static void ValidarEstilosPersistidos(string? texto,string? formato)
    {
        var trechos=TextoFormatado.Ler(texto,formato);
        Check(trechos.Any(t=>t.Negrito && t.Texto.Contains("Evolução fictícia")),"Metadata não contém seleção em negrito.");
        Check(trechos.Any(t=>t.Italico && t.Texto.Contains("Conduta e orientação.")),"Metadata não contém seleção em itálico.");
        Check(string.Concat(trechos.Select(t=>t.Texto))==texto,"Metadata mudou palavras ou quebras do texto clínico.");
    }

    static void ValidarLeitorFormatado(DependencyObject raiz)
    {
        var campo=Visuals(raiz).OfType<RichTextBox>().Single(r=>TextoDoRich(r).Contains("Evolução fictícia"));
        Check(campo.IsReadOnly,"O leitor do registro anterior permite editar.");
        SelecionarTrechoRich(campo,"Evolução fictícia");
        Check(Equals(campo.Selection.GetPropertyValue(TextElement.FontWeightProperty),FontWeights.Bold),"O leitor perdeu o negrito da evolução.");
        SelecionarTrechoRich(campo,"Conduta e orientação.");
        Check(Equals(campo.Selection.GetPropertyValue(TextElement.FontStyleProperty),FontStyles.Italic),"O leitor perdeu o itálico da evolução.");
    }
}
