# Continuidade temporária sem A1

Ativação por `Configuracoes[Clinica.ContinuidadeSemAssinatura] = true`, solicitada pela administração em 02/10/2026. O padrão é desativado.

O médico confere a prescrição e alergias e libera para enfermagem. A situação é `Liberada`, com data e auditoria próprias; não há assinatura fictícia. A enfermagem registra cada item e salva a execução. Impressão não é requisito. Orientação externa salva segue para avaliação do médico responsável.

Receitas, atestados e demais documentos continuam disponíveis sem certificado. O PDF de infusão identifica médico e executantes com nome, conselho e CPF disponível e indica ausência de assinatura digital. Assinaturas anteriores e seus arquivos permanecem preservados.

## Publicação

Aplicar o pacote aditivo em homologação, ativar a configuração com backup e auditoria e executar o aceite HTTP com dados fictícios. Publicar o mesmo SHA em produção após aceite. Atualizar Consultório e Gerente: versões anteriores não reconhecem a nova situação `Liberada`. Reabrir os aplicativos após a atualização.

O recuo mantém colunas e evidências clínicas. Para interromper novas liberações, alterar a configuração para `false`. Não executar downgrade destrutivo nem reescrever prescrições liberadas como assinadas.

## Verificação

Testes de liberação/autoria, alergias, versão e idempotência, execução incompleta, conclusão de registros anteriores, preservação da assinatura médica, orientação externa e seis tipos de documento. Teste do portal cobre médico, enfermagem, impressão opcional, fila vazia ao concluir, acessibilidade e larguras de 320 a 1280 pixels.
