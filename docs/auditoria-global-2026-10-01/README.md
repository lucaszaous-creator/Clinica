# Auditoria global dos sistemas da clínica — 01/10/2026

> **Escopo vigente — portal web preservado:** por decisão do proprietário em 01/10/2026, não alterar o portal web nem reunir evolução e consentimentos nele. Recomendações sobre portal neste inventário são históricas e não autorizam implementação. Ver [regra obrigatória, imagens desktop e cobertura](ESCOPO-E-PROPOSTAS-VISUAIS.md).

**70 achados documentados**, com localização no código, consequência, proposta e critério de aceite. O trabalho cobre o ecossistema **Clinica, clinica-site e semdor-crm**, incluindo as cinco aplicações desktop, a web de leitura, os portais, o site institucional e a central de atendimento. A segunda passagem aprofundou ações internas, estados de erro, paginação, nomenclatura, persistência e continuidade entre sistemas, além dos primeiros 30 achados.

Esta entrega é uma auditoria e uma proposta de reorganização. **Nenhuma correção de produto, operação clínica, emissão, envio ou alteração de paciente foi executada.** Não há julgamento sobre a conduta da equipe: os problemas são tratados como decisões de produto e manutenção que precisam ser reconciliadas.

## Por onde começar

A [comparação visual interativa por módulo — Antes e Depois](COMPARACAO-VISUAL-POR-MODULO.md)
reúne os cinco aplicativos desktop e o CRM, com 97 cenas comparativas, três referências
preservadas e consulta ao inventário completo. O Antes é reconstruído do código; o Depois
é uma proposta. O portal continua fora do escopo de mudanças.

| Documento | O que contém |
| --- | --- |
| [Achados e propostas](ACHADOS.md) | Os 70 casos, individualmente: situação atual, efeito, sugestão, aceite e evidências imutáveis |
| [Mapa dos sistemas e funções](MAPA-SISTEMAS-E-FUNCOES.md) | Responsabilidades por domínio, superfícies equivalentes, funções que devem continuar distintas e lugar proposto para cada assunto |
| [Jornadas e simplificação](JORNADAS-E-SIMPLIFICACAO.md) | Caminhos atuais e propostos, incluindo emissão/impressão, agenda, enfermagem, pagamentos, TISS e CRM |
| [Navegação por perfil](NAVEGACAO-POR-PERFIL.md) | Matriz de 5 aplicativos × 7 perfis padrão, derivada das declarações e regras de composição |
| [Revisão por superfície](REVISAO-POR-SUPERFICIE.md) | Uma ficha para cada um dos 263 arquivos de interface identificados, vinculando ações, domínio, achados e verificação recomendada |
| [Catálogo de controles](CATALOGO.md) | Rótulos, abas, campos, botões, ações e linhas de origem extraídos do código |
| [Revisão do Jev](REVISAO-JEV.md) | Método, cobertura, votos, ressalvas e divergências; não é um selo de aprovação |
| [Versões e cobertura](VERSOES-E-COBERTURA.md) | Quais fontes foram examinadas, diferenças locais e o que a auditoria estática não demonstra |
| [Plano de reorganização](PLANO-DE-REORGANIZACAO.md) | Ordem recomendada, arquitetura de informação, padrões de ação e validação de cada etapa |
| [Validação da entrega](VALIDACAO.md) | Checagens efetivamente executadas e suas limitações |

## O que foi inventariado

| Medida | Quantidade | Interpretação correta |
| --- | ---: | --- |
| Arquivos rastreados nos três repositórios | 2.955 | Manifesto completo das três bases principais; inclui suporte, testes, documentação e ativos |
| Fontes elegíveis processadas | 1.685 | Extração automatizada de código; não significa leitura humana integral de cada arquivo |
| Arquivos com interfaces/templates identificados | 263 | Inclui páginas, componentes, janelas, estilos XAML e App.xaml; **não são 263 telas independentes** |
| Declarações de controles | 6.743 | Inclui textos, campos e templates condicionais, não apenas botões |
| Candidatos a funções nomeadas | 7.986 | Índice por expressões regulares; callbacks e construções dinâmicas podem exigir inspeção adicional |
| Declarações de rotas | 182 | Inclui fragmentos de grupos e bridge histórico; não equivale a 182 endpoints ativos |
| Links/chamadas identificados | 430 | Referências estáticas, inclusive destinos montados por template |
| Declarações de navegação | 443 | Antes de composição, permissões e deduplicação |
| Combinações aplicativo/perfil | 35 | Perfis padrão; não inclui toda possível exceção individual de permissão |
| Achados editoriais | 70 | Mistura explicitamente identificada de inconsistências, limitações e oportunidades |

**“Global” aqui significa corpus estático indexado nas três bases e revisão transversal dos fluxos e superfícies. Não significa todos os estados executados em produção.** As contagens não são uma porcentagem de ausência de defeitos. O relatório permite localizar qualquer arquivo inventariado e continuar a verificação sem começar outra raspagem do zero.

## Resultados que merecem atenção primeiro

- **Documentos:** orientar cancelar/reemitir para assinar depois contradiz a assinatura do documento existente; falha depois da persistência deixa um caminho de nova emissão; imprimir perde destaque quando há assinatura pendente. Ver A01–A06.
- **Faturamento:** retorno parcial pode conservar guias ausentes como aceitas; segunda via do XML é uma regeneração; verificação XSD não informa separadamente se foi executada. Ver A64–A66.
- **Comunicação:** abrir WhatsApp é tratado como envio em Campanhas. Preparar, enviar, entregar e ler precisam ser eventos diferentes. Ver A31.
- **Continuidade:** há atalhos sem entidade de destino, estados vazios que só mandam procurar outra tela e erro de atualização mostrado em diálogo já fechado. Ver A32, A37, A40–A41, A48 e A62.
- **Organização:** preço particular com dois pais, nomes antigos em abas, diferentes significados de Documentos/CRM/Acompanhamento e múltiplas filas com sobreposição. Ver A08–A14, A20–A25, A56 e A63.
- **Integrações e versões:** há fontes históricas copiadas no CRM e uma lacuna na localização reproduzível do bridge canônico nas bases examinadas. Isso não prova defeito na produção. Ver A17–A19 e o documento de cobertura.

No exemplo pedido pelo usuário, a solução proposta é uma tela de resultado **“Documento emitido nº …”**, com **Imprimir**, **Assinar este documento** quando cabível e **Fechar**. Impressão não deve exigir reemissão. Assinatura profissional e consentimento do paciente continuam sendo ações distintas.

## Dados consultáveis

- [achados.csv](achados.csv): lista editorial filtrável em planilha.
- [arquivos.csv](arquivos.csv): todos os arquivos rastreados, com classificação do escopo de extração.
- [superficies.csv](superficies.csv), [controles.csv](controles.csv), [funcoes.csv](funcoes.csv), [rotas.csv](rotas.csv), [links.csv](links.csv) e [navegacao.csv](navegacao.csv): inventários detalhados.
- [inventario.json](inventario.json), [evidencias.json](evidencias.json) e [bases.json](bases.json): fontes estruturadas e revisões congeladas.
- [navegacao-perfis.json](navegacao-perfis.json): resultado da matriz; [copias-crm.json](copias-crm.json): comparação das fontes compartilhadas.
- [jev/](jev/): pedidos e respostas sanitizados, com modelo e hash do pedido. Nenhuma chave de API está incluída.

## Reproduzir

Os comandos abaixo são executados na raiz do repositório Clinica. Substitua os caminhos dos outros dois checkouts. A extração lê objetos Git, não banco de dados nem configuração local.

```powershell
python tools/mapear-superficies.py --site CAMINHO_SITE --crm CAMINHO_CRM
python tools/mapear-navegacao-perfis.py --dotnet CAMINHO_DOTNET
python tools/consolidar-auditoria-global.py --site CAMINHO_SITE --crm CAMINHO_CRM
python tools/documentar-cobertura-auditoria.py
python tools/validar-auditoria-global.py
python tools/verificar-suite.py
```

Para repetir exatamente esta fotografia, as referências dos três repositórios precisam resolver para os commits de `bases.json`. A extração usa `origin/main` por padrão; não atualize os dados históricos sem criar uma nova pasta de auditoria. A matriz usa o código do checkout Clinica, que nesta execução estava no mesmo commit congelado. As chamadas ao Jev são opcionais, exigem `--executar` ou `--executar-jev` e chave em arquivo externo. Novos pedidos podem ter custo; não fazem parte da reprodução local sem esses argumentos.

Uma melhoria só deve ser marcada como resolvida depois de ligar a correção ao achado, testar o caminho com o perfil adequado e identificar a versão publicada. Os critérios de aceite deste relatório são **propostas para as correções**, não testes que já passaram no produto.
