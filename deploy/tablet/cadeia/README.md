# Cadeia pública para o assinador do portal

O desktop valida o certificado no Windows. O portal valida na VPS como usuário
dedicado; compartilhar o PostgreSQL não compartilha o repositório de certificados.

Este pacote configura a cadeia ICP-Brasil v5 / RFB v4 / Safeweb RFB v5 no
`CurrentUser` do serviço. Não altera a confiança global do Linux, a chave do
profissional, as permissões do banco ou a validação do assinador clínico.
As intermediárias ficam em `CertificateAuthority`, nunca em `Root`.

Fontes consultadas em 16/09/2026:

- [Repositório do ITI](https://www.gov.br/iti/pt-br/assuntos/repositorio/repositorio-ac-raiz)
- [Raiz v5](https://acraiz.icpbrasil.gov.br/credenciadas/RAIZ/ICP-Brasilv5.crt)
- [Repositório Safeweb](https://www.safeweb.com.br/repositorio)
- [Cadeia Safeweb RFB v5](https://repositorio.acsafeweb.com.br/ac-safewebrfb/ac-safewebrfbv5.p7b)

Os hashes SHA-256 dos três certificados DER estão fixados no instalador. Antes de
gravar, ele confere validade, extensão de autoridade e cadeia criptográfica offline.
Depois verifica a cadeia usando o repositório padrão do serviço. Em falha, remove
somente os certificados que acabou de adicionar. O modo `verificar` não altera nada.

Executar `instalar /pasta/dos/certificados` como `clinica-posto-hml` primeiro.
Após validar a assinatura completa na homologação, repetir como `clinica-tablet`.
Reiniciar somente o serviço correspondente para renovar o cache de confiança.
Não usar `exigirCadeiaConfiavel: false` para contornar uma falha.

Este pacote contém apenas esta cadeia. Certificados de outra autoridade exigem
conferência e inclusão explícita da cadeia oficial correspondente.

## Publicação

```powershell
dotnet run --project deploy/tablet/cadeia/InstalarCadeia.csproj -c Release -- validar deploy/tablet/cadeia/certificados
dotnet publish deploy/tablet/cadeia/InstalarCadeia.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/instalar-cadeia-linux
tar -czf artifacts/cadeia-safeid-v5.tar.gz -C artifacts/instalar-cadeia-linux InstalarCadeia -C ../../deploy/tablet/cadeia certificados
```

Transferir o pacote para `/home/clinica-admin/tablet-stage/`, conferir SHA-256
e executar `instalar.py homologacao <sha256>` como administrador da VPS.
O instalador executa a importação como o usuário dedicado, verifica o resultado,
reinicia apenas esse serviço e confirma que o outro ambiente não foi reiniciado.
O relatório não contém credenciais. Reexecução aceita somente o mesmo pacote.

## Evidência da correção em 16/09/2026

- A tentativa real na homologação falhou com `CADEIA-CERTIFICADO`.
- Não havia autoridades em `CurrentUser/Root` ou `CurrentUser/CA` dos dois
  serviços. A raiz oficial v5 tem o mesmo SHA-256 da raiz confiada pelo Windows.
- Os três certificados oficiais passaram na validação criptográfica do pacote.
- Depois da importação, `X509Chain` reconheceu a cadeia com o repositório padrão
  do serviço, sem downloads nem `ExtraStore` ou confiança personalizada.
- Reconhecer a cadeia é uma etapa da validação. A assinatura completa precisa
  terminar com PDF assinado conferido e arquivado, usando autorização do titular.
