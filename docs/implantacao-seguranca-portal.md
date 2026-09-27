# Implantação das correções de segurança do portal

Esta versão exige publicação conjunta da API e da interface `clinica-site`: a interface
envia `POST /api/clinico/atividade`, e a API deixa de renovar o prazo clínico por leituras.
Não publicar apenas um dos dois repositórios.

## Proteção opcional das credenciais compartilhadas

Para manter compatibilidade com desktops existentes, a cifra AES-256-GCM só é ativada
quando `CLINICA_CREDENCIAIS_CRIPTOGRAFIA_HABILITADA=true` estiver configurada junto
com `CLINICA_CREDENCIAIS_CHAVE` (32 bytes aleatórios em Base64). Com a ativação ausente,
leituras e gravações continuam no formato legado e nenhuma migração automática ocorre.
Esta liberação mantém a ativação ausente; não exige senha nem chave nos computadores dos
operadores.

Planejar a ativação da cifra para uma janela futura: distribuir a mesma chave para API
e todos os desktops que leem ou editam SMTP, SafeID ou S3; atualizar e fechar clientes
antigos antes de ativar. Um cliente antigo não entende `enc:v1:`. Depois da ativação,
preservar a chave em cofre separado dos backups do banco; sem ela, valores cifrados não
podem ser lidos. Nunca colocar chave em repositório, pacote, log ou interface pública.

Para esta publicação sem HML, o atualizador aceita a opção explícita
`--pular-hml motivo`, registra a tentativa em arquivo privado antes das alterações e
registra a dispensa no relatório de produção quando termina. A rota é recusada se a
unidade systemd ou qualquer `EnvironmentFile` não confirmar que a cifra está ausente ou
desativada, ou se a base contiver credenciais cifradas. Não fabricar relatórios HML nem
tratar a dispensa como aceite funcional. O caminho normal sem essa opção continua
exigindo os relatórios de saúde e aceite HML do mesmo pacote.

## Túnel e limite de requisições

Na VPS, a API só deve ser acessível pelo socket Unix restrito ao `cloudflared`. O
proxy precisa fornecer `CF-Connecting-IP` com exatamente um endereço IP válido. Sem
esse cabeçalho, a API recusa `/api` com HTTP 403; verificar essa condição na
homologação antes de trocar a rota de produção. Manter o acesso direto ao socket
restrito, pois esse cabeçalho só é confiável nessa fronteira privada.

## Verificações antes da troca

1. No caminho normal, publicar API e site na homologação, testar login, expiração clínica após 15 minutos,
   gesto real que renova o prazo e saída após inatividade em duas abas.
2. Testar upload com usuário autenticado e confirmar que tentativa anônima recebe
   recusa antes do limite maior de corpo.
3. Conferir que o pacote público não contém `manifesto.json` nem credenciais e que
   o release da API não contém `SQLitePCLRaw` ou `e_sqlite3`.
4. Para ativar a cifra, confirmar a atualização de todos os postos e a disponibilidade
   da chave antes de migrar credenciais. Na publicação atual, manter a cifra desativada
   para preservar compatibilidade com os desktops existentes.

Nenhuma dessas etapas substitui o aceite assistido dos titulares SafeID em produção.
