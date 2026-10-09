using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Prontuario;
namespace Clinica.Desktop.Shell.Componentes;

public sealed partial class AssinaturaPacienteViewModel
{
 private string _tracoWeb="";
 private byte[]? _pngWeb;
 private int _larguraWeb,_alturaWeb;
 private PainelAssinaturaWeb? _painelWeb;
 public event Action? Confirmado {add=>Fechou+=value;remove=>Fechou-=value;}
 public bool PodeAssinarLocal=>_painelWeb is null && !TemTracoRemoto && !AguardandoCelular;
 public string SituacaoDaColeta=>TemTracoRemoto?"Assinatura recebida do celular. Confira as respostas e o documento antes de confirmar.":AguardandoCelular?"Aguardando assinatura pelo celular.":_painelWeb is not null?"O paciente assina na segunda tela. Confira suas respostas antes de confirmar.":"Peça ao paciente para assinar na área indicada.";
 public string AlergiasResumo=>AlergiasNaoConferidas?"Não foi possível conferir as alergias. Confirme com o paciente.":Alergias.Count>0?string.Join("; ",Alergias):"Nenhuma alergia registrada. Confirme com o paciente.";
 public string? ImagemRemota=>TracoRemotoPng is {} bytes?"data:image/png;base64,"+Convert.ToBase64String(bytes):null;
 public string TracoWeb
 {
  get=>_tracoWeb;
  set {if(!PodeAssinarLocal)throw new InvalidOperationException("A assinatura está sendo colhida em outro dispositivo.");ReceberTracoWeb(value);}
 }
 internal void ReceberTracoWeb(string? valor)
 {
  if(Carregando || TemTracoRemoto || AguardandoCelular)throw new InvalidOperationException("Aguarde a coleta atual.");
  if(string.IsNullOrEmpty(valor)){_tracoWeb="";_pngWeb=null;TemTraco=false;OnPropertyChanged(nameof(TracoWeb));return;}
  if(valor.Length>2800000)throw new InvalidOperationException("A assinatura ultrapassou o tamanho permitido. Limpe e tente novamente.");
  using var json=JsonDocument.Parse(valor);var r=json.RootElement;
  var larguraValor=r.GetProperty("largura").GetDouble();var alturaValor=r.GetProperty("altura").GetDouble();
  if(!double.IsFinite(larguraValor)||!double.IsFinite(alturaValor)||larguraValor is <100 or >4096||alturaValor is <60 or >2048)throw new InvalidOperationException("Área de assinatura inválida.");
  var largura=(int)Math.Round(larguraValor);var altura=(int)Math.Round(alturaValor);
  var imagem=r.GetProperty("png").GetString()??"";
  if(largura is <100 or >4096 || altura is <60 or >2048 || !imagem.StartsWith("data:image/png;base64,",StringComparison.Ordinal))throw new InvalidOperationException("Área de assinatura inválida.");
  var bytes=Convert.FromBase64String(imagem[22..]);
  if(bytes.Length is <256 or >2097152 || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidOperationException("Assinatura vazia ou formato inválido.");
  var w=BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4));var h=BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4));
  if(w<1||h<1||w>8192||h>4096||(long)w*h>16000000)throw new InvalidOperationException("Dimensões da assinatura inválidas.");
  using var arquivo=new MemoryStream(bytes);var bitmap=new PngBitmapDecoder(arquivo,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
  var formato=new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap,System.Windows.Media.PixelFormats.Bgra32,null,0);var pixels=new byte[w*h*4];formato.CopyPixels(pixels,w*4,0);
  var pontos=0;for(var i=0;i<pixels.Length;i+=4)if(pixels[i+3]>20 && pixels[i]+pixels[i+1]+pixels[i+2]<650)pontos++;
  if(pontos<12)throw new InvalidOperationException("Peça ao paciente para assinar antes de confirmar.");
  _pngWeb=bytes;_larguraWeb=largura;_alturaWeb=altura;_tracoWeb=valor;TemTraco=true;OnPropertyChanged(nameof(TracoWeb));
 }
 [RelayCommand] private void LimparTracoWeb()
 {
  if(TemTracoRemoto || AguardandoCelular)return;
  ReceberTracoWeb("");_painelWeb?.Limpar();
 }
 [RelayCommand] private async Task ConfirmarWebAsync()
 {
  if(TemTracoRemoto){await ConfirmarAsync(TracoRemotoPng!,TracoRemotoLargura,TracoRemotoAltura);return;}
  if(_pngWeb is null){MensagemEhErro=true;Mensagem="Peça ao paciente para assinar na área indicada.";return;}
  await ConfirmarAsync(_pngWeb,_larguraWeb,_alturaWeb);
 }
 public async Task PrepararWebAsync()
 {
  await CarregarAsync();
  if(TelaDoPaciente is {} tela)
  {
   try{_painelWeb=new PainelAssinaturaWeb(this);await _painelWeb.AbrirAsync(tela);TelasDoSistema.ManterAcordado(true);}
   catch(Exception ex){Application.Diagnostico.Registrar("Assinatura — segunda tela web",ex);_painelWeb?.Close();_painelWeb=null;MensagemEhErro=true;Mensagem="A segunda tela não abriu. A assinatura pode ser colhida nesta tela.";}
  }
  PropertyChanged+=AoMudarEstadoWeb;
  OnPropertyChanged(nameof(PodeAssinarLocal));OnPropertyChanged(nameof(SituacaoDaColeta));
 }
 private void AoMudarEstadoWeb(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
 {
  if(e.PropertyName is nameof(TemTracoRemoto) or nameof(AguardandoCelular)){OnPropertyChanged(nameof(PodeAssinarLocal));OnPropertyChanged(nameof(SituacaoDaColeta));OnPropertyChanged(nameof(ImagemRemota));}
  if(e.PropertyName is nameof(TemAlergia) or nameof(AlergiasNaoConferidas))OnPropertyChanged(nameof(AlergiasResumo));
 }
 public void EncerrarWeb()
 {
  PararVigia();PropertyChanged-=AoMudarEstadoWeb;_painelWeb?.Close();_painelWeb=null;_pngWeb=null;_tracoWeb="";TelasDoSistema.ManterAcordado(false);
 }
 internal void PainelWebFechado(){_painelWeb=null;TelasDoSistema.ManterAcordado(false);OnPropertyChanged(nameof(PodeAssinarLocal));OnPropertyChanged(nameof(SituacaoDaColeta));}
}
public sealed partial class DeclaracaoItem
{
 public IReadOnlyList<string> OpcoesResposta {get;}=[RespostaDeclaracao.Sim,RespostaDeclaracao.Nao];
}
