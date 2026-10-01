# Revisão por superfície

Uma ficha por arquivo identificado na extração. A triagem do Jev recebeu os controles relevantes de todos os 263 arquivos. A revisão editorial aprofundou os fluxos descritos nos achados; **não houve execução de cada tela nem leitura manual integral de todos os arquivos**. As recomendações de domínio abaixo são roteiros de melhoria/verificação, não defeitos adicionais contabilizados. Estilos e App.xaml permanecem no inventário para não serem confundidos com telas perdidas.

Para cada rótulo, campo e ação individual, consulte [CATALOGO.md](CATALOGO.md) e [controles.csv](controles.csv). Para implementações, consulte [funcoes.csv](funcoes.csv). Ausência de achado específico não significa aprovação da interface.

## S001 — Clinica / src/Clinica.Clinico/App.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Clinico/App.xaml) · 12 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.44.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S002 — Clinica / src/Clinica.Desktop.Shell/Componentes/AjudaView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/AjudaView.xaml) · 97 linhas · 20 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** [A69](ACHADOS.md#a69), [A70](ACHADOS.md#a70).

**Triagem Jev:** `acao` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S003 — Clinica / src/Clinica.Desktop.Shell/Componentes/AssinaturaPacienteWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/AssinaturaPacienteWindow.xaml) · 376 linhas · 41 controles extraídos.

**Abas/ações/títulos identificados:** Paciente recusou assinar; Cancelar; Confirmar assinatura; Marcar todas como Sim; Registrar alergia; Limpar assinatura; Enviar para o celular…; Cancelar envio.

**Achados relacionados por arquivo/nome de ViewModel:** [A28](ACHADOS.md#a28).

**Triagem Jev:** `acao` · confiança retornada 0.77.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S004 — Clinica / src/Clinica.Desktop.Shell/Componentes/BuscaCidWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/BuscaCidWindow.xaml) · 66 linhas · 11 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Usar este código; Limpar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.46.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S005 — Clinica / src/Clinica.Desktop.Shell/Componentes/BuscaDePacienteView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/BuscaDePacienteView.xaml) · 140 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** [A40](ACHADOS.md#a40).

**Triagem Jev:** `insuficiente` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S006 — Clinica / src/Clinica.Desktop.Shell/Componentes/Cadastro/CadastroPacienteWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/Cadastro/CadastroPacienteWindow.xaml) · 130 linhas · 49 controles extraídos.

**Abas/ações/títulos identificados:** Tirar foto; Remover foto; Cancelar; Salvar cadastro.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S007 — Clinica / src/Clinica.Desktop.Shell/Componentes/CatalogoEnfermagemWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/CatalogoEnfermagemWindow.xaml) · 141 linhas · 13 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Acrescentar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.34.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S008 — Clinica / src/Clinica.Desktop.Shell/Componentes/CatalogoPacotesWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/CatalogoPacotesWindow.xaml) · 82 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Novo pacote; Editar; Excluir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S009 — Clinica / src/Clinica.Desktop.Shell/Componentes/CobrancaDoPacienteWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/CobrancaDoPacienteWindow.xaml) · 134 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Cobrar pelo WhatsApp; Fechar; Receber.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.71.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S010 — Clinica / src/Clinica.Desktop.Shell/Componentes/ConsultaContextualWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ConsultaContextualWindow.xaml) · 17 linhas · 3 controles extraídos.

**Abas/ações/títulos identificados:** Voltar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.20.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S011 — Clinica / src/Clinica.Desktop.Shell/Componentes/ConsultaDeEnfermagemView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ConsultaDeEnfermagemView.xaml) · 64 linhas · 6 controles extraídos.

**Abas/ações/títulos identificados:** Abrir a consulta de enfermagem….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.58.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S012 — Clinica / src/Clinica.Desktop.Shell/Componentes/ConsultaDeEnfermagemWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ConsultaDeEnfermagemWindow.xaml) · 80 linhas · 7 controles extraídos.

**Abas/ações/títulos identificados:** Fechar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.47.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S013 — Clinica / src/Clinica.Desktop.Shell/Componentes/ConsumosPacoteWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ConsumosPacoteWindow.xaml) · 104 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Devolver ao saldo.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.53.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S014 — Clinica / src/Clinica.Desktop.Shell/Componentes/DadosClinicaView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DadosClinicaView.xaml) · 21 linhas · 27 controles extraídos.

**Abas/ações/títulos identificados:** Salvar dados da clínica.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.31.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S015 — Clinica / src/Clinica.Desktop.Shell/Componentes/DadosRecebimentoView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DadosRecebimentoView.xaml) · 25 linhas · 20 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.47.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S016 — Clinica / src/Clinica.Desktop.Shell/Componentes/DetalheSessaoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DetalheSessaoWindow.xaml) · 129 linhas · 28 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Buscar CID….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S017 — Clinica / src/Clinica.Desktop.Shell/Componentes/DocumentoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoWindow.xaml) · 416 linhas · 63 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Emitir e imprimir; Usar modelo; Salvar novo modelo; Atualizar selecionado; Excluir selecionado; Adicionar linha; Remover; Buscar….

**Achados relacionados por arquivo/nome de ViewModel:** [A04](ACHADOS.md#a04), [A21](ACHADOS.md#a21).

**Triagem Jev:** `acao` · confiança retornada 0.75.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S018 — Clinica / src/Clinica.Desktop.Shell/Componentes/EnfermagemView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EnfermagemView.xaml) · 464 linhas · 36 controles extraídos.

**Abas/ações/títulos identificados:** Atender;  Voltar; {Binding FolhaDeHoje}; {Binding TermoPendente}; Feito; Não feito; Cancelar correção; Imprimir a passagem; {Binding Passagem.RotuloDoBotao, FallbackValue=Registrar}; A passagem de hoje; Passagens do paciente; Prontuário do paciente.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.57.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S019 — Clinica / src/Clinica.Desktop.Shell/Componentes/EscolhaDeConvenioWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolhaDeConvenioWindow.xaml) · 103 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Vincular convênio e continuar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.60.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S020 — Clinica / src/Clinica.Desktop.Shell/Componentes/EscolherCertificadoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherCertificadoWindow.xaml) · 147 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar lista; Buscar no SafeID (nuvem); Cancelar; Assinar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.63.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S021 — Clinica / src/Clinica.Desktop.Shell/Componentes/EscolherPacienteWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherPacienteWindow.xaml) · 59 linhas · 8 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Escolher.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S022 — Clinica / src/Clinica.Desktop.Shell/Componentes/EscolherSessaoDoTermoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherSessaoDoTermoWindow.xaml) · 53 linhas · 7 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Colher assinatura….

**Achados relacionados por arquivo/nome de ViewModel:** [A28](ACHADOS.md#a28).

**Triagem Jev:** `acao` · confiança retornada 0.54.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S023 — Clinica / src/Clinica.Desktop.Shell/Componentes/EscolherSessaoEnfermagemWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherSessaoEnfermagemWindow.xaml) · 19 linhas · 5 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Vincular a esta sessão.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.48.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S024 — Clinica / src/Clinica.Desktop.Shell/Componentes/EscolherTermoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherTermoWindow.xaml) · 68 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Colher assinatura….

**Achados relacionados por arquivo/nome de ViewModel:** [A28](ACHADOS.md#a28).

**Triagem Jev:** `acao` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S025 — Clinica / src/Clinica.Desktop.Shell/Componentes/EscreverSessaoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscreverSessaoWindow.xaml) · 175 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Salvar sessão; Fechar; Anexar arquivo…; Retirar…; Salvar como….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.45.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S026 — Clinica / src/Clinica.Desktop.Shell/Componentes/EvolucaoEnfermagemWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EvolucaoEnfermagemWindow.xaml) · 117 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar correção; Fechar; {Binding RotuloDoBotao}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.78.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S027 — Clinica / src/Clinica.Desktop.Shell/Componentes/FolhaDaSessaoView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/FolhaDaSessaoView.xaml) · 187 linhas · 25 controles extraídos.

**Abas/ações/títulos identificados:** Usar texto anterior; Mapa corporal; Modelos…; {Binding AbrirDetalheCommand}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.54.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S028 — Clinica / src/Clinica.Desktop.Shell/Componentes/FolhaExecucaoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/FolhaExecucaoWindow.xaml) · 355 linhas · 44 controles extraídos.

**Abas/ações/títulos identificados:** Imprimir folha; Imprimir registro; Anotar; Fechar; Encerrar execução; Validar e assinar como médico; Devolver à enfermagem; Revisar devolução; Corrigir horários; Cancelar registro; Assinar execução; ☐ Sim; ☐ Não; ☐ Não executável; Retificar; Suspender.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.84.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S029 — Clinica / src/Clinica.Desktop.Shell/Componentes/InfusaoExternaWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/InfusaoExternaWindow.xaml) · 57 linhas · 29 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar registro e revisar assinatura.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.58.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S030 — Clinica / src/Clinica.Desktop.Shell/Componentes/LegendaFamiliasView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/LegendaFamiliasView.xaml) · 62 linhas · 7 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S031 — Clinica / src/Clinica.Desktop.Shell/Componentes/LinhaDoTempoClinicaView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/LinhaDoTempoClinicaView.xaml) · 226 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** Ver; {Binding DataContext.RotuloAbrir, RelativeSource={RelativeSource AncestorType=ItemsControl}}; Cancelar….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S032 — Clinica / src/Clinica.Desktop.Shell/Componentes/MapaCorporalControl.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/MapaCorporalControl.xaml) · 164 linhas · 39 controles extraídos.

**Abas/ações/títulos identificados:** {Binding DataContext.SelecionarPontoCommand, RelativeSource={RelativeSource AncestorType=UserControl}}; Copiar mapa; Usar modelo; Salvar modelo com estes pontos; Excluir modelo selecionado; Desfazer; Limpar pontos; Remover.

**Achados relacionados por arquivo/nome de ViewModel:** [A21](ACHADOS.md#a21).

**Triagem Jev:** `acao` · confiança retornada 0.54.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S033 — Clinica / src/Clinica.Desktop.Shell/Componentes/MapaCorporalWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/MapaCorporalWindow.xaml) · 42 linhas · 5 controles extraídos.

**Abas/ações/títulos identificados:** Descartar alterações; Usar mapa nesta sessão.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.53.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S034 — Clinica / src/Clinica.Desktop.Shell/Componentes/MateriaisProcedimentoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/MateriaisProcedimentoWindow.xaml) · 48 linhas · 19 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; {Binding RotuloConfirmar}; Outro lote.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.63.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S035 — Clinica / src/Clinica.Desktop.Shell/Componentes/ModelosEvolucaoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ModelosEvolucaoWindow.xaml) · 146 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Repetir a última sessão deste paciente; Fechar; Aplicar nesta sessão; Guardar; Apagar o modelo escolhido.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.43.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S036 — Clinica / src/Clinica.Desktop.Shell/Componentes/PacoteCatalogoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PacoteCatalogoWindow.xaml) · 77 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; {Binding RotuloDoBotao}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.46.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S037 — Clinica / src/Clinica.Desktop.Shell/Componentes/PacoteVendaWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PacoteVendaWindow.xaml) · 242 linhas · 38 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Vender; Cadastrar um pacote no catálogo….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.63.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S038 — Clinica / src/Clinica.Desktop.Shell/Componentes/PacotesView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PacotesView.xaml) · 181 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Catálogo…; Orçamento…; Limpar filtro; Atualizar; Vender pacote…; Usar sessão; Sessões…; Cancelar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S039 — Clinica / src/Clinica.Desktop.Shell/Componentes/PainelDoPacienteWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PainelDoPacienteWindow.xaml) · 110 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Apagar e assinar de novo.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.64.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S040 — Clinica / src/Clinica.Desktop.Shell/Componentes/PassagemDeEnfermagemView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PassagemDeEnfermagemView.xaml) · 130 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S041 — Clinica / src/Clinica.Desktop.Shell/Componentes/PassagensDeEnfermagemView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PassagensDeEnfermagemView.xaml) · 180 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Vincular à sessão; Corrigir; Cancelar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.51.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S042 — Clinica / src/Clinica.Desktop.Shell/Componentes/PrecoParticularWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PrecoParticularWindow.xaml) · 83 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar preço.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.34.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S043 — Clinica / src/Clinica.Desktop.Shell/Componentes/PrecosParticularView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PrecosParticularView.xaml) · 153 linhas · 19 controles extraídos.

**Abas/ações/títulos identificados:** Novo preço; Atualizar; Editar; Excluir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.30.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S044 — Clinica / src/Clinica.Desktop.Shell/Componentes/ProcessoDeEnfermagemView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ProcessoDeEnfermagemView.xaml) · 294 linhas · 38 controles extraídos.

**Abas/ações/títulos identificados:** 1 · Histórico; 2 e 3 · Diagnósticos; Escrever à mão; Escolher do catálogo…; Remover; 4 · Cuidados; 5 · Avaliação.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `navegacao` · confiança retornada 0.48.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S045 — Clinica / src/Clinica.Desktop.Shell/Componentes/RecebimentoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/RecebimentoWindow.xaml) · 25 linhas · 5 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Confirmar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.47.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S046 — Clinica / src/Clinica.Desktop.Shell/Componentes/RegrasFaturamentoView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/RegrasFaturamentoView.xaml) · 47 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** Salvar faturamento.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S047 — Clinica / src/Clinica.Desktop.Shell/Componentes/SalaInfusaoView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/SalaInfusaoView.xaml) · 264 linhas · 30 controles extraídos.

**Abas/ações/títulos identificados:** Abrir pelo código; Atualizar; Registrar infusão com orientação externa; Avaliar e assinar; Imprimir; Anotar; Abrir folha.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.65.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S048 — Clinica / src/Clinica.Desktop.Shell/Componentes/SessaoDoProntuarioWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/SessaoDoProntuarioWindow.xaml) · 160 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** {Binding AnexosTexto}; {Binding CorrecoesTexto}; Fechar; Imprimir esta sessão.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.53.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S049 — Clinica / src/Clinica.Desktop.Shell/Componentes/TelaComAbas.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/TelaComAbas.xaml) · 44 linhas · 1 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S050 — Clinica / src/Clinica.Desktop.Shell/Componentes/TrocaSenhaWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/TrocaSenhaWindow.xaml) · 37 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Trocar a senha.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.42.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S051 — Clinica / src/Clinica.Desktop.Shell/Componentes/VersoesEvolucaoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/VersoesEvolucaoWindow.xaml) · 108 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S052 — Clinica / src/Clinica.Desktop.Shell/Controls/CapturaFotoWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Controls/CapturaFotoWindow.xaml) · 62 linhas · 13 controles extraídos.

**Abas/ações/títulos identificados:** Escolher arquivo…; Cancelar; Repetir; Capturar; Usar esta foto.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.56.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S053 — Clinica / src/Clinica.Desktop.Shell/Controls/PromptWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Controls/PromptWindow.xaml) · 30 linhas · 7 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Confirmar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.19.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S054 — Clinica / src/Clinica.Desktop.Shell/Shell/LoginWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/LoginWindow.xaml) · 170 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** Entrar; Sair; Entrar com certificado (SafeID).

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.65.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S055 — Clinica / src/Clinica.Desktop.Shell/Shell/SetupWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/SetupWindow.xaml) · 48 linhas · 11 controles extraídos.

**Abas/ações/títulos identificados:** Testar conexão; Salvar e continuar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.46.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S056 — Clinica / src/Clinica.Desktop.Shell/Shell/ShellWindow.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/ShellWindow.xaml) · 345 linhas · 33 controles extraídos.

**Abas/ações/títulos identificados:** Treinamento; {Binding DataContext.NavegarResultadoCommand,                                                               RelativeSource={RelativeSource AncestorType=Window}}; {Binding AbrirFilaInfusaoCommand}; {Binding AbrirAvisosCommand}; Minha senha; Trocar usuário.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `navegacao` · confiança retornada 0.15.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S057 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Abas.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Abas.xaml) · 123 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.47.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S058 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/AgendaModeloA.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/AgendaModeloA.xaml) · 85 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.60.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S059 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Botoes.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Botoes.xaml) · 254 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S060 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Campos.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Campos.xaml) · 574 linhas · 11 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.72.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S061 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Cartoes.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Cartoes.xaml) · 448 linhas · 3 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S062 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Feedback.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Feedback.xaml) · 375 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** {TemplateBinding TextoAcao}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.83.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S063 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Graficos.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Graficos.xaml) · 54 linhas · 2 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.45.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S064 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Icones.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Icones.xaml) · 72 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.45.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S065 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Midia.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Midia.xaml) · 64 linhas · 1 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S066 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Navegacao.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Navegacao.xaml) · 130 linhas · 1 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.57.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S067 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Pacientes.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Pacientes.xaml) · 190 linhas · 11 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.44.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S068 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Selecao.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Selecao.xaml) · 223 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S069 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Sobreposicao.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Sobreposicao.xaml) · 142 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.47.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S070 — Clinica / src/Clinica.Desktop.Shell/Styles/Componentes/Tabelas.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Tabelas.xaml) · 109 linhas · 1 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.41.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S071 — Clinica / src/Clinica.Desktop.Shell/Styles/Suite.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Suite.xaml) · 28 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.41.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S072 — Clinica / src/Clinica.Desktop.Shell/Styles/Theme.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Theme.xaml) · 126 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S073 — Clinica / src/Clinica.Desktop.Shell/Styles/Tokens.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Tokens.xaml) · 197 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.55.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S074 — Clinica / src/Clinica.Desktop.Shell/Treinamento/TreinamentoView.xaml

**Domínio:** Componentes e shell. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Treinamento/TreinamentoView.xaml) · 37 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** Fechar aula; Marcar como concluída; Rever desde o início; {Binding Titulo}; Abrir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.75.

**Verificação/melhoria recomendada:** Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.

## S075 — Clinica / src/Clinica.Desktop/App.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop/App.xaml) · 9 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.49.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S076 — Clinica / src/Clinica.Financeiro/App.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Financeiro/App.xaml) · 12 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S077 — Clinica / src/Clinica.Gerente/App.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Gerente/App.xaml) · 12 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.56.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S078 — Clinica / src/Clinica.Modulo.Clinico/Janelas/AnexosSessaoWindow.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/AnexosSessaoWindow.xaml) · 124 linhas · 13 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Anexar arquivo; Salvar em disco; Remover.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.30.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S079 — Clinica / src/Clinica.Modulo.Clinico/Janelas/AplicarAvaliacaoWindow.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/AplicarAvaliacaoWindow.xaml) · 89 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Gravar avaliação.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.41.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S080 — Clinica / src/Clinica.Modulo.Clinico/Janelas/PrescricaoInternaWindow.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/PrescricaoInternaWindow.xaml) · 276 linhas · 49 controles extraídos.

**Abas/ações/títulos identificados:** Acrescentar item; Fechar; Salvar rascunho; Assinar e enviar à sala; Usar modelo selecionado; Salvar novo modelo; Atualizar selecionado; Remover.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.67.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S081 — Clinica / src/Clinica.Modulo.Clinico/Janelas/ProblemaWindow.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/ProblemaWindow.xaml) · 107 linhas · 21 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Gravar; Buscar….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S082 — Clinica / src/Clinica.Modulo.Clinico/Janelas/RegistrarMedidaWindow.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/RegistrarMedidaWindow.xaml) · 85 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Registrar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S083 — Clinica / src/Clinica.Modulo.Clinico/Janelas/RegistrarResultadoExameWindow.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/RegistrarResultadoExameWindow.xaml) · 136 linhas · 29 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Registrar; Escolher arquivo…; Tirar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.31.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S084 — Clinica / src/Clinica.Modulo.Clinico/Janelas/ResultadosDoPedidoWindow.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/ResultadosDoPedidoWindow.xaml) · 122 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Abrir exames do paciente; Fechar; Abrir laudo.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S085 — Clinica / src/Clinica.Modulo.Clinico/Janelas/ResumoProntuarioWindow.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/ResumoProntuarioWindow.xaml) · 212 linhas · 28 controles extraídos.

**Abas/ações/títulos identificados:** Abrir prontuário completo; 2ª via da anamnese; Fechar; Evoluções; Abrir sessão; Anamnese; Anexos.

**Achados relacionados por arquivo/nome de ViewModel:** [A41](ACHADOS.md#a41).

**Triagem Jev:** `navegacao` · confiança retornada 0.52.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S086 — Clinica / src/Clinica.Modulo.Clinico/Views/AcompanhamentoView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AcompanhamentoView.xaml) · 32 linhas · 4 controles extraídos.

**Abas/ações/títulos identificados:** Evolução da dor; Medidas; Avaliações.

**Achados relacionados por arquivo/nome de ViewModel:** [A25](ACHADOS.md#a25), [A63](ACHADOS.md#a63).

**Triagem Jev:** `navegacao` · confiança retornada 0.61.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S087 — Clinica / src/Clinica.Modulo.Clinico/Views/AnamneseView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AnamneseView.xaml) · 202 linhas · 19 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Gravar; {Binding RotuloDoBotao}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.63.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S088 — Clinica / src/Clinica.Modulo.Clinico/Views/AnexosPacienteView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AnexosPacienteView.xaml) · 330 linhas · 31 controles extraídos.

**Abas/ações/títulos identificados:** Registrar resultado…; Anexar arquivo à ficha…; Anexar laudo…; Abrir laudo; Cancelar…; Abrir; Salvar….

**Achados relacionados por arquivo/nome de ViewModel:** [A61](ACHADOS.md#a61).

**Triagem Jev:** `acao` · confiança retornada 0.52.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S089 — Clinica / src/Clinica.Modulo.Clinico/Views/AtendimentoEnfermagemView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AtendimentoEnfermagemView.xaml) · 376 linhas · 29 controles extraídos.

**Abas/ações/títulos identificados:** Registrar infusão; Consultar ficha; Exames e anexos; {Binding FolhaDeHoje}; {Binding TermoPendente}; Feito; Não feito; Cancelar correção; Imprimir a passagem; {Binding Passagem.RotuloDoBotao}; A passagem de hoje; Passagens do paciente; Conduta médica e infusões.

**Achados relacionados por arquivo/nome de ViewModel:** [A20](ACHADOS.md#a20).

**Triagem Jev:** `acao` · confiança retornada 0.46.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S090 — Clinica / src/Clinica.Modulo.Clinico/Views/AtendimentoView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AtendimentoView.xaml) · 433 linhas · 27 controles extraídos.

**Abas/ações/títulos identificados:** Imprimir a sessão; Salvar sessão; {Binding RotuloBsv}; Consultar ficha; Exames e anexos; Receitas, documentos e infusão; Vincular registro existente; A sessão de hoje; Colher termo…; Sessões anteriores; Enfermagem e infusões.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S091 — Clinica / src/Clinica.Modulo.Clinico/Views/AvaliacoesView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AvaliacoesView.xaml) · 197 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Aplicar; Cancelar….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S092 — Clinica / src/Clinica.Modulo.Clinico/Views/DocumentosPacienteView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/DocumentosPacienteView.xaml) · 17 linhas · 5 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar documentos; Receitas e documentos; Infusões.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `navegacao` · confiança retornada 0.28.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S093 — Clinica / src/Clinica.Modulo.Clinico/Views/EmissoesNoAtendimentoView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/EmissoesNoAtendimentoView.xaml) · 177 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Prescrever infusão; 2ª via; Emitir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.60.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S094 — Clinica / src/Clinica.Modulo.Clinico/Views/EvolucaoDorView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/EvolucaoDorView.xaml) · 192 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** Exportar CSV; Atualizar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.44.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S095 — Clinica / src/Clinica.Modulo.Clinico/Views/ExamesView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/ExamesView.xaml) · 209 linhas · 24 controles extraídos.

**Abas/ações/títulos identificados:** {Binding NovoPedidoCommand}; Anexar laudo; Ver resultados; Detalhes.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.30.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S096 — Clinica / src/Clinica.Modulo.Clinico/Views/MedidasView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/MedidasView.xaml) · 286 linhas · 26 controles extraídos.

**Abas/ações/títulos identificados:** Prontuário; Registrar medida; Exportar CSV; Cancelar….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S097 — Clinica / src/Clinica.Modulo.Clinico/Views/MeuDiaView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/MeuDiaView.xaml) · 350 linhas · 25 controles extraídos.

**Abas/ações/títulos identificados:** <; >; Hoje; Atualizar; {Binding RotuloPendentes}; {Binding RotuloRegistro}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.34.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S098 — Clinica / src/Clinica.Modulo.Clinico/Views/MeusNumerosView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/MeusNumerosView.xaml) · 215 linhas · 39 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Ver e escrever.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.37.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S099 — Clinica / src/Clinica.Modulo.Clinico/Views/MinhaSemanaView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/MinhaSemanaView.xaml) · 270 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** {Binding DataContext.AbrirCommand,                               RelativeSource={RelativeSource AncestorType=UserControl}}; <; >; Esta semana; Atualizar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `navegacao` · confiança retornada 0.24.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S100 — Clinica / src/Clinica.Modulo.Clinico/Views/PacienteCapaView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PacienteCapaView.xaml) · 131 linhas · 42 controles extraídos.

**Abas/ações/títulos identificados:** Adicionar registro; Editar; Resolvido; Reabrir; Descartar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.59.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S101 — Clinica / src/Clinica.Modulo.Clinico/Views/PacienteView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PacienteView.xaml) · 28 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** WhatsApp; Editar cadastro; Atualizar ficha; Dados e alertas; Sessões e guias; Autorizações e validade do convênio; Anamnese; Agenda e pendências; Relacionamento; Privacidade e consentimentos; Termos do paciente.

**Achados relacionados por arquivo/nome de ViewModel:** [A07](ACHADOS.md#a07), [A16](ACHADOS.md#a16).

**Triagem Jev:** `navegacao` · confiança retornada 0.49.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S102 — Clinica / src/Clinica.Modulo.Clinico/Views/PacienteWorkspaceView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PacienteWorkspaceView.xaml) · 545 linhas · 34 controles extraídos.

**Abas/ações/títulos identificados:** {Binding VoltarCommand}; Iniciar atendimento; Reabrir atendimento; Concluir sessão; Registrar materiais; Trocar paciente; {Binding Atendimento.ColherTermoCommand}; Atendimento; Atendimento de enfermagem; Ficha do paciente; Histórico; Exames e anexos; Prescrições e documentos; Acompanhamento.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `navegacao` · confiança retornada 0.43.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S103 — Clinica / src/Clinica.Modulo.Clinico/Views/PrescricaoInfusaoView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PrescricaoInfusaoView.xaml) · 204 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Nova prescrição; Editar; Abrir; Imprimir; Execução; Cancelar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S104 — Clinica / src/Clinica.Modulo.Clinico/Views/PrescricoesClinicasView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PrescricoesClinicasView.xaml) · 318 linhas · 19 controles extraídos.

**Abas/ações/títulos identificados:** Prescrição de infusão; {Binding Rotulo}; Assinar; 2ª via; ⋯.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.62.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S105 — Clinica / src/Clinica.Modulo.Clinico/Views/ProntuarioClinicoView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/ProntuarioClinicoView.xaml) · 141 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar histórico; Evoluções médicas; Abrir sessão; {Binding AnexosTexto}; {Binding CorrecoesTexto}; Enfermagem e infusões.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `navegacao` · confiança retornada 0.15.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S106 — Clinica / src/Clinica.Modulo.Clinico/Views/ProntuariosView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/ProntuariosView.xaml) · 235 linhas · 28 controles extraídos.

**Abas/ações/títulos identificados:** {Binding NovoProntuarioCommand}; Escrever; Assinar; Abrir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S107 — Clinica / src/Clinica.Modulo.Clinico/Views/RegistrosPendentesView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/RegistrosPendentesView.xaml) · 142 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Limpar filtro; Escrever evolução.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.36.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S108 — Clinica / src/Clinica.Modulo.Clinico/Views/SessoesEnfermagemView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/SessoesEnfermagemView.xaml) · 58 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Filtrar / atualizar; Limpar filtros; Anterior; Próxima; Abrir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.26.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S109 — Clinica / src/Clinica.Modulo.Clinico/Views/SessoesPacienteView.xaml

**Domínio:** Clínico. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/SessoesPacienteView.xaml) · 46 linhas · 11 controles extraídos.

**Abas/ações/títulos identificados:** Anterior; Próxima.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.18.

**Verificação/melhoria recomendada:** Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.

## S110 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/AvisoPendenciasWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/AvisoPendenciasWindow.xaml) · 55 linhas · 8 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Ver painel de pendências.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.79.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S111 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/BaixaGuiaWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/BaixaGuiaWindow.xaml) · 42 linhas · 13 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Registrar baixa.

**Achados relacionados por arquivo/nome de ViewModel:** [A15](ACHADOS.md#a15).

**Triagem Jev:** `acao` · confiança retornada 0.52.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S112 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/BaixaLoteWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/BaixaLoteWindow.xaml) · 57 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Confirmar baixas.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.62.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S113 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/ConvenioWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/ConvenioWindow.xaml) · 230 linhas · 54 controles extraídos.

**Abas/ações/títulos identificados:** Fechar.

**Achados relacionados por arquivo/nome de ViewModel:** [A33](ACHADOS.md#a33).

**Triagem Jev:** `sem_indicio` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S114 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/EnvioLoteWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/EnvioLoteWindow.xaml) · 31 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Marcar enviado.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.64.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S115 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/GlosaWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/GlosaWindow.xaml) · 56 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Ver o guia completo; Cancelar; Registrar glosa.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.62.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S116 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/GuiaGlosasWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/GuiaGlosasWindow.xaml) · 108 linhas · 20 controles extraídos.

**Abas/ações/títulos identificados:** Fechar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.41.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S117 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/HistoricoGuiaWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/HistoricoGuiaWindow.xaml) · 99 linhas · 13 controles extraídos.

**Abas/ações/títulos identificados:** Fechar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.30.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S118 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/NaoConformidadeWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/NaoConformidadeWindow.xaml) · 29 linhas · 8 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Marcar não conformidade.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.67.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S119 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/ObservacaoPendenciaWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/ObservacaoPendenciaWindow.xaml) · 31 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar observação.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.45.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S120 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/RegrasFamiliaWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RegrasFamiliaWindow.xaml) · 58 linhas · 8 controles extraídos.

**Abas/ações/títulos identificados:** Fechar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S121 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml) · 78 linhas · 13 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Registrar retorno.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.53.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S122 — Clinica / src/Clinica.Modulo.Faturamento/Alertas/RodadaPendenciasWindow.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RodadaPendenciasWindow.xaml) · 72 linhas · 12 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Concluir rodada.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.65.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S123 — Clinica / src/Clinica.Modulo.Faturamento/Styles/Componentes/Tabelas.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Styles/Componentes/Tabelas.xaml) · 63 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S124 — Clinica / src/Clinica.Modulo.Faturamento/Styles/Conversores.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Styles/Conversores.xaml) · 30 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.46.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S125 — Clinica / src/Clinica.Modulo.Faturamento/Views/BaixaView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/BaixaView.xaml) · 67 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Confirmar baixa.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S126 — Clinica / src/Clinica.Modulo.Faturamento/Views/ConsultaGuiasView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/ConsultaGuiasView.xaml) · 191 linhas · 36 controles extraídos.

**Abas/ações/títulos identificados:** {Binding BuscarCommand}; Limpar; Capa; Histórico.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.25.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S127 — Clinica / src/Clinica.Modulo.Faturamento/Views/DashboardView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/DashboardView.xaml) · 572 linhas · 80 controles extraídos.

**Abas/ações/títulos identificados:** {Binding AtualizarCommand}; {Binding RodarPendenciasCommand}; Tentar de novo; Limpar filtros; {Binding DarBaixaEmLoteCommand}; Dar baixa; {Binding DataContext.WhatsappCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}; {Binding TemObservacao, Converter={StaticResource ObservacaoParaRotulo}}; NC; Abrir glosas; Abrir ficha; {Binding DataContext.WhatsappCarteirinhaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}; Renovar; {Binding DataContext.WhatsappConsultaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}.

**Achados relacionados por arquivo/nome de ViewModel:** [A32](ACHADOS.md#a32).

**Triagem Jev:** `acao` · confiança retornada 0.62.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S128 — Clinica / src/Clinica.Modulo.Faturamento/Views/FaturadosView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/FaturadosView.xaml) · 112 linhas · 24 controles extraídos.

**Abas/ações/títulos identificados:** Buscar; {Binding ExportarCsvCommand}; Limpar filtro; Capa; Guia; Glosar; Estornar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.41.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S129 — Clinica / src/Clinica.Modulo.Faturamento/Views/FaturamentoHostView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/FaturamentoHostView.xaml) · 47 linhas · 3 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S130 — Clinica / src/Clinica.Modulo.Faturamento/Views/GlosasView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/GlosasView.xaml) · 152 linhas · 29 controles extraídos.

**Abas/ações/títulos identificados:** {Binding BuscarCommand}; {Binding GerarRecursoXmlCommand}; {Binding AbrirGuiaCommand}; Limpar filtro; Entenda; Reapresentar; Recuperada.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.55.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S131 — Clinica / src/Clinica.Modulo.Faturamento/Views/NaoConformidadesView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/NaoConformidadesView.xaml) · 113 linhas · 16 controles extraídos.

**Abas/ações/títulos identificados:** {Binding AtualizarCommand}; Limpar filtro; Ver justificativa; Reabrir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S132 — Clinica / src/Clinica.Modulo.Faturamento/Views/ParametrosView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/ParametrosView.xaml) · 269 linhas · 49 controles extraídos.

**Abas/ações/títulos identificados:** Convênios; Números das regras…; + Novo convênio; Abrir; Excluir; Prazos; Modalidades; + Nova modalidade; Especialidades; + Nova especialidade; Clínica / prestador; Códigos TUSS; Salvar configurações.

**Achados relacionados por arquivo/nome de ViewModel:** [A33](ACHADOS.md#a33), [A34](ACHADOS.md#a34).

**Triagem Jev:** `navegacao` · confiança retornada 0.24.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S133 — Clinica / src/Clinica.Modulo.Faturamento/Views/RelatoriosView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/RelatoriosView.xaml) · 297 linhas · 51 controles extraídos.

**Abas/ações/títulos identificados:** Gerar; {Binding ExportarCsvCommand}; {Binding GerarFechamentoCommand}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.59.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S134 — Clinica / src/Clinica.Modulo.Faturamento/Views/TissView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/TissView.xaml) · 92 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Gerar lote TISS (.xml); XML; Marcar enviado; Registrar retorno.

**Achados relacionados por arquivo/nome de ViewModel:** [A64](ACHADOS.md#a64), [A65](ACHADOS.md#a65), [A66](ACHADOS.md#a66), [A68](ACHADOS.md#a68).

**Triagem Jev:** `acao` · confiança retornada 0.55.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S135 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/CategoriaWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/CategoriaWindow.xaml) · 46 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Criar categoria.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.31.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S136 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/CobrancaPixWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/CobrancaPixWindow.xaml) · 95 linhas · 16 controles extraídos.

**Abas/ações/títulos identificados:** Gerar código; Copiar; Fechar.

**Achados relacionados por arquivo/nome de ViewModel:** [A58](ACHADOS.md#a58).

**Triagem Jev:** `acao` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S137 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/ContaWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ContaWindow.xaml) · 92 linhas · 27 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Registrar conta.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S138 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/ContasFixasWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ContasFixasWindow.xaml) · 90 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Nova conta fixa; Editar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.32.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S139 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/ExtratoEstoqueWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ExtratoEstoqueWindow.xaml) · 110 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Fechar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.54.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S140 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/ItemEstoqueWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ItemEstoqueWindow.xaml) · 89 linhas · 41 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar item.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.33.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S141 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/LancamentoWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/LancamentoWindow.xaml) · 172 linhas · 39 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar lançamento.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.31.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S142 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/MovimentoEstoqueWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/MovimentoEstoqueWindow.xaml) · 138 linhas · 38 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Registrar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.49.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S143 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/OrcamentoWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/OrcamentoWindow.xaml) · 45 linhas · 12 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar teto.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.47.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S144 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/RecorrenteWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/RecorrenteWindow.xaml) · 93 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar conta fixa.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.29.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S145 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/RegraRepasseWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/RegraRepasseWindow.xaml) · 99 linhas · 21 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar regra.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S146 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/RegrasRepasseWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/RegrasRepasseWindow.xaml) · 116 linhas · 12 controles extraídos.

**Abas/ações/títulos identificados:** Excluir; Cancelar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S147 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/TaxaWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/TaxaWindow.xaml) · 113 linhas · 27 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar taxa.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.32.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S148 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/TributoWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/TributoWindow.xaml) · 98 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar tributo.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.29.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S149 — Clinica / src/Clinica.Modulo.Financeiro/Janelas/ValidadesEstoqueWindow.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ValidadesEstoqueWindow.xaml) · 80 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.37.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S150 — Clinica / src/Clinica.Modulo.Financeiro/Views/CaixaView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/CaixaView.xaml) · 291 linhas · 51 controles extraídos.

**Abas/ações/títulos identificados:** ◀; ▶; Atualizar; Exportar CSV; Cobrar por Pix; Novo lançamento; Limpar filtro; Histórico; Realizar; Recibo; Cancelar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.56.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S151 — Clinica / src/Clinica.Modulo.Financeiro/Views/ConciliacaoView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ConciliacaoView.xaml) · 476 linhas · 71 controles extraídos.

**Abas/ações/títulos identificados:** ◀; ▶; Atualizar; A lançar; Limpar filtro; Reter?; Lançar; Glosadas; Derrubar receita; Particulares.

**Achados relacionados por arquivo/nome de ViewModel:** [A35](ACHADOS.md#a35), [A37](ACHADOS.md#a37).

**Triagem Jev:** `acao` · confiança retornada 0.53.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S152 — Clinica / src/Clinica.Modulo.Financeiro/Views/ContasView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ContasView.xaml) · 260 linhas · 44 controles extraídos.

**Abas/ações/títulos identificados:** Contas fixas…; Nova conta; Gerar fixas; Exportar CSV; Atualizar; Histórico; Baixar; Adiar 7d.

**Achados relacionados por arquivo/nome de ViewModel:** [A27](ACHADOS.md#a27).

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S153 — Clinica / src/Clinica.Modulo.Financeiro/Views/EstoqueView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/EstoqueView.xaml) · 295 linhas · 54 controles extraídos.

**Abas/ações/títulos identificados:** Validades e mínimos…; Itens; Lista de compras; Novo item; Atualizar; Extrato; Movimentar…; Inventário…; Editar; Excluir; Materiais dos atendimentos; Salvar ativação; Consultar; Ver / registrar materiais; Custo por sessão.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.24.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S154 — Clinica / src/Clinica.Modulo.Financeiro/Views/ExtratoBancoView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ExtratoBancoView.xaml) · 172 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Abrir extrato (.ofx)…; Recruzar; Conferir; Desfazer.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.45.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S155 — Clinica / src/Clinica.Modulo.Financeiro/Views/FechamentoCaixaView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/FechamentoCaixaView.xaml) · 289 linhas · 40 controles extraídos.

**Abas/ações/títulos identificados:** Conferir caixa; Conferir; Atualizar; Reabrir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.62.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S156 — Clinica / src/Clinica.Modulo.Financeiro/Views/FluxoCaixaView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/FluxoCaixaView.xaml) · 293 linhas · 50 controles extraídos.

**Abas/ações/títulos identificados:** Exportar; Atualizar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S157 — Clinica / src/Clinica.Modulo.Financeiro/Views/InadimplenciaView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/InadimplenciaView.xaml) · 312 linhas · 42 controles extraídos.

**Abas/ações/títulos identificados:** Limpar filtro; Atualizar; Exportar; WhatsApp; Recebi.

**Achados relacionados por arquivo/nome de ViewModel:** [A36](ACHADOS.md#a36).

**Triagem Jev:** `acao` · confiança retornada 0.65.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S158 — Clinica / src/Clinica.Modulo.Financeiro/Views/PlanoContasView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/PlanoContasView.xaml) · 103 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** Nova categoria; Atualizar; Ativar / desativar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S159 — Clinica / src/Clinica.Modulo.Financeiro/Views/ProducaoView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ProducaoView.xaml) · 155 linhas · 28 controles extraídos.

**Abas/ações/títulos identificados:** 6 meses; 12 meses; Atualizar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.62.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S160 — Clinica / src/Clinica.Modulo.Financeiro/Views/RecebiveisView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/RecebiveisView.xaml) · 295 linhas · 45 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Previstos; Caiu; Já caíram; Desfazer.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.58.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S161 — Clinica / src/Clinica.Modulo.Financeiro/Views/RepassesView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/RepassesView.xaml) · 125 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** ◀; ▶; Exportar CSV; Atualizar; Regras e apurações…; Apurar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.33.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S162 — Clinica / src/Clinica.Modulo.Financeiro/Views/ResultadoView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ResultadoView.xaml) · 277 linhas · 43 controles extraídos.

**Abas/ações/títulos identificados:** ◀; ▶; Exportar CSV; Atualizar; Resultado; Teto de gasto; Definir teto; Excluir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.21.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S163 — Clinica / src/Clinica.Modulo.Financeiro/Views/TaxasView.xaml

**Domínio:** Financeiro. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/TaxasView.xaml) · 501 linhas · 72 controles extraídos.

**Abas/ações/títulos identificados:** Maquininha; Nova taxa; Editar; Excluir; Regime tributário; Salvar alíquota; Novo tributo; Simulador; Simular; Apuração do mês; ◀; ▶; Apurar; Exportar CSV.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.19.

**Verificação/melhoria recomendada:** Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.

## S164 — Clinica / src/Clinica.Modulo.Gerente/Janelas/MetaWindow.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Janelas/MetaWindow.xaml) · 104 linhas · 21 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar meta.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.36.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S165 — Clinica / src/Clinica.Modulo.Gerente/Janelas/PrecoConvenioWindow.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Janelas/PrecoConvenioWindow.xaml) · 98 linhas · 20 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar preço.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.30.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S166 — Clinica / src/Clinica.Modulo.Gerente/Janelas/UsuarioWindow.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Janelas/UsuarioWindow.xaml) · 204 linhas · 31 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar usuário.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.30.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S167 — Clinica / src/Clinica.Modulo.Gerente/Views/AcessosView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/AcessosView.xaml) · 158 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Novo usuário; Editar; Redefinir senha; Excluir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.43.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S168 — Clinica / src/Clinica.Modulo.Gerente/Views/AuditoriaView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/AuditoriaView.xaml) · 260 linhas · 39 controles extraídos.

**Abas/ações/títulos identificados:** Consultar; Limpar; Exportar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.33.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S169 — Clinica / src/Clinica.Modulo.Gerente/Views/CampanhasView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/CampanhasView.xaml) · 200 linhas · 33 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Abrir confirmações da agenda; NPS de ontem; Abrir fila de recall; WhatsApp; Nota; Respondeu; Dispensar.

**Achados relacionados por arquivo/nome de ViewModel:** [A31](ACHADOS.md#a31), [A39](ACHADOS.md#a39).

**Triagem Jev:** `acao` · confiança retornada 0.66.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S170 — Clinica / src/Clinica.Modulo.Gerente/Views/CamposPersonalizadosWindow.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/CamposPersonalizadosWindow.xaml) · 170 linhas · 29 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Salvar campo; Novo campo; Editar; {Binding DataContext.AlternarCommand,                                                               RelativeSource={RelativeSource AncestorType=Window}}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.67.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S171 — Clinica / src/Clinica.Modulo.Gerente/Views/ConfiguracoesView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/ConfiguracoesView.xaml) · 389 linhas · 136 controles extraídos.

**Abas/ações/títulos identificados:** Clínica; Atendimento; Salvar regras de conclusão; Atualizar pendências; Operação; Escrever termos…; Campos do prontuário…; Salvar operação; Faturamento; Integrações; Salvar publicação; {Binding RotuloTesteConexao}; Enviar arquivo de teste; Salvar lembretes; {Binding RotuloTesteEmail}; Salvar; Testar; Backup; Escolher pasta…; Salvar política; Copiar agora; Salvar cópia em…; Conferir um backup.

**Achados relacionados por arquivo/nome de ViewModel:** [A21](ACHADOS.md#a21), [A59](ACHADOS.md#a59).

**Triagem Jev:** `acao` · confiança retornada 0.51.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S172 — Clinica / src/Clinica.Modulo.Gerente/Views/CustoTransacaoView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/CustoTransacaoView.xaml) · 311 linhas · 51 controles extraídos.

**Abas/ações/títulos identificados:** Exportar; Atualizar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.47.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S173 — Clinica / src/Clinica.Modulo.Gerente/Views/DocumentosEmitidosView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/DocumentosEmitidosView.xaml) · 330 linhas · 41 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Conferir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.29.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S174 — Clinica / src/Clinica.Modulo.Gerente/Views/FaturamentoGerencialView.xaml

**Domínio:** Faturamento. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/FaturamentoGerencialView.xaml) · 317 linhas · 48 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Fechamento em PDF.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.37.

**Verificação/melhoria recomendada:** Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.

## S175 — Clinica / src/Clinica.Modulo.Gerente/Views/GuardaProntuarioView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/GuardaProntuarioView.xaml) · 222 linhas · 38 controles extraídos.

**Abas/ações/títulos identificados:** Calcular a guarda; Exportar o prontuário desta pessoa; Exportar a clínica inteira.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.71.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S176 — Clinica / src/Clinica.Modulo.Gerente/Views/ImportacaoPacientesView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/ImportacaoPacientesView.xaml) · 323 linhas · 48 controles extraídos.

**Abas/ações/títulos identificados:** Escolher o pacote (ZIP) do Smart Clinic…; Ou um CSV de pacientes…; Ou o ZIP de arquivos (receitas, laudos)…; Gerar a prévia; {Binding RotuloImportar}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.77.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S177 — Clinica / src/Clinica.Modulo.Gerente/Views/IndicadoresView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/IndicadoresView.xaml) · 331 linhas · 54 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Exportar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.55.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S178 — Clinica / src/Clinica.Modulo.Gerente/Views/MetasView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/MetasView.xaml) · 187 linhas · 28 controles extraídos.

**Abas/ações/títulos identificados:** ◀; ▶; Nova meta; Editar; Excluir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.36.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S179 — Clinica / src/Clinica.Modulo.Gerente/Views/ModelosTermoWindow.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/ModelosTermoWindow.xaml) · 241 linhas · 34 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Salvar termo; Novo termo; Criar os termos do BSV…; Acrescentar; Remover; Passar a exigir; {Binding AcaoRotulo}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.80.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S180 — Clinica / src/Clinica.Modulo.Gerente/Views/OrigensView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/OrigensView.xaml) · 162 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.51.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S181 — Clinica / src/Clinica.Modulo.Gerente/Views/PainelDirecaoView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/PainelDirecaoView.xaml) · 378 linhas · 49 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; {Binding DestinoRotulo}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.47.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S182 — Clinica / src/Clinica.Modulo.Gerente/Views/PrecosConvenioView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/PrecosConvenioView.xaml) · 199 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** Limpar filtro; Novo preço; Atualizar; Editar; Excluir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.33.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S183 — Clinica / src/Clinica.Modulo.Gerente/Views/RentabilidadeConvenioView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/RentabilidadeConvenioView.xaml) · 270 linhas · 48 controles extraídos.

**Abas/ações/títulos identificados:** Exportar; Atualizar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.54.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S184 — Clinica / src/Clinica.Modulo.Gerente/Views/RetencaoView.xaml

**Domínio:** Gestão. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/RetencaoView.xaml) · 183 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Exportar CSV; Atualizar; Limpar filtro; Chamar de volta.

**Achados relacionados por arquivo/nome de ViewModel:** [A38](ACHADOS.md#a38).

**Triagem Jev:** `acao` · confiança retornada 0.59.

**Verificação/melhoria recomendada:** Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.

## S185 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/AgendamentoWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/AgendamentoWindow.xaml) · 225 linhas · 35 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Assumir encaixe; Salvar horário.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.63.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S186 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/AutorizacaoWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/AutorizacaoWindow.xaml) · 116 linhas · 21 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar autorização.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S187 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/BloqueioWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/BloqueioWindow.xaml) · 93 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Fechar a agenda.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.68.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S188 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/ConciliacaoAgendaWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ConciliacaoAgendaWindow.xaml) · 176 linhas · 24 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Fechar; Foi falta; Aconteceu — lançar; Já foi lançada — encerrar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.79.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S189 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/ConfirmacoesWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ConfirmacoesWindow.xaml) · 17 linhas · 2 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.56.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S190 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/DetalheHorarioWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/DetalheHorarioWindow.xaml) · 172 linhas · 28 controles extraídos.

**Abas/ações/títulos identificados:** Remarcar; Confirmar pelo WhatsApp; Comprovante; Quem chamar?; Marcar falta; Cancelar horário; Reabrir este horário; Cancelar o resto da série; Fechar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.75.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S191 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/EstornoAtendimentoWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/EstornoAtendimentoWindow.xaml) · 83 linhas · 16 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Estornar atendimento.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.34.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S192 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/FechamentoSessaoWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/FechamentoSessaoWindow.xaml) · 193 linhas · 39 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Fechar; Concluir sessão.

**Achados relacionados por arquivo/nome de ViewModel:** [A24](ACHADOS.md#a24).

**Triagem Jev:** `acao` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S193 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/HorariosProfissionalWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/HorariosProfissionalWindow.xaml) · 64 linhas · 29 controles extraídos.

**Abas/ações/títulos identificados:** Salvar configuração; Fechar; Atualizar lista; Fechar agenda…; Atualizar bloqueios.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.28.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S194 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaPainelWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaPainelWindow.xaml) · 105 linhas · 13 controles extraídos.

**Abas/ações/títulos identificados:** Adicionar à lista; Ver a lista inteira; Fechar; Chamar; Sair da lista.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.35.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S195 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaWindow.xaml) · 105 linhas · 21 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Entrar na lista.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sobreposicao` · confiança retornada 0.24.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S196 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/OrcamentoWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/OrcamentoWindow.xaml) · 184 linhas · 33 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; Emitir e imprimir; Adicionar linha; ; Recalcular total.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.59.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S197 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/ProfissionalWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ProfissionalWindow.xaml) · 180 linhas · 48 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar profissional.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.43.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S198 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/ProximasVagasWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ProximasVagasWindow.xaml) · 56 linhas · 7 controles extraídos.

**Abas/ações/títulos identificados:** Fechar; {Binding Rotulo}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.43.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S199 — Clinica / src/Clinica.Modulo.Recepcao/Janelas/SalaWindow.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/SalaWindow.xaml) · 61 linhas · 16 controles extraídos.

**Abas/ações/títulos identificados:** Cancelar; Salvar sala.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.55.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S200 — Clinica / src/Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml) · 129 linhas · 92 controles extraídos.

**Abas/ações/títulos identificados:** Recall · pacientes sem retorno; Novos BSV · primeira sessão; Atualizar; Configurar acompanhamento; Buscar pacientes; {Binding AAssumirTexto}; {Binding HojeTexto}; {Binding AtrasadosTexto}; {Binding SemContatoTexto}; Mais filtros; Limpar filtros; {Binding AcaoTexto}; Aplicar filtros e voltar; Voltar à lista; Assumir acompanhamento; Abrir WhatsApp; Agendar sessão; Salvar acompanhamento; Salvar configuração; Cadastrar motivo.

**Achados relacionados por arquivo/nome de ViewModel:** [A25](ACHADOS.md#a25), [A63](ACHADOS.md#a63).

**Triagem Jev:** `acao` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S201 — Clinica / src/Clinica.Modulo.Recepcao/Views/AgendaView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/AgendaView.xaml) · 323 linhas · 64 controles extraídos.

**Abas/ações/títulos identificados:** {Binding DataContext.AbrirHorarioCommand,                               RelativeSource={RelativeSource AncestorType=UserControl}}; {Binding DataContext.AgendarNaFaixaCommand, RelativeSource={RelativeSource AncestorType=UserControl}}; +; Horários e travas; Agendar; Próximas vagas; {Binding EsperaResumo}; Confirmar amanhã…; Conferir atendimentos…; Imprimir folha; Fechar agenda…; Novo horário; Tentar novamente; Ir para hoje; ‹; ›; Dia; Semana; Atualizar; Ver mais dias e horários; {Binding DataContext.EscolherVagaPlanejamentoCommand, RelativeSource={RelativeSource AncestorType=UserControl}}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S202 — Clinica / src/Clinica.Modulo.Recepcao/Views/ConfirmacoesView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ConfirmacoesView.xaml) · 131 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Amanhã; Gerar rodada; Enviar e-mails; Atualizar; WhatsApp; Confirmou.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.29.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S203 — Clinica / src/Clinica.Modulo.Recepcao/Views/ConsultasView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ConsultasView.xaml) · 194 linhas · 24 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Limpar filtro; Renovar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S204 — Clinica / src/Clinica.Modulo.Recepcao/Views/ConvenioPacienteView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ConvenioPacienteView.xaml) · 73 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** Renovar validade da consulta; Nova autorização; Editar; Excluir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.34.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S205 — Clinica / src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml) · 701 linhas · 65 controles extraídos.

**Abas/ações/títulos identificados:** Emitir; {Binding DataContext.EscolherDeHojeCommand,                                                               RelativeSource={RelativeSource AncestorType=ItemsControl}}; Trocar; {Binding Previa.AcaoRotulo}; {Binding DataContext.EscolherFolhaCommand,                                                           RelativeSource={RelativeSource AncestorType=ItemsControl}}; O que já saiu; Atualizar; Conferir; Segunda via; Assinar; 2ª via; ⋯.

**Achados relacionados por arquivo/nome de ViewModel:** [A01](ACHADOS.md#a01), [A03](ACHADOS.md#a03), [A05](ACHADOS.md#a05), [A06](ACHADOS.md#a06), [A29](ACHADOS.md#a29).

**Triagem Jev:** `acao` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S206 — Clinica / src/Clinica.Modulo.Recepcao/Views/EquipeView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/EquipeView.xaml) · 288 linhas · 34 controles extraídos.

**Abas/ações/títulos identificados:** Profissionais; Novo profissional; Editar; Excluir; Salas; Nova sala; Bloqueios; Fechar agenda…; Empurrar sessões; Reabrir.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.15.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S207 — Clinica / src/Clinica.Modulo.Recepcao/Views/FilaView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/FilaView.xaml) · 580 linhas · 41 controles extraídos.

**Abas/ações/títulos identificados:** ◀; ▶; Hoje; Atualizar; Marcar atendimento; Novo horário; {Binding Rotulo}; {Binding ProximoPasso}; Editar; ⋯.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S208 — Clinica / src/Clinica.Modulo.Recepcao/Views/LancamentosView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/LancamentosView.xaml) · 209 linhas · 27 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Limpar filtros; Estornar….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.42.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S209 — Clinica / src/Clinica.Modulo.Recepcao/Views/NovoAtendimentoView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/NovoAtendimentoView.xaml) · 1271 linhas · 107 controles extraídos.

**Abas/ações/títulos identificados:** Trocar; Próximas vagas…; {Binding DataContext.EscolherModalidadeCommand,                                                       RelativeSource={RelativeSource AncestorType=ItemsControl}}; Capa inicial (PDF); {Binding RotuloLancar}; {Binding RotuloOutro}; Limpar; Escolher convênio…; Receber…; Vender pacote…; {Binding RotuloVerGuia}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.53.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S210 — Clinica / src/Clinica.Modulo.Recepcao/Views/PacientesView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PacientesView.xaml) · 165 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Ver todos; Novo paciente.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.20.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S211 — Clinica / src/Clinica.Modulo.Recepcao/Views/PagamentosView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PagamentosView.xaml) · 56 linhas · 12 controles extraídos.

**Abas/ações/títulos identificados:** Trocar paciente; Atualizar; Receber; Recibo.

**Achados relacionados por arquivo/nome de ViewModel:** [A05](ACHADOS.md#a05), [A62](ACHADOS.md#a62).

**Triagem Jev:** `sem_indicio` · confiança retornada 0.24.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S212 — Clinica / src/Clinica.Modulo.Recepcao/Views/PainelView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PainelView.xaml) · 419 linhas · 59 controles extraídos.

**Abas/ações/títulos identificados:** ◀; ▶; Hoje; Atualizar; WhatsApp; Parabenizar.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.23.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S213 — Clinica / src/Clinica.Modulo.Recepcao/Views/PrivacidadePacienteView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PrivacidadePacienteView.xaml) · 89 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Colher assinatura…; Revogar; Exportar meus dados; Anonimizar cadastro.

**Achados relacionados por arquivo/nome de ViewModel:** [A57](ACHADOS.md#a57).

**Triagem Jev:** `acao` · confiança retornada 0.45.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S214 — Clinica / src/Clinica.Modulo.Recepcao/Views/ProntuarioView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ProntuarioView.xaml) · 226 linhas · 31 controles extraídos.

**Abas/ações/títulos identificados:**  Pacientes; Nova sessão; Ver; Editar; Cancelar….

**Achados relacionados por arquivo/nome de ViewModel:** [A15](ACHADOS.md#a15).

**Triagem Jev:** `acao` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S215 — Clinica / src/Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml) · 56 linhas · 12 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** [A56](ACHADOS.md#a56).

**Triagem Jev:** `sem_indicio` · confiança retornada 0.46.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S216 — Clinica / src/Clinica.Modulo.Recepcao/Views/ResumoAdministrativoPacienteView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ResumoAdministrativoPacienteView.xaml) · 97 linhas · 16 controles extraídos.

**Abas/ações/títulos identificados:** Receber…; Abrir na agenda.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.24.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S217 — Clinica / src/Clinica.Modulo.Recepcao/Views/RetornoView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RetornoView.xaml) · 203 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Gerar rodada do mês; Limpar filtro; WhatsApp; Respondeu.

**Achados relacionados por arquivo/nome de ViewModel:** [A15](ACHADOS.md#a15).

**Triagem Jev:** `acao` · confiança retornada 0.34.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S218 — Clinica / src/Clinica.Modulo.Recepcao/Views/RetornosAMarcarView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RetornosAMarcarView.xaml) · 158 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Atualizar; Marcar horário; WhatsApp.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.28.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S219 — Clinica / src/Clinica.Modulo.Recepcao/Views/TermosPacienteView.xaml

**Domínio:** Recepção. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/TermosPacienteView.xaml) · 60 linhas · 10 controles extraídos.

**Abas/ações/títulos identificados:** Colher um termo…; Colher assinatura….

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.

## S220 — Clinica / src/Clinica.Recepcao/App.xaml

**Domínio:** Recurso compartilhado. **Natureza:** recurso/composição, não página autônoma.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Recepcao/App.xaml) · 12 linhas · 0 controles extraídos.

**Abas/ações/títulos identificados:** nenhum rótulo de ação extraído; examinar bindings, composição e consumidores..

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.55.

**Verificação/melhoria recomendada:** Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.

## S221 — Clinica / src/Clinica.Web/Paginas.cs

**Domínio:** Web de leitura. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Web/Paginas.cs) · 370 linhas · 21 controles extraídos.

**Abas/ações/títulos identificados:** {T(p.Titulo)}; Sair; Entrar; O dia; Ver; O mês; {T(p.Nome)}; Pacientes; Buscar; {T(paciente.Nome)}; Próximas sessões; Prontuário.

**Achados relacionados por arquivo/nome de ViewModel:** [A52](ACHADOS.md#a52).

**Triagem Jev:** `acao` · confiança retornada 0.23.

**Verificação/melhoria recomendada:** Explicitar recorte e limites; permitir continuidade autorizada sem tornar leitura em alteração.

## S222 — clinica-site / conteudo/404.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/404.html) · 39 linhas · 10 controles extraídos.

**Abas/ações/títulos identificados:** Esta página não existe; Início; Especialidades; Convênios; Contato.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.59.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S223 — clinica-site / conteudo/a-clinica.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/a-clinica.html) · 15 linhas · 21 controles extraídos.

**Abas/ações/títulos identificados:** A Clínica SemDor, em Macaé; Clínica da Dor Macaé, CDM e Clínica SemDor; atendimentos disponíveis; convênios; contatos da recepção; @clinicadadormacae; Áreas de atendimento; Conheça as especialidades e os serviços; Direção técnica; Conheça o Dr. Gustavo Lacerda; Atendimento em Imbetiba; Veja o endereço no mapa e os contatos; Informação e privacidade; política de privacidade; {{ email_privacidade }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.22.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S224 — clinica-site / conteudo/acessibilidade.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/acessibilidade.html) · 11 linhas · 11 controles extraídos.

**Abas/ações/títulos identificados:** Acessibilidade; Acesso ao local; endereço e o mapa; Recursos do site; Encontrou uma dificuldade?; {{ email }}; {{ telefone }}; WhatsApp.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.23.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S225 — clinica-site / conteudo/acupuntura-medica.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/acupuntura-medica.html) · 70 linhas · 30 controles extraídos.

**Abas/ações/títulos identificados:** Acupuntura médica em Macaé; Consultar agenda pelo WhatsApp; Endereço e telefone; Acupuntura no cuidado da dor; NCCIH/NIH resume essas evidências; consulta para dor persistente; Como são as sessões de acupuntura e eletroacupuntura; O que considerar antes do tratamento; Dúvidas sobre acupuntura; convênios atendidos; Acupuntura por convênio ou particular em Macaé; as orientações para usar seu convênio; como chegar à clínica; como solicitar o agendamento; clínica da dor; Informações de referência; Acupuncture: Effectiveness and Safety, do NCCIH/NIH; o que levar à primeira consulta; os registros do diretor técnico; Agendar uma avaliação; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.24.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S226 — clinica-site / conteudo/atendimento.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/atendimento.html) · 13 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Agendamento e atendimento; Agendar pelo WhatsApp; Ligar: {{ telefone }}; Como organizar o agendamento; convênios atendidos; o que levar à consulta; Alterações, retorno e procedimentos; Endereço e contato; Veja como chegar, estacionamento e acessibilidade; {{ email }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.18.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S227 — clinica-site / conteudo/bloqueio-simpatico-venoso.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/bloqueio-simpatico-venoso.html) · 59 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Bloqueio simpático venoso (BSV); A decisão é feita em consulta; orientações para a primeira consulta; Preparo para o dia agendado; Consentimento e acompanhamento; Riscos e cuidados após a sessão; Dúvidas sobre o BSV; página de convênios; Converse com a equipe; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S228 — clinica-site / conteudo/clinica-da-dor.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/clinica-da-dor.html) · 160 linhas · 31 controles extraídos.

**Abas/ações/títulos identificados:** Clínica da Dor em Macaé; Consultar agenda pelo WhatsApp; Endereço e telefone; Quando procurar uma clínica da dor; fibromialgia; Como é o tratamento; acupuntura médica; reabilitação; Dúvidas sobre o tratamento da dor; o mapa e as orientações para chegar; WhatsApp {{ whatsapp }}; como funciona o agendamento; orientações sobre avaliação e acompanhamento da fibromialgia; Como preparar sua consulta em Macaé; guia da primeira consulta; formação e os registros do Dr. Gustavo Lacerda; confirmada com a recepção; o mapa e as orientações de acesso; Marcar uma avaliação; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.18.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S229 — clinica-site / conteudo/contato.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/contato.html) · 90 linhas · 11 controles extraídos.

**Abas/ações/títulos identificados:** Clínica SemDor em Macaé: contato e como chegar; Endereço; Atendimento; Ligar: {{ telefone }}; WhatsApp {{ whatsapp }}; {{ email }}; Agendar pelo WhatsApp; Abrir no Google Maps; Como chegar e ser atendido.

**Achados relacionados por arquivo/nome de ViewModel:** [A46](ACHADOS.md#a46).

**Triagem Jev:** `acao` · confiança retornada 0.31.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S230 — clinica-site / conteudo/convenios.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/convenios.html) · 116 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Convênios atendidos em Macaé; Como funciona na prática; O que perguntam sobre o convênio; Conferir a cobertura do seu plano; consulta para avaliação da dor; sessões de acupuntura; demais atendimentos disponíveis; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.23.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S231 — clinica-site / conteudo/dr-gustavo-lacerda.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/dr-gustavo-lacerda.html) · 65 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** {{ medico_nome }}; Avaliação e acompanhamento na Clínica SemDor; clínica da dor; acupuntura médica; como se preparar para a primeira consulta; Agendar com o Dr. Gustavo Lacerda em Macaé; os convênios atendidos; o endereço completo, o mapa e os contatos; Perfis públicos do Dr. Gustavo Lacerda; Marcar uma consulta; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.16.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S232 — clinica-site / conteudo/endocrinologia.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/endocrinologia.html) · 42 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** Endocrinologia em Macaé; Consultar agenda pelo WhatsApp; Endereço e telefone; O que faz o endocrinologista; Sociedade Brasileira de Endocrinologia e Metabologia; Informações úteis para levar à endocrinologia; Primeira consulta e continuidade do acompanhamento; Perguntas antes da consulta; primeira consulta; convênios atendidos; Endereço da consulta em Macaé; os contatos e as orientações para chegar; Agendar consulta de endocrinologia; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.18.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S233 — clinica-site / conteudo/especialidades.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/especialidades.html) · 112 linhas · 15 controles extraídos.

**Abas/ações/títulos identificados:** Especialidades e serviços em Macaé; Áreas de atendimento médico; avaliação e acompanhamento de fibromialgia; Consultas em Macaé para quem mora na região; como chegar à clínica; Outros serviços da clínica; O que perguntam antes de escolher; página de convênios; Não sabe qual consulta marcar?; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S234 — clinica-site / conteudo/fibromialgia.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/fibromialgia.html) · 50 linhas · 25 controles extraídos.

**Abas/ações/títulos identificados:** Fibromialgia em Macaé; Consultar agenda pelo WhatsApp; Endereço e telefone; O que é fibromialgia; NIAMS, instituto de saúde dos Estados Unidos, apresenta uma visão geral da fibromialgia; Como é feita a avaliação; como organizar os documentos para a primeira consulta; Tratamento e continuidade do cuidado; NIAMS explica a investigação e as diferentes frentes do tratamento; consulta de acompanhamento da dor; serviços disponíveis; Dúvidas sobre a consulta de fibromialgia; as orientações sobre convênios; Onde fica a clínica e como organizar sua visita; o mapa, os contatos e as informações de acesso; Agendar avaliação de fibromialgia em Macaé; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.21.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S235 — clinica-site / conteudo/geriatria.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/geriatria.html) · 43 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Geriatria em Macaé; Consultar agenda pelo WhatsApp; Endereço e telefone; O papel do geriatra no acompanhamento; Sociedade Brasileira de Geriatria e Gerontologia explica esses conceitos em seu guia sobre envelhecimento; O que conversar na consulta de geriatria; atendimento para avaliação da dor; Como a pessoa idosa e o acompanhante podem se preparar; Dúvidas sobre o agendamento; as informações de acessibilidade; lista de convênios; Consulta de geriatria em Imbetiba; como chegar à Clínica SemDor; Agendar consulta de geriatria; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.28.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S236 — clinica-site / conteudo/ginecologia.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/ginecologia.html) · 42 linhas · 22 controles extraídos.

**Abas/ações/títulos identificados:** Ginecologia em Macaé; Consultar agenda pelo WhatsApp; Endereço e telefone; Assuntos para conversar com o ginecologista; Ministério da Saúde reúne orientações sobre saúde da mulher; Como se preparar para a consulta de ginecologia; Consulta, exames e retornos; Dúvidas sobre ginecologia na clínica; planos atendidos; a orientação para a primeira consulta; Localização do atendimento; como chegar e falar com a recepção; Agendar consulta de ginecologia; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S237 — clinica-site / conteudo/inicio.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/inicio.html) · 79 linhas · 49 controles extraídos.

**Abas/ações/títulos identificados:** Clínica SemDor; clínica da dor; acupuntura médica; Conheça a clínica; Sua próxima consulta; Agendar uma consulta; {{ telefone }}; Endereço e como chegar; /primeira-consulta/; /convenios/; /contato/; Cuidado com a dor.; Clínica da Dor; Conheça o atendimento; Acupuntura médica; Sobre as sessões; Especialidades e serviços; Psiquiatria; Geriatria; Endocrinologia; Ginecologia; Ver todos os atendimentos; fibromialgia em Macaé; Quem responde; Registro profissional e formação; Orientações de acesso; Consulta por convênio ou particular; os convênios atendidos em Macaé; como agendar uma avaliação; O que você precisa saber.; Acesse o guia do paciente; a identificação e os atendimentos da clínica; WhatsApp; as orientações de agendamento; os convênios e documentos necessários; como chegar à Clínica SemDor; Fale com a recepção.; Conversar pelo WhatsApp; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.15.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S238 — clinica-site / conteudo/mapa-do-site.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/mapa-do-site.html) · 8 linhas · 2 controles extraídos.

**Abas/ações/títulos identificados:** Mapa do site.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.73.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S239 — clinica-site / conteudo/para-pacientes.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/para-pacientes.html) · 12 linhas · 26 controles extraídos.

**Abas/ações/títulos identificados:** Guia do paciente; Agendamento; Organizar o atendimento; Primeira consulta; Veja o que levar; Convênios e particular; Conferir os convênios; Como chegar; Planejar o deslocamento; Portal do paciente; Acessar o portal do paciente; Consulta e procedimento têm orientações diferentes; página sobre o BSV; as informações sobre as sessões; Precisa de apoio para a visita?; recursos de acessibilidade do site e do local; política de privacidade; Falar com a recepção.

**Achados relacionados por arquivo/nome de ViewModel:** [A44](ACHADOS.md#a44).

**Triagem Jev:** `acao` · confiança retornada 0.18.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S240 — clinica-site / conteudo/politica-de-privacidade.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/politica-de-privacidade.html) · 43 linhas · 13 controles extraídos.

**Abas/ações/títulos identificados:** Política de Privacidade; Quem responde pelos seus dados; {{ email_privacidade }}; Ao visitar este site; Ao entrar em contato; Dados do atendimento; Armazenamento e conservação; Como exercer seus direitos; Dúvidas e atualizações.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.33.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S241 — clinica-site / conteudo/primeira-consulta.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/primeira-consulta.html) · 94 linhas · 12 controles extraídos.

**Abas/ações/títulos identificados:** A primeira consulta; O que levar; Como é a avaliação; O que perguntam antes de vir; informações sobre o preparo do BSV; Marcar a primeira consulta; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.15.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S242 — clinica-site / conteudo/psiquiatria.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/psiquiatria.html) · 43 linhas · 24 controles extraídos.

**Abas/ações/títulos identificados:** Psiquiatria em Macaé; Consultar agenda pelo WhatsApp; Endereço e telefone; O que conversar com o psiquiatra; NIMH, instituto de saúde mental dos Estados Unidos; Como organizar as informações para a consulta; o que levar à primeira consulta; Psiquiatra ou psicólogo: qual é a diferença?; Associação Americana de Psiquiatria explica essas diferenças; psicologia e os demais serviços; Dúvidas antes de agendar psiquiatria; planos atendidos; Onde fica o atendimento; endereço, contatos e como chegar; Agendar consulta de psiquiatria; WhatsApp {{ whatsapp }}; Ligar: {{ telefone }}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.26.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S243 — clinica-site / modelos/base.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/modelos/base.html) · 73 linhas · 3 controles extraídos.

**Abas/ações/títulos identificados:** Pular para o conteúdo; {{ nome }} — página inicial; Agendar consulta.

**Achados relacionados por arquivo/nome de ViewModel:** [A46](ACHADOS.md#a46).

**Triagem Jev:** `sem_indicio` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S244 — clinica-site / modelos/rodape.html

**Domínio:** Site institucional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/modelos/rodape.html) · 57 linhas · 18 controles extraídos.

**Abas/ações/títulos identificados:** Como chegar; Contato; Ligar: {{ telefone }}; WhatsApp {{ whatsapp }}; {{ email }}; Atendimento; A clínica; Clínica da Dor em Macaé; Acupuntura médica; Especialidades; Convênios; Guia do paciente; Portal do paciente; Agendamento; Dr. Gustavo Lacerda; Política de Privacidade; Acessibilidade; Mapa do site.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `navegacao` · confiança retornada 0.25.

**Verificação/melhoria recomendada:** Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.

## S245 — clinica-site / portal/index.html

**Domínio:** Coleta de termos. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/index.html) · 20 linhas · 4 controles extraídos.

**Abas/ações/títulos identificados:** Clínica SemDor — voltar ao site; Voltar ao site; Consultório · atendimento pelo tablet.

**Achados relacionados por arquivo/nome de ViewModel:** [A11](ACHADOS.md#a11), [A20](ACHADOS.md#a20).

**Triagem Jev:** `sem_indicio` · confiança retornada 0.12.

**Verificação/melhoria recomendada:** Distinguir equipe e paciente, identidade, assinatura recebida e arquivamento; retomar sem nova emissão.

## S246 — clinica-site / portal/portal.js

**Domínio:** Coleta de termos. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/portal.js) · 219 linhas · 61 controles extraídos.

**Abas/ações/títulos identificados:** Acesso protegido; Entrar; Vamos reconectar; Tentar novamente; ${completos?'Termos assinados':'Acompanhe os termos'}; Abrir via assinada; Retomar arquivamento; Portal do paciente; Entrar com segurança; Termos de hoje; Sair; Buscar; BSV de hoje; ${agenda?`; ${esc(p.nome)}; Trocar paciente; Antes de entregar; Preparar nova coleta; !m.coberto)?'disabled':''}>Entregar tablet ao paciente; ${esc(d.nome)}; Chamar a enfermeira; ${esc(d.titulo)}; Suas respostas; Sua rubrica; Limpar rubrica; Confirmar e assinar; Não desejo assinar; ${recusa?'Você pode recusar a assinatura.':'Chame a enfermeira.'}; ${recusa?'Registrar recusa e encerrar':'Encerrar leitura e chamar equipe'}; Continuar lendo; ${esc(titulo)}; Retornar à equipe; ${concluido?'Assinado e arquivado':falha?'Assinatura recebida':'Assinaturas recebidas'}; ${esc(c.titulo||'Termo assinado')}; Conferir situação.

**Achados relacionados por arquivo/nome de ViewModel:** [A44](ACHADOS.md#a44), [A45](ACHADOS.md#a45).

**Triagem Jev:** `acao` · confiança retornada 0.38.

**Verificação/melhoria recomendada:** Distinguir equipe e paciente, identidade, assinatura recebida e arquivamento; retomar sem nova emissão.

## S247 — clinica-site / portal/profissional/clinico.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/clinico.js) · 395 linhas · 92 controles extraídos.

**Abas/ações/títulos identificados:** Seu consultório, onde você atende.; Entrar no consultório; Meu dia; Hoje; Atualizar; ${esc(h.nome)}; ${estado.permissoes?.atender===false?'Ver ficha':h.finalizado?'Ver atendimento':'Atender'}; Agenda livre nesta data; Continue seu trabalho; ${iconesAtalho.fila}; ${iconesAtalho.documentos}; ${iconesAtalho.pacientes}; ← Meu dia; ${esc(r.paciente.nome)}; Ficha completa; ${r.novoBsv.indicado?'Em acompanhamento BSV':'Novo paciente de BSV'}; ${t}; Salvar e concluir atendimento; Registrar materiais; Copiar última evolução; Evoluções anteriores; Copiar para esta sessão; Copiar mapa corporal; Prescrições e documentos; Nova prescrição; ${d.classe==='infusao'?rotuloPrescricao(d):'Abrir PDF'}; Corrigir rascunho; Cancelar rascunho; Copiar; a.papel==='Executante'&&a.registroArquivado))?'disabled':''}>${d.origemEnfermagem?'Validar e assinar como médico':'Assinar com SafeID'}; ${titulo}; Fechar; Voltar; '+esc(acao)+'; Salvar texto como modelo; Emitir documento; Fechar imagem; Página anterior; Próxima página; Fechar documento; Imprimir PDF; Autorizar no SafeID; Salvar endereço no cadastro; Conferir PDF atualizado; Mapa corporal; Guardar pontos como modelo; Remover ${esc(p.nome||'ponto '+(i+1))}; Adicionar ponto; Guardar modelo.

**Achados relacionados por arquivo/nome de ViewModel:** [A13](ACHADOS.md#a13), [A14](ACHADOS.md#a14).

**Triagem Jev:** `acao` · confiança retornada 0.36.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S248 — clinica-site / portal/profissional/coleta.html

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/coleta.html) · 21 linhas · 4 controles extraídos.

**Abas/ações/títulos identificados:** Clínica SemDor — voltar ao site; Voltar ao site; Consultório · atendimento pelo tablet.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `navegacao` · confiança retornada 0.21.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S249 — clinica-site / portal/profissional/execucao-direta.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/execucao-direta.js) · 41 linhas · 10 controles extraídos.

**Abas/ações/títulos identificados:** Execução de enfermagem; Voltar; Salvar justificativa.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.34.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S250 — clinica-site / portal/profissional/ficha-edicao.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/ficha-edicao.js) · 71 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** Voltar; '+botao+'; Fechar histórico.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `insuficiente` · confiança retornada 0.67.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S251 — clinica-site / portal/profissional/fila-enfermagem.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/fila-enfermagem.js) · 46 linhas · 14 controles extraídos.

**Abas/ações/títulos identificados:** ${historico?'Sessões anteriores':'Sessões de hoje'}; Hoje; Sessões anteriores; Filtrar sessões; Limpar filtros; ${esc(s.paciente)}; ${s.registrada?'Ver registros':s.chegadaRegistrada?'Registrar após aplicação':'Registrar chegada'}; Ver sessões anteriores; Anterior; Próxima.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.15.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S252 — clinica-site / portal/profissional/index.html

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/index.html) · 48 linhas · 17 controles extraídos.

**Abas/ações/títulos identificados:** Pular para o conteúdo; Clínica SemDor — meu dia; Áreas; /; /profissional/treinamento/; Sair; Seu atendimento está protegido.; Continuar atendimento.

**Achados relacionados por arquivo/nome de ViewModel:** [A11](ACHADOS.md#a11), [A20](ACHADOS.md#a20).

**Triagem Jev:** `insuficiente` · confiança retornada 0.15.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S253 — clinica-site / portal/profissional/materiais.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/materiais.js) · 60 linhas · 7 controles extraídos.

**Abas/ações/títulos identificados:** Outro lote deste produto; Fechar; ${dados.registrado?'Tentar baixa novamente':'Registrar materiais'}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.50.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S254 — clinica-site / portal/profissional/modelos-documento.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/modelos-documento.js) · 82 linhas · 16 controles extraídos.

**Abas/ações/títulos identificados:** Usar modelo; Acrescentar ao texto.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.29.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S255 — clinica-site / portal/profissional/modelos.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/modelos.js) · 89 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Modelos; ${esc(nome)}; Novo modelo; ${m?'Editar modelo':'Novo modelo'}; Cancelar; Salvar modelo; Arquivar; Salvar novo modelo.

**Achados relacionados por arquivo/nome de ViewModel:** [A21](ACHADOS.md#a21).

**Triagem Jev:** `acao` · confiança retornada 0.29.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S256 — clinica-site / portal/profissional/observacoes-enfermagem.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/observacoes-enfermagem.js) · 155 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** ← Voltar à agenda; ${esc(nome)}; Evolução; Termos de consentimento; Momento do atendimento; 1 · Chegada; 2 · Após aplicação; Sinais vitais e horário; Evolução de enfermagem; Gerenciar modelos de evolução; Aplicar modelo; Copiar evolução da chegada; Complementos; Adicionar intercorrência; Adicionar observação; Salvar chegada; Remover.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.21.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S257 — clinica-site / portal/profissional/posto.js

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js) · 247 linhas · 157 controles extraídos.

**Abas/ações/títulos identificados:** Acompanhamento da infusão; Revisar e reenviar; Próxima etapa; Próxima etapa da enfermagem; Como executar esta infusão; ${externa?'Infusão com orientação externa':'Pacientes'}; Buscar paciente; ${esc(p.nome)}; ${externa?'Registrar infusão':'Abrir ficha'}; ← Pacientes; Atender agora; Emitir documento; Registrar infusão externa realizada; Atualizar ficha; ${t}; Mais recentes; Mais antigos; Colher / revisar anamnese; Registrar problema ou alergia; Registrar medida; Histórico da anamnese; Dados cadastrais; Anamnese; Problemas, alergias e medicações; ${a.tipo==='Alergia'?'Editar alergia':'Editar registro'}; ${a.tipo==='Alergia'?'Revisar / remover alerta':'Alterar situação'}; Medidas e avaliações; Cancelar medida incorreta; Sessões e atendimentos; ${s.situacao==='Realizado'?'Ver atendimento':'Continuar atendimento'}; Histórico clínico; Ver mapa corporal; Resultados de exames; Registrar resultado de exame; Avaliações clínicas; Documentos do prontuário; ${d.classe==='infusao'?rotuloPrescricao(d):'Abrir PDF'}; ${d.situacao==='Devolvida'?'Revisar devolução':'Revisar ou cancelar'}; Corrigir rascunho; Cancelar rascunho; Copiar; a.papel==='Executante'&&a.registroArquivado))?'disabled':''}>${d.origemEnfermagem?'Validar e assinar como médico':'Assinar com SafeID'}; Registros de enfermagem; Registrar evolução de enfermagem; Vincular à sessão; Retificar evolução; Anexos; Enviar anexo; Abrir anexo PDF; Abrir imagem; Voltar; Iniciar atendimento; Todas as etapas; ${rotulo} · ${Number(valor)||0}; ${titulo}; ${acaoDaFila(p)}; Fila de infusões; Atualizar fila; Localizar paciente · orientação externa; Anterior; Próxima; ← ${e.origemInfusao==='pendencias'?'Minhas pendências':'Fila de infusões'}; ${esc(p.paciente)}; Ficha completa; ${rotuloPrescricao(p)}; Folha de checagens da enfermagem; Corrigir horários; Revisar devolução; Cancelar registro; ${esc(i.descricao)}; Retificar registro; Avaliar e assinar com SafeID; Devolver à enfermagem; Encerrar execução; Assinar execução com SafeID; Salvar registro; Salvar correção; Fechar mapa; Documentos para assinar; Atualizar; Localizar paciente; ${esc(d.paciente)}; Abrir este documento; Minhas pendências; Atendimentos sem conclusão clínica; Conferir atendimento; Documentos sem assinatura; Conferir este documento; Conferência da recepção; Ver protocolo e guias; Enfermagem; Ver infusões para executar ou assinar; ← Voltar à agenda; Imprimir registro; Registro de enfermagem; ${esc(f.paciente.nome)}.

**Achados relacionados por arquivo/nome de ViewModel:** [A11](ACHADOS.md#a11), [A12](ACHADOS.md#a12), [A14](ACHADOS.md#a14), [A42](ACHADOS.md#a42), [A43](ACHADOS.md#a43).

**Triagem Jev:** `acao` · confiança retornada 0.43.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S258 — clinica-site / portal/profissional/treinamento/index.html

**Domínio:** Portal profissional. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/treinamento/index.html) · 44 linhas · 9 controles extraídos.

**Abas/ações/títulos identificados:** Pular para as aulas; Voltar ao consultório; ← Voltar ao consultório; Aprenda cada função do consultório.; Selecione uma aula; Abrir o consultório.

**Achados relacionados por arquivo/nome de ViewModel:** [A11](ACHADOS.md#a11), [A20](ACHADOS.md#a20).

**Triagem Jev:** `sem_indicio` · confiança retornada 0.37.

**Verificação/melhoria recomendada:** Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.

## S259 — semdor-crm / src/Clinica.Crm/wwwroot/app.js

**Domínio:** CRM. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js) · 815 linhas · 129 controles extraídos.

**Abas/ações/títulos identificados:** Abrir atendimento de ${esc(name(c))}; Responder a esta mensagem; Ver tarefas →; Ver ${cs.length} atendimento(s) →; Editar; ${esc(s)}; ${esc(name(c))}; ${esc(name(c))} →; ${r.archived?'Restaurar':'Arquivar'}; WhatsApp; Instagram; Messenger; ${r.enabled?'Pausar':'Ativar'}; Remover anexo; Cancelar citação; ${url}; Baixar áudio; Baixar vídeo; ${icon('note')}; Atualizar consulta; ${esc(agent(t.id))}; Editar perfil e folgas; Revisar conversa →; Mensagem ${c.messages.findIndex(m=>m.id===id)+1} ↗; Abrir atendimento →; Tentar sincronizar novamente; ×; Renomear; + Nova especialidade; + Opção nesta etapa; Remover intervalo; Abrir conversa →; Ver conversa; Sincronizar registro; Atender →; Conversa; Registrar solução; Conferir atendimento →; Copiar texto; Preencher ${t.automation==='recall'?'recall':'lembrete'}; Preparar acompanhamento; Consultar vagas e remarcar; Abrir tarefa; Abrir conversa; Retomar sincronização; ${esc(label)}; Fechar; Revisar →; Abrir →; Atendimento →; Consultar fonte; ${k.status==='archived'?'Restaurar rascunho':'Arquivar'}; Revisar aprendizado; Descartar; Ver conhecimento; Testar exemplo; ${t.status==='archived'?'Restaurar':'Arquivar'}; Resposta adequada; Precisa melhorar; Criar exemplo a partir do teste; Ver atendimento; Abrir atendimento; Remarcar pela clínica; Fechar remarcação; Consultar vagas; Confirmar remarcação na clínica; Preparar mensagem ao paciente; Conferir resultado do pedido.

**Achados relacionados por arquivo/nome de ViewModel:** [A26](ACHADOS.md#a26), [A47](ACHADOS.md#a47), [A48](ACHADOS.md#a48), [A49](ACHADOS.md#a49), [A50](ACHADOS.md#a50), [A51](ACHADOS.md#a51).

**Triagem Jev:** `acao` · confiança retornada 0.45.

**Verificação/melhoria recomendada:** Conservar conversa/paciente confirmado, filtro da fila, estado do envio e próximo responsável.

## S260 — semdor-crm / src/Clinica.Crm/wwwroot/booking.js

**Domínio:** CRM. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/booking.js) · 153 linhas · 31 controles extraídos.

**Abas/ações/títulos identificados:** Agendar nesta conversa; Voltar à conversa; Manhã; Tarde; Após 17h; Buscar horários disponíveis; Atualizar; Confirmar no Clínico; Liberar vaga; Conferir resultado; ${esc(clinicTime(t))}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.58.

**Verificação/melhoria recomendada:** Conservar conversa/paciente confirmado, filtro da fila, estado do envio e próximo responsável.

## S261 — semdor-crm / src/Clinica.Crm/wwwroot/conversation-workflow.js

**Domínio:** CRM. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/conversation-workflow.js) · 85 linhas · 23 controles extraídos.

**Abas/ações/títulos identificados:** Precisa da minha atenção; Fechar atenção; Retomar conversa; Fechar lembrete; Cancelar; Salvar lembrete; Recusar transferência; Fechar recusa; Voltar; Confirmar recusa; Minha atenção; Lembrar de retomar; ${text}; Abrir conversa; Desafixar nota; ${c.pinnedNoteIds?.includes(note.id)?'Desafixar nota':'Fixar nota'}.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `acao` · confiança retornada 0.27.

**Verificação/melhoria recomendada:** Conservar conversa/paciente confirmado, filtro da fila, estado do envio e próximo responsável.

## S262 — semdor-crm / src/Clinica.Crm/wwwroot/flow-editor.js

**Domínio:** CRM. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/flow-editor.js) · 55 linhas · 7 controles extraídos.

**Abas/ações/títulos identificados:** Atendimento; Expediente; Agenda e lembretes; Visão do paciente; Fechar prévia.

**Achados relacionados por arquivo/nome de ViewModel:** nenhum específico consolidado; não equivale a ausência de problema..

**Triagem Jev:** `sem_indicio` · confiança retornada 0.40.

**Verificação/melhoria recomendada:** Conservar conversa/paciente confirmado, filtro da fila, estado do envio e próximo responsável.

## S263 — semdor-crm / src/Clinica.Crm/wwwroot/index.html

**Domínio:** CRM. **Natureza:** arquivo de interface/template; alcance depende de composição e estado.
**Origem:** [código congelado](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/index.html) · 144 linhas · 487 controles extraídos.

**Abas/ações/títulos identificados:** Ir para o conteúdo; /; Atendimentos; Meu dia; Agenda; Contatos; Recall; Tarefas; Funil; Envios do Clínico; Visão geral; Operação; Relatórios; IA e treinamento; Auditoria; Canais; Configurações; Buscar na central; Ativar notificações neste navegador; Sair; Clínica SemDor; Entrar na central; Entrar; Atualizar; + Conversas de exemplo; Novo atendimento; Novos; Meus; Todos; Encerrados; Limpar; Próximo atendimento →; Uma conversa. Todo o contexto.; Ver o que precisa de atenção hoje →; Voltar à lista; Assumir; Transferir; Reabrir; Concluir; Auditar; Buscar na conversa; Ficha do contato; Agendar; Agenda e retornos; Assumir humano; Devolver à IA; Histórico da IA; Responder mensagem pendente; Simular pedido humano; Testar como paciente; Descartar; Aprovar e inserir no rascunho; Responder; Nota interna; Sugerir resposta; Respostas rápidas; ＋ Anexar; ◉ Gravar áudio; Oferecer atendimento humano; Modelos WhatsApp; Enviar; Fechar ficha do contato; Editar; Adicionar tarefa para este contato; Salvar especialidade; Meus retornos; Ver todos →; Aguardando resposta; Ver fila →; Consultas de hoje; Abrir agenda →; Verificar conexão; Jade, nossa assistente virtual.; Enviar no teste; Base de conhecimento; Ensinar e treinar; Aprender com a recepção; Simular conversa; Comportamento e atendimento humano; O que a IA pode responder; Adicionar conhecimento; Cada atendimento pode ensinar; Ativar coleta; Do jeito que a clínica atende; Executar testes publicados; Adicionar exemplo; Converse antes de ativar; Documentos; Pedir uma pessoa; Dúvida clínica; Testar resposta; Nova simulação; Histórico de testes e atendimento automático; Comportamento da IA; Salvar comportamento; Atendimento humano tem prioridade; Histórico de configuração; Painel de atendimento; Distribuição por especialidade; Jornada dos contatos; Equipe de atendimento; Novo contato; Funil de relacionamento; Tarefas e retornos; Nova tarefa; Pendentes; Minhas; Concluídas; Respostas salvas; Adicionar respostas da clínica; Criar resposta; Atualizar envios; Agenda e confirmações; Configurar lembretes; Sincronizar; Próximos agendamentos; Histórico da automação; Recall de pacientes; Configurar recall; Regras de contato; Consultar modelos disponíveis; Salvar configuração; Modelo de mensagem; Pacientes para acompanhamento; Histórico de recall; Operação e consumo; Saúde da operação; Atendimentos com responsável desconectado; Pacientes aguardando resposta; Pedidos de remarcação; Estimativa de custo; Configurar tarifas; Salvar tarifas da simulação; Concluir pedido de remarcação; Fechar; Registrar conclusão; Números e canais; Automação do atendimento; Nova regra; Expediente da recepção; + Intervalo semanal; Salvar expediente; Menu de boas-vindas; Menu de especialidades SemDor; + Opção; Confirmação 24 horas antes; Consultar modelos da Infobip; Salvar fluxo de atendimento; Relatórios de atendimento; Exportar CSV; Resultado dos atendimentos; Desempenho por atendente; Equipe e permissões; Responsabilidades de cada perfil; Auditoria de conversas; Atualizar auditoria; Exportar seleção; Acesso reservado à gestão; Fila de revisão; Acompanhamento por atendente; Regras de recall; Ver pacientes; Baixar pacote; Consultar aprovação; Sistema da clínica; Consultar agenda; Tarifas de mensagens; Ver consumo; Concluir atendimento; Fechar conclusão; Continuar atendimento; Transferir atendimento; Cancelar; Salvar contato; Salvar tarefa; Resposta rápida; Salvar resposta; Inserir resposta rápida; Regra de encaminhamento; Salvar regra; Abrir atendimento; Escolher mensagem; Fechar modelos; Gerenciar modelos; Voltar; Enviar mensagem; Fechar agenda e retornos; Atualizar dados; Voltar à conversa; Gravar áudio; Cancelar gravação; Parar e revisar; Editar integrante; Salvar alterações; Auditar atendimento; Fechar auditoria; Salvar rascunho; Concluir avaliação; Avaliação preservada; Fechar histórico; Fechar busca; Aprovar e publicar; Fechar exemplo; Aprovar e publicar exemplo; Histórico da IA neste atendimento; Fechar histórico da IA; Editar especialidade; Fechar edição de especialidade.

**Achados relacionados por arquivo/nome de ViewModel:** [A23](ACHADOS.md#a23), [A24](ACHADOS.md#a24).

**Triagem Jev:** `navegacao` · confiança retornada 0.39.

**Verificação/melhoria recomendada:** Conservar conversa/paciente confirmado, filtro da fila, estado do envio e próximo responsável.
