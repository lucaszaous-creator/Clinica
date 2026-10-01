# PDFs sintéticos anteriores à remoção

Estes PDFs foram produzidos para testes antes da retirada da integração remota.
Não contêm dados clínicos reais nem um certificado da cliente. O relatório tem
uma assinatura e a segunda fixture tem duas assinaturas de teste.

Os testes de `A1Tests` leem esses arquivos sem regenerá-los, simulam registros
arquivados e verificam os bytes retornados pela API e pelos serviços de PDF,
com A1 desabilitado e sem credencial cadastrada. Também verificam a integridade
criptográfica; não afirmam confiança pública dos certificados fictícios.

Não substituir estes arquivos pela saída da implementação sob teste: eles são
a referência anterior à remoção. Nunca incluir prontuários, PFX ou senhas reais
nesta pasta.
