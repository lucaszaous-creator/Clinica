# Mídia fixa para testes WebView2

`pacotes.mp4` e `documentos.mp4` são as aulas públicas da versão
`treinamento-desktop-20260928`, com os mesmos SHA-256 do catálogo embutido.
O teste recusa divergência antes de reproduzir. São usadas somente pelo QA;
não são adicionadas ao pacote dos aplicativos.

Manter a mídia local permite conferir reprodução e os botões de treinamento
sem depender da disponibilidade do download no GitHub. O erro HTTP e a
recuperação do download são testados separadamente em `TreinamentoTests`.
