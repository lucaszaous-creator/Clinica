# Implantação das correções de segurança do portal

Esta versão exige publicação conjunta da API e da interface `clinica-site`: a interface
envia `POST /api/clinico/atividade`, e a API deixa de renovar o prazo clínico por leituras.
Não publicar apenas um dos dois repositórios.

## Chave das credenciais compartilhadas

As senhas de SMTP, SafeID e armazenamento S3 passam a ser cifradas no banco com
AES-256-GCM. A variável `CLINICA_CREDENCIAIS_CHAVE` deve conter **32 bytes aleatórios
em Base64**, gerados por fonte criptográfica. Guardar a chave em cofre separado dos
backups do banco. Nunca colocá-la no repositório, pacote, log ou interface pública.

Antes de iniciar a versão nova, distribuir **a mesma chave** para a API e para todos
os postos desktop que leem ou editam essas integrações. Restringir o acesso aos
arquivos de ambiente e parar os clientes antigos durante a troca. A primeira leitura
de cada credencial antiga a migra para `enc:v1:`. Um cliente antigo não entende esse
formato; por isso, a atualização dos clientes deve ser coordenada. Sem a chave, a
leitura ou gravação de uma credencial não vazia falha de modo explícito.

Conferir em homologação o envio de e-mail, SafeID e publicação S3 com valores
fictícios. Após a migração, conferir no banco somente que os quatro campos sensíveis
começam com `enc:v1:`; não exibir os valores. Preservar a chave para restauração de
backups. A rotação exige decifrar e cifrar novamente com uma chave nova em uma janela
controlada; substituir a variável sem recifrar torna as credenciais ilegíveis.

## Túnel e limite de requisições

Na VPS, a API só deve ser acessível pelo socket Unix restrito ao `cloudflared`. O
proxy precisa fornecer `CF-Connecting-IP` com exatamente um endereço IP válido. Sem
esse cabeçalho, a API recusa `/api` com HTTP 403; verificar essa condição na
homologação antes de trocar a rota de produção. Manter o acesso direto ao socket
restrito, pois esse cabeçalho só é confiável nessa fronteira privada.

## Verificações antes da troca

1. Publicar API e site na homologação, testar login, expiração clínica após 15 minutos,
   gesto real que renova o prazo e saída após inatividade em duas abas.
2. Testar upload com usuário autenticado e confirmar que tentativa anônima recebe
   recusa antes do limite maior de corpo.
3. Conferir que o pacote público não contém `manifesto.json` nem credenciais e que
   o release da API não contém `SQLitePCLRaw` ou `e_sqlite3`.
4. Confirmar a atualização de todos os postos e a disponibilidade da chave antes de
   migrar as credenciais existentes ou abrir o serviço ao público.

Nenhuma dessas etapas substitui o aceite assistido dos titulares SafeID em produção.
