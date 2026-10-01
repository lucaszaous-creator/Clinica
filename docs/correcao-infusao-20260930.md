# Correção da execução de infusões — 30/09/2026

Selecionar **Não** ou **Não executável** mostra imediatamente a justificativa
obrigatória. O servidor também recusa ausência ou texto em branco, inclusive na
retificação. Sim mantém a intercorrência opcional.

As novas prescrições digitais reservam campos AcroForm antes da assinatura médica.
Ao encerrar, data, hora efetiva, resultado, executante, justificativa e motivo de
retificação são preenchidos por revisão incremental. A assinatura da enfermagem
sela essa revisão. Um único PDF e um único arquivo contêm as duas assinaturas.
O arquivo médico original permanece guardado e é prefixo idêntico do PDF final.
O preenchimento recusa campos divergentes, duplicados, preenchidos ou texto que
não caiba: não trunca nem redesenha páginas assinadas.

A ação de documento na infusão fica concentrada na prescrição. A fila só solicita
arquivamento quando falta o arquivo principal da assinatura recebida. A ausência
da antiga folha separada não mantém a prescrição já arquivada na fila.

## Compatibilidade e documentos históricos

A migration já publicada `20260928211121_ChecagemNaoExecutavel` fornece o indicador
booleano `NaoExecutavel`, padrão falso. Não há nova migration nesta correção. A coluna histórica `Situacao` continua
usando `Realizado`/`NaoRealizado`. Aplicativos anteriores conseguem ler a negativa
com a justificativa completa; aplicativos atualizados distinguem Não executável.

PDFs assinados anteriormente não são reescritos. Sem os campos reservados, a
orquestração mantém o circuito legado e a tela informa que a execução deve ser
consultada no registro eletrônico. Corrigir retroativamente a aparência de um
PDF antigo exige uma nova emissão e novas assinaturas dos titulares; esta mudança
não cria assinaturas nem altera o prontuário de pacientes em produção.

## Verificação

- Testes cobrem as duas negativas, persistência após recarga, horário editado,
  justificativa integral de 1.000 caracteres, várias páginas, arquivo único e
  integridade criptográfica das duas assinaturas.
- Verificação independente com pyHanko reconheceu o preenchimento como
  `FORM_FILLING`, com ambas as assinaturas íntegras e válidas. Certificados usados
  são fictícios e confiados apenas pelo teste, sem chamar o provedor SafeID.
- O teste de navegador verifica campo obrigatório, bloqueio de espaços, reabertura
  da checagem, remoção do botão duplicado e a mensagem de arquivamento legado.
- Compilação nativa Windows e verificação estática da suíte realizadas.

## Publicação

Usar o pacote produzido por `tools/empacotar-continuidade-tablet.ps1`, que inclui
API, portal, manifesto SHA-256 e migration idempotente. Seguir a homologação e
publicação descritas em `continuidade-portal.md`. A instalação requer acesso
administrativo ao servidor; preparar ou enviar um pacote não significa publicá-lo.
