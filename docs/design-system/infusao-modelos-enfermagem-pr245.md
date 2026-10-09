# Infusão, modelos, enfermagem e seções — PR 245

Referência: correções solicitadas pelo proprietário em 09/10/2026, incluindo a segunda imagem da prescrição de infusão e o autocomplete de modelos. Esta revisão não recebeu avaliação do Jev.

## Comportamento entregue

- Cada infusão reúne diluente, volume, via, tempo, horário e seus medicamentos/doses, com campos abertos. Criar infusão, adicionar/remover medicamento, copiar a última prescrição deste paciente, salvar rascunho e liberar para enfermagem têm ações diretas. Observações ficam recolhidas como na referência.
- Buscar modelos mostra sugestões abaixo do campo. Clique ou Enter na sugestão seleciona e aplica pelo comando do host, sem um segundo clique em Aplicar. Digitar apenas filtra. O mesmo componente atende infusão, documentos e evolução da sessão. Quando há várias infusões, o destino é identificado e pode ser escolhido. Confirmações de substituição de conteúdo permanecem no host.
- Medicamentos têm sugestões a partir de duas letras, com setas, Enter e clique. Texto livre continua permitido. Escolher um nome não determina dose nem preparo.
- O conteúdo aplicado aparece no editor; documentos e infusões levam a rolagem ao conteúdo preenchido. A evolução informa que o modelo foi aplicado e precisa ser revisado antes de salvar.
- Emitir documento tem o botão **Emitir e abrir PDF** no rodapé. Modelos ficam junto ao editor. Itens aparecem somente nos tipos que os utilizam; alertas e exigências só ocupam espaço quando existem. As validações de emissão continuam no C#.
- **Ver** na enfermagem abre a leitura integral, com paciente, profissional, horário do fato, horário de registro, sessão agendada, prescrição e conteúdo clínico. Registros cancelados/retificados são identificados.
- A evolução originada de uma folha usa o vínculo da prescrição com a sessão. Duas sessões BSV no mesmo dia não provocam escolha ambígua. Vínculos divergentes ou de outro paciente são rejeitados. A sessão médica inclui a enfermagem desse paciente e desse agendamento.
- Exames/anexos mostram somente a seção escolhida. Anamnese, histórico, medidas e avaliações usam seletores com nomes visíveis e indicação da seleção, em lugar da tabela genérica de opções.

## Verificação reproduzível

Todos os dados usados são sintéticos. O teste funcional usa WebView2 real, comandos reais e SQLite em memória; não acessa banco clínico.

| Verificação | Evidência |
|---|---|
| `dotnet test tests/Clinica.Tests/Clinica.Tests.csproj` | 2.988 testes aprovados, zero falhas/ignorados |
| `dotnet run --project tests/Clinica.Clinico.Web.Qa -- --infusao-modelos` | Busca sem resultado, busca parcial sem aplicação, sugestões por clique/Enter, modelo de evolução salvo/reaberto, modelo de duas infusões salvo/reaberto/liberado, cópia sem gravação ao cancelar, receita com modelo e PDF, enfermagem e sessão vinculadas |
| Mesmo cenário WebView2 | Oito cliques de exames/anexos pelas duas entradas, seis seções de anamnese, sete medidas e cinco escalas; seleção e conteúdo correspondentes |
| `dotnet run --project tests/Clinica.Clinico.Web.Qa -- --web` | Páginas clínicas e operações da ficha em 1440, 1044 e 900 px; sessão, PDF, correções, anexos, exames, campos complementares, permissões e cancelamento |
| `dotnet run --project tools/VerificarInfusao -- artifacts/infusao-regressao` | 34 verificações aprovadas de infusão, execução, cadastro e cancelamento |
| `python tools/compilar-sombra.py` | C# dos 11 projetos WPF compila |
| `python tools/verificar-suite.py` | 225 XAML, 10 projetos e 143 construtores verificados; avisos de migrations anteriores |
| `npm run build` no frontend compartilhado e depois no Financeiro | Pacotes locais recompilados |

O novo cenário `--infusao-modelos` integra o workflow Windows. A verificação da consulta PostgreSQL cobre sua tradução pelo Npgsql; os testes locais de persistência usam SQLite. Não há migration nesta correção. Registros históricos sem vínculo não são associados automaticamente a uma sessão pela coincidência de data.

## Capturas

As capturas abaixo foram obtidas nos testes de uso, com dados fictícios.

- [Infusão em 1440 px](evidencias-pr245/prescricao-modelo-1440.png)
- [Infusão em notebook](evidencias-pr245/prescricao-modelo-1044.png)
- [Sugestão de modelo de infusão](evidencias-pr245/autocomplete-duas.png)
- [Sugestão de modelo de documento](evidencias-pr245/autocomplete-receita.png)
- [Sugestão de modelo de evolução](evidencias-pr245/autocomplete-roteiro.png)
- [Documento preenchido pelo modelo](evidencias-pr245/documento-modelo.png)
- [Seção de exames/anexos selecionada](evidencias-pr245/exames-secao-selecionada.png)
- [Prescrição recebida pela enfermagem](evidencias-pr245/enfermagem-execucao.png)
- [Leitura da evolução de enfermagem](evidencias-pr245/ver-evolucao-enfermagem.png)
- [Sessão com enfermagem vinculada](evidencias-pr245/sessao-com-enfermagem.png)
