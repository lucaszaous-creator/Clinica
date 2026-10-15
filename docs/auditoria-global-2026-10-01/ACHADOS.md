# Achados e propostas de simplificação

> **Escopo vigente — portal web preservado:** por decisão do proprietário em 01/10/2026, não alterar o portal web nem reunir evolução e consentimentos nele. Recomendações sobre portal neste inventário são históricas e não autorizam implementação. Ver [regra obrigatória, imagens desktop e cobertura](ESCOPO-E-PROPOSTAS-VISUAIS.md).

P1: tratar primeiro por induzir retrabalho/documentos duplicados ou impedir reprodução da integração. P2: corrigir percurso, ação ou organização. P3: clareza e manutenção. Prioridade é proposta desta auditoria, sem estimativa de frequência em produção.

“Confirmado” significa verificado no código ou na matriz indicada. Não significa reproduzido na instalação da clínica. O voto do Jev é revisão auxiliar, não validação independente de execução.

| ID | Prioridade | Área | Achado | Evidência |
| --- | --- | --- | --- | --- |
| [A01](#a01) | P1 | Documentos | Mensagem manda cancelar e reemitir para assinar depois | Confirmado no código |
| [A02](#a02) | P1 | Documentos | Repetir Emitir após falha posterior cria outra emissão | Confirmado no código |
| [A03](#a03) | P2 | Documentos | Impressão perde destaque quando há assinatura pendente | Confirmado no código |
| [A04](#a04) | P2 | Documentos | Emitir e imprimir executa Salvar como e abrir leitor externo | Confirmado no código |
| [A05](#a05) | P2 | Recepção e financeiro | Recibo na central aponta ao Caixa ausente na Recepção | Confirmado no código |
| [A06](#a06) | P2 | Documentos | Período de uma emissão é escolhido em outra aba | Confirmado no código |
| [A07](#a07) | P2 | Paciente | Sessões e guias repetido em duas camadas de abas | Confirmado no código |
| [A08](#a08) | P2 | Financeiro | Mesmo preço particular sob dois grupos no Gerente | Confirmado por matriz de perfis |
| [A09](#a09) | P2 | Navegação | Rótulos novos não chegam às abas compostas | Confirmado no código |
| [A10](#a10) | P2 | Financeiro | Financeiro abre em Estoque por efeito da ordenação | Confirmado por matriz de perfis |
| [A11](#a11) | P2 | Portal clínico | Documentos no menu é somente uma fila de assinatura | Confirmado no código |
| [A12](#a12) | P2 | Portal clínico | Pendências e Documentos repetem a fila de documentos sem assinatura | Confirmado no código |
| [A13](#a13) | P2 | Portal clínico | Áreas internas não têm navegação persistida na URL | Confirmado no código |
| [A14](#a14) | P2 | Portal clínico | Ações dos documentos mantidas em dois templates | Oportunidade com evidência estrutural |
| [A15](#a15) | P2 | Manutenção | Três interfaces sem referência de abertura encontrada | Confirmado por busca estática |
| [A16](#a16) | P2 | Paciente e termos | Instrução de erro usa nomes antigos de abas | Confirmado no código |
| [A17](#a17) | P1 | Versões | Pastas de trabalho e main representam sistemas diferentes | Lacuna de rastreabilidade |
| [A18](#a18) | P1 | CRM e clínica | Ponte canônica citada pelo CRM não está na main auditada de Clinica | Lacuna de integração nas bases principais |
| [A19](#a19) | P3 | Manutenção entre repositórios | CRM conserva 445 fontes também presentes em Clinica | Confirmado no código |
| [A20](#a20) | P2 | Enfermagem | Agenda, passagem e infusão têm entradas e vocabulário dispersos | Oportunidade com evidência estrutural |
| [A21](#a21) | P2 | Modelos | Modelos distribuídos por emissão, evolução, mapa e configuração | Oportunidade com evidência estrutural |
| [A22](#a22) | P2 | Gerente | Gerente ainda reúne 27 entradas antes das abas internas | Oportunidade apoiada pela matriz |
| [A23](#a23) | P2 | CRM | Meu dia, Visão geral e Operação dividem acompanhamento da fila | Oportunidade com evidência estrutural |
| [A24](#a24) | P2 | CRM e clínica | Concluir atendimento nomeia dois fatos diferentes | Confirmado no código |
| [A25](#a25) | P2 | CRM e relacionamento | Retorno, lembrete, tarefa e recall precisam de um mapa comum | Oportunidade com evidência estrutural |
| [A26](#a26) | P2 | CRM | Registrar solução de remarcação não confirma alteração na agenda | Confirmado no código |
| [A27](#a27) | P3 | Financeiro | Adiar conta oferece prazo fixo de sete dias | Confirmado no código |
| [A28](#a28) | P2 | Termos do paciente | Coleta pode atravessar escolhas em várias janelas | Oportunidade com evidência estrutural |
| [A29](#a29) | P2 | Documentos | Emissão pelo catálogo exige escolher antes de abrir o editor | Oportunidade com evidência estrutural |
| [A30](#a30) | P3 | Documentação e navegação | Comentários arquiteturais descrevem módulos que hoje são carregados | Confirmado no código |
| [A31](#a31) | P1 | Campanhas | Abrir WhatsApp é registrado como envio | Confirmado no código |
| [A32](#a32) | P2 | Faturamento | Atalho de glosa descarta o contexto da linha | Confirmado no código |
| [A33](#a33) | P2 | Configurações | Convênio fecha uma janela mas só salva na tela de trás | Confirmado no código |
| [A34](#a34) | P2 | Configurações | Catálogos misturam salvar em lote e excluir imediatamente | Confirmado no código |
| [A35](#a35) | P2 | Financeiro | Prévia da retenção apaga o resultado quando falha | Confirmado no código |
| [A36](#a36) | P2 | Financeiro | Recebi na inadimplência fixa a data em hoje | Confirmado no código |
| [A37](#a37) | P2 | Financeiro | Devolução de receita recebida exige reconstruir lançamento no Caixa | Oportunidade de simplificação |
| [A38](#a38) | P2 | Relacionamento | Chamar de volta não registra a tentativa nessa ação | Confirmado no código |
| [A39](#a39) | P2 | Campanhas | NPS de ontem deixa a recuperação de outros dias pouco acessível | Oportunidade de simplificação |
| [A40](#a40) | P2 | Cadastro | Busca vazia orienta trocar de página para cadastrar | Oportunidade de simplificação |
| [A41](#a41) | P2 | Clínico | Resumo de prontuário instrui abrir outra seção para trabalhar anexos | Oportunidade de simplificação |
| [A42](#a42) | P2 | Portal | Pendências usa uma página compartilhada entre filas diferentes | Confirmado no código |
| [A43](#a43) | P2 | Portal | Imprimir uma sessão de enfermagem busca todo o histórico paginado | Confirmado no código |
| [A44](#a44) | P2 | Portal de termos | Portal do paciente é uma entrada da equipe para coleta presencial | Oportunidade de clareza |
| [A45](#a45) | P2 | Portal de termos | Busca de pacientes atende somente BSV agendado hoje | Limitação funcional explícita |
| [A46](#a46) | P2 | Site | Agendar consulta do cabeçalho abre página intermediária | Oportunidade de simplificação |
| [A47](#a47) | P2 | CRM | Concluir conversa muda a lista para concluídos | Confirmado no código |
| [A48](#a48) | P1 | CRM | Falha de atualização após concluir escreve erro em diálogo já fechado | Confirmado no código |
| [A49](#a49) | P2 | CRM | Modelos WhatsApp necessários ficam em Mais ações | Oportunidade de simplificação |
| [A50](#a50) | P2 | CRM | Alerta de espera mistura tempo corrido e expediente | Confirmado no código |
| [A51](#a51) | P2 | CRM | Listas cortadas não oferecem paginação no componente | Confirmado no código |
| [A52](#a52) | P2 | Web de consulta | Prontuário web termina nas 20 sessões mais recentes | Limitação funcional explícita |
| [A53](#a53) | P2 | Navegação | Pesquisa de seções depende de acentos e dos nomes exatos | Confirmado no código |
| [A54](#a54) | P2 | Navegação | Pesquisa sem resultados fecha o painel de resultados | Confirmado no código |
| [A55](#a55) | P3 | Treinamento | Progresso das aulas é local à máquina | Limitação funcional explícita |
| [A56](#a56) | P2 | Relacionamento | Seção CRM da ficha apresenta contatos de campanha | Confirmado no código |
| [A57](#a57) | P3 | Privacidade | Exportar meus dados usa a voz do paciente em tela da equipe | Confirmado no código |
| [A58](#a58) | P2 | Financeiro | Pix presencial oferece copia e cola, sem QR nessa janela | Oportunidade de simplificação |
| [A59](#a59) | P2 | Integrações | Teste de publicação deixa limpeza para o painel do provedor | Oportunidade de simplificação |
| [A60](#a60) | P2 | Acesso | Troca de usuário exige reiniciar a aplicação | Oportunidade de simplificação |
| [A61](#a61) | P2 | Exames | Estado vazio reúne nenhum pedido e todos respondidos | Oportunidade de clareza |
| [A62](#a62) | P2 | Recepção | Pagamentos vazio instrui voltar ao fechamento ou à venda | Oportunidade de simplificação |
| [A63](#a63) | P3 | Nomenclatura | Acompanhamento designa cuidado clínico e retorno comercial | Oportunidade de clareza |
| [A64](#a64) | P1 | Faturamento | Guias sem resposta no XML permanecem com decisão de aceite | Confirmado no código |
| [A65](#a65) | P1 | Faturamento | Baixar XML como segunda via regenera com dados atuais | Confirmado no código |
| [A66](#a66) | P2 | Faturamento | Validação TISS não expõe separadamente XSD não executado | Confirmado no código |
| [A67](#a67) | P2 | Faturamento | Rodada vencida bloqueia a janela até decidir todas as guias | Oportunidade de redesenho de regra |
| [A68](#a68) | P2 | Faturamento | Radar interrompe exportação com texto, sem abrir a correção | Oportunidade de simplificação |
| [A69](#a69) | P3 | Ajuda | Ajuda exige localizar manualmente arquivos de diagnóstico | Oportunidade de simplificação |
| [A70](#a70) | P3 | Nomenclatura | Ajuda promete que falha nunca aparece como sucesso | Confirmado no código |

<a id="a01"></a>
## A01 — Mensagem manda cancelar e reemitir para assinar depois

**P1 · Confirmado no código · Documentos**

**Hoje:** Ao desistir do seletor de certificado após emitir, a mensagem orienta: para assinar depois, cancele e emita outro. A central já chama AcoesDoDocumento.AssinarAsync sobre o documento existente.

**Efeito:** Induz cancelamento e nova emissão desnecessários, aumentando documentos e trabalho.

**Proposta:** Mostrar o número emitido e oferecer Assinar este documento, Imprimir e Fechar. Reemissão deve ser reservada à correção de conteúdo.

**Critério de aceite para a correção:** Cancelar o seletor e retomar a assinatura conserva DocumentoId e número, sem exigir cancelamento.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs:960](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs#L960); [Clinica/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs:1138](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs#L1138).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.96. A confiança não mede incidência ou certeza de um defeito.

<a id="a02"></a>
## A02 — Repetir Emitir após falha posterior cria outra emissão

**P1 · Confirmado no código · Documentos**

**Hoje:** EmitirAsync persiste antes de assinar/abrir o PDF. Cancelamento de certificado ou erro de abertura retorna mantendo a janela. O finally libera Emitindo; PodeEmitir não verifica DocumentoEmitidoId. Um novo clique chama novamente DocumentoClinicoService.EmitirAsync, que numera uma nova entidade.

**Efeito:** Uma tentativa de terminar assinatura ou impressão pode gerar outro documento do mesmo conteúdo.

**Proposta:** Após persistir, mudar a janela para o estado Documento emitido e retomar impressão/assinatura pelo ID. Nova emissão deve ser uma ação explícita.

**Critério de aceite para a correção:** Falhar após persistência, clicar novamente e confirmar que permanece exatamente um documento; ensaio futuro em banco fictício.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs:207](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs#L207); [Clinica/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs:868](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs#L868); [Clinica/src/Clinica.Application/Servicos/DocumentoClinicoService.cs:111](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Application/Servicos/DocumentoClinicoService.cs#L111).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.39. A confiança não mede incidência ou certeza de um defeito.

<a id="a03"></a>
## A03 — Impressão perde destaque quando há assinatura pendente

**P2 · Confirmado no código · Documentos**

**Hoje:** Na lista O que já saiu, Assinar aparece quando PodeAssinar é verdadeiro; nesse mesmo estado, 2ª via fica Collapsed. Imprimir continua disponível em ⋯ → Imprimir a 2ª via. O texto literal Solicitar assinatura do exemplo do usuário não foi localizado nas bases principais.

**Efeito:** Quem quer entregar papel precisa descobrir um menu secundário mesmo quando esse é seu próximo passo.

**Proposta:** Oferecer Imprimir claramente na linha ou no resultado da emissão, mantendo assinatura digital como ação distinta e condicionada ao tipo de documento.

**Critério de aceite para a correção:** Documento emitido sem assinatura oferece Imprimir sem passar por ⋯; assinatura e consentimento continuam separados.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml:645](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml#L645); [Clinica/src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml:665](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml#L665); [Clinica/src/Clinica.Desktop.Shell/Componentes/MenuDoDocumento.cs:71](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/MenuDoDocumento.cs#L71).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.76. A confiança não mede incidência ou certeza de um defeito.

<a id="a04"></a>
## A04 — Emitir e imprimir executa Salvar como e abrir leitor externo

**P2 · Confirmado no código · Documentos**

**Hoje:** O botão Emitir e imprimir termina em ImpressaoPdf.SalvarEAbrirAsync: solicita caminho, salva e abre o leitor padrão. Não envia diretamente à impressão. Cancelar Salvar como retorna null, igual ao sucesso, e a emissão pode encerrar a janela.

**Efeito:** A pessoa espera imprimir e recebe uma escolha de arquivo; impressão, download e cancelamento não têm resultados distintos na interface.

**Proposta:** Separar Imprimir de Baixar PDF; distinguir emitido, arquivo salvo, impressão solicitada e cancelamento. Reutilizar o documento já emitido.

**Critério de aceite para a correção:** Cancelar download não aparece como impressão concluída; Imprimir abre a interface de impressão, sem obrigar a nomear arquivo.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/DocumentoWindow.xaml:41](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoWindow.xaml#L41); [Clinica/src/Clinica.Desktop.Shell/Componentes/ImpressaoPdf.cs:39](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ImpressaoPdf.cs#L39); [Clinica/src/Clinica.Desktop.Shell/Componentes/ImpressaoPdf.cs:53](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ImpressaoPdf.cs#L53).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.45. A confiança não mede incidência ou certeza de um defeito.

<a id="a05"></a>
## A05 — Recibo na central aponta ao Caixa ausente na Recepção

**P2 · Confirmado no código · Recepção e financeiro**

**Hoje:** O catálogo de Documentos exige NavegacaoSuite.Existe(Caixa) para recibos. A Recepção carrega Recepção e Clínico contextual, sem Caixa. Entretanto, Recebimentos de pacientes já permite emitir recibo de um lançamento recebido.

**Efeito:** O mesmo aplicativo informa indisponibilidade em Documentos e oferece a função em outra área.

**Proposta:** Encaminhar Recibo para os recebimentos do paciente selecionado, preservando o lançamento de origem e sem liberar acesso amplo ao Caixa.

**Critério de aceite para a correção:** Perfil Recepção chega ao recibo de um pagamento permitido a partir de Documentos sem trocar de aplicativo.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs:563](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs#L563); [Clinica/src/Clinica.Modulo.Recepcao/ViewModels/PagamentosViewModel.cs:96](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/ViewModels/PagamentosViewModel.cs#L96); [Clinica/src/Clinica.Recepcao/App.xaml.cs:26](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Recepcao/App.xaml.cs#L26).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.82. A confiança não mede incidência ou certeza de um defeito.

<a id="a06"></a>
## A06 — Período de uma emissão é escolhido em outra aba

**P2 · Confirmado no código · Documentos**

**Hoje:** Fechamento do período usa Inicio/Fim da aba O que já saiu, embora a ação seja escolhida na aba Emitir. A própria pendência explica que se deve usar o período da outra aba.

**Efeito:** Dependência escondida entre emissão e consulta histórica; é preciso sair do contexto para ajustar a emissão.

**Proposta:** Pedir o período junto da prévia de Fechamento, sem depender do filtro da consulta de emitidos.

**Critério de aceite para a correção:** É possível selecionar e conferir o período antes de emitir, na mesma sequência de trabalho.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs:586](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs#L586); [Clinica/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs:1041](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs#L1041).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.45. A confiança não mede incidência ou certeza de um defeito.

<a id="a07"></a>
## A07 — Sessões e guias repetido em duas camadas de abas

**P2 · Confirmado no código · Paciente**

**Hoje:** Ficha do paciente contém TabItem Sessões e guias com outro TabControl cujo primeiro TabItem também se chama Sessões e guias. Autorizações e validade do convênio é a segunda aba interna.

**Efeito:** A pessoa atravessa e interpreta dois níveis com o mesmo nome.

**Proposta:** Usar um único nível para Sessões e guias e Autorizações, preservando a permissão administrativa.

**Critério de aceite para a correção:** A ficha tem apenas uma seleção chamada Sessões e guias e autorização fica alcançável no mesmo nível.

**Evidências:** [Clinica/src/Clinica.Modulo.Clinico/Views/PacienteView.xaml:20](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PacienteView.xaml#L20).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.94. A confiança não mede incidência ou certeza de um defeito.

<a id="a08"></a>
## A08 — Mesmo preço particular sob dois grupos no Gerente

**P2 · Confirmado por matriz de perfis · Financeiro**

**Hoje:** A chave precos-particular é reivindicada por Particular e pacotes e Tabela de preço. A matriz reproduziu ambos os pais no Gerente para os perfis Gerente e Financeiro; a navegação por chave escolhe o primeiro pai.

**Efeito:** Dois endereços aparentes para o mesmo cadastro; o caminho de retorno pode não ser o que a pessoa esperava.

**Proposta:** Definir uma página canônica Preços; manter atalho contextual explícito em Pacotes, se necessário.

**Critério de aceite para a correção:** A chave tem um pai canônico e o atalho mantém contexto e identificação de retorno.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/Modulo/ModuloRecepcao.cs:342](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Modulo/ModuloRecepcao.cs#L342); [Clinica/src/Clinica.Modulo.Gerente/Modulo/ModuloGerente.cs:123](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Modulo/ModuloGerente.cs#L123); [Clinica/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs:328](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs#L328).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.28. A confiança não mede incidência ou certeza de um defeito.

<a id="a09"></a>
## A09 — Rótulos novos não chegam às abas compostas

**P2 · Confirmado no código · Navegação**

**Hoje:** OrganizacaoNavegacao renomeia acessos, extrato-banco, conciliacao e resultado. Abas dos módulos continuam com Acessos, Extrato do banco, Conciliação e Resultado do mês. AbaMenu possui rótulo independente do ItemMenuModulo.

**Efeito:** Busca, página e caminho exibido usam vocabulários diferentes para o mesmo destino.

**Proposta:** Resolver o título canônico por chave e permitir variação somente quando ela explicar um contexto real.

**Critério de aceite para a correção:** Menu, abas, breadcrumb e busca concordam para cada uma das quatro chaves.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:49](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs#L49); [Clinica/src/Clinica.Modulo.Financeiro/Modulo/ModuloFinanceiro.cs:107](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Modulo/ModuloFinanceiro.cs#L107); [Clinica/src/Clinica.Modulo.Gerente/Modulo/ModuloGerente.cs:192](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Modulo/ModuloGerente.cs#L192).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.55. A confiança não mede incidência ou certeza de um defeito.

<a id="a10"></a>
## A10 — Financeiro abre em Estoque por efeito da ordenação

**P2 · Confirmado por matriz de perfis · Financeiro**

**Hoje:** Estoque foi movido para Gestão e não há item financeiro marcado Inicial. O shell escolhe o primeiro item visível por grupo. Na matriz, Financeiro com perfil Financeiro ou Gerente abre em estoque.

**Efeito:** A abertura do aplicativo de dinheiro passa a ser um cadastro/operação de materiais, sem decisão explícita de página inicial.

**Proposta:** Definir uma abertura financeira explícita, com tarefas de recebimento/caixa, e manter Estoque acessível.

**Critério de aceite para a correção:** A abertura é estável quando um novo item de Gestão é acrescentado; validada para os perfis financeiros.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:37](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs#L37); [Clinica/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs:247](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs#L247); [Clinica/src/Clinica.Financeiro/App.xaml.cs:18](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Financeiro/App.xaml.cs#L18).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.34. A confiança não mede incidência ou certeza de um defeito.

<a id="a11"></a>
## A11 — Documentos no menu é somente uma fila de assinatura

**P2 · Confirmado no código · Portal clínico**

**Hoje:** O menu diz Documentos; a página diz Documentos para assinar e consulta pendências próprias. Emitidos ficam na ficha do paciente. A central Windows usa Documentos para emitir e consultar.

**Efeito:** A mesma palavra promete abrangências diferentes entre interfaces; localizar uma via exige mudar de área e buscar paciente.

**Proposta:** Renomear a fila para Para assinar ou oferecer dentro de Documentos as visões Para assinar e Emitidos com filtro por paciente.

**Critério de aceite para a correção:** A pessoa consegue distinguir pendência de acervo pelo menu e alcançar a via sem tentativa e erro.

**Evidências:** [clinica-site/portal/profissional/index.html:35](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/index.html#L35); [clinica-site/portal/profissional/posto.js:219](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js#L219).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.35. A confiança não mede incidência ou certeza de um defeito.

<a id="a12"></a>
## A12 — Pendências e Documentos repetem a fila de documentos sem assinatura

**P2 · Confirmado no código · Portal clínico**

**Hoje:** documentos() e pendencias() consultam /posto/pendencias; ambas exibem r.documentos e levam à ficha-documentos. Uma é uma fila especializada; outra é um agregador.

**Efeito:** O usuário pode interpretar duas filas como obrigações diferentes e procurar diferenças inexistentes.

**Proposta:** Preservar a visão agregada como resumo com contador e link para a fila canônica; usar os mesmos filtros e rótulos.

**Critério de aceite para a correção:** A mesma pendência tem identidade, contagem e ação consistentes nas duas entradas, sem manutenção de duas listas completas.

**Evidências:** [clinica-site/portal/profissional/posto.js:219](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js#L219); [clinica-site/portal/profissional/posto.js:225](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js#L225).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.41. A confiança não mede incidência ou certeza de um defeito.

<a id="a13"></a>
## A13 — Áreas internas não têm navegação persistida na URL

**P2 · Confirmado no código · Portal clínico**

**Hoje:** Agenda, ficha, documentos e infusão mudam estado.pagina e DOM. Não foi encontrado roteador pushState/popstate para essas áreas; o replaceState existente trata retorno SafeID. O CRM já possui roteador próprio com hash e histórico.

**Efeito:** Atualizar a página, usar Voltar do navegador ou retomar um endereço não representa o percurso interno. Isso deve ser confirmado em ensaio de navegador.

**Proposta:** Criar rotas por área e identificador autorizado, com guarda de rascunho e revalidação de acesso.

**Critério de aceite para a correção:** Voltar/Avançar e recarga recuperam a área correta sem expor dados na URL nem descartar alterações silenciosamente.

**Evidências:** [clinica-site/portal/profissional/clinico.js:16](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/clinico.js#L16); [clinica-site/portal/profissional/clinico.js:97](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/clinico.js#L97); [semdor-crm/src/Clinica.Crm/wwwroot/workspace-navigation.js:3](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/workspace-navigation.js#L3).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.31. A confiança não mede incidência ou certeza de um defeito.

<a id="a14"></a>
## A14 — Ações dos documentos mantidas em dois templates

**P2 · Oportunidade com evidência estrutural · Portal clínico**

**Hoje:** clinico.js renderiza documentos no atendimento; posto.js renderiza na ficha. Ambos montam Abrir PDF, Corrigir rascunho, Cancelar rascunho, Copiar e Assinar com SafeID com condicionais próprios.

**Efeito:** A correção de próxima ação precisa acompanhar duas implementações. Não foi demonstrada perda de dados entre elas.

**Proposta:** Compartilhar um componente/lista de ações de documento, mantendo contexto de sessão versus paciente explícito.

**Critério de aceite para a correção:** Mesma situação e permissão geram as mesmas ações nos dois locais; atalhos contextuais continuam disponíveis.

**Evidências:** [clinica-site/portal/profissional/clinico.js:127](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/clinico.js#L127); [clinica-site/portal/profissional/posto.js:145](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js#L145).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.78. A confiança não mede incidência ou certeza de um defeito.

<a id="a15"></a>
## A15 — Três interfaces sem referência de abertura encontrada

**P2 · Confirmado por busca estática · Manutenção**

**Hoje:** ProntuarioView.xaml da Recepção, RetornoView.xaml e BaixaGuiaWindow.xaml permanecem no código, mas não têm referência de uso fora da própria definição/code-behind na busca de fontes. A fábrica atual usa AcompanhamentoView para retorno-pacientes.

**Efeito:** Raspagens e manutenção podem tratar interfaces antigas como atuais; aumentam o volume de código e a ambiguidade.

**Proposta:** Confirmar ausência de carga por reflexão/recursos externos e então remover ou identificar explicitamente como legado.

**Critério de aceite para a correção:** Cada interface possui uma entrada documentada e verificável, ou sai da compilação/catálogo ativo.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/Modulo/ModuloRecepcao.cs:495](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Modulo/ModuloRecepcao.cs#L495); [Clinica/src/Clinica.Modulo.Recepcao/Views/RetornoView.xaml:1](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RetornoView.xaml#L1); [Clinica/src/Clinica.Modulo.Recepcao/Views/ProntuarioView.xaml:1](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ProntuarioView.xaml#L1); [Clinica/src/Clinica.Modulo.Faturamento/Alertas/BaixaGuiaWindow.xaml:1](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/BaixaGuiaWindow.xaml#L1).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.37. A confiança não mede incidência ou certeza de um defeito.

<a id="a16"></a>
## A16 — Instrução de erro usa nomes antigos de abas

**P2 · Confirmado no código · Paciente e termos**

**Hoje:** DocumentoEdicaoViewModel orienta colher consentimento na aba LGPD e termo na aba Documentos. A ficha atual mostra Privacidade e consentimentos e Termos do paciente.

**Efeito:** Uma instrução aparentemente precisa manda procurar rótulos que mudaram.

**Proposta:** Usar o nome atual e oferecer encaminhamento com paciente preservado; não depender de texto de navegação escrito à mão.

**Critério de aceite para a correção:** As mensagens de orientação apontam à aba existente no aplicativo/perfil e o link abre o paciente correto.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs:456](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs#L456); [Clinica/src/Clinica.Modulo.Clinico/Views/PacienteView.xaml:24](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PacienteView.xaml#L24).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.99. A confiança não mede incidência ou certeza de um defeito.

<a id="a17"></a>
## A17 — Pastas de trabalho e main representam sistemas diferentes

**P1 · Lacuna de rastreabilidade · Versões**

**Hoje:** A pasta Clinica original tem um commit próprio e está 77 commits atrás de origin/main, além de mudanças locais. clinica-site tem quatro commits locais adicionais com correções de execução e SafeID. A auditoria principal congela as três mains; os deltas foram inventariados separadamente.

**Efeito:** É possível mapear, corrigir ou publicar uma versão diferente da que a equipe acredita usar.

**Proposta:** Estabelecer matriz por componente: commit aprovado, pacote publicado, ambiente e mudanças pendentes. Não deduzir produção a partir do checkout.

**Critério de aceite para a correção:** Cada superfície implantada pode ser ligada a um commit e a um pacote, e os quatro commits do portal têm destino explícito.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:4](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs#L4).

**Revisão auxiliar Jev:** `nao_sustentado`, confiança retornada 0.74. A confiança não mede incidência ou certeza de um defeito.

**Nota editorial após a revisão:** o título se refere a revisões diferentes do código, não a sistemas comprovadamente diferentes em produção. O trecho de navegação acima não comprova a divergência Git. A evidência apropriada é [VERSOES-E-COBERTURA.md](VERSOES-E-COBERTURA.md) e [versoes-locais.json](versoes-locais.json). O voto não sustentado foi preservado; produção permanece não verificada.

<a id="a18"></a>
## A18 — Ponte canônica citada pelo CRM não está na main auditada de Clinica

**P1 · Lacuna de integração nas bases principais · CRM e clínica**

**Hoje:** README do CRM afirma que a ponte canônica foi preparada em Clinica e que sua implantação está pendente. O projeto histórico do CRM bloqueia Publish. A árvore main de Clinica auditada não contém src/Clinica.Crm.Bridge; o caminho local citado pelos scripts antigos também não foi encontrado.

**Efeito:** A partir somente dessas mains não se reproduz a entrega completa da integração de agenda, reservas e envios. Isso não prova indisponibilidade de uma implantação externa.

**Proposta:** Localizar e integrar o commit da ponte canônica, contratos e migrações; registrar o estado real de homologação e implantação.

**Critério de aceite para a correção:** As mains oferecem um caminho único e reproduzível para a ponte requerida pelo CRM, sem publicar a cópia histórica.

**Evidências:** [semdor-crm/README.md:91](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/README.md#L91); [semdor-crm/src/Clinica.Crm.Bridge/Clinica.Crm.Bridge.csproj:7](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm.Bridge/Clinica.Crm.Bridge.csproj#L7); [semdor-crm/src/Clinica.Crm/ClinicConnector.cs:52](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/ClinicConnector.cs#L52).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.28. A confiança não mede incidência ou certeza de um defeito.

<a id="a19"></a>
## A19 — CRM conserva 445 fontes também presentes em Clinica

**P3 · Confirmado no código · Manutenção entre repositórios**

**Hoje:** A comparação encontrou 445 caminhos de código compartilhados: 442 iguais e três divergentes. O README identifica bibliotecas e ponte como históricas para regressão; o projeto principal Clinica.Crm não referencia essas bibliotecas, e a ponte histórica tem publicação bloqueada.

**Efeito:** Risco de corrigir a cópia histórica, confundir rotas legadas com ativas e inflar o inventário. Não foi concluído que duas versões estejam em produção.

**Proposta:** Isolar claramente o acervo de regressão e documentar sincronização/aposentadoria; aplicar novas alterações no componente canônico.

**Critério de aceite para a correção:** Cada cópia tem finalidade e proprietário; nenhuma ferramenta de release escolhe a árvore histórica por engano.

**Evidências:** [semdor-crm/README.md:86](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/README.md#L86); [semdor-crm/src/Clinica.Crm/Clinica.Crm.csproj:7](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/Clinica.Crm.csproj#L7); [semdor-crm/src/Clinica.Crm.Bridge/Clinica.Crm.Bridge.csproj:7](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm.Bridge/Clinica.Crm.Bridge.csproj#L7).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.46. A confiança não mede incidência ou certeza de um defeito.

<a id="a20"></a>
## A20 — Agenda, passagem e infusão têm entradas e vocabulário dispersos

**P2 · Oportunidade com evidência estrutural · Enfermagem**

**Hoje:** Há Sessões de enfermagem, Enfermagem/Passagens, Atendimento de enfermagem, Sala/Fila de infusão e ficha do paciente. São tarefas relacionadas, mas não equivalentes: chegada/observações, registro longitudinal e execução de prescrição.

**Efeito:** A equipe precisa conhecer a diferença entre telas e termos antes de escolher o próximo passo.

**Proposta:** Usar uma entrada de trabalho da enfermagem com contexto de paciente/sessão e próximas ações por estado; manter histórico e fila especializada como visões identificadas.

**Critério de aceite para a correção:** De chegada até registro após aplicação e assinatura, é possível continuar pelo mesmo paciente sem nova busca e sem misturar atos distintos.

**Evidências:** [Clinica/src/Clinica.Modulo.Clinico/Modulo/ModuloClinico.cs:236](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Modulo/ModuloClinico.cs#L236); [Clinica/src/Clinica.Modulo.Clinico/Views/AtendimentoEnfermagemView.xaml:54](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AtendimentoEnfermagemView.xaml#L54); [clinica-site/portal/profissional/index.html:34](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/index.html#L34).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.28. A confiança não mede incidência ou certeza de um defeito.

<a id="a21"></a>
## A21 — Modelos distribuídos por emissão, evolução, mapa e configuração

**P2 · Oportunidade com evidência estrutural · Modelos**

**Hoje:** DocumentoWindow gerencia modelos de documento; ModelosEvolucaoWindow trata evolução; MapaCorporalControl tem gerenciar modelos; Configurações abre termos. O portal tem Modelos e aplicação contextual.

**Efeito:** Quem procura administrar um modelo precisa primeiro adivinhar a família e o local de manutenção.

**Proposta:** Criar catálogo administrativo por tipo e escopo, com atalhos de aplicar/salvar no contexto clínico. Não fundir modelo de consentimento com prescrição ou mapa.

**Critério de aceite para a correção:** Busca por tipo/nome encontra todos os modelos autorizados e informa onde são utilizados.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/DocumentoWindow.xaml:198](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoWindow.xaml#L198); [Clinica/src/Clinica.Desktop.Shell/Componentes/MapaCorporalControl.xaml:60](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/MapaCorporalControl.xaml#L60); [Clinica/src/Clinica.Modulo.Gerente/Views/ConfiguracoesView.xaml:119](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/ConfiguracoesView.xaml#L119); [clinica-site/portal/profissional/modelos.js:1](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/modelos.js#L1).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.94. A confiança não mede incidência ou certeza de um defeito.

<a id="a22"></a>
## A22 — Gerente ainda reúne 27 entradas antes das abas internas

**P2 · Oportunidade apoiada pela matriz · Gerente**

**Hoje:** Com perfil Gerente, a matriz deriva 27 entradas principais e 92 destinos autorizados, além de Treinamento acrescentado pelo shell. As entradas se distribuem em cinco grupos; não são 27 botões necessariamente visíveis simultaneamente.

**Efeito:** Persistem muitas decisões de navegação, inclusive sobre paciente, prontuário, prescrições e documentos parcialmente relacionados.

**Proposta:** Definir donos de tarefas e destinos canônicos; manter indicadores como atalhos para a tarefa, reduzindo categorias que apenas reorganizam outras telas.

**Critério de aceite para a correção:** Validar tarefas reais com gestão e reduzir mudanças de contexto sem retirar funções ou permissões.

**Evidências:** [Clinica/src/Clinica.Gerente/App.xaml.cs:29](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Gerente/App.xaml.cs#L29); [Clinica/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs:216](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs#L216).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.50. A confiança não mede incidência ou certeza de um defeito.

<a id="a23"></a>
## A23 — Meu dia, Visão geral e Operação dividem acompanhamento da fila

**P2 · Oportunidade com evidência estrutural · CRM**

**Hoje:** Meu dia apresenta retornos/esperas/consultas; Visão geral mostra distribuição e equipe; Operação mostra saúde, esperas, pedidos de remarcação e consumo. Relatórios acrescenta outra leitura histórica.

**Efeito:** Perguntas próximas ficam em áreas distintas, com nomes amplos. Não são cópias exatas: períodos, finalidade e perfis diferem.

**Proposta:** Dar a cada área uma pergunta explícita: meu trabalho, supervisão ao vivo, diagnóstico técnico/consumo e histórico. Oferecer links com o filtro preservado.

**Critério de aceite para a correção:** Uma atendente encontra sua próxima ação e uma supervisora encontra filas travadas sem percorrer os quatro painéis.

**Evidências:** [semdor-crm/src/Clinica.Crm/wwwroot/index.html:57](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/index.html#L57); [semdor-crm/src/Clinica.Crm/wwwroot/index.html:86](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/index.html#L86); [semdor-crm/src/Clinica.Crm/wwwroot/index.html:29](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/index.html#L29).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.40. A confiança não mede incidência ou certeza de um defeito.

<a id="a24"></a>
## A24 — Concluir atendimento nomeia dois fatos diferentes

**P2 · Confirmado no código · CRM e clínica**

**Hoje:** No CRM, Concluir atendimento registra resultado de uma conversa e pode criar tarefa. No sistema clínico, Concluir atendimento/sessão se refere ao atendimento assistencial. Não é a mesma conclusão e não há autorização para fundi-las.

**Efeito:** A equipe que alterna produtos pode interpretar conversa concluída como consulta concluída ou vice-versa.

**Proposta:** Usar Concluir conversa no CRM e Concluir sessão clínica no assistencial; exibir o estado do outro sistema como contexto, quando disponível.

**Critério de aceite para a correção:** Concluir conversa não aparenta marcar presença, encerrar sessão ou faturar; os rótulos explicam a diferença.

**Evidências:** [semdor-crm/src/Clinica.Crm/wwwroot/index.html:116](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/index.html#L116); [Clinica/src/Clinica.Modulo.Recepcao/Janelas/FechamentoSessaoWindow.xaml:4](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/FechamentoSessaoWindow.xaml#L4).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.62. A confiança não mede incidência ou certeza de um defeito.

<a id="a25"></a>
## A25 — Retorno, lembrete, tarefa e recall precisam de um mapa comum

**P2 · Oportunidade com evidência estrutural · CRM e relacionamento**

**Hoje:** CRM mantém FollowupState/ResumeAt, tarefas e recall; Clinica tem retornos solicitados e Acompanhamento com recall/novos BSV. Há integração de snapshots/notificações no código, portanto não é correto afirmar ausência total de integração.

**Efeito:** O mesmo paciente pode aparecer em filas com finalidades distintas sem que o operador reconheça quem é responsável pelo próximo contato.

**Proposta:** Mostrar tipo, origem, responsável, prazo e última ação num contexto único do paciente/conversa; reconciliar contatos após envio e resposta.

**Critério de aceite para a correção:** Uma tarefa manual e um recall não são tratados como duplicatas automaticamente; a equipe consegue identificar quando representam o mesmo contato planejado.

**Evidências:** [semdor-crm/src/Clinica.Crm/Models.cs:38](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/Models.cs#L38); [semdor-crm/src/Clinica.Crm/PatientContext.cs:19](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/PatientContext.cs#L19); [Clinica/src/Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml:19](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml#L19).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.37. A confiança não mede incidência ou certeza de um defeito.

<a id="a26"></a>
## A26 — Registrar solução de remarcação não confirma alteração na agenda

**P2 · Confirmado no código · CRM**

**Hoje:** Na Operação, Registrar solução encerra um job attention após texto livre. ResolveReschedule grava done e auditoria; não chama a ponte. A remarcação real usa ClinicScheduling e confirma o resultado na clínica.

**Efeito:** São duas conclusões diferentes para um assunto com o mesmo nome; uma observação administrativa pode ser confundida com a remarcação realizada.

**Proposta:** Nomear como Registrar tratamento do pedido e mostrar resultado separado: remarcado com ID, cancelado, desistiu ou resolvido fora do sistema com justificativa.

**Critério de aceite para a correção:** Encerrar o pedido manualmente nunca aparece como horário remarcado sem evidência da agenda.

**Evidências:** [semdor-crm/src/Clinica.Crm/Operations.cs:40](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/Operations.cs#L40); [semdor-crm/src/Clinica.Crm/ClinicScheduling.cs:40](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/ClinicScheduling.cs#L40); [semdor-crm/src/Clinica.Crm/wwwroot/app.js:408](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L408).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.83. A confiança não mede incidência ou certeza de um defeito.

<a id="a27"></a>
## A27 — Adiar conta oferece prazo fixo de sete dias

**P3 · Confirmado no código · Financeiro**

**Hoje:** Contas tem ação Adiar 7d. É um atalho legítimo, mas o controle não expressa escolher o vencimento negociado na própria ação.

**Efeito:** A rotina favorece um prazo arbitrário quando o acordo real pode ter outra data.

**Proposta:** Manter +7 dias como sugestão dentro de Adiar vencimento, com data e motivo explícitos conforme as regras do domínio.

**Critério de aceite para a correção:** O operador escolhe a data negociada em uma ação e vê o histórico, sem repetir adiamentos de sete dias.

**Evidências:** [Clinica/src/Clinica.Modulo.Financeiro/Views/ContasView.xaml:244](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ContasView.xaml#L244).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.49. A confiança não mede incidência ou certeza de um defeito.

<a id="a28"></a>
## A28 — Coleta pode atravessar escolhas em várias janelas

**P2 · Oportunidade com evidência estrutural · Termos do paciente**

**Hoje:** Existem EscolherTermoWindow, EscolherSessaoDoTermoWindow e AssinaturaPacienteWindow. As escolhas são necessárias quando falta contexto; quando paciente, sessão e modelo já são conhecidos, repetição deve ser evitada.

**Efeito:** Uma ação curta no balcão pode virar uma sequência de modais; a quantidade depende da porta de entrada e dos dados.

**Proposta:** Propagar paciente, sessão e modelo; mostrar uma revisão única e pedir apenas a informação ausente.

**Critério de aceite para a correção:** Da sessão com um único termo aplicável, a coleta chega diretamente à revisão, preservando opção de corrigir contexto.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/EscolherTermoWindow.xaml:5](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherTermoWindow.xaml#L5); [Clinica/src/Clinica.Desktop.Shell/Componentes/EscolherSessaoDoTermoWindow.xaml:4](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherSessaoDoTermoWindow.xaml#L4); [Clinica/src/Clinica.Desktop.Shell/Componentes/AssinaturaPacienteWindow.xaml:74](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/AssinaturaPacienteWindow.xaml#L74).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.65. A confiança não mede incidência ou certeza de um defeito.

<a id="a29"></a>
## A29 — Emissão pelo catálogo exige escolher antes de abrir o editor

**P2 · Oportunidade com evidência estrutural · Documentos**

**Hoje:** A central possui escolha do paciente, escolha da folha e prévia/ação; documentos escritos abrem DocumentoWindow. O editor também permite selecionar tipo. Não foi medido tempo de uso nem cliques por perfil.

**Efeito:** Para um documento frequente e paciente já conhecido, há decisões intermediárias potencialmente dispensáveis.

**Proposta:** Usar ação contextual Emitir documento com tipo frequente e paciente herdado; deixar o catálogo para descoberta ou mudança de tipo.

**Critério de aceite para a correção:** A partir da ficha, paciente não precisa ser buscado novamente; trocar tipo não apaga conteúdo sem aviso.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml:409](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml#L409); [Clinica/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs:921](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/ViewModels/DocumentosViewModel.cs#L921); [Clinica/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs:469](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs#L469).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.29. A confiança não mede incidência ou certeza de um defeito.

<a id="a30"></a>
## A30 — Comentários arquiteturais descrevem módulos que hoje são carregados

**P3 · Confirmado no código · Documentação e navegação**

**Hoje:** App do Clínico afirma em comentário carregar um único módulo; o código carrega Clínico e Recepção contextual. Comentários do agrupamento de pacientes/prontuário ainda descrevem ausência do outro módulo nos executáveis.

**Efeito:** Quem mantém ou audita usando os comentários pode concluir incorretamente que uma aba não existe ou que a função exige outro aplicativo.

**Proposta:** Atualizar comentários e documentação arquitetural a partir da matriz executável de navegação.

**Critério de aceite para a correção:** Descrições de carregamento e visibilidade concordam com os cinco App/ModuloFaturamentoAplicativo e com os perfis padrão.

**Evidências:** [Clinica/src/Clinica.Clinico/App.xaml.cs:13](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Clinico/App.xaml.cs#L13); [Clinica/src/Clinica.Clinico/App.xaml.cs:24](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Clinico/App.xaml.cs#L24); [Clinica/src/Clinica.Modulo.Recepcao/Modulo/ModuloRecepcao.cs:41](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Modulo/ModuloRecepcao.cs#L41).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.50. A confiança não mede incidência ou certeza de um defeito.

<a id="a31"></a>
## A31 — Abrir WhatsApp é registrado como envio

**P1 · Confirmado no código · Campanhas**

**Hoje:** EnviarAsync abre o aplicativo externo e, se a abertura não retorna erro, chama RegistrarEnvioAsync e mostra Envio registrado. Abrir a conversa não comprova que a pessoa enviou a mensagem.

**Efeito:** O acompanhamento pode indicar contato realizado mesmo que o operador apenas tenha aberto e fechado o WhatsApp.

**Proposta:** Separar Preparar mensagem, Confirmar envio manual e estados de entrega confirmados por integração. Conservar autor e horário de cada evento.

**Critério de aceite para a correção:** Abrir e fechar o aplicativo externo não marca envio; confirmação manual fica distinguível de entrega pelo provedor.

**Evidências:** [Clinica/src/Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs:274](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs#L274); [Clinica/src/Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs:291](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs#L291).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.95. A confiança não mede incidência ou certeza de um defeito.

<a id="a32"></a>
## A32 — Atalho de glosa descarta o contexto da linha

**P2 · Confirmado no código · Faturamento**

**Hoje:** O botão Abrir glosas de cada linha do painel não passa CommandParameter. AbrirGlosas não recebe a guia e dispara um evento sem argumento.

**Efeito:** A pessoa precisa procurar outra vez a glosa cujo prazo acabou de consultar.

**Proposta:** Passar guia/glosa e período ao destino, selecionar o registro e oferecer Voltar ao painel preservando filtros.

**Critério de aceite para a correção:** Abrir uma glosa específica no painel mostra essa mesma glosa no destino.

**Evidências:** [Clinica/src/Clinica.Modulo.Faturamento/Views/DashboardView.xaml:448](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/DashboardView.xaml#L448); [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/DashboardViewModel.cs:607](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/DashboardViewModel.cs#L607).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.65. A confiança não mede incidência ou certeza de um defeito.

<a id="a33"></a>
## A33 — Convênio fecha uma janela mas só salva na tela de trás

**P2 · Confirmado no código · Configurações**

**Hoje:** ConvenioWindow liga os campos diretamente à edição e tem Fechar; o rodapé informa que é necessário Salvar configurações na tela de trás.

**Efeito:** A conclusão do diálogo não conclui a tarefa de edição. O usuário precisa lembrar de uma segunda ação em outro contexto.

**Proposta:** Dar ao diálogo Salvar convênio e Cancelar com estado próprio, ou expor explicitamente uma revisão única de alterações pendentes na tela principal.

**Critério de aceite para a correção:** A pessoa consegue identificar sem ambiguidade se as alterações já estão persistidas e descartá-las antes de salvar.

**Evidências:** [Clinica/src/Clinica.Modulo.Faturamento/Alertas/ConvenioWindow.xaml:20](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/ConvenioWindow.xaml#L20); [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs:183](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs#L183).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.82. A confiança não mede incidência ou certeza de um defeito.

<a id="a34"></a>
## A34 — Catálogos misturam salvar em lote e excluir imediatamente

**P2 · Confirmado no código · Configurações**

**Hoje:** Editar/adicionar integra o Salvar configurações; RemoverConvenio e RemoverEspecialidade chamam exclusão no serviço imediatamente, com confirmação. Não há alegação de exclusão sem confirmação.

**Efeito:** Uma mesma tela usa dois momentos de persistência; sair sem salvar não desfaz uma exclusão já confirmada.

**Proposta:** Padronizar persistência por entidade; se mantiver exclusão imediata, dizer na confirmação que ela independe do botão Salvar e mostrar resultado por item.

**Critério de aceite para a correção:** Criar, editar e excluir têm estados e consequências explícitos, incluindo o que permanecerá ao abandonar a página.

**Evidências:** [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs:218](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs#L218); [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs:427](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs#L427).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.75. A confiança não mede incidência ou certeza de um defeito.

<a id="a35"></a>
## A35 — Prévia da retenção apaga o resultado quando falha

**P2 · Confirmado no código · Financeiro**

**Hoje:** PreverAsync captura a exceção, grava diagnóstico e atribui Retencao = null; nesse catch não explica a falha ao usuário. Valor inválido também retorna sem mensagem.

**Efeito:** Reter? pode parecer não ter efeito, e a ausência de prévia não esclarece se há indisponibilidade ou dado inválido.

**Proposta:** Exibir erro junto à linha, conservar o valor digitado e oferecer Tentar novamente. Diferenciar retenção inexistente de consulta não concluída.

**Critério de aceite para a correção:** Falha simulada no serviço deixa indicação visível de cálculo indisponível, nunca de imposto zero.

**Evidências:** [Clinica/src/Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs:797](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs#L797); [Clinica/src/Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs:819](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs#L819).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.74. A confiança não mede incidência ou certeza de um defeito.

<a id="a36"></a>
## A36 — Recebi na inadimplência fixa a data em hoje

**P2 · Confirmado no código · Financeiro**

**Hoje:** ReceberAsync confirma valor e descrição e passa DateOnly.FromDateTime(DateTime.Today). Esse atalho não solicita a data real do recebimento.

**Efeito:** Ao registrar hoje um pagamento de outro dia, este percurso não permite informar a data correta.

**Proposta:** Reutilizar uma confirmação de recebimento com data, forma e referência disponíveis conforme a operação; iniciar em hoje sem impedir correção autorizada.

**Critério de aceite para a correção:** Recebimento de ontem pode ser registrado por esse caminho com a data escolhida e auditada.

**Evidências:** [Clinica/src/Clinica.Modulo.Financeiro/ViewModels/InadimplenciaViewModel.cs:304](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/ViewModels/InadimplenciaViewModel.cs#L304); [Clinica/src/Clinica.Modulo.Financeiro/ViewModels/InadimplenciaViewModel.cs:321](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/ViewModels/InadimplenciaViewModel.cs#L321).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.98. A confiança não mede incidência ou certeza de um defeito.

<a id="a37"></a>
## A37 — Devolução de receita recebida exige reconstruir lançamento no Caixa

**P2 · Oportunidade de simplificação · Financeiro**

**Hoje:** Conciliação bloqueia corretamente cancelar dinheiro já recebido, mas apenas instrui lançar a devolução como saída no Caixa.

**Efeito:** A pessoa troca de tela e recompõe paciente, guia, valor e motivo; a orientação não oferece a continuação contextual.

**Proposta:** Oferecer Registrar devolução, abrindo saída pré-preenchida e vinculada à receita original, mantendo a conferência do operador.

**Critério de aceite para a correção:** Nenhuma entrada original é apagada; a saída preserva vínculo, data efetiva, valor e motivo.

**Evidências:** [Clinica/src/Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs:700](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs#L700).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.90. A confiança não mede incidência ou certeza de um defeito.

<a id="a38"></a>
## A38 — Chamar de volta não registra a tentativa nessa ação

**P2 · Confirmado no código · Relacionamento**

**Hoje:** RetencaoViewModel.Chamar valida permissão e consentimento, abre WhatsApp e retorna se não houver erro. Não persiste tentativa ou próximo contato nesse método.

**Efeito:** Quem consulta a lista depois não sabe por essa ação se outro operador já iniciou o contato.

**Proposta:** Usar o histórico de acompanhamento como destino canônico: preparar mensagem, registrar tentativa/resultado e combinar próximo passo, sem declarar envio automático.

**Critério de aceite para a correção:** Uma tentativa registrada fica visível no acompanhamento por responsável e data; simples abertura não se torna entrega.

**Evidências:** [Clinica/src/Clinica.Modulo.Gerente/ViewModels/RetencaoViewModel.cs:263](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/ViewModels/RetencaoViewModel.cs#L263); [Clinica/src/Clinica.Modulo.Gerente/ViewModels/RetencaoViewModel.cs:316](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/ViewModels/RetencaoViewModel.cs#L316).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.35. A confiança não mede incidência ou certeza de um defeito.

<a id="a39"></a>
## A39 — NPS de ontem deixa a recuperação de outros dias pouco acessível

**P2 · Oportunidade de simplificação · Campanhas**

**Hoje:** O comando GerarNpsAsync usa Today.AddDays(-1), e o botão se chama NPS de ontem. Não oferece período nesse comando.

**Efeito:** Se a rotina não for feita em um dia, o atalho seguinte não resolve explicitamente o período perdido.

**Proposta:** Manter Ontem como opção rápida e permitir Selecionar período/Recuperar pendências, com prévia de elegíveis e de contatos já gerados.

**Critério de aceite para a correção:** Selecionar período anterior não duplica contatos já gerados e explica quem ficou fora.

**Evidências:** [Clinica/src/Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs:228](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs#L228); [Clinica/src/Clinica.Modulo.Gerente/Views/CampanhasView.xaml:50](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/CampanhasView.xaml#L50).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.97. A confiança não mede incidência ou certeza de um defeito.

<a id="a40"></a>
## A40 — Busca vazia orienta trocar de página para cadastrar

**P2 · Oportunidade de simplificação · Cadastro**

**Hoje:** O componente BuscaDePacienteView mostra Pacientes → Novo paciente como instrução textual no resultado vazio.

**Efeito:** Um fluxo iniciado com nome/CPF precisa abandonar o seletor, cadastrar e voltar a procurar.

**Proposta:** Quando o perfil puder cadastrar, oferecer Cadastrar esta pessoa e retornar seu ID ao fluxo de origem; checar duplicidade antes de criar.

**Critério de aceite para a correção:** Ao concluir cadastro a partir da busca, o paciente fica selecionado na tarefa anterior sem nova busca.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/BuscaDePacienteView.xaml:90](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/BuscaDePacienteView.xaml#L90).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.92. A confiança não mede incidência ou certeza de um defeito.

<a id="a41"></a>
## A41 — Resumo de prontuário instrui abrir outra seção para trabalhar anexos

**P2 · Oportunidade de simplificação · Clínico**

**Hoje:** ResumoProntuarioWindow informa que baixar ou anexar arquivos exige abrir o prontuário completo, seção Exames e anexos.

**Efeito:** O resumo é útil para leitura, mas não oferece nessa instrução uma continuação direta para o arquivo.

**Proposta:** Converter a orientação em Abrir exames e anexos deste paciente, mantendo sessão e seleção.

**Critério de aceite para a correção:** O atalho leva ao mesmo paciente e à seção correta com permissões preservadas.

**Evidências:** [Clinica/src/Clinica.Modulo.Clinico/Janelas/ResumoProntuarioWindow.xaml:199](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/ResumoProntuarioWindow.xaml#L199).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.98. A confiança não mede incidência ou certeza de um defeito.

<a id="a42"></a>
## A42 — Pendências usa uma página compartilhada entre filas diferentes

**P2 · Confirmado no código · Portal**

**Hoje:** pendencias(pagina) envia um único número e renderiza sessões, documentos e conferência da recepção, com Anterior/Próxima comuns e até 50 registros por seção.

**Efeito:** Avançar para ver mais documentos também avança as outras listas. Uma seção vazia nessa página não significa que a fila esteja zerada.

**Proposta:** Separar paginação/filtro por fila ou usar abas de trabalho com total e posição próprios; manter um resumo geral.

**Critério de aceite para a correção:** Percorrer documentos não altera a posição de sessões e recepção; os estados vazios informam o recorte.

**Evidências:** [clinica-site/portal/profissional/posto.js:225](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js#L225); [clinica-site/portal/profissional/posto.js:227](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js#L227).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.95. A confiança não mede incidência ou certeza de um defeito.

<a id="a43"></a>
## A43 — Imprimir uma sessão de enfermagem busca todo o histórico paginado

**P2 · Confirmado no código · Portal**

**Hoje:** folhaEnfermagem consulta a ficha e percorre todas as páginas de enfermagem antes de filtrar agendamentoId da sessão escolhida.

**Efeito:** O tempo de abrir uma única folha cresce com o histórico do paciente, mesmo que a sessão tenha poucos registros.

**Proposta:** Criar leitura autorizada por sessão e carregar somente seus registros e dados de cabeçalho; manter histórico completo em consulta própria.

**Critério de aceite para a correção:** Abrir uma folha faz número de consultas limitado e independente da quantidade de páginas antigas.

**Evidências:** [clinica-site/portal/profissional/posto.js:230](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js#L230); [clinica-site/portal/profissional/posto.js:233](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js#L233).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.97. A confiança não mede incidência ou certeza de um defeito.

<a id="a44"></a>
## A44 — Portal do paciente é uma entrada da equipe para coleta presencial

**P2 · Oportunidade de clareza · Portal de termos**

**Hoje:** O site oferece Acessar o portal do paciente; a página explica que a equipe faz login e o paciente assina sem senha. O portal não é um painel de autosserviço do paciente.

**Efeito:** O nome amplo pode criar expectativa de consultar agenda, exames e documentos em casa, embora o texto explique a função real.

**Proposta:** Nomear Coleta de termos na clínica no site e Termos e assinaturas na operação. Reservar Portal do paciente para autosserviço caso venha a existir.

**Critério de aceite para a correção:** Antes do clique a finalidade e quem faz login ficam explícitos; não se exige credencial da equipe do paciente.

**Evidências:** [clinica-site/conteudo/para-pacientes.html:10](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/para-pacientes.html#L10); [clinica-site/portal/portal.js:104](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/portal.js#L104).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.99. A confiança não mede incidência ou certeza de um defeito.

<a id="a45"></a>
## A45 — Busca de pacientes atende somente BSV agendado hoje

**P2 · Limitação funcional explícita · Portal de termos**

**Hoje:** dia() apresenta Buscar paciente, mas informa que lista e busca mostram somente pacientes com BSV agendado para hoje.

**Efeito:** Quem tenta colher termo de outro procedimento ou outra data encontra uma fronteira funcional que precisa conhecer.

**Proposta:** Renomear Buscar BSV de hoje e, se o escopo desejado for mais amplo, oferecer coleta por sessão autorizada com data/procedimento explícitos.

**Critério de aceite para a correção:** O campo deixa claro o universo pesquisado; eventual ampliação mantém conferência de identidade e contexto da sessão.

**Evidências:** [clinica-site/portal/portal.js:109](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/portal.js#L109).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 1.00. A confiança não mede incidência ou certeza de um defeito.

<a id="a46"></a>
## A46 — Agendar consulta do cabeçalho abre página intermediária

**P2 · Oportunidade de simplificação · Site**

**Hoje:** O CTA global Agendar consulta aponta para /atendimento/. A página Contato já oferece Agendar pelo WhatsApp.

**Efeito:** Para quem já decidiu falar com a recepção, o cabeçalho acrescenta uma etapa informativa ao percurso.

**Proposta:** Oferecer Falar com a recepção/Agendar pelo WhatsApp como ação direta e manter Como funciona o agendamento como orientação secundária. Medir antes de remover conteúdo útil.

**Critério de aceite para a correção:** O CTA informa o canal de destino e não promete reserva confirmada de horário.

**Evidências:** [clinica-site/modelos/base.html:59](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/modelos/base.html#L59); [clinica-site/conteudo/contato.html:35](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/contato.html#L35).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.78. A confiança não mede incidência ou certeza de um defeito.

<a id="a47"></a>
## A47 — Concluir conversa muda a lista para concluídos

**P2 · Confirmado no código · CRM**

**Hoje:** Depois de fechar o diálogo, o handler define filter=closed e atualiza a tela.

**Efeito:** Quem está esvaziando uma fila precisa trocar novamente o filtro para continuar os atendimentos abertos.

**Proposta:** Preservar a seleção de origem e oferecer Próximo atendimento elegível; mostrar a conclusão por confirmação e link ao histórico.

**Critério de aceite para a correção:** Concluir uma conversa não tira o operador da fila em que estava trabalhando.

**Evidências:** [semdor-crm/src/Clinica.Crm/wwwroot/app.js:557](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L557).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.96. A confiança não mede incidência ou certeza de um defeito.

<a id="a48"></a>
## A48 — Falha de atualização após concluir escreve erro em diálogo já fechado

**P1 · Confirmado no código · CRM**

**Hoje:** No handler closeForm, a conclusão é gravada, closeDialog.close() é chamado e depois ocorre await refresh(). O mesmo catch escreve em closeError, que fica dentro do diálogo fechado.

**Efeito:** Se apenas a atualização falhar, a mensagem pode ficar fora da vista; a operação já persistida pode ser confundida com falha de conclusão.

**Proposta:** Separar sucesso da mutação e atualização da tela. Após concluir, mostrar aviso global e Tentar atualizar se a leitura falhar.

**Critério de aceite para a correção:** Simular sucesso do POST e falha no refresh exibe Conclusão registrada, atualização pendente, sem repetir a conclusão.

**Evidências:** [semdor-crm/src/Clinica.Crm/wwwroot/app.js:557](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L557).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.91. A confiança não mede incidência ou certeza de um defeito.

<a id="a49"></a>
## A49 — Modelos WhatsApp necessários ficam em Mais ações

**P2 · Oportunidade de simplificação · CRM**

**Hoje:** mountSettings move o botão templates para um details Mais ações junto a aiOfferHuman. Não é uma cópia do botão.

**Efeito:** Em situações que exigem modelo aprovado, a ação necessária pode ficar em um menu pouco evidente.

**Proposta:** Quando a conversa exigir modelo, promover Enviar modelo aprovado junto ao compositor e explicar a condição; manter Mais ações para usos ocasionais.

**Critério de aceite para a correção:** No estado em que texto livre não pode ser enviado, o caminho disponível para um modelo aparece diretamente.

**Evidências:** [semdor-crm/src/Clinica.Crm/wwwroot/app.js:449](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L449); [semdor-crm/src/Clinica.Crm/wwwroot/app.js:236](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L236).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.71. A confiança não mede incidência ou certeza de um defeito.

<a id="a50"></a>
## A50 — Alerta de espera mistura tempo corrido e expediente

**P2 · Confirmado no código · CRM**

**Hoje:** serviceWaitBadge mostra as duas medidas; renderServiceQueue conta Acima de 15 min usando Date.now()-waitingSince, isto é, tempo corrido.

**Efeito:** O resumo não explicita no próprio título que o limite inclui período fora do expediente, embora a linha individual diferencie.

**Proposta:** Nomear Acima de 15 min corridos e permitir alternar para Em expediente, com mesma base no indicador e na filtragem.

**Critério de aceite para a correção:** Uma conversa recebida fora do expediente tem classificação consistente com a métrica selecionada.

**Evidências:** [semdor-crm/src/Clinica.Crm/wwwroot/app.js:551](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L551); [semdor-crm/src/Clinica.Crm/wwwroot/app.js:550](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L550).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.89. A confiança não mede incidência ou certeza de um defeito.

<a id="a51"></a>
## A51 — Listas cortadas não oferecem paginação no componente

**P2 · Confirmado no código · CRM**

**Hoje:** O CRM renderiza só 60 confirmações pendentes, 70 eventos da agenda, 60 eventos de recall e 100 candidatos de recall. O recall informa até 100 e tem filtros; esses renders não oferecem próxima página. Meu dia, ao contrário, tem atalhos para listas maiores e não é tratado como perda de acesso.

**Efeito:** O operador precisa restringir filtros ou ir a outra área para investigar o que ficou fora; o total pode ser maior que a lista disponível.

**Proposta:** Usar paginação/Carregar mais com total do recorte e manter falhas pendentes acessíveis independentemente da idade. Não remover limites técnicos sem paginação no servidor.

**Critério de aceite para a correção:** Com mais de 100 candidatos e 60 pendências, todos os itens autorizados podem ser percorridos sem mudar artificialmente o filtro.

**Evidências:** [semdor-crm/src/Clinica.Crm/wwwroot/app.js:395](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L395); [semdor-crm/src/Clinica.Crm/wwwroot/app.js:572](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L572); [semdor-crm/src/Clinica.Crm/wwwroot/app.js:330](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L330); [semdor-crm/src/Clinica.Crm/wwwroot/app.js:396](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js#L396).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.91. A confiança não mede incidência ou certeza de um defeito.

<a id="a52"></a>
## A52 — Prontuário web termina nas 20 sessões mais recentes

**P2 · Limitação funcional explícita · Web de consulta**

**Hoje:** Paginas.Sessoes usa Take(20) e informa o total. Nesse render não há paginação nem caminho para sessões mais antigas.

**Efeito:** Um usuário autorizado consulta parte do histórico e precisa mudar de aplicação para continuar.

**Proposta:** Oferecer Carregar anteriores ou pesquisa por período, mantendo esse produto como leitura e preservando a trilha de acesso.

**Critério de aceite para a correção:** Paciente fictício com 21 sessões permite alcançar a mais antiga e continua mostrando o total correto.

**Evidências:** [Clinica/src/Clinica.Web/Paginas.cs:336](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Web/Paginas.cs#L336); [Clinica/src/Clinica.Web/Paginas.cs:359](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Web/Paginas.cs#L359).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.97. A confiança não mede incidência ou certeza de um defeito.

<a id="a53"></a>
## A53 — Pesquisa de seções depende de acentos e dos nomes exatos

**P2 · Confirmado no código · Navegação**

**Hoje:** ShellViewModel.Casa usa Contains com OrdinalIgnoreCase, sem normalização de acentos nem catálogo de sinônimos. Isso ignora caixa, mas não torna relatorios equivalente a relatórios.

**Efeito:** A busca que deveria encurtar caminhos pode não encontrar o destino quando o operador escreve sem acento ou usa um nome anterior.

**Proposta:** Normalizar acentos e cadastrar aliases por chave canônica, como impressão, segunda via e documentos, respeitando as permissões do destino.

**Critério de aceite para a correção:** Pesquisar com/sem acentos retorna os mesmos destinos; aliases não revelam telas proibidas.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs:479](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs#L479).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.93. A confiança não mede incidência ou certeza de um defeito.

<a id="a54"></a>
## A54 — Pesquisa sem resultados fecha o painel de resultados

**P2 · Confirmado no código · Navegação**

**Hoje:** Ao terminar a busca, PesquisaAberta recebe ResultadosPesquisa.Count > 0. Com texto não vazio e zero resultados, o painel é fechado.

**Efeito:** A pessoa não recebe nesse painel uma explicação de que não houve correspondência nem sugestão para continuar.

**Proposta:** Manter o painel aberto para a consulta não vazia com Nenhuma seção encontrada e exemplos de busca; distinguir ausência de destino de falta de permissão sem revelar dados restritos.

**Critério de aceite para a correção:** Digitar termo inexistente exibe estado vazio acessível; apagar o termo fecha o painel.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs:504](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs#L504).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.88. A confiança não mede incidência ou certeza de um defeito.

<a id="a55"></a>
## A55 — Progresso das aulas é local à máquina

**P3 · Limitação funcional explícita · Treinamento**

**Hoje:** AcervoTreinamento grava progresso-{usuarioId}.json em LocalApplicationData. O progresso desse componente não acompanha automaticamente o profissional entre computadores.

**Efeito:** Ao mudar de posto, o mesmo usuário pode ver aulas sem o progresso que marcou em outro dispositivo.

**Proposta:** Explicitar Progresso neste computador ou sincronizar por usuário com política de conflito e uso offline. Manter cache de vídeo local.

**Critério de aceite para a correção:** A interface informa o escopo do progresso; se houver sincronização, dois dispositivos retomam a posição correta.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Treinamento/AcervoTreinamento.cs:22](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Treinamento/AcervoTreinamento.cs#L22).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.94. A confiança não mede incidência ou certeza de um defeito.

<a id="a56"></a>
## A56 — Seção CRM da ficha apresenta contatos de campanha

**P2 · Confirmado no código · Relacionamento**

**Hoje:** RelacionamentoPacienteView chama-se Relacionamento (CRM) e fala das últimas conversas, mas renderiza Contatos com tipo, detalhe e situação de campanha; o estado vazio é Nenhum contato de campanha registrado.

**Efeito:** O nome pode sugerir que o histórico completo de conversas do semdor-crm está ali. A auditoria não encontrou esse histórico nesse componente.

**Proposta:** Nomear Campanhas e origem do paciente e oferecer abertura contextual da central de conversas quando a integração estiver comprovada; mostrar a origem de cada registro.

**Critério de aceite para a correção:** O operador consegue distinguir contato de campanha, conversa WhatsApp e acompanhamento clínico.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml:7](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml#L7); [Clinica/src/Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml:15](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml#L15).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.34. A confiança não mede incidência ou certeza de um defeito.

<a id="a57"></a>
## A57 — Exportar meus dados usa a voz do paciente em tela da equipe

**P3 · Confirmado no código · Privacidade**

**Hoje:** PrivacidadePacienteView apresenta Exportar meus dados na ficha de um paciente aberta pelo operador.

**Efeito:** O pronome meus pode ser interpretado como dados da conta conectada, em vez dos dados do paciente selecionado.

**Proposta:** Usar Exportar dados deste paciente, com nome/identificador e descrição do conteúdo na confirmação; manter autor e destinatário distintos.

**Critério de aceite para a correção:** A confirmação deixa explícito de quem são os dados e qual arquivo será produzido.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/Views/PrivacidadePacienteView.xaml:79](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PrivacidadePacienteView.xaml#L79).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.96. A confiança não mede incidência ou certeza de um defeito.

<a id="a58"></a>
## A58 — Pix presencial oferece copia e cola, sem QR nessa janela

**P2 · Oportunidade de simplificação · Financeiro**

**Hoje:** CobrancaPixWindow exibe texto Pix copia e cola e Copiar. A explicação pede colar no aplicativo do paciente ou enviar pelo WhatsApp; a janela não contém QR.

**Efeito:** No balcão, transferir texto entre computador e celular acrescenta uma etapa ao pagamento.

**Proposta:** Adicionar QR do mesmo payload e opção de impressão, com valor e recebedor visíveis. Manter o aviso de que gerar código não confirma recebimento.

**Critério de aceite para a correção:** QR e texto codificam exatamente o mesmo valor/referência; mudar esses campos invalida ambos.

**Evidências:** [Clinica/src/Clinica.Modulo.Financeiro/Janelas/CobrancaPixWindow.xaml:53](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/CobrancaPixWindow.xaml#L53); [Clinica/src/Clinica.Modulo.Financeiro/ViewModels/CobrancaPixViewModel.cs:113](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/ViewModels/CobrancaPixViewModel.cs#L113).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.98. A confiança não mede incidência ou certeza de um defeito.

<a id="a59"></a>
## A59 — Teste de publicação deixa limpeza para o painel do provedor

**P2 · Oportunidade de simplificação · Integrações**

**Hoje:** EnviarExemploAsync publica um arquivo de exemplo e orienta abrir o endereço e depois apagar o objeto no painel do provedor.

**Efeito:** Testar a configuração exige alternar de sistema e lembrar de uma limpeza manual.

**Proposta:** Exibir Abrir teste e Remover arquivo de teste, com identificador do objeto criado; oferecer expiração automática apenas para objetos explicitamente de teste.

**Critério de aceite para a correção:** O teste informa seu estado e permite limpar somente seu próprio objeto, sem acesso aos documentos reais.

**Evidências:** [Clinica/src/Clinica.Modulo.Gerente/ViewModels/ConfiguracoesViewModel.cs:609](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/ViewModels/ConfiguracoesViewModel.cs#L609); [Clinica/src/Clinica.Modulo.Gerente/ViewModels/ConfiguracoesViewModel.cs:636](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/ViewModels/ConfiguracoesViewModel.cs#L636).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.98. A confiança não mede incidência ou certeza de um defeito.

<a id="a60"></a>
## A60 — Troca de usuário exige reiniciar a aplicação

**P2 · Oportunidade de simplificação · Acesso**

**Hoje:** TrocarUsuario avisa para salvar, abre um novo processo e encerra o atual. O comentário explica que permissões das ViewModels são construídas com a sessão.

**Efeito:** Em posto compartilhado, a troca implica reconstruir a sessão de trabalho; não basta mudar o nome conectado.

**Proposta:** Projetar encerramento controlado com pendências salvas/descartadas e reconstrução dos escopos de usuário. Preservar a barreira atual até comprovar isolamento entre sessões.

**Critério de aceite para a correção:** A nova sessão não herda dados, botões ou permissões da anterior; alterações não salvas têm tratamento explícito.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs:540](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs#L540).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.86. A confiança não mede incidência ou certeza de um defeito.

<a id="a61"></a>
## A61 — Estado vazio reúne nenhum pedido e todos respondidos

**P2 · Oportunidade de clareza · Exames**

**Hoje:** AnexosPacienteView mostra Nenhum exame aguardando e explica duas possibilidades: todos com resultado ou nenhum exame pedido.

**Efeito:** Os dois estados pedem próximos passos diferentes, mas recebem a mesma orientação.

**Proposta:** Quando não houver pedidos, oferecer Solicitar exame ao perfil habilitado; quando todos estiverem respondidos, oferecer Ver resultados.

**Critério de aceite para a correção:** Os dois cenários fictícios geram mensagem e ação próprias, sem sugerir um pedido clínico automaticamente.

**Evidências:** [Clinica/src/Clinica.Modulo.Clinico/Views/AnexosPacienteView.xaml:144](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AnexosPacienteView.xaml#L144).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.90. A confiança não mede incidência ou certeza de um defeito.

<a id="a62"></a>
## A62 — Pagamentos vazio instrui voltar ao fechamento ou à venda

**P2 · Oportunidade de simplificação · Recepção**

**Hoje:** O estado vazio de PagamentosView orienta registrar cobrança no fechamento da sessão ou na venda do pacote, sem ação contextual nesse bloco.

**Efeito:** Quem chegou para receber precisa descobrir qual tarefa anterior está faltando e procurar o paciente novamente em outro fluxo.

**Proposta:** Oferecer Conferir sessões deste paciente e Ver pacotes, com retorno à cobrança. Não criar dívida automaticamente só porque a lista está vazia.

**Critério de aceite para a correção:** O operador pode continuar para a origem da cobrança mantendo o paciente e retornar ao recebimento.

**Evidências:** [Clinica/src/Clinica.Modulo.Recepcao/Views/PagamentosView.xaml:26](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PagamentosView.xaml#L26).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.87. A confiança não mede incidência ou certeza de um defeito.

<a id="a63"></a>
## A63 — Acompanhamento designa cuidado clínico e retorno comercial

**P3 · Oportunidade de clareza · Nomenclatura**

**Hoje:** AcompanhamentoView do Clínico reúne dor, medidas e avaliações. AcompanhamentoView da Recepção reúne recall e novos BSV, responsáveis e contato.

**Efeito:** Busca, treinamento e pedidos de suporte podem usar o mesmo nome para tarefas diferentes.

**Proposta:** Usar Evolução e medidas para a leitura clínica e Acompanhamento de retornos para a operação de contato; conservar vínculos entre os domínios.

**Critério de aceite para a correção:** Menus, busca e ajuda distinguem os dois assuntos sem fundir seus dados.

**Evidências:** [Clinica/src/Clinica.Modulo.Clinico/Views/AcompanhamentoView.xaml:22](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AcompanhamentoView.xaml#L22); [Clinica/src/Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml:19](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml#L19).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.32. A confiança não mede incidência ou certeza de um defeito.

<a id="a64"></a>
## A64 — Guias sem resposta no XML permanecem com decisão de aceite

**P1 · Confirmado no código · Faturamento**

**Hoje:** O fluxo alerta que guias ausentes no XML ficam aceitas e podem ser ajustadas manualmente. RetornoLoteWindow constrói todas as linhas e AplicarImportacao altera apenas as decisões encontradas; ao confirmar, todas entram em Decisoes.

**Efeito:** Ausência de resposta exige correção manual para não ser tratada como decisão positiva. O aviso existe, mas o padrão favorece a conclusão indevida.

**Proposta:** Representar Sem retorno como estado próprio, destacar divergências e exigir decisão explícita para itens ausentes antes de encerrar o lote; validar o modelo de domínio necessário.

**Critério de aceite para a correção:** XML parcial conserva itens ausentes como pendentes e nunca os converte silenciosamente em aceitos.

**Evidências:** [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs:365](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs#L365); [Clinica/src/Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml.cs:70](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml.cs#L70); [Clinica/src/Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml.cs:87](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml.cs#L87).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.43. A confiança não mede incidência ou certeza de um defeito.

<a id="a65"></a>
## A65 — Baixar XML como segunda via regenera com dados atuais

**P1 · Confirmado no código · Faturamento**

**Hoje:** O botão XML é descrito como baixar novamente/2ª via. BaixarXml lê o lote e os parâmetros atuais e chama GerarLoteXml novamente, preservando o registro ANS salvo no lote. Não lê o arquivo originalmente exportado.

**Efeito:** Depois de alterar cadastro/parâmetros, uma suposta segunda via pode não ter os mesmos bytes do XML já entregue.

**Proposta:** Separar Baixar original arquivado e Gerar versão corrigida, com hash, data e situação de envio explícitos.

**Critério de aceite para a correção:** Alterar parâmetros não muda o original; regeneração fica identificada e não substitui silenciosamente o artefato enviado.

**Evidências:** [Clinica/src/Clinica.Modulo.Faturamento/Views/TissView.xaml:67](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/TissView.xaml#L67); [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs:262](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs#L262).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.64. A confiança não mede incidência ou certeza de um defeito.

<a id="a66"></a>
## A66 — Validação TISS não expõe separadamente XSD não executado

**P2 · Confirmado no código · Faturamento**

**Hoje:** ValidarXmlGerado faz validação estrutural e tenta XSD somente se encontrar arquivo na pasta local. Exceção nessa etapa vai para log; o retorno é apenas uma lista de problemas.

**Efeito:** Uma lista sem problemas não informa se o XSD rodou, faltou ou falhou; o operador não vê o nível de verificação realizado.

**Proposta:** Retornar estado por etapa: estrutura, schema/versão, não executado e erro de validação, com correção acessível. Não prometer aceite da operadora.

**Critério de aceite para a correção:** Sem XSD ou com erro ao lê-lo, a tela informa validação incompleta mesmo que a estrutura básica passe.

**Evidências:** [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs:394](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs#L394); [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs:411](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs#L411).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.97. A confiança não mede incidência ou certeza de um defeito.

<a id="a67"></a>
## A67 — Rodada vencida bloqueia a janela até decidir todas as guias

**P2 · Oportunidade de redesenho de regra · Faturamento**

**Hoje:** A inicialização do Faturamento chama a rodada com bloqueante:true. Nesse modo Cancelar some e OnClosing impede fechar sem concluir todas as decisões exigidas. A ativação efetiva depende do status da rodada.

**Efeito:** Quem precisa consultar outra informação para decidir pode ficar preso numa tarefa que exige resolver tudo naquele momento.

**Proposta:** Avaliar uma fila de pendências obrigatórias com progresso salvo e bloqueio apenas das ações dependentes; prever responsável, escalonamento e adiamento justificado conforme regra aprovada.

**Critério de aceite para a correção:** O operador consegue consultar o necessário e retomar, mantendo a obrigação de resolver as guias e o histórico das decisões.

**Evidências:** [Clinica/src/Clinica.Desktop/App.xaml.cs:328](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop/App.xaml.cs#L328); [Clinica/src/Clinica.Modulo.Faturamento/Alertas/RodadaPendenciasWindow.xaml.cs:120](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RodadaPendenciasWindow.xaml.cs#L120).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.95. A confiança não mede incidência ou certeza de um defeito.

<a id="a68"></a>
## A68 — Radar interrompe exportação com texto, sem abrir a correção

**P2 · Oportunidade de simplificação · Faturamento**

**Hoje:** Exportar apresenta riscos em confirmação e, ao cancelar, orienta corrigir guias/configurações. Os trechos mostram listas textuais e retorno, sem destino contextual para cada pendência.

**Efeito:** A pessoa precisa localizar as entidades mencionadas e depois reconstruir a seleção do lote.

**Proposta:** Criar prévia de prontidão com linha por guia/campo e Corrigir aqui/Abrir cadastro, retomando o lote após a correção.

**Critério de aceite para a correção:** Cada pendência aponta para seu registro e a prévia é recalculada antes de gerar o lote.

**Evidências:** [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs:160](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs#L160); [Clinica/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs:171](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs#L171).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.93. A confiança não mede incidência ou certeza de um defeito.

<a id="a69"></a>
## A69 — Ajuda exige localizar manualmente arquivos de diagnóstico

**P3 · Oportunidade de simplificação · Ajuda**

**Hoje:** AjudaView descreve caminhos de logs e pede anotar horário e tarefa. Esse componente é texto e não oferece coletar um diagnóstico.

**Efeito:** O suporte depende de navegar no sistema de arquivos e correlacionar o episódio manualmente.

**Proposta:** Oferecer Copiar diagnóstico deste erro com referência, versão e contexto técnico revisável, e Abrir pasta de logs para quem estiver autorizado. Evitar incluir dados de pacientes por padrão.

**Critério de aceite para a correção:** Um erro tem referência recuperável e o operador consegue fornecer contexto sem procurar arquivo mensal à mão.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/AjudaView.xaml:92](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/AjudaView.xaml#L92).

**Revisão auxiliar Jev:** `sustentado`, confiança retornada 0.94. A confiança não mede incidência ou certeza de um defeito.

<a id="a70"></a>
## A70 — Ajuda promete que falha nunca aparece como sucesso

**P3 · Confirmado no código · Nomenclatura**

**Hoje:** AjudaView usa a afirmação absoluta Falha nunca aparece como sucesso. A35 e A48 mostram percursos cujo feedback ainda precisa de correção; a frase descreve uma intenção arquitetural como garantia universal.

**Efeito:** A ajuda pode levar a interpretar silêncio ou uma confirmação como prova de que todo o fluxo terminou.

**Proposta:** Explicar os estados esperados e a recuperação sem garantia absoluta: operação salva, atualização pendente, envio preparado, envio confirmado.

**Critério de aceite para a correção:** O texto de ajuda corresponde aos estados implementados e é verificado nos casos de falha documentados.

**Evidências:** [Clinica/src/Clinica.Desktop.Shell/Componentes/AjudaView.xaml:89](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/AjudaView.xaml#L89).

**Revisão auxiliar Jev:** `parcial`, confiança retornada 0.31. A confiança não mede incidência ou certeza de um defeito.
