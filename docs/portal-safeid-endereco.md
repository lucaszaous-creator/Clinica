# Endereço na assinatura do portal

A autorização de uma receita sem endereço no cadastro era bloqueada pela conferência de conteúdo. As mensagens controladas do serviço SafeID não usavam o marcador de validação pública, por isso a API devolvia uma frase genérica.

## Correção

- Validações controladas de SafeID voltam com orientação ao profissional, preservando o filtro de exceções internas e a sanitização de falhas do provedor.
- GET/POST de endereço no contexto de uma receita: mesma sessão, CSRF, permissão de prescrever, vínculo com o documento e conferência de assinatura/cancelamento.
- Completa somente endereço ausente, com 5 a 300 caracteres; endereço já preenchido não é sobrescrito por esse fluxo.
- Gravação transacional e condicional, lock de paciente compatível com a assinatura, reenvio idempotente e auditoria sem copiar o endereço para logs.
- Não emite outra receita e não autoriza ou assina automaticamente.

## Testes

Teste HTTP com dados fictícios: mensagem de endereço, validação de entrada, CSRF, enfermagem impedida, documento inexistente, gravação, reenvio, conflito, auditoria única e documento preservado/cancelado.

A interface correspondente está em clinica-site PR #24. Publicar primeiro a API em homologação, validar e publicar em produção com o frontend. Não há migration de banco.
