# Notificação sobre conteúdo WebView2

Execute no Windows com desktop interativo e WebView2 instalado:

```powershell
dotnet run --project tools/validar-notificacao-web -c Release
```

O teste abre brevemente uma janela real do shell com conteúdo web inteiramente fictício.
Não carrega bootstrap, conexão salva nem banco de pacientes. Um perfil WebView2 exclusivo
fica em `artifacts/notificacao-web/perfil-web`.

Confere a notificação acima do HWND web usando a composição real do desktop. A captura
inclui somente a área cliente da janela sintética em primeiro plano, e verifica pixels
escuros no topo e no rodapé do aviso. `CapturePreview` do navegador e `RenderTargetBitmap`
do WPF, isoladamente, não reproduzem a sobreposição dos HWNDs.

Também confere foco do campo web, ausência de topmost global, mover/redimensionar,
ocultar quando outra janela é ativada ou o shell é minimizado, restaurar enquanto o
aviso está vigente, expiração de quatro segundos, histórico e fechamento. A ativação
explícita usada para preparar a captura existe apenas neste harness; o componente de
produto não solicita ativação, foco nem captura do mouse.

Evidência: `artifacts/notificacao-web/notificacao-integral-sobre-web.png`.

A causa é o [airspace do WebView2 WPF](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf):
o controle hospeda uma janela Win32 acima da renderização WPF. O host usa um
[Popup com janela própria](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup),
remove o topmost padrão e o limita ao shell ativo. O serviço e o histórico permanecem iguais.
