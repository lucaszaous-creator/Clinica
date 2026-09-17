# Consultório: ficha, navegação e continuidade com o portal

## O que mudou

- A lista de pacientes oferece **Ficha** e **Atender**, inclusive fora da agenda. A rota da ficha agora abre a seção correta; antes caía em Atendimento.
- **Ficha do paciente** reúne Dados e alertas, Sessões e guias e Anamnese. Mostra contato, endereço, convênio, carteirinha, tratamento, alergias e problemas.
- Sessões são consultadas em páginas de 25, com data, profissional, modalidade, vínculo da evolução, estado do atendimento e quantidade de guias. Uma evolução avulsa não é vinculada por coincidência de data.
- **Prescrições e documentos** reúne receitas/documentos e infusões no paciente já aberto. O atalho de infusão permanece nesse contexto.
- O histórico tem espaço próprio para evoluções médicas e para enfermagem/infusões.
- Cabeçalhos e ferramentas quebram linha em janelas menores. Mapa corporal e Modelos deixam de ficar cortados; os editores têm rolagem e as ações de salvar/finalizar continuam no rodapé.
- Atualizar a ficha e as listas de documentos consulta os registros atuais sem recarregar a evolução em edição. A abertura dessas seções também atualiza a leitura.

## Integração e segurança

Portal e desktop usam os serviços e as tabelas clínicas compartilhadas. A nova consulta é somente de leitura, filtrada pelo paciente, sem rastreamento e paginada; não carrega o texto integral das evoluções nem arquivos binários para montar o resumo.

As permissões de leitura cadastral, leitura clínica, prescrição e edição continuam separadas. A enfermagem não recebe permissão de prescrição médica por acessar a nova aba. O acesso ao prontuário continua auditado.

Evolução salva, presença, encerramento clínico e guias são fatos distintos. A ficha não trata guia antecipada como sessão concluída nem cria atendimentos para preencher o histórico. Nenhuma migration foi acrescentada.

## Evidências locais

- 2.644 testes da suíte clínica aprovados, incluindo seis novas verificações de leitura portal/desktop, conclusão idempotente, isolamento de pacientes, paginação, estados da sessão e tradução da consulta para PostgreSQL.
- 9 testes da API de assinaturas aprovados. Build completo de `Clinica.sln` em Release no Windows, sem compilação incremental: zero erros (40 avisos preexistentes).
- Compilação-sombra dos 10 projetos WPF aprovada.
- Verificação estática: 179 XAML, 9 projetos e 127 construtores de ViewModel. Espelho dos 28 tokens de cores aprovado.
- Ensaio WPF com SQLite em memória e dados fictícios: ficha, paginação, navegação para infusões, preservação do rascunho durante atualização, leitura de alteração feita em outro contexto e permissões da enfermagem. Nenhum erro de binding capturado.
- Telas renderizadas em 1024 × 680 e 1366 × 768; conferidos cadastro, atendimento, enfermagem, sessões, documentos, infusões e lista de pacientes.

## Portal em produção

Em 17/09/2026, a leitura da VPS confirmou o serviço ativo e o pacote `tablet-continuidade-0af336113d7d-eea378e047f0`. O portal profissional e `/health` responderam HTTP 200; `/api/pacientes` sem autenticação respondeu 401. Permanecem os cabeçalhos `no-store`, CSP e HSTS. Homologação também respondeu 200 em `/health`.

Esta entrega altera o desktop e não exige reinstalar esse pacote na VPS. Os testes de escrita desta entrega usaram dados fictícios locais; não simulam aceite humano de atendimento real.

## Publicação e aceite

Publicar os canais Consultório (`clinico`) e Gerente Geral (`gerente`) após o CI. Os aplicativos instalados pelo Velopack consultam seu canal de atualização na abertura; cópia portátil não equivale a instalação com atualização automática.

Continua pendente o aceite assistido pela equipe: médico e enfermagem com seus próprios certificados SafeID, seguido da conferência do mesmo atendimento pela recepção/faturista. A indisponibilidade anterior dos titulares não é tratada como teste aprovado.
