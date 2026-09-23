# Revisão do cadastro expandido e da navegação

## Correções desta revisão

- Janela comum de cadastro abre maximizada, tanto para novo paciente quanto para edição.
- Retirados MaxWidth/MaxHeight fixos, que limitavam a expansão ao monitor principal.
- Preenchimento e captura de foto ficam bloqueados durante a carga e a gravação. A carga tem indicação visível.
- Cancelar e fechar aguardam o término da gravação; conclusão bem-sucedida fecha normalmente.
- Telefone, e-mail, carteirinha, indicação e observações atualizam o valor digitado sem depender de sair do campo antes de salvar.
- O formulário consulta somente o cadastro, sem trazer o histórico de atendimentos/códigos.

## Verificação real

268 verificações Windows aprovadas, nenhuma falha, em banco SQLite isolado com dados fictícios. Além das rotas dos cinco aplicativos e dos seis perfis/composições, a rodada verifica abertura maximizada, ausência de limites fixos, carga lenta, tentativa de fechar durante gravação, atualização dos campos pelo teclado, cadastro/edição e preservação dos dados ao reabrir.

Janelas restauradas: 600×450, 960×720, 1366×768 e 1920×1080. Salvar e Cancelar ficaram na área útil; último campo alcançável por rolagem, sem rolagem horizontal. A captura maximizada mede 1920×1027 de área cliente no monitor usado.

## Jev

Consulta real à TypeSafe, modelo retornado `jev-1.13.0`, com código dos componentes e inventário dos 32 achados. Confirmou a abertura normal anterior, recomendou maximizar sem limites fixos, usar leitura só cadastral e priorizar rotas/perfis. Os riscos de carga e fechamento foram confirmados em testes próprios; a resposta do modelo não substitui execução.

## Escopo e publicação

Os 32 achados seguem mapeados. Esta revisão foi incorporada à PR #205; a distribuição aos computadores da clínica permanece pendente. As verificações usam dados fictícios e não constituem aceite operacional em produção.
