# Treinamento no desktop

A navegação compartilhada abre Treinamento em todos os cinco executáveis. O
catálogo usa as mesmas chaves e permissões das telas. Dependências contextuais
não liberam aulas; entram somente abas publicadas. Gerente reaproveita os mesmos
IDs, sem duplicação. O recorte continua respeitando perfis exclusivos.

O player WPF reproduz dentro da janela, com pausa, volume, capítulos, tela cheia,
retomada e conclusão por usuário deste computador. Trocar de tela interrompe a
reprodução. O primeiro acesso baixa o arquivo HTTPS versionado; o SHA-256 do
catálogo é validado antes de reproduzir. Downloads incompletos não são usados.

`CLINICA_TREINAMENTO_MIDIA` pode apontar a uma pasta local de vídeos `<id>.mp4`
para a verificação. Sem essa opção usa a mídia ao lado da aplicação e depois o
acervo HTTPS. O progresso fica em LocalAppData/ClinicaSemDor/Treinamento.

## Captura e verificação

`dotnet run --project tools/treinamento/captura/CapturaTreinamento.csproj -- <saída> todas`
captura a janela real com serviços reais e um banco SQLite em memória. Não roda
o startup de produção, nem envio de e-mails, WhatsApp ou assinatura externa.
Os nomes são fictícios. `inventario` lista a navegação; `qa-treinamento` verifica
escopos dos cinco módulos, busca, reprodução, capítulos, tela cheia e parada.

Os roteiros, narração Bella e renderização Remotion ficam no repositório
clinica-site em `ferramentas/treinamento-desktop`. O catálogo validado deve ser
idêntico a `src/Clinica.Desktop.Shell/Treinamento/catalogo.json` antes da release.
Os vídeos são assets públicos da release `treinamento-desktop-20260928` do Clinica,
nomeados pelo SHA-256. Não exigem login, token, VPS ou acesso ao banco. Publicar
e verificar todos os assets antes de liberar os executáveis. Não sobrescrever
assets existentes: clientes antigos precisam continuar acessando suas versões.

Primeira publicação autorizada: 24 vídeos prontos de 75 planejados; saldo do
DaVinci bloqueou as demais narrações. O catálogo contém apenas as aulas prontas.
Não há alteração de banco, migração ou dados de produção nesta implementação.
