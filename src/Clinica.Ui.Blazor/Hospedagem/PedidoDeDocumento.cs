namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Abra a central de Documentos já NESTE paciente" — o pedido que uma tela de fora deixa antes
/// de navegar para a chave <c>documentos</c> (out/2026: o "Emitir documento" da ficha do balcão
/// WPF, que abria a <c>EscolhaDeDocumentoWindow</c> + <c>DocumentoWindow</c>, passou a levar à
/// central, onde o papel é escolhido e escrito no <c>DocumentoForm</c>).
///
/// Um pedido só; ler LIMPA (pedido órfão abriria o paciente de ontem na próxima vez que alguém
/// entrasse na central). Quem consome é a <c>DocumentosDaRecepcao</c>, na abertura e na volta à
/// aba — o palco guarda a tela montada, e navegar de novo é uma VOLTA, não uma abertura.
/// </summary>
public sealed class PedidoDeDocumento
{
    private readonly object _trava = new();
    private int? _pacienteId;

    public void Definir(int pacienteId)
    {
        lock (_trava) _pacienteId = pacienteId;
    }

    public int? Consumir()
    {
        lock (_trava)
        {
            var p = _pacienteId;
            _pacienteId = null;
            return p;
        }
    }
}
