using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

static partial class Program
{
    // dotnet run --project tools/ValidarAtendimentoDesktop -- <pasta-evidencias> --copia
    // Usa exclusivamente o SQLite em memória criado em Run; nenhuma conexão clínica real.
    static async Task ValidarCopia(ServiceProvider sp, ClinicaDbContext db,
        Paciente paciente, Agendamento agendamento)
    {
        var registro=await db.Evolucoes.SingleAsync(e=>e.PacienteId==paciente.Id);
        registro.QueixaPrincipal="Queixa fictícia para cópia\nSegunda linha da queixa.";
        registro.HistoriaDoencaAtual="História fictícia completa\nInformação que não aparece no resumo.";
        registro.ExameFisico="Exame fictício\nAchado de demonstração.";
        registro.HipoteseDiagnostica="Hipótese de demonstração";
        registro.CidSessao="M54.5";
        registro.Conduta="Conduta fictícia\nSegunda linha da conduta.";
        registro.TextoEvolucao="Evolução fictícia com acentuação\nTrecho selecionável de demonstração.\n\n"
            +string.Join("\n",Enumerable.Range(1,36).Select(i=>$"Linha {i:00}: conteúdo fictício da sessão anterior."))
            +"\nFIM DO REGISTRO LONGO — não truncar.";
        registro.PlanoTerapeutico="Plano fictício\nReavaliar resposta.";
        registro.Orientacoes="Orientações fictícias\nTexto preservado na cópia.";
        registro.RetornoSugeridoEm=new DateOnly(2026,10,16);
        registro.RetornoSugeridoNota="Nota fictícia do retorno";
        registro.Encaminhamento="Encaminhamento fictício\nÚltimo campo clínico.";
        const string campoExtra="Campo personalizado fictício\nConteúdo adicional preservado.";
        registro.CamposPersonalizados.Add(new ValorCampoPersonalizado{
            Campo=new CampoPersonalizadoProntuario{Rotulo="Observação adicional",Tipo=TipoCampoPersonalizado.TextoLongo,Ativo=false},
            Rotulo="Observação adicional",Tipo=TipoCampoPersonalizado.TextoLongo,Valor=campoExtra});
        await db.SaveChangesAsync();
        var campos=new[]{registro.QueixaPrincipal,registro.HistoriaDoencaAtual,registro.ExameFisico,
            registro.HipoteseDiagnostica,registro.CidSessao,registro.Conduta,registro.TextoEvolucao,
            registro.PlanoTerapeutico,registro.Orientacoes,registro.RetornoSugeridoNota,registro.Encaminhamento,campoExtra};
        // Compara duas leituras persistidas, incluindo a normalização de DateTime pelo SQLite.
        var antes=SnapshotRegistro(db,await db.Evolucoes.AsNoTracking().SingleAsync(e=>e.Id==registro.Id));
        int quantidadeAntes=await db.Evolucoes.CountAsync();
        var evidencias=new List<object>();
        var foco=sp.GetRequiredService<PacienteEmFoco>();
        var workspace=new PacienteWorkspaceViewModel(sp,foco,ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento));
        await workspace.Atendimento.CarregarAsync();
        const string rascunho="Rascunho atual — manter integralmente.\nSegunda linha que ainda não foi salva.";
        workspace.Atendimento.TextoEvolucao=rascunho;
        workspace.Atendimento.HistoricoConsulta.Aberto=true;
        await workspace.Atendimento.HistoricoConsulta.RecarregarAsync();

        // Preserva o clipboard do usuário sem imprimi-lo ou gravá-lo nos artefatos.
        var clipboardAnterior=ComClipboard(CapturarClipboard);
        var janelas=new List<Window>();
        Exception? falhaOriginal=null;
        try
        {
            var view=new PacienteWorkspaceView{DataContext=workspace};
            var janela=JanelaDeCopia(view,1366,820);janelas.Add(janela);
            System.Windows.Application.Current.MainWindow=janela;
            janela.Show();await EstabilizarCopia(janela);
            var historico=Visuals(view).OfType<HistoricoConsultaView>().Single();
            var item=workspace.Atendimento.HistoricoConsulta.Itens.Single(r=>r.Natureza==NaturezaRegistroClinico.SessaoMedica);
            var copiar=BotaoDeCopia(historico,item);
            ValidarConteudoCompleto(await CopiarPorBotao(copiar,false),campos);
            ValidarConteudoCompleto(await CopiarPorBotao(copiar,true),campos);
            SelecionarECopiar(historico,"Trecho selecionável de demonstração.");
            Check(workspace.Atendimento.TextoEvolucao==rascunho,"Copiar no histórico alterou o rascunho.");
            CapturarCopia(janela,"copia-historico-atendimento.png");
            evidencias.Add(new{cenario="historico_atendimento",clique=true,automacaoAcessibilidade=true,
                todosCampos=true,multilinha=true,semTruncamento=true,selecao=true,somenteLeitura=true,rascunhoPreservado=true});

            var prontuario=new ProntuarioClinicoViewModel(sp.GetRequiredService<IServiceScopeFactory>(),foco);
            await prontuario.CarregarAsync();
            var registrosView=new ProntuarioClinicoView{DataContext=prontuario};
            var registrosJanela=JanelaDeCopia(registrosView,1100,760);janelas.Add(registrosJanela);
            registrosJanela.Show();await EstabilizarCopia(registrosJanela);
            var linha=prontuario.Sessoes.Single(s=>s.EvolucaoId==registro.Id);
            ValidarConteudoCompleto(await CopiarPorBotao(BotaoDeCopia(registrosView,linha),true),campos);
            SelecionarECopiar(registrosView,"Trecho selecionável de demonstração.");
            CapturarCopia(registrosJanela,"copia-lista-registros.png");
            evidencias.Add(new{cenario="lista_registros",todosCampos=true,selecao=true,somenteLeitura=true});

            var sessaoVm=new SessaoDoProntuarioViewModel(sp.GetRequiredService<IServiceScopeFactory>(),registro.Id,paciente.Nome,false);
            await AguardarCopia(()=>!sessaoVm.Carregando,"A sessão completa não terminou de carregar.");
            Check(!sessaoVm.NaoVerificado,"A sessão completa falhou ao carregar: "+sessaoVm.Mensagem);
            var sessaoJanela=new SessaoDoProntuarioWindow(sessaoVm){ShowActivated=false,ShowInTaskbar=false,
                WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
            janelas.Add(sessaoJanela);sessaoJanela.Show();await EstabilizarCopia(sessaoJanela);
            ValidarConteudoCompleto(await CopiarPorBotao(BotaoDeCopia(sessaoJanela,sessaoVm),true),campos);
            SelecionarECopiar(sessaoJanela,"Trecho selecionável de demonstração.");
            CapturarCopia(sessaoJanela,"copia-sessao-completa.png");
            evidencias.Add(new{cenario="sessao_completa",todosCampos=true,selecao=true,somenteLeitura=true});
            sessaoJanela.Close();registrosJanela.Close();

            workspace.Atendimento.HistoricoConsulta.FecharCommand.Execute(null);
            await EstabilizarCopia(janela);
            var editor=Visuals(view).OfType<TextBox>().Single(b=>b.Name=="EditorEvolucao");
            Check(NormalizarCopia(editor.Text)==rascunho && workspace.Atendimento.TextoEvolucao==rascunho,
                "Voltar da consulta ao histórico perdeu o rascunho atual.");
            Check(workspace.Atendimento.EvolucaoId==0,"A cópia salvou uma sessão sem solicitação.");
            var depois=await db.Evolucoes.AsNoTracking().SingleAsync(e=>e.Id==registro.Id);
            Check(SnapshotRegistro(db,depois)==antes && await db.Evolucoes.CountAsync()==quantidadeAntes,
                "Copiar ou selecionar alterou um registro ou criou uma sessão.");
            Check(await db.Set<ValorCampoPersonalizado>().AsNoTracking().Where(c=>c.EvolucaoId==registro.Id)
                .Select(c=>c.Valor).SingleAsync()==campoExtra,"A cópia alterou o campo personalizado gravado.");
            Check(agendamento.Id==foco.AgendamentoId,"A consulta ao histórico alterou o atendimento em foco.");
            evidencias.Add(new{cenario="preservacao",rascunhoPreservado=true,registroAnteriorIntacto=true,semGravacaoAutomatica=true});
            File.WriteAllText(Path.Combine(output,"cenarios-copia.json"),JsonSerializer.Serialize(evidencias,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS: cópia integral e de trechos no histórico, registros e sessão; campos completos e multilinha; leitura somente; rascunho e banco preservados.");
        }
        catch(Exception ex)
        {
            falhaOriginal=ex;
            throw;
        }
        finally
        {
            try
            {
                ComClipboard(()=>{
                    if(clipboardAnterior is null) Clipboard.Clear();
                    else Clipboard.SetDataObject(clipboardAnterior,true);
                });
            }
            catch(Exception) when(falhaOriginal is not null)
            {
                // O erro do cenário é o diagnóstico principal; não expõe dados do clipboard.
                Console.Error.WriteLine("Aviso: não foi possível restaurar o clipboard após a falha do cenário.");
            }
            finally
            {
                foreach(var janela in janelas.Where(w=>w.IsLoaded).Reverse()) janela.Close();
            }
        }
    }

    static string SnapshotRegistro(ClinicaDbContext db,Evolucao registro)
        =>JsonSerializer.Serialize(db.Entry(registro).CurrentValues.Properties
            .ToDictionary(p=>p.Name,p=>db.Entry(registro).CurrentValues[p]));

    static Window JanelaDeCopia(FrameworkElement view,double largura,double altura)
        =>new(){Content=view,Width=largura,Height=altura,ShowActivated=false,ShowInTaskbar=false,
            WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};

    static async Task EstabilizarCopia(Window janela)
    {
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        janela.UpdateLayout();
        await Task.Delay(100);
        janela.UpdateLayout();
    }

    static Button BotaoDeCopia(DependencyObject raiz,object contexto)
    {
        var botao=Visuals(raiz).OfType<Button>().Single(b=>ReferenceEquals(b.DataContext,contexto)
            && (b.Content?.ToString()?.StartsWith("Copiar registro",StringComparison.Ordinal)==true));
        Check(botao.IsVisible && botao.IsEnabled && botao.Focusable,"Botão de cópia não está acessível.");
        Check(!string.IsNullOrWhiteSpace(Copiavel.GetTexto(botao)),"Botão de cópia sem o texto vinculado.");
        return botao;
    }

    static async Task<string> CopiarPorBotao(Button botao,bool automacao)
    {
        ComClipboard(()=>Clipboard.SetText("MARCADOR ANTES DA CÓPIA FICTÍCIA"));
        if(automacao)
        {
            // Invoke percorre OnClick, como teclado/acessibilidade, sem movimentar o foco do desktop.
            var peer=new ButtonAutomationPeer(botao);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        }
        else botao.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await AguardarCopia(()=>ComClipboard(()=>Clipboard.ContainsText() && Clipboard.GetText()!="MARCADOR ANTES DA CÓPIA FICTÍCIA"),
            "A ação do botão não copiou o registro.");
        return ComClipboard(()=>Clipboard.GetText());
    }

    static void ValidarConteudoCompleto(string copiado,IEnumerable<string?> campos)
    {
        var texto=NormalizarCopia(copiado);
        foreach(var campo in campos) Check(texto.Contains(NormalizarCopia(campo!)),"Cópia integral omitiu ou truncou campo clínico fictício.");
        Check(texto.Contains("25/09/2026") && texto.Contains("Profissional de demonstração")
            && texto.Contains("16/10/2026") && texto.Contains("EVA"),"Cópia perdeu data, autoria, retorno ou escala de dor.");
    }

    static void SelecionarECopiar(DependencyObject raiz,string trecho)
    {
        var texto=Visuals(raiz).OfType<TextBox>().FirstOrDefault(t=>t.Text.Contains(trecho));
        Check(texto is not null,"O texto do registro não oferece seleção de trechos.");
        Check(texto!.IsReadOnly && texto.IsEnabled && texto.Focusable,"Texto anterior não é legível/selecionável em modo somente leitura.");
        var antes=texto.Text;
        texto.Select(texto.Text.IndexOf(trecho,StringComparison.Ordinal),trecho.Length);
        Check(ApplicationCommands.Copy.CanExecute(null,texto),"Ctrl+C indisponível para o trecho selecionado.");
        ComClipboard(()=>ApplicationCommands.Copy.Execute(null,texto));
        Check(ComClipboard(()=>Clipboard.GetText())==trecho,"A seleção não copiou somente o trecho escolhido.");
        Check(!ApplicationCommands.Cut.CanExecute(null,texto) && !ApplicationCommands.Paste.CanExecute(null,texto),
            "O registro anterior permite recortar ou colar e editar o conteúdo.");
        Check(texto.Text==antes,"A cópia de trecho alterou o registro anterior.");
        texto.Select(0,0);
    }

    static async Task AguardarCopia(Func<bool> terminou,string mensagem)
    {
        var limite=DateTime.UtcNow.AddSeconds(5);
        while(!terminou() && DateTime.UtcNow<limite) await Task.Delay(20);
        Check(terminou(),mensagem);
    }

    static string NormalizarCopia(string texto)=>texto.Replace("\r\n","\n");

    static T ComClipboard<T>(Func<T> acao)
    {
        for(int tentativa=0;;tentativa++)
        {
            try { return acao(); }
            catch(ExternalException) when(tentativa<7) { Thread.Sleep(75); }
        }
    }

    static void ComClipboard(Action acao)=>ComClipboard(()=>{acao();return true;});

    static DataObject? CapturarClipboard()
    {
        var origem=Clipboard.GetDataObject();
        if(origem is null) return null;
        var copia=new DataObject();
        // Materializa os formatos ANTES de substituir o clipboard. O IDataObject original
        // pode depender do proprietário anterior e deixar de funcionar após SetText.
        foreach(var formato in origem.GetFormats(autoConvert:false))
        {
            var dado=origem.GetData(formato,autoConvert:false);
            if(dado is Stream fluxo)
            {
                long posicao=fluxo.CanSeek ? fluxo.Position : 0;
                if(fluxo.CanSeek) fluxo.Position=0;
                var memoria=new MemoryStream();fluxo.CopyTo(memoria);memoria.Position=0;
                if(fluxo.CanSeek) fluxo.Position=posicao;
                dado=memoria;
            }
            else if(dado is BitmapSource imagem) dado=imagem.CloneCurrentValue();
            else if(dado is Array vetor) dado=vetor.Clone();
            if(dado is not null) copia.SetData(formato,dado,autoConvert:false);
        }
        return copia;
    }

    static void CapturarCopia(Window janela,string nome)
    {
        janela.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)janela.ActualWidth,(int)janela.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(janela);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var arquivo=File.Create(Path.Combine(output,nome));encoder.Save(arquivo);
    }
}
