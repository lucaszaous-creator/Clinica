using System.Text.Json;
using System.Windows.Input;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Clinico.Views;

/// <summary>Contrato de apresentação das duas listas. IDs e ações são resolvidos no contexto atual.</summary>
public sealed class PrescricoesWebAdapter
{
    private readonly PrescricoesClinicasViewModel? _documentos;
    private readonly PrescricaoInfusaoViewModel? _infusoes;
    private bool _trocando;
    private string _buscaPublicada = "";
    private int _revisaoBusca;
    public PrescricoesWebAdapter(PrescricoesClinicasViewModel vm) => _documentos = vm;
    public PrescricoesWebAdapter(PrescricaoInfusaoViewModel vm) => _infusoes = vm;
    private SeletorPacienteViewModel Seletor => _documentos?.Seletor ?? _infusoes!.Seletor;
    private int PacienteId => _documentos?.PacienteAtualId ?? _infusoes!.PacienteAtualId;
    private bool Cabecalho => _documentos?.MostrarCabecalho ?? _infusoes!.MostrarCabecalho;
    private bool Carregando => _documentos?.Carregando ?? _infusoes!.Carregando;
    private bool NaoVerificado => _documentos?.NaoVerificado ?? _infusoes!.NaoVerificado;
    private string Contexto => _documentos?.ContextoDaLista ?? _infusoes!.ContextoDaLista;
    private bool Selecionando => Cabecalho && (_trocando || PacienteId == 0);

    public void ExigirAcesso() => SessaoUsuario.Atual.Exigir(
        _infusoes is not null && Cabecalho ? Permissao.Prescrever : Permissao.VerProntuario, "consultar prescrições");

    public object ObterEstado()
    {
        ExigirAcesso();
        // O selo muda também quando a busca substitui resultados: um clique antigo não escolhe outra pessoa.
        var assinatura = $"{Seletor.Termo}|{Seletor.ResultadoAtual}|{Seletor.Buscando}|{Seletor.Erro}|{string.Join(',', Seletor.Resultados.Select(p => p.Id))}";
        if (_buscaPublicada != assinatura) { _buscaPublicada = assinatura; _revisaoBusca++; }
        return new
        {
            modo = _infusoes is null ? "documentos" : "infusoes", contexto = Contexto,
            pacienteId = PacienteId, paciente = _documentos?.Paciente ?? _infusoes!.Paciente,
            mostrarCabecalho = Cabecalho, selecionando = Selecionando,
            carregando = Carregando, naoVerificado = NaoVerificado,
            mensagem = _documentos?.Mensagem ?? _infusoes?.Mensagem,
            mensagemEhErro = _documentos?.MensagemEhErro ?? _infusoes!.MensagemEhErro,
            busca = new { termo = Seletor.Termo ?? "", buscando = Seletor.Buscando || (!Seletor.Ocioso && !Seletor.ResultadoAtual && !Seletor.TemErro), erro = Seletor.Erro,
                ocioso = Seletor.Ocioso, resumo = Seletor.ResumoDaLista, revisao = _revisaoBusca,
                resultados = Selecionando && Seletor.ResultadoAtual ? Seletor.Resultados.Select(p => new { id = p.Id, nome = p.Nome,
                    documento = p.DocumentoFormatado, convenio = p.ConvenioNome,
                    nascimento = p.DataNascimento?.ToString("dd/MM/yyyy") }).ToArray() : [] },
            tipos = _documentos?.FolhasParaEmitir.Select(f => new { chave = f.Chave, rotulo = f.Rotulo, descricao = f.Descricao }).ToArray(),
            podeCriar = PacienteId > 0 && !Carregando && !NaoVerificado && !Selecionando && (_infusoes?.PodePrescrever ?? true),
            temInfusao = _documentos?.TemPrescricaoDeInfusao ?? false,
            linhas = _documentos is not null ? LinhasDocumentos() : LinhasInfusoes()
        };
    }

    private object[] LinhasDocumentos() => _documentos!.Documentos.Select(l => (object)new
    {
        id = l.DocumentoId, titulo = l.Tipo, numero = l.Numero, data = l.Data, descricao = l.Profissional,
        situacao = l.Cancelado ? "Cancelado" : l.Assinado ? "Assinado digitalmente" : "Válido",
        detalheSituacao = l.Cancelado || l.Assinado ? l.Situacao : "",
        grupo = l.Cancelado ? "cancelado" : l.Assinado ? "assinado" : "emitido",
        detalhe = l.Sessao, codigo = l.Codigo, link = l.Link,
        principal = l.PodeAssinar ? "assinar" : "imprimir", acoes = Acoes(l)
    }).ToArray();

    private object[] LinhasInfusoes() => _infusoes!.Prescricoes.Select(l => (object)new
    {
        id = l.PrescricaoId, titulo = "Prescrição de infusão", numero = l.Numero, data = l.Data,
        descricao = l.Resumo, situacao = l.Situacao, grupo = l.GrupoVisual, detalheSituacao = "",
        detalhe = l.Execucao, codigo = l.Codigo, link = "",
        principal = l.PodeEditar ? "editar" : "abrir", acoes = Acoes(l)
    }).ToArray();

    private sealed record Acao(string Chave, string Rotulo, bool Habilitada, bool Perigosa = false);
    private Acao[] Acoes(LinhaDocumentoClinico l) =>
    [new("imprimir", "Imprimir 2ª via", SessaoUsuario.Atual.PodeAlgum(l.Documento.AcessoParaVer)),
     new("assinar", "Assinar com e-CPF", l.PodeAssinar), new("enviar", "Enviar ao paciente", l.PodeEnviar),
     new("renovar", "Renovar link", l.PodeRenovarLink), new("despublicar", "Retirar link do ar", l.PodeTirarDoAr, true),
     new("cancelar", "Cancelar documento", l.PodeCancelar, true)];
    private Acao[] Acoes(LinhaPrescricaoInterna l) =>
    [new("editar", "Continuar rascunho", l.PodeEditar), new("abrir", "Ver prescrição", true),
     new("imprimir", "Imprimir folha", true), new("registro", "Registro histórico", l.ExibirRegistroSeparado && l.TemRegistroExecucao),
     new("cancelar", "Cancelar prescrição", l.PodeCancelar, true)];

    public async Task ExecutarAsync(JsonElement m)
    {
        ExigirAcesso();
        var acao = Texto(m, "acao");
        if (Texto(m, "contexto") != Contexto) throw new InvalidOperationException("O paciente ou a lista mudou. Tente novamente.");
        if (acao == "trocar-paciente")
        {
            ExigirTroca(); _trocando = true; Seletor.Limpar(); Seletor.Termo = ""; return;
        }
        if (acao == "voltar-paciente") { ExigirTroca(); _trocando = false; return; }
        if (acao is "campo" or "listar-pacientes" or "selecionar-paciente")
        {
            ExigirTroca();
            if (!Selecionando) throw new InvalidOperationException("Abra a troca de paciente antes de pesquisar.");
            if (acao == "campo")
            {
                var termo = Texto(m, "valor");
                if (termo.Length > 120) throw new InvalidOperationException("Use um nome ou CPF de até 120 caracteres.");
                Seletor.Termo = termo; return;
            }
            if (acao == "listar-pacientes") { await Seletor.DesligarSugestaoCommand.ExecuteAsync(null); return; }
            if (!Seletor.ResultadoAtual || Seletor.TemErro || !m.TryGetProperty("revisao", out var rev) || rev.GetInt32() != _revisaoBusca
                || Texto(m, "termo") != (Seletor.Termo ?? "")) throw new InvalidOperationException("A busca mudou. Escolha no resultado atual.");
            var paciente = Seletor.Resultados.SingleOrDefault(p => p.Id == Numero(m, "id"))
                ?? throw new InvalidOperationException("Paciente não encontrado na busca atual.");
            Seletor.Limpar(); Seletor.Selecionado = paciente; _trocando = false; return;
        }
        if (acao == "atualizar")
        {
            await Executar(_documentos?.CarregarCommand ?? _infusoes!.CarregarCommand); return;
        }
        if (PacienteId == 0 || Selecionando || Carregando || NaoVerificado)
            throw new InvalidOperationException("Selecione o paciente e aguarde os dados antes de continuar.");
        if (_documentos is { } docs)
        {
            if (acao == "emitir")
            {
                var folha = docs.FolhasParaEmitir.SingleOrDefault(f => f.Chave == Texto(m, "tipo"))
                    ?? throw new InvalidOperationException("Tipo de documento indisponível.");
                SessaoUsuario.Atual.Exigir(folha.PermissaoEmitir, "emitir documento");
                await Executar(docs.EmitirCommand, folha); return;
            }
            if (acao == "ir-infusao" && docs.TemPrescricaoDeInfusao) { await Executar(docs.IrParaInfusaoCommand); return; }
            var linha = docs.Documentos.SingleOrDefault(l => l.DocumentoId == Numero(m, "id") && l.PacienteId == PacienteId)
                ?? throw new InvalidOperationException("Documento fora do paciente atual.");
            ExigirAcao(Acoes(linha), acao);
            ICommand comando = acao switch { "imprimir" => docs.ImprimirCommand, "assinar" => docs.AssinarCommand,
                "enviar" => docs.EnviarCommand, "renovar" => docs.RenovarLinkCommand,
                "despublicar" => docs.TirarDoArCommand, "cancelar" => docs.CancelarCommand,
                _ => throw new InvalidOperationException("Ação indisponível.") };
            await Executar(comando, linha);
        }
        else if (_infusoes is { } inf)
        {
            if (acao is "nova" or "copiar-ultima")
            {
                SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "prescrever");
                await Executar(acao == "nova" ? inf.NovaCommand : inf.CopiarUltimaPrescricaoCommand); return;
            }
            var linha = inf.Prescricoes.SingleOrDefault(l => l.PrescricaoId == Numero(m, "id"))
                ?? throw new InvalidOperationException("Prescrição fora do paciente atual.");
            ExigirAcao(Acoes(linha), acao);
            ICommand comando = acao switch { "editar" => inf.EditarCommand, "abrir" => inf.AbrirCommand,
                "imprimir" => inf.ImprimirCommand, "registro" => inf.ImprimirExecucaoCommand, "cancelar" => inf.CancelarCommand,
                _ => throw new InvalidOperationException("Ação indisponível.") };
            await Executar(comando, linha);
        }
    }
    private void ExigirTroca() { if (!Cabecalho || !Seletor.Editavel) throw new InvalidOperationException("Troque o paciente pelo cabeçalho da ficha aberta."); }
    private static void ExigirAcao(Acao[] acoes, string chave)
    { if (!acoes.Any(a => a.Chave == chave && a.Habilitada)) throw new InvalidOperationException("Esta ação não está disponível para este documento e acesso."); }
    private static async Task Executar(ICommand comando, object? parametro = null)
    {
        if (!comando.CanExecute(parametro)) throw new InvalidOperationException("Ação indisponível neste momento.");
        if (comando is IAsyncRelayCommand asyncCommand) await asyncCommand.ExecuteAsync(parametro); else comando.Execute(parametro);
    }
    private static string Texto(JsonElement m, string chave) => m.TryGetProperty(chave, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString()! : "";
    private static int Numero(JsonElement m, string chave) => m.TryGetProperty(chave, out var valor) && valor.TryGetInt32(out var id) ? id : 0;
}
