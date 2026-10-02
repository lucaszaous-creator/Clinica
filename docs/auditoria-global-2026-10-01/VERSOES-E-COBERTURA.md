# Versões, método e cobertura da raspagem

## Bases principais congeladas

| Repositório | Commit examinado | Arquivos rastreados | Fontes extraídas |
| --- | --- | ---: | ---: |
| Clinica | `e6dec9abddb6242b1faaec79a48cc62c054ca79c` | 1.817 | 1.128 |
| clinica-site | `cc45636a9c8894605b81c39fffe84333e6065553` | 328 | 48 |
| semdor-crm | `33c9ffcb836286c27a61bf1f91640a62acb5b70c` | 810 | 509 |

Os links dos achados apontam para esses commits, não para uma branch mutável. O terceiro repositório é o código da central CRM encontrado no workspace; seu nome Git é `semdor-crm`. Os dados estão em [bases.json](bases.json).

A auditoria foi produzida em checkout isolado do Clinica, branch `codex/mapa-global-clinica`, a partir da main acima. Não foram misturadas alterações de produto existentes no diretório original. A documentação final também é disponibilizada nesse diretório para consulta.

## Diferenças dos diretórios locais

O registro estruturado está em [versoes-locais.json](versoes-locais.json). São fatos do Git observados durante a auditoria; não identificam automaticamente o que está instalado na clínica.

### Clinica original

- HEAD `da4610caa69ece6533ea40e49e600f1f38f02a1d`, branch `codex/continuidade-publicacao-20260923`.
- Um commit próprio de documentação e 77 commits da main ausentes dessa branch.
- Doze arquivos rastreados tinham alterações locais: dois scripts de implantação, nove fontes de Application/Clínico/Web e um arquivo de testes.
- **Dez dos doze arquivos locais já correspondem ao conteúdo da main**, desconsiderando terminadores/espaço final do texto. São as mudanças de controle de registros pendentes desde uma data de início. Portanto, não foram listadas como um novo defeito nem como correção necessariamente ausente da main.
- Os dois scripts de implantação diferem da main; seu propósito de publicação/continuidade foi registrado, mas eles não foram executados e nenhuma implantação foi feita.

Pastas locais como `output/`, `tmp/`, `semdor-film/` e arquivos operacionais não rastreados não foram incorporados como aplicações adicionais ou evidências de produção. Não foram lidos segredos de implantação para esta auditoria. A única credencial utilizada foi a autorização externa para a consulta solicitada ao Jev; não entrou na entrega.

### clinica-site

HEAD `a8bb98989d90d1100cf6d6e0cb4a4a6e2beb28d7`, quatro commits à frente da main e sem alteração rastreada pendente:

1. `803c535` — exigir justificativas e concluir infusão com a prescrição assinada.
2. `624fdbe` — aplicar justificativa imediata ao fluxo atual de enfermagem.
3. `9f08a1b` — retomar infusão assinada sem exigir segundo clique de conclusão.
4. `a8bb989` — identificar infusão pendente e oferecer retomada no SafeID.

Os deltas atingem `clinico.js`, `confirmacoes.js`, `execucao-direta.js`, `posto.js` e três testes em `ferramentas/`. A leitura do diff confirmou evolução na coleta de justificativa/horários, aviso de conclusão automática e consulta/retomada da assinatura pendente. **Não propor a mesma correção de novo sem confrontar esses commits e a API implantada.** As propostas A20/A43 e a jornada de infusão se referem às responsabilidades e ao caminho; não são alegação de que todos os problemas anteriores de SafeID continuam presentes nessa branch.

O catálogo principal preserva a main congelada para consistência entre os sistemas. Esses quatro commits são uma camada adicional de cobertura, não foram misturados silenciosamente ao inventário principal nem considerados publicados.

### semdor-crm e bridge

HEAD e main coincidem em `33c9ffc…`. O projeto web usa seu próprio código CRM. Há 445 caminhos de fontes também existentes em Clinica: 442 idênticos e três divergentes, listados em [copias-crm.json](copias-crm.json). Os divergentes são serviços/registros do tablet em `Clinica.Infrastructure/Tablet`.

O README identifica o bridge copiado como histórico, e `src/Clinica.Crm.Bridge/Clinica.Crm.Bridge.csproj` bloqueia a publicação dessa cópia com `CanonicalClinicBridgeOnly`. Nas bases examinadas, a main de Clinica não contém `src/Clinica.Crm.Bridge`; o caminho de checkout canônico esperado por scripts do CRM também não foi localizado nessa disposição de diretórios. **A18 é uma lacuna de reprodução/localização, não prova de integração indisponível na produção.** Resolver isso exige identificar a revisão/pasta efetivamente responsável pelo bridge implantado.

## Como a cobertura foi construída

1. Manifesto de todos os arquivos rastreados dos três commits.
2. Extração dos tipos de fonte elegíveis em `src`, `portal`, `modelos`, `conteudo` e `estatico`. Excluídos da extração textual: dependências, bin/obj, minificados, arquivos Designer, appsettings, locks e snapshots. Eles permanecem no manifesto quando rastreados.
3. Extração de controles, rótulos, ações/bindings, funções nomeadas, links, rotas e declarações de navegação, mantendo arquivo/linha.
4. Triagem de todos os 263 arquivos de superfície com Jev e revisão editorial aprofundada dos fluxos e implementações associados aos achados.
5. Harness .NET sem WPF/banco usando declarações reais de módulos e perfis; reprodução da composição/filtro/ordem do shell para 35 combinações.
6. Comparação de fontes copiadas no CRM, busca de referências de abertura e conferência dos deltas locais.
7. Resolução de 146 âncoras dos 70 achados e revisão desses trechos pelo Jev.
8. Documentação por sistema, superfície e jornada, com propostas e critérios de aceite.

### Matriz de navegação: limites específicos

O harness usa Domain, perfis padrão, chaves, normalização e declarações de itens originais. A composição dos aplicativos, filtros, deduplicação e ordenação são reproduzidos no harness. Não há criação das telas por injeção de dependência nem observação WPF. A entrada de Treinamento adicionada separadamente pelo shell não integra a contagem de itens de módulo. Permissões concedidas/revogadas individualmente, dados e instalação podem mudar a disponibilidade.

As 27 entradas e 92 destinos do Gerente com perfil Gerente **não são 92 abas simultaneamente abertas**. Preços do particular com dois pais e abertura inicial do Financeiro em Estoque são resultados dessa matriz derivada e devem ser confirmados no aplicativo antes da correção.

### Candidatos sem referência de abertura

`ProntuarioView` e `RetornoView` antigos da Recepção, e `BaixaGuiaWindow` do Faturamento, não tiveram referência textual de uso encontrada fora de suas definições. A15 registra **candidatos**, não autoriza exclusão: recursos, reflexão e empacotamento podem alterar a conclusão. Os resultados estão em [superficies-sem-referencia.json](superficies-sem-referencia.json).

## O que não foi demonstrado

| Área | Limitação | Como concluir uma validação futura |
| --- | --- | --- |
| Produção | Não foi identificada a revisão de cada pacote efetivamente instalado | Relacionar versão mostrada no app, pacote e commit |
| UI em execução | Não houve percurso visual de todos os controles nem testes em resoluções/dispositivos | Usar instância fictícia, perfis reais e cenários definidos |
| Dados reais | Nenhuma consulta a banco clínico ou contato com paciente | Testes com dados fictícios; observação operacional autorizada se necessária |
| Assinatura/impressora/provedor | Sem assinatura externa, impressão física ou envio real | Homologação com falhas, cancelamento e retomada |
| Backend em execução | Índice de funções não prova que toda função é alcançável ou correta | Testes de integração por jornada e grafo de chamadas quando necessário |
| Extração | Regex não é parser completo; callbacks, controles gerados e bindings podem escapar ou gerar candidatos extras | Inspeção dos arquivos de origem e instrumentação de execução |
| Permissões | Perfis padrão não cobrem todas as combinações individuais | Testar os perfis efetivamente concedidos na instalação |
| Histórico Git | Auditoria principal examina três revisões, não cada commit/branch antiga | Examinar branch específica quando houver sinal de uso/implantação |
| UX quantitativa | Não foram medidos cliques, tempo, frequência ou satisfação | Sessões observadas e comparação antes/depois |
| Regra clínica/legal/fiscal | Não foi auditada validade profissional ou normativa | Revisão pelos responsáveis desses processos; sugestões aqui são de software e percurso |

O compromisso de não omitir partes foi atendido pelo manifesto e pelos índices rastreáveis do corpus definido. **Não existe base para afirmar que nenhum outro defeito existe ou que todos os estados possíveis foram percorridos.** Os pontos que dependem de execução permanecem explícitos e ligados a um roteiro, em vez de serem apresentados como verificados.
