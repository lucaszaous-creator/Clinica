using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Clinico.WebInfusao;

/// <summary>Contrato fechado de apresentação. Toda gravação continua no ViewModel e nos serviços clínicos.</summary>
public sealed class InfusaoWebAdapter(PrescricaoInternaEdicaoViewModel vm, Action fechar)
{
    private readonly Dictionary<object, string> _ids = new(ReferenceEqualityComparer.Instance);
    private string Id(object objeto)
    {
        if (!_ids.TryGetValue(objeto, out var id)) _ids[objeto] = id = Guid.NewGuid().ToString("N");
        return id;
    }

    public static void ExigirAcesso()
    {
        if (!SessaoUsuario.Atual.Autenticado || !SessaoUsuario.Atual.Pode(Permissao.Prescrever))
            throw new UnauthorizedAccessException("Seu acesso não permite editar prescrições de infusão.");
    }

    public object ObterEstado() => new
    {
        paciente = vm.Paciente, numero = vm.Numero, indicacao = vm.Indicacao ?? "",
        indicacaoFormatada = vm.IndicacaoFormatada, observacoes = vm.Observacoes ?? "",
        observacoesFormatadas = vm.ObservacoesFormatadas,
        data = vm.DataPrescricao?.ToString("yyyy-MM-dd") ?? "", hora = vm.HoraPrescricao,
        ocupado = vm.Ocupado || !vm.Inicializacao.IsCompleted,
        podeEditar = vm.Inicializacao.IsCompletedSuccessfully && vm.PodeCopiarUltimaPrescricao,
        podeAssinar = vm.Inicializacao.IsCompletedSuccessfully && vm.PodeCopiarUltimaPrescricao && vm.PodeAssinar,
        operacao = vm.TextoOperacao, mensagem = vm.Mensagem, erro = vm.MensagemEhErro,
        liberacao = vm.RotuloLiberacao, alertas = vm.Alertas.ToArray(),
        vias = vm.Vias.Select(v => new { valor = v.ToString(), rotulo = RotuloVia(v) }).ToArray(),
        medicamentos = vm.CatalogoMedicamentos.Select(m => new { codigo = m.Codigo, nome = m.Nome, apresentacao = m.Apresentacao }).ToArray(),
        modelos = vm.Modelos.Select(m => new { id = m.Id, nome = m.Nome, previa = m.Corpo ?? "" }).ToArray(),
        grupos = vm.Infusoes.Select(g => new
        {
            id = Id(g), titulo = g.Titulo, diluente = g.Diluente ?? "", volume = g.Volume ?? "",
            via = g.Via.ToString(), tempo = g.Tempo ?? "", horario = g.Horario ?? "",
            itens = g.Itens.Select(i => new
            {
                id = Id(i), descricao = i.Descricao, dose = i.Dose ?? "", dicaDose = i.DicaDose,
                observacoes = i.Observacoes ?? "", observacoesFormatadas = i.ObservacoesFormatadas,
                seNecessario = i.SeNecessario
            }).ToArray()
        }).ToArray()
    };

    private static string RotuloVia(ViaAdministracao via) => via switch
    {
        ViaAdministracao.Endovenosa => "Endovenosa",
        ViaAdministracao.Subcutanea => "Subcutânea",
        ViaAdministracao.Intradermica => "Intradérmica",
        ViaAdministracao.Inalatoria => "Inalatória",
        ViaAdministracao.Topica => "Tópica",
        _ => via.ToString()
    };

    private static string Texto(JsonElement mensagem, string chave, int maximo = 100_000)
    {
        if (!mensagem.TryGetProperty(chave, out var valor) || valor.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Informe {chave}.");
        var texto = valor.GetString() ?? "";
        if (texto.Length > maximo) throw new InvalidOperationException($"O campo {chave} excedeu o limite de texto.");
        return texto;
    }

    private GrupoInfusaoEdicao Grupo(JsonElement mensagem)
    {
        var id = Texto(mensagem, "grupo", 32);
        return vm.Infusoes.FirstOrDefault(g => Id(g) == id)
            ?? throw new InvalidOperationException("Esta infusão não está mais no rascunho. Confira a tela atual.");
    }

    private LinhaItemPrescricao Item(JsonElement mensagem)
    {
        var grupo = Grupo(mensagem);
        var id = Texto(mensagem, "item", 32);
        return grupo.Itens.FirstOrDefault(i => Id(i) == id)
            ?? throw new InvalidOperationException("Este medicamento não está mais nesta infusão.");
    }

    public async Task ExecutarAsync(JsonElement mensagem)
    {
        var acao = Texto(mensagem, "acao", 50);
        if (acao == "fechar") { if (!vm.Ocupado) fechar(); return; }
        ExigirAcesso();
        if (!vm.Inicializacao.IsCompletedSuccessfully || !vm.PodeCopiarUltimaPrescricao || vm.Ocupado)
            throw new InvalidOperationException("Aguarde a prescrição carregar antes de editar.");
        switch (acao)
        {
            case "campo": AlterarCampo(mensagem); break;
            case "criarInfusao": Comando(vm.CriarInfusaoCommand); break;
            case "removerInfusao": Comando(vm.RemoverInfusaoCommand, Grupo(mensagem)); break;
            case "adicionarItem": Comando(vm.AcrescentarItemCommand, Grupo(mensagem)); break;
            case "removerItem": Comando(vm.RemoverItemCommand, Item(mensagem)); break;
            case "copiarUltima": await ComandoAsync(vm.CopiarUltimaPrescricaoCommand); break;
            case "salvar": await ComandoAsync(vm.SalvarRascunhoCommand); break;
            case "assinar":
                if (!vm.PodeAssinar) throw new InvalidOperationException("Acrescente medicamentos e confira a prescrição antes de liberar.");
                await ComandoAsync(vm.AssinarCommand); break;
            case "recarregarMedicamentos": await ComandoAsync(vm.RecarregarMedicamentosCommand); break;
            case "aplicarModelo":
                var grupo = Grupo(mensagem);
                if (!mensagem.TryGetProperty("modelo", out var id) || !id.TryGetInt32(out var modeloId))
                    throw new InvalidOperationException("Escolha um modelo da lista.");
                var modelo = vm.Modelos.FirstOrDefault(m => m.Id == modeloId)
                    ?? throw new InvalidOperationException("Este modelo não está disponível.");
                vm.AplicarModelo(grupo, modelo);
                break;
            case "salvarModelo": await vm.SalvarModeloAsync(Grupo(mensagem), Texto(mensagem, "nome", 100).Trim()); break;
            default: throw new InvalidOperationException("Ação não disponível nesta prescrição.");
        }
    }

    private static void Comando(ICommand comando, object? parametro = null)
    {
        if (!comando.CanExecute(parametro)) throw new InvalidOperationException("Ação indisponível neste momento. Aguarde e tente novamente.");
        comando.Execute(parametro);
    }

    private static async Task ComandoAsync(IAsyncRelayCommand comando)
    {
        if (!comando.CanExecute(null)) throw new InvalidOperationException("Ação indisponível neste momento. Aguarde e tente novamente.");
        await comando.ExecuteAsync(null);
    }

    private void AlterarCampo(JsonElement mensagem)
    {
        var campo = Texto(mensagem, "campo", 40);
        if (mensagem.TryGetProperty("item", out _))
        {
            var item = Item(mensagem);
            switch (campo)
            {
                case "descricao": item.Descricao = Texto(mensagem, "valor", 4000); break;
                case "dose": item.Dose = Texto(mensagem, "valor", 60); break;
                case "observacoes":
                    var texto = Texto(mensagem, "valor");
                    var formato = Formato(mensagem, texto);
                    item.Observacoes = texto; item.ObservacoesFormatadas = formato;
                    break;
                case "seNecessario": item.SeNecessario = mensagem.GetProperty("valor").GetBoolean(); break;
                default: throw new InvalidOperationException("Campo de medicamento não permitido.");
            }
        }
        else if (mensagem.TryGetProperty("grupo", out _))
        {
            var grupo = Grupo(mensagem);
            switch (campo)
            {
                case "diluente": grupo.Diluente = Texto(mensagem, "valor", 120); break;
                case "volume": grupo.Volume = Texto(mensagem, "valor", 60); break;
                case "tempo": grupo.Tempo = Texto(mensagem, "valor", 60); break;
                case "horario": grupo.Horario = Texto(mensagem, "valor", 5); break;
                case "via":
                    if (!Enum.TryParse<ViaAdministracao>(Texto(mensagem, "valor", 40), out var via) || !Enum.IsDefined(via))
                        throw new InvalidOperationException("Via de administração inválida.");
                    grupo.Via = via; break;
                default: throw new InvalidOperationException("Campo de preparo não permitido.");
            }
        }
        else
        {
            switch (campo)
            {
                case "indicacao":
                    var indicacao = Texto(mensagem, "valor");
                    var formatoIndicacao = Formato(mensagem, indicacao);
                    vm.Indicacao = indicacao; vm.IndicacaoFormatada = formatoIndicacao; break;
                case "observacoes":
                    var observacoes = Texto(mensagem, "valor");
                    var formatoObservacoes = Formato(mensagem, observacoes);
                    vm.Observacoes = observacoes; vm.ObservacoesFormatadas = formatoObservacoes; break;
                case "data":
                    var data = Texto(mensagem, "valor", 10);
                    if (data.Length == 0) vm.DataPrescricao = null;
                    else if (DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valorData))
                        vm.DataPrescricao = valorData;
                    else throw new InvalidOperationException("Data de prescrição inválida.");
                    break;
                case "hora": vm.HoraPrescricao = Texto(mensagem, "valor", 5); break;
                default: throw new InvalidOperationException("Campo de prescrição não permitido.");
            }
        }
    }

    private static string? Formato(JsonElement mensagem, string texto)
    {
        if (!mensagem.TryGetProperty("formato", out var formato) || formato.ValueKind == JsonValueKind.Null) return null;
        return TextoFormatado.Normalizar(texto, Texto(mensagem, "formato", 250_000));
    }
}
