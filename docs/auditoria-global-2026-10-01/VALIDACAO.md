# Validação da entrega documental

Esta página registra verificações da auditoria e de sua consistência. Não é um relatório de aprovação dos fluxos clínicos em produção.

| Verificação executada | Resultado |
| --- | --- |
| Extração das três bases por objetos Git | 2.955 arquivos rastreados e 1.685 fontes elegíveis processadas |
| Resolução de âncoras com `consolidar-auditoria-global.py` | 146 referências de código dos 70 achados localizadas nos commits congelados |
| Harness .NET de navegação | Compilado/executado; 35 combinações de aplicativo e perfil; não abre WPF nem banco |
| Jev, triagem de superfícies | 263 respostas para 263 arquivos em cinco lotes |
| Jev, revisão de achados | 70 respostas em 14 lotes: 56 sustentado, 13 parcial, 1 não sustentado |
| `tools/validar-auditoria-global.py` | Confere correspondência JSON/CSV, manifesto, fichas, IDs, evidências, hashes dos pedidos/respostas, matriz e links internos; resultado estruturado em `validacao-inventario.json` |
| Sintaxe dos scripts de auditoria | Análise de sintaxe Python pelo validador; scripts principais também executados |
| `python tools/verificar-suite.py` | **OK — 219 XAML, 10 projetos e 136 construtores de ViewModel verificados** |
| Diff e escopo | Alterações restritas a documentação, vínculo no README e ferramentas da auditoria; sem código de produto alterado |

O verificador da suíte exibiu oito avisos já declarados no repositório sobre migrations não aditivas. Não houve alteração nem execução de migration nesta tarefa. Esses avisos não impediram o resultado OK.

O harness registrou um aviso de campo não usado em seu código temporário de composição; isso não impediu compilação ou execução. Sua lógica reproduz partes do shell e, por isso, continua sendo evidência derivada, não prova visual de comportamento do aplicativo.

Não foram executados testes com pacientes reais, banco de produção, impressoras, envios WhatsApp/e-mail, assinatura externa ou publicação de aplicativos. Não se rodou toda a suíte funcional do produto para uma entrega que não altera seu comportamento. Os critérios de aceite em ACHADOS.md descrevem testes a executar ao implementar cada correção.

As divergências do Jev e os limites da extração estão documentados, especialmente A17, que depende do registro Git. Os arquivos de requisição preservam o material efetivamente submetido para revisão; a redação complementar posterior não é atribuída ao Jev.
