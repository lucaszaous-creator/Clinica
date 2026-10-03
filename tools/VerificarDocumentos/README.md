# Conferência dos documentos e da navegação

Execute no Windows com o SDK .NET 8:

```powershell
dotnet run --project tools/VerificarDocumentos -- artifacts/documentos-modelo-b
dotnet run --project tools/VerificarDocumentos -- artifacts/barra-modelo-b --barras
```

A ferramenta abre as janelas reais fora da área visível, com dados fictícios em SQLite na memória. Não usa o banco da clínica e não envia documentos à impressora.

- Documentos: captura receita, atestado, comparecimento e pedido de exames; confere edição e negrito no controle real, criação/aplicação/exclusão de modelos, ausência da opção de assinatura e entrega de arquivos históricos assinados.
- Barra: navega pelo MenuItem real até Prescrições e verifica a largura da navegação em 1366, 1280, 1024 e 880 px. Captura o popup nativo separadamente e o compõe na posição de seu item para mostrar a janela com o menu aberto, pois o popup tem sua própria superfície WPF.
- `bindings.log` registra erros de bindings. As capturas aprovadas da implementação estão em `docs/capturas/documentos-modelo-b`.

As capturas de documentos em 1280 × 800 facilitam a comparação com o modelo aprovado; a janela de produção abre em 1280 × 680 e mantém rolagem na folha.
