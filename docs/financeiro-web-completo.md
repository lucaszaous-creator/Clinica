# Financeiro integral em HTML/CSS/TypeScript

A versão anterior modernizou apenas o resumo e encaminhava a navegação para views WPF. Agora as 15 rotas do executável Financeiro permanecem no WebView2 local, com componentes comuns de página, tabela, campo, gráfico e diálogo. A logo da clínica, fonte Inter e ícones Lucide são arquivos locais. O padrão para migrações futuras está em `AGENTS.md`.

Rotas: resumo/caixa, contas, inadimplência, plano de contas, fluxo de caixa, resultado, fechamento, recebíveis, conciliação, extrato bancário, produção, pacotes, estoque, repasses e taxas. Abas e ações existentes estão nos registros explícitos de páginas e diálogos. Formulários, confirmações, histórico e troca de senha são web. Seleção de arquivos pelo sistema operacional e abertura de exportações permanecem nativas.

O C# conserva serviços, cálculos, auditoria, autenticação e permissões. O navegador envia apenas ações/campos registrados, com contexto de página e IDs da instância atual. A sessão é revalidada; cargas e gravações bloqueiam comandos concorrentes. Campos recusados impedem salvar silenciosamente o valor anterior. O apresentador assíncrono preserva os fluxos WPF dos outros executáveis que compartilham ViewModels.

## Evidências locais

- TypeScript/Vite e build Windows do Financeiro concluídos.
- WebView2 real com SQLite em memória: 15 páginas em 1440×900, 1100×720 e 900×600, sem navegação para outra janela WPF.
- 25 contratos de formulários/prompts, diálogos filhos, campos inválidos, IDs obsoletos, sessão/permissões, cancelamento e dupla gravação.
- Criação real de lançamento, conta, categoria e estoque pela interface; criação/edição de taxa e recorrência pelos contratos e serviços. Cancelamento verificado sem persistência.
- Cargas iniciais/concorrentes com consultas retidas e séries de gráficos comparadas aos valores das tabelas.
- Harness WPF compartilhado: 15 rotas em três tamanhos, 15 diálogos e séries com 299/300/301 lançamentos.
- Compilador sombra, verificador da suíte e espelho dos tokens executados. O CI Windows também executa o harness web antes de gerar os aplicativos.

Os testes não emitem Pix, movimentação bancária ou documentos externos e não acessam dados da clínica. `?demo=1` continua sendo somente uma demonstração do resumo; não é a evidência de migração integral.

## Participação do Jev

Duas consultas efetivas à API TypeSafe retornaram `jev-1.13.0`, com código e cenários sintéticos. O pedido inicial maior foi recusado com HTTP 400 e não produziu parecer; a avaliação foi dividida em páginas e diálogos.

- Páginas: `coerente`, confiança retornada 0,91; SHA-256 do pedido `62179a501c01f011f5c63af2a3c782422d4e0d37aefc2dd6849ab855076ea80c`.
- Integração de diálogos: `adequada_com_testes`, confiança retornada 0,49; SHA-256 do pedido `df939b85f9b6b886d78b59031e21fb6865a6f02683f9b8117515a1de01c43117`.

São escolhas tipadas, não justificativas textuais nem aprovação irrestrita. A menor confiança do segundo parecer é registrada explicitamente. Jev não recebeu imagens, não executou os testes e não certificou produção. A inspeção visual foi feita nas capturas reais do WebView2; publicação depende ainda do PR e do CI.
