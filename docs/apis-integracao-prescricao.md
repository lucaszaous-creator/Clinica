# APIs que dá para integrar, e quanto custa cada uma

Levantamento de agosto/2026, feito para responder à pergunta da cliente sobre receita
eletrônica. Preço de API muda; **os valores abaixo têm data e fonte**, e os que não são
públicos estão marcados como "sob contrato" em vez de estimados — chute com aparência de
orçamento é pior do que campo vazio (a mesma razão pela qual `PrecoConvenioService` deixa o
valor em branco quando não há tabela cadastrada).

## O pedido tem três resultados diferentes, e só um deles é caro

1. **A folha de infusão vale?** Já vale hoje. É prontuário interno, assinado com ICP-Brasil
   pela prescritora (`AssinaturaDigitalService`, parcela 42) e executado no papel pela
   enfermagem. Não passa por farmácia, não há órgão que valide. **Custo de API: zero.**
2. **A receita em papel vale?** Sempre valeu, e continua valendo — a ANVISA confirma que o
   receituário físico coexiste com o eletrônico. É o que a clínica faz hoje. **Custo: zero.**
3. **A receita que o paciente leva no celular e a farmácia dispensa.** Este é o único que
   precisa de coisa nova, e a peça cara **não é a assinatura** — é o **endpoint público de
   validação** que o farmacêutico consulta. Nosso sistema é desktop falando com o banco da
   própria clínica: não existe endereço na internet para a farmácia consultar.

O resto deste documento é sobre o item 3, mais as APIs adjacentes que apareceram no caminho
e que valem mais a pena do que ele.

---

## 1. Prescrição eletrônica com validação em farmácia

Aqui **não se constrói, se integra**. Construir o portal de validação é construir uma Memed —
e ainda ficaria faltando a rede de farmácias que reconhece o documento.

| API | O que resolve | Preço | Observação |
|---|---|---|---|
| **Mevo** (ex-Nexodata) | Prescrição assinada + rede de farmácias que valida e dispensa | **Sem cobrança de uso, sem taxa de adesão, sem cobrança de suporte** (declarado pela empresa) | Permite **manter a nossa interface** ("escolha entre manter a sua interface ou usar a nossa") — é o único que não nos obriga a entregar a tela |
| **Memed** (Sinapse) | Idem, maior base instalada (~150 mil médicos, 100 mi de receitas/ano, 350+ parceiros) | Uso da prescrição **gratuito** para o médico; integração de parceiro **sob contrato** (não publicado). Envio por WhatsApp da Memed: **a partir de R$ 59,90/mês** | A integração é front-end + back-end com API Key/Secret Key: na prática **a tela é deles** |
| **Receita Digital / Prescrição Eletrônica CFM** (CFM + ITI + CFF) | Receita, atestado, relatório, pedido de exame e receituário de controle especial, com validação da assinatura e do CRM no ato da dispensação | **Gratuita** | Não achei API pública documentada para prontuário de terceiros — hoje é plataforma para o médico usar direto. **Confirmar com o CFM antes de contar com ela.** |

**A conclusão prática:** a Mevo é a única das três que combina custo zero declarado com a
possibilidade de não abrir mão da nossa tela. É a primeira a procurar.

### O que muda no nosso código

Qualquer uma delas entra como **serviço novo na camada Application**, ao lado do
`PrescricaoService` — nunca dentro dele. A conferência de alergia
(`PrescricaoService.Conferir`, parcela 40) tem de continuar rodando **antes** do envio: ela
compara o que está sendo prescrito com o que a própria clínica anotou sobre aquele paciente,
e nenhuma dessas plataformas sabe disso, porque o prontuário é nosso.

Estimativa minha, não orçamento de fornecedor: **1 a 2 semanas** para a integração, mais o
que a homologação do parceiro levar.

---

## 2. Certificado digital — e a armadilha do nosso fluxo atual

`CertificadoIcpBrasil.DoRepositorioDoUsuario()` lê o `X509Store` do **usuário da máquina**, e
`DeArquivo` lê um `.pfx`. Ou seja: **o certificado precisa estar na máquina.**

| Opção | Preço | Serve ao nosso fluxo hoje? |
|---|---|---|
| **AR-CFM (certificado gratuito do CFM)** | **R$ 0** para médico com CRM ativo e CIM em policarbonato | **Não** — é **em nuvem** (Valid/VIDaaS, via app). Nosso código não fala com PSC |
| **Bird ID / Soluti** (e-CPF A3 em nuvem) | **R$ 34,90/mês** ou **R$ 149,90/ano** (certificado vale 5 anos, mas o plano tem de estar ativo para assinar) | Não, mesma razão |
| **VIDaaS / Valid** (e-CPF A3 nuvem, até 30 mil assinaturas/mês) | **R$ 49,00** — e-CNPJ equivalente R$ 199,00 | Não, mesma razão |
| **e-CPF A1/A3 em token ou arquivo** | R$ 150–400/ano, conforme a AC | **Sim, é o que funciona hoje** |
| **API de assinatura em nuvem** (VIDaaS/IntegraICP — OAuth 2.0 com PKCE, `single_signature` ou `signature_session`) | **Sob contrato**, não publicado | Exigiria refatorar a assinatura |
| **API de assinatura avançada gov.br** | **Gratuita** | Produz assinatura **avançada**, não qualificada — não substitui ICP-Brasil onde a farmácia exige |
| **Carimbo do tempo (ACT)** — Certisign, Valid, Bry, Serpro, Caixa, Prodesp | **Sob contrato**, vendido em pacote | Já suportado: RFC 3161 opcional em Configurações → Operação |

**O achado que importa:** existe certificado ICP-Brasil **de graça** para a médica (AR-CFM), e
o nosso sistema **não consegue usá-lo**, porque ele é em nuvem e nós exigimos chave local.
Isso é uma decisão de produto esperando para ser tomada: ou a clínica compra um certificado
que fica na máquina, ou refatoramos a assinatura para aceitar PSC em nuvem.

A refatoração não é grande, mas é estrutural: hoje o `AssinaturaDigitalService` monta o
`SignedCms` com a chave em mãos. Com PSC em nuvem o fluxo vira **calcular o hash aqui →
mandar assinar lá → colar o PKCS#7 de volta no PDF**, com a médica confirmando pelo celular.
O `Conferir` não muda (a conferência já é sobre os bytes), e a regra de comparar o **CPF de
dentro do certificado** (OID `2.16.76.1.3.1`) com `Profissional.Cpf` continua valendo tal e
qual — é ela que impede o e-CPF de outra pessoa assinar pela médica. Estimativa: **1 a 2
semanas**, e não vale começar sem o preço da API de PSC em mãos.

---

## 3. Medicamentos controlados — SNCR da ANVISA

Sem isso, receita eletrônica de controlado não funciona a partir de **30/09/2026** (prazo já
prorrogado uma vez, de 01/06/2026).

- **Preço: gratuito.** É sistema de governo.
- A ANVISA **publicou a documentação técnica da API em junho/2026**, com especificação,
  orientações de integração e **ambiente de treinamento para desenvolvedores** (página
  "Documentos do SNCR", no portal da ANVISA).
- Cobre numeração eletrônica de Notificação de Receita, Receita de Controle Especial e
  Receita Sujeita a Retenção.

**Só entra na conta se a clínica prescreve controlado.** Se a acupuntura e as demais
especialidades da casa não prescrevem, isto sai do escopo inteiro — e se prescrevem, o mais
provável é que venha resolvido pela Mevo/Memed, que precisam se integrar de qualquer jeito.
Confirmar isso com a cliente é a pergunta mais barata deste documento.

---

## 4. Base de medicamentos (o autocomplete que hoje não existe)

Nosso campo de receita é **texto livre**, por decisão — e é por isso que a conferência de
alergia compara por palavra inteira com piso de 4 caracteres. Uma base estruturada melhoraria
a receita e a conferência ao mesmo tempo.

| API | Preço | O que traz |
|---|---|---|
| **Dados abertos ANVISA / CMED** | **R$ 0** (CSV/planilha publicada) | Preço máximo e registro. Sem bula estruturada, sem DCB, e a atualização é manual |
| **Medicamentos API.br** | **Gratuita** (REST) | ANVISA + preço CMED + bulário |
| **Bulapi** | Base aberta | Substâncias, apresentações, preços CMED, classificação terapêutica |
| **PharmaDB** | Grátis 300 req/mês (**sem uso comercial**); **R$ 237/mês** no plano básico; faixas até 300 mil req/mês | 27 mil produtos, 5.700 princípios ativos DCB, 8.700 bulas, EAN, interações — em pt-BR |

Se formos pela Mevo/Memed, **isto vem junto** (as duas têm base própria de medicamentos) e
não se paga nada. Contratar PharmaDB só faz sentido se decidirmos **não** integrar prescrição
e ainda assim quisermos autocomplete — o que é a pior combinação das duas.

---

## 5. Certificação SBIS/CFM (S-RES) — só se a clínica quiser abandonar o papel

Isto **não** é requisito para prescrever nem para assinar. É requisito para o prontuário
**eletrônico substituir o arquivo físico** (NGS2). Enquanto a clínica imprime e guarda, não
precisa.

- A SBIS publica tabela de preços com **desconto por perfil de faturamento** (Perfil 0 até
  R$ 250 mil/ano; faixas até acima de R$ 5 mi) e **35% de desconto** em upgrade/renovação de
  sistema já certificado.
- **Os valores não estão acessíveis publicamente** (a página de preços responde 403 a acesso
  automatizado). Para orçamento real: `secretaria@sbis.org.br`.
- É auditoria paga, com estágios, e alcança o sistema inteiro — não só a prescrição.

Não coloquei número aqui de propósito.

---

## 6. As três APIs adjacentes que valem mais que o item 3

Apareceram na pesquisa e resolvem dor que a clínica **já tem hoje**, ao contrário da receita
eletrônica, que resolve uma que ela não tem (a receita em papel funciona).

| API | Preço | Por que vale |
|---|---|---|
| **WhatsApp Cloud API** (Meta) | Cobrança **por mensagem** desde jan/2026: *utility* **R$ 0,0340**, *authentication* R$ 0,0340, *marketing* **R$ 0,3125**, *service* (resposta dentro de 24h) **grátis**. Faturamento em reais pela entidade brasileira da Meta desde 01/07/2026 | `CampanhaService` já monta a mensagem e registra `ContatoCampanha` com idempotência por `Origem` — **o envio é que é um clique por paciente, à mão, no WhatsApp da clínica**. Confirmação de sessão e cobrança entram como *utility*: 500 confirmações/mês custam **~R$ 17**. Recall e NPS são *marketing* e exigem consentimento vigente, como já está implementado |
| **NFS-e Nacional** (ADN, governo federal) | **Gratuita** (exige certificado A1 do CNPJ da clínica; municípios aderentes ao padrão nacional). Gateways pagos existem se o município não aderiu | O Financeiro emite **recibo**, não nota fiscal. Hoje alguém digita a nota em outro site depois de fechar o caixa |
| **API de assinatura avançada gov.br** | **Gratuita** | Para atestado, comparecimento e relatório — onde não há farmacêutico exigindo assinatura qualificada — resolve com custo zero |

---

## Recomendação

**Caminho A — o barato, e o que eu faria.** A médica usa a Mevo ou a plataforma gratuita do
CFM para a receita que sai da clínica; o nosso sistema faz o que ninguém mais faz (folha de
infusão com checagem, prontuário, faturamento). **Custo de API: R$ 0.** Custo de
desenvolvimento: zero.

**Caminho B — o que tem melhor relação custo/benefício se for para mexer.** Integrar a
**Mevo** mantendo a nossa tela, com a conferência de alergia rodando antes do envio.
**R$ 0/mês** declarados, 1–2 semanas de trabalho, e resolve o item 3 inteiro — validação em
farmácia, base de medicamentos e (via parceiro) o caminho do SNCR.

**Caminho C — o caro.** Portal próprio de validação com QR, integração direta ao SNCR e
certificação SBIS. Isto é construir uma Memed com uma clínica de cliente. **Não recomendo**,
e o preço não é o problema principal: é que o documento só vale se a farmácia reconhecer o
emissor, e ninguém reconhece um endpoint novo.

**Independente do caminho:** assinar com ICP-Brasil os quatro documentos da feature 07
(receita, atestado, comparecimento, pedido de exame) continua valendo — o motor já está
pronto, é reuso do `AssinaturaDigitalService`, e melhora atestado e relatório, que o paciente
entrega em RH e convênio, onde não há farmacêutico exigindo verificação. E o **certificado
gratuito do CFM** torna essa conversa mais fácil, desde que a assinatura em nuvem entre no
mesmo pacote.

## As duas perguntas que faltam para fechar orçamento

1. **A clínica prescreve medicamento sujeito a controle especial?** Se não, o SNCR sai do
   escopo e o prazo de 30/09/2026 não é nosso.
2. **A médica já tem certificado, e de que tipo?** Se for o gratuito do CFM (nuvem), o sistema
   hoje **não o alcança**, e a assinatura em nuvem vira pré-requisito de tudo.

## Fontes

- [Memed — soluções para operadores de software](https://memed.com.br/parceiro-software/) ·
  [documentação de integração](https://doc.memed.com.br/integracao-rapida) ·
  [envio por WhatsApp Business](https://suporte-medico.memed.com.br/hc/pt-br/articles/8559532621851-Como-funciona-o-envio-de-prescri%C3%A7%C3%B5es-pelo-WhatsApp-Business-da-Memed)
- [Nexodata/Mevo — receitas e integração](https://medicos.nexodata.com.br/) ·
  [mudança de marca para Mevo](https://medicinasa.com.br/nexodata-mevo/)
- [Prescrição Eletrônica CFM](https://prescricaoeletronica.cfm.org.br/) ·
  [CFF/CRF-BA sobre a plataforma que conecta farmacêuticos, médicos e pacientes](https://www.crf-ba.org.br/conheca-a-nova-plataforma-de-prescricao-que-conecta-farmaceuticos-medicos-e-pacientes/)
- [AR-CFM — certificado digital gratuito](https://certificadodigital.cfm.org.br/) ·
  [anúncio do CFM](https://portal.cfm.org.br/noticias/cfm-inova-e-oferece-certificacao-digital-gratuito-aos-medicos-brasileiros/)
- [Soluti — Bird ID, planos](https://soluti.com.br/certificado-digital/bird-id/) ·
  [Valid — certificado em nuvem VIDaaS](https://validcertificadora.com.br/pages/certificado-em-nuvem) ·
  [Valid — integração via API (PSC)](https://validcertificadora.com.br/pages/psc-integracao-via-api)
- [ITI — Autoridades de Carimbo do Tempo](https://www.gov.br/iti/pt-br/assuntos/icp-brasil/autoridades-de-carimbo-do-tempo)
- [ANVISA — publicação da documentação técnica da API do SNCR](https://www.gov.br/anvisa/pt-br/assuntos/noticias-anvisa/anvisa-publica-documentacao-tecnica-para-integracao-de-sistemas-de-prescricao-eletronica-ao-sncr) ·
  [prorrogação para 30/09/2026](https://www.gov.br/anvisa/pt-br/assuntos/noticias-anvisa/2026/sncr-anvisa-inicia-etapa-de-integracao-com-sistemas-de-prescricao-eletronica-e-amplia-prazo-para-implementacao)
- [SBIS — tabelas de preços da certificação S-RES](https://sbis.org.br/certificacoes/certificacao-software/tabelas-de-precos-para-certificacao-de-s-res-sbis/) ·
  [Manual de Certificação S-RES v5.0](https://www.sbis.org.br/certificacao/Manual_Certificacao_S-RES_SBIS_v5-0.pdf)
- [PharmaDB — API de medicamentos](https://pharmadb.com.br/) ·
  [Medicamentos API.br](https://medicamentos.api.br/) · [Bulapi](https://bulapi.com.br/) ·
  [ANVISA — lista de preços CMED](https://www.gov.br/anvisa/pt-br/assuntos/medicamentos/cmed/precos)
- [Preço da WhatsApp Business API no Brasil em 2026](https://www.socialhub.pro/blog/preco-whatsapp-api-2026-brasil/) ·
  [mudanças de cobrança em 2026](https://www.aleguimas.com.br/blog/whatsapp-business-api-o-que-muda/)
- [Portal NFS-e — API de integração](https://www.gov.br/nfse/pt-br/municipios/produtos-disponiveis/api-de-integracao)
- [Manual de integração da API de assinatura avançada gov.br](https://manual-integracao-assinatura-eletronica.servicos.gov.br/pt-br/4.4/iniciarintegracao.html)
