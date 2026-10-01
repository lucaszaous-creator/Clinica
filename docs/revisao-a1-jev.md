> Registro histórico. A integração descrita foi retirada em 01/10/2026. Para o fluxo atual, consulte [assinatura A1](assinatura-a1.md) e [relatório de remoção](remocao-provedor-assinatura.md).

# Revisão e correções A1 com Jev — 01/10/2026

Escopo: alterações locais em `codex/assinatura-a1` e `codex/portal-a1`, incluindo
arquivos ainda não commitados. Revisão e correções solicitadas pelo usuário.
Nenhuma alteração em produção e nenhum certificado real utilizado.

## Resultado das correções

1. **P1 — Resposta de preparação substituía a revisão de outro documento.**
   O portal confere se a janela e o elemento da revisão original continuam ativos,
   inclusive a confirmação marcada. Uma resposta de uma revisão fechada/substituída
   é descartada. A confirmação da senha identifica o número e o tipo de operação,
   fornecidos pela API. No teste de navegador, a resposta atrasada de 501 não altera
   a revisão de 502; somente a autorização de 502 é enviada para assinar.

2. **P1 — A fotografia de conferência omitia conteúdo do PDF.**
   Foram incluídos horários, períodos, formatação, desenhos e ordenação dos itens dos
   documentos. A revisão em lote encontrou a mesma omissão nas prescrições: diluição
   única, diluente global, volume e formatos. A execução agora abrange também checagens,
   identidade e datas dos executantes, evoluções, intercorrências, sinais vitais,
   diagnósticos e cuidados de enfermagem, além de alergias e assinaturas existentes.
   O contrato é compartilhado por A1 e SafeID. Mudanças exigem nova revisão.
   O teste HTTP bloqueia onze mutações adicionais com 409 e nenhum PDF arquivado.
   Outro teste confirma treze mudanças de prescrição/execução, lendo o banco em
   escopos independentes para não depender de entidades já carregadas em memória.

3. **P2 — Gestão de certificado podia substituir ou fechar outra janela.**
   A consulta inicial e as atualizações após cadastro/remoção verificam a identidade
   da janela antes de abri-la ou fechá-la. Três cenários com respostas atrasadas
   preservam a outra revisão. A reprodução da consulta inicial com o código anterior
   falha explicitamente: título observado “Meu certificado A1”, esperado “Outra revisão”.

4. **P2 — Falha de atualização ocultava o resultado da assinatura.**
   Após assinatura bem-sucedida, falha ao recarregar o atendimento agora informa que
   o documento foi assinado e arquivado e orienta reabrir a consulta ao PDF. O teste
   retorna 200 na assinatura e 503 na atualização, verifica essa mensagem e apenas
   uma requisição de assinatura.

5. **P2 — Erro ao importar A1 mantinha selecionado o certificado anterior no desktop.**
   A escolha do arquivo e o início da importação limpam a seleção. Falha exige nova
   escolha explícita, inclusive se ocorrer antes da abertura do certificado. A janela
   WPF real reproduziu a seleção indevida antes da mudança; depois, verifica seleção
   vazia, confirmação bloqueada, possibilidade de escolher novamente e limpeza da senha.

## Verificações

- 2.917 testes de regras e assinatura aprovados.
- 23 testes da API aprovados, incluindo custódia, CPF, CSRF, consentimento, troca de
  certificado, senha incorreta, expiração, uso único e integridade do PDF arquivado.
- Seis scripts de navegador: A1, concorrência A1, diálogos A1, consultório, posto e
  endereço SafeID. Incluem responsividade e acessibilidade nos fluxos existentes.
- Janela WPF A1 conferida em 780 e 960 pixels, com os cenários de erro de importação
  e limpeza da senha. Compilação do validador executada no Windows.
- Verificação estrutural: 219 XAML, 10 projetos e 136 construtores de ViewModel.
  Compilação de sombra: 11 projetos WPF. Avisos existentes de migrations antigas e
  outros módulos permanecem; não houve nova migration nesta rodada de correções.
- Pacote do portal conferido: 294 arquivos públicos. Os três testes A1 estão no CI.

## Participação efetiva do Jev

Nesta rodada houve **três chamadas em lote e duas conferências posteriores** à
TypeSafe; todas retornaram o modelo `jev-1.13.0`. Os lotes cobriram interface,
conteúdo assinado e custódia/desktop. A API classifica hipóteses estruturadas; não
executa testes, não aplica correções e não constitui aprovação de segurança.

O Jev indicou os problemas de gestão de diálogos, atualização após assinatura e
seleção anterior no desktop. O lote inicial não reconheceu as omissões de conteúdo;
os testes locais as comprovaram mesmo assim. A primeira conferência das correções
classificou quatro cenários como corrigidos e manteve gestão de diálogos pendente.
A consulta inicial atrasada foi então reproduzida, corrigida e testada; a última
chamada classificou esse cenário como corrigido. As conclusões técnicas se apoiam
nos testes e no código, não somente nas classificações do modelo.

Pedidos, respostas, hashes e uso retornado estão em
`artifacts/revisao-a1-jev/lotes-correcao/`, fora do versionamento. As provas da rodada
anterior permanecem em `artifacts/revisao-a1-jev/`; as provas de navegador ficam no
mesmo diretório de artefatos do repositório da interface. Os testes permanentes são
`tests/Clinica.Assinaturas.Tests/A1Tests.cs`, o cenário `--a1` do validador WPF e os
três scripts `ferramentas/testar-portal-a1*.mjs` da interface.

Somente fontes e dados sintéticos foram enviados ao Jev. A credencial da API foi
lida localmente e usada apenas na autenticação da TypeSafe.

## Limites ainda aplicáveis

Os cenários reproduzidos nesta revisão foram corrigidos. Isso não comprova ausência
de todas as falhas possíveis. A cadeia ICP-Brasil, revogação, certificado A1 real e
infraestrutura precisam de homologação antes de ativar para a cliente. A validação
continua exigindo cadeia confiável instalada/disponível e raiz homologada; não foi
relaxada para acomodar certificados de teste. A hipótese sobre intermediários no
PFX não foi demonstrada como defeito nesta rodada. PostgreSQL de produção não foi
usado nos testes locais. Configuração e ativação: `docs/assinatura-a1.md`.
