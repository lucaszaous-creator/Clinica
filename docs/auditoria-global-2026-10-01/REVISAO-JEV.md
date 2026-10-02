# Participação e revisão do Jev

Foi utilizado o Jev real pela API TypeSafe (`jev-1.13.0`, endpoint `/v1/systemone`), conforme solicitado pelo usuário. Não foi um agente interno com o nome Jev. A API usada devolve escolhas estruturadas e confiança; **não devolveu uma justificativa textual por escolha nesta consulta**. Nenhum raciocínio explicativo foi inventado para representar sua resposta.

## Primeira passagem: todas as superfícies

Foram enviados rótulos e vínculos de controles de 263 arquivos, em cinco lotes. O objetivo era priorizar revisão de navegação, ações e sobreposição. Recursos de estilo e bindings sem contexto podem receber insuficiente. As classes são focos de investigação, não diagnóstico de defeito.

| Classe | Arquivos |
| --- | ---: |
| acao | 153 |
| insuficiente | 24 |
| navegacao | 13 |
| sem_indicio | 72 |
| sobreposicao | 1 |

Cada voto está associado à sua superfície em [REVISAO-POR-SUPERFICIE.md](REVISAO-POR-SUPERFICIE.md). Os pedidos conservam os controles efetivamente fornecidos, permitindo conferir a cobertura.

## Segunda passagem: evidências dos 70 achados

Quatorze lotes de cinco achados levaram descrição, classe, ressalvas, proposta e trechos de código com linhas. Os trechos são limitados; matrizes de navegação e observações Git exigem evidências complementares. Foram resolvidas 146 referências de código.

| Voto | Achados |
| --- | ---: |
| nao_sustentado | 1 |
| parcial | 13 |
| sustentado | 56 |

| ID | Voto | Confiança retornada | Título |
| --- | --- | ---: | --- |
| [A01](ACHADOS.md#a01) | sustentado | 0.96 | Mensagem manda cancelar e reemitir para assinar depois |
| [A02](ACHADOS.md#a02) | parcial | 0.39 | Repetir Emitir após falha posterior cria outra emissão |
| [A03](ACHADOS.md#a03) | sustentado | 0.76 | Impressão perde destaque quando há assinatura pendente |
| [A04](ACHADOS.md#a04) | sustentado | 0.45 | Emitir e imprimir executa Salvar como e abrir leitor externo |
| [A05](ACHADOS.md#a05) | sustentado | 0.82 | Recibo na central aponta ao Caixa ausente na Recepção |
| [A06](ACHADOS.md#a06) | sustentado | 0.45 | Período de uma emissão é escolhido em outra aba |
| [A07](ACHADOS.md#a07) | sustentado | 0.94 | Sessões e guias repetido em duas camadas de abas |
| [A08](ACHADOS.md#a08) | parcial | 0.28 | Mesmo preço particular sob dois grupos no Gerente |
| [A09](ACHADOS.md#a09) | sustentado | 0.55 | Rótulos novos não chegam às abas compostas |
| [A10](ACHADOS.md#a10) | parcial | 0.34 | Financeiro abre em Estoque por efeito da ordenação |
| [A11](ACHADOS.md#a11) | parcial | 0.35 | Documentos no menu é somente uma fila de assinatura |
| [A12](ACHADOS.md#a12) | sustentado | 0.41 | Pendências e Documentos repetem a fila de documentos sem assinatura |
| [A13](ACHADOS.md#a13) | parcial | 0.31 | Áreas internas não têm navegação persistida na URL |
| [A14](ACHADOS.md#a14) | sustentado | 0.78 | Ações dos documentos mantidas em dois templates |
| [A15](ACHADOS.md#a15) | parcial | 0.37 | Três interfaces sem referência de abertura encontrada |
| [A16](ACHADOS.md#a16) | sustentado | 0.99 | Instrução de erro usa nomes antigos de abas |
| [A17](ACHADOS.md#a17) | nao_sustentado | 0.74 | Pastas de trabalho e main representam sistemas diferentes |
| [A18](ACHADOS.md#a18) | sustentado | 0.28 | Ponte canônica citada pelo CRM não está na main auditada de Clinica |
| [A19](ACHADOS.md#a19) | parcial | 0.46 | CRM conserva 445 fontes também presentes em Clinica |
| [A20](ACHADOS.md#a20) | sustentado | 0.28 | Agenda, passagem e infusão têm entradas e vocabulário dispersos |
| [A21](ACHADOS.md#a21) | sustentado | 0.94 | Modelos distribuídos por emissão, evolução, mapa e configuração |
| [A22](ACHADOS.md#a22) | parcial | 0.50 | Gerente ainda reúne 27 entradas antes das abas internas |
| [A23](ACHADOS.md#a23) | parcial | 0.40 | Meu dia, Visão geral e Operação dividem acompanhamento da fila |
| [A24](ACHADOS.md#a24) | sustentado | 0.62 | Concluir atendimento nomeia dois fatos diferentes |
| [A25](ACHADOS.md#a25) | sustentado | 0.37 | Retorno, lembrete, tarefa e recall precisam de um mapa comum |
| [A26](ACHADOS.md#a26) | sustentado | 0.83 | Registrar solução de remarcação não confirma alteração na agenda |
| [A27](ACHADOS.md#a27) | parcial | 0.49 | Adiar conta oferece prazo fixo de sete dias |
| [A28](ACHADOS.md#a28) | parcial | 0.65 | Coleta pode atravessar escolhas em várias janelas |
| [A29](ACHADOS.md#a29) | sustentado | 0.29 | Emissão pelo catálogo exige escolher antes de abrir o editor |
| [A30](ACHADOS.md#a30) | sustentado | 0.50 | Comentários arquiteturais descrevem módulos que hoje são carregados |
| [A31](ACHADOS.md#a31) | sustentado | 0.95 | Abrir WhatsApp é registrado como envio |
| [A32](ACHADOS.md#a32) | sustentado | 0.65 | Atalho de glosa descarta o contexto da linha |
| [A33](ACHADOS.md#a33) | sustentado | 0.82 | Convênio fecha uma janela mas só salva na tela de trás |
| [A34](ACHADOS.md#a34) | sustentado | 0.75 | Catálogos misturam salvar em lote e excluir imediatamente |
| [A35](ACHADOS.md#a35) | sustentado | 0.74 | Prévia da retenção apaga o resultado quando falha |
| [A36](ACHADOS.md#a36) | sustentado | 0.98 | Recebi na inadimplência fixa a data em hoje |
| [A37](ACHADOS.md#a37) | sustentado | 0.90 | Devolução de receita recebida exige reconstruir lançamento no Caixa |
| [A38](ACHADOS.md#a38) | sustentado | 0.35 | Chamar de volta não registra a tentativa nessa ação |
| [A39](ACHADOS.md#a39) | sustentado | 0.97 | NPS de ontem deixa a recuperação de outros dias pouco acessível |
| [A40](ACHADOS.md#a40) | sustentado | 0.92 | Busca vazia orienta trocar de página para cadastrar |
| [A41](ACHADOS.md#a41) | sustentado | 0.98 | Resumo de prontuário instrui abrir outra seção para trabalhar anexos |
| [A42](ACHADOS.md#a42) | sustentado | 0.95 | Pendências usa uma página compartilhada entre filas diferentes |
| [A43](ACHADOS.md#a43) | sustentado | 0.97 | Imprimir uma sessão de enfermagem busca todo o histórico paginado |
| [A44](ACHADOS.md#a44) | sustentado | 0.99 | Portal do paciente é uma entrada da equipe para coleta presencial |
| [A45](ACHADOS.md#a45) | sustentado | 1.00 | Busca de pacientes atende somente BSV agendado hoje |
| [A46](ACHADOS.md#a46) | sustentado | 0.78 | Agendar consulta do cabeçalho abre página intermediária |
| [A47](ACHADOS.md#a47) | sustentado | 0.96 | Concluir conversa muda a lista para concluídos |
| [A48](ACHADOS.md#a48) | sustentado | 0.91 | Falha de atualização após concluir escreve erro em diálogo já fechado |
| [A49](ACHADOS.md#a49) | sustentado | 0.71 | Modelos WhatsApp necessários ficam em Mais ações |
| [A50](ACHADOS.md#a50) | sustentado | 0.89 | Alerta de espera mistura tempo corrido e expediente |
| [A51](ACHADOS.md#a51) | sustentado | 0.91 | Listas cortadas não oferecem paginação no componente |
| [A52](ACHADOS.md#a52) | sustentado | 0.97 | Prontuário web termina nas 20 sessões mais recentes |
| [A53](ACHADOS.md#a53) | sustentado | 0.93 | Pesquisa de seções depende de acentos e dos nomes exatos |
| [A54](ACHADOS.md#a54) | sustentado | 0.88 | Pesquisa sem resultados fecha o painel de resultados |
| [A55](ACHADOS.md#a55) | sustentado | 0.94 | Progresso das aulas é local à máquina |
| [A56](ACHADOS.md#a56) | sustentado | 0.34 | Seção CRM da ficha apresenta contatos de campanha |
| [A57](ACHADOS.md#a57) | sustentado | 0.96 | Exportar meus dados usa a voz do paciente em tela da equipe |
| [A58](ACHADOS.md#a58) | sustentado | 0.98 | Pix presencial oferece copia e cola, sem QR nessa janela |
| [A59](ACHADOS.md#a59) | sustentado | 0.98 | Teste de publicação deixa limpeza para o painel do provedor |
| [A60](ACHADOS.md#a60) | sustentado | 0.86 | Troca de usuário exige reiniciar a aplicação |
| [A61](ACHADOS.md#a61) | sustentado | 0.90 | Estado vazio reúne nenhum pedido e todos respondidos |
| [A62](ACHADOS.md#a62) | sustentado | 0.87 | Pagamentos vazio instrui voltar ao fechamento ou à venda |
| [A63](ACHADOS.md#a63) | parcial | 0.32 | Acompanhamento designa cuidado clínico e retorno comercial |
| [A64](ACHADOS.md#a64) | sustentado | 0.43 | Guias sem resposta no XML permanecem com decisão de aceite |
| [A65](ACHADOS.md#a65) | sustentado | 0.64 | Baixar XML como segunda via regenera com dados atuais |
| [A66](ACHADOS.md#a66) | sustentado | 0.97 | Validação TISS não expõe separadamente XSD não executado |
| [A67](ACHADOS.md#a67) | sustentado | 0.95 | Rodada vencida bloqueia a janela até decidir todas as guias |
| [A68](ACHADOS.md#a68) | sustentado | 0.93 | Radar interrompe exportação com texto, sem abrir a correção |
| [A69](ACHADOS.md#a69) | sustentado | 0.94 | Ajuda exige localizar manualmente arquivos de diagnóstico |
| [A70](ACHADOS.md#a70) | parcial | 0.31 | Ajuda promete que falha nunca aparece como sucesso |

## Tratamento editorial das divergências

**A17: voto não sustentado.** O trecho de OrganizacaoNavegacao enviado não demonstra a divergência entre checkouts. O voto é preservado. A afirmação verificável sobre commits é sustentada pelo levantamento Git de [VERSOES-E-COBERTURA.md](VERSOES-E-COBERTURA.md) e `versoes-locais.json`, não por esse trecho. A expressão do título “sistemas diferentes” deve ser lida como **revisões diferentes do código**, não como prova de produtos diferentes implantados. O commit da produção permanece não verificado.

**A02:** a sequência persistir → falhar depois → liberar Emitindo e a ausência de guarda por DocumentoEmitidoId sustentam um caminho estático de nova emissão. Não houve reprodução em banco; o ensaio proposto continua necessário.

**A08, A10 e A22:** as conclusões dependem da composição completa; a matriz executada com declarações reais e filtro reproduzido é evidência complementar. Não são capturas de menu em produção.

**A11, A13 e A15:** a função de Documentos está no texto/código do portal; a navegação sem rota e os candidatos sem referência exigem confirmação em execução. Reflexão ou composição dinâmica podem alterar o alcance.

**A19:** a comparação Git sustenta a quantidade de cópias; não prova execução duplicada. A condição histórica indicada no próprio CRM é preservada.

**A23, A27, A28 e A63:** são propostas de organização/clareza e possibilidades de percurso. Não há medição de confusão, cliques ou frequência. A preferência precisa de validação com quem opera.

**A70:** o texto absoluto está no código, mas o julgamento de sua inadequação editorial depende dos estados e exceções descritos em outros achados; permanece prioridade de clareza.

Votos sustentados também não dispensam teste. A confiança retornada pelo modelo não é probabilidade calibrada de defeito, gravidade nem frequência. A responsabilidade pelas conclusões e recomendações deste relatório permanece editorial.

## Dados enviados e rastreabilidade

Os pedidos contêm material de código, rótulos, nomes de arquivos, commits e propostas desta auditoria. Não foi consultado nem enviado banco de pacientes. As respostas salvas foram reduzidas a modelo solicitado, escolhas, confiança, uso e SHA-256 do pedido. A chave foi lida de arquivo externo ao repositório. Não há repetição automática após falha/resultado incerto.

Todos os arquivos `*-resultado.json` têm seu pedido correspondente em `jev/`. O validador desta entrega confere hashes, conjuntos de IDs e cobertura. Alterar uma evidência exige uma nova rodada identificável, sem reaproveitar silenciosamente o voto antigo.
