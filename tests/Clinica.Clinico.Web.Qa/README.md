# Verificação clínica Web

Executar na raiz do worktree, com .NET 8 SDK e Windows/WebView2:

```
dotnet run --project tests/Clinica.Clinico.Web.Qa -- --fluxos
dotnet run --project tests/Clinica.Clinico.Web.Qa -- --web
```

Sem argumento: valida os contratos explícitos de 20 páginas, 17 formulários próprios e os formulários compartilhados consumidos, e exporta metadados públicos locais para `tipos.json`. Não conecta a banco nesta modalidade.

`--fluxos`: SQLite exclusivamente em memória, nomes sintéticos, serviços reais. Verifica abertura das páginas, medida, cancelamento sem gravação, resultado de exame, alerta clínico, escala incompleta/completa, editor clínico, rascunho preservado entre seções, mapa cancelado/confirmado e gravação da sessão. Também confirma o conteúdo da leitura de sessão e versões anteriores, com rejeição de escrita nesses campos. A conexão PostgreSQL configurada aponta para porta local inválida; qualquer uso indevido falha. Diálogos nativos de domínio lançam erro.

`--web`: mesmos fluxos e 60 capturas WebView2 das 20 páginas em 1440×900, 1044×788 e 900×600. O host da página exclusiva da enfermagem é criado com sua própria sessão autenticada, preservando a barreira de troca de usuário. Verifica navegação superior, ausência de barra lateral e ausência de transbordamento horizontal. Inclui uma captura adicional e gesto no mapa de costas, usando a geometria 220×460 compartilhada com o PDF. Imagens em `artifacts/clinico-web`.

`gerar-secoes.py` e `gerar-enfermagem.py` são auxiliares para extração inicial das Views conhecidas; produzem registros C# explícitos. Não são executados no aplicativo. Ajustes semânticos vivem em `ClinicoWebRegistro.Apresentacao.cs` e nos contratos de diálogos. As pendências da extração (semana aninhada e ações do workspace) foram tratadas nos registros especializados.

Nenhuma chamada paga de assinatura ou publicação é feita. A assinatura mantém o serviço e o fluxo existentes; seus testes são compartilhados pelo Shell.
