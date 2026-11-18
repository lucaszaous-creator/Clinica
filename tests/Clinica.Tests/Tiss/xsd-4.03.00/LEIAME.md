# XSD TISS 4.03.00

Componente de Comunicação do Padrão TISS, pacote `PadroTISSComunicao_202511.zip`
(gov.br/ans, página "Padrão TISS – setembro 2026"), pasta `Padrão TISS Comunicação 040300`.
Os arquivos `tiss*V4_03_00.xsd`, `tissAssinaturaDigital_v1.01.xsd` e `tissLoteGuiasV4_03_00.wsdl`
estão como vieram do pacote.

Única troca: `xmldsig-core-schema.xsd` é o schema W3C de assinatura XML sem o `<!DOCTYPE>` (a mesma
cópia da pasta `xsd-4.01.00`). O do pacote traz um DOCTYPE que o leitor de schemas do .NET recusa,
e nenhum tipo do lote de guias depende da diferença.
