# Coleta de termos sem redigitação — 07/10/2026

A preparação usa o paciente já selecionado e os modelos de termos. Não solicita
CPF, documento ou nascimento, inclusive quando o cadastro não contém esses dados.
O registro identifica a seleção pela equipe e a operadora autenticada. A assinatura,
as respostas e as vias históricas são preservadas. Não há migração de banco.

## Verificação executada

- 78 testes de PortalTabletTests e TermoAssinadoPeloPacienteTests aprovados.
- 3 testes HTTP de ColetaNaEvolucaoHttpTests e FronteiraHttpTests aprovados.
- Verificação estrutural: 222 XAML, 10 projetos e 136 construtores aprovados.
- Chromium com contrato simulado: coleta direta e embutida, payload mínimo,
  CSRF/token, seleção obrigatória, respostas, assinatura e isolamento aprovados.
- Chromium com API real em demonstração local: dois termos assinados e
  arquivados, alergias, rotação, texto ampliado, bloqueio das rotas da equipe no
  modo paciente e PDFs repetidos idênticos. Apenas dados fictícios.
- Pacote da interface validado: 296 arquivos e hashes conferidos.
- JEV 1.13.0 via TypeSafe: sete critérios `atende`. Evidência e hashes das
  fontes em [termos-sem-cpf-nascimento-jev.json](termos-sem-cpf-nascimento-jev.json).
  A auditoria é auxiliar e não substitui os testes.

## Implantação

Publicar primeiro a API, que aceita também o corpo antigo. Depois publicar a
interface do repositório clinica-site na branch `fix/termos-sem-cpf-nascimento`.
Não foram alterados o ambiente de produção nem os cadastros ou termos reais
durante a implementação e os testes. A aprovação no ambiente local não declara
que a atualização já foi instalada na clínica.
