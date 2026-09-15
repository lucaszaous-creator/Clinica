# Prescrição livre e atalhos no editor clínico

## Comportamento entregue

- **Receituário:** campo único de prescrição, com escrita, copiar e colar e múltiplos parágrafos. Não exige itens de medicamento, quantidade ou posologia em campos separados. Uma receita vazia continua sendo recusada antes da numeração.
- **Sugestões do profissional:** os modelos de receita salvos pela clínica aparecem como checkboxes. Marcar acrescenta o texto ao final da prescrição. Desmarcar preserva o texto, inclusive alterações manuais; a remoção é feita no editor. Modelos antigos com itens são convertidos em texto com descrição, quantidade e orientação preservadas.
- **Endereço na emissão:** quando falta, a própria janela pergunta e guarda o endereço no cadastro. Cancelar mantém a prescrição aberta e não emite nem numera o documento. A atualização completa somente esse campo, com auditoria do operador. Não é necessário sair para editar toda a ficha.
- **Infusão:** descrição livre, sem limite de 300 caracteres, e um bloco por checagem da enfermagem. Novos blocos começam com `SF 0,9%` e `1 hora`, ambos editáveis. Reabrir uma prescrição anterior mantém seus parâmetros, inclusive campos vazios.
- **Atendimento:** o botão `Prescrever infusão` abre o editor com o paciente, profissional, agendamento e evolução salva do atendimento. Ao fechar, atualiza a linha do tempo sem recarregar o texto da sessão.
- **Evolução:** a folha compartilhada pelo atendimento e pelo editor de sessões mostra a seleção das sessões anteriores disponíveis e o botão `Usar texto anterior`. O reaproveitamento preenche apenas campos vazios. A seleção é descartada quando muda o contexto.
- **Acupuntura:** o mapa corporal existente ganhou um botão mais visível, `Repetir pontos anteriores`. A rotina existente de repetição dos pontos continua sendo utilizada.
- **Acesso:** prescrição e sugestões ficam em evidência; a gestão de modelos fica recolhida. O rodapé da infusão mantém os botões de salvar e assinar visíveis. As mudanças compartilhadas alcançam o módulo Clínico e o Gerente Geral.

Os alertas de alergia passam a conferir o texto livre da receita. Identificação, autoria, assinatura e checagem da enfermagem mantêm suas regras. Emitir uma prescrição não confirma sua administração nem encerra o atendimento.

## Banco e compatibilidade

A migration `20260915140519_PrescricaoInfusaoTextoLivre` amplia `ItensPrescricaoInterna.Descricao` de `varchar(300)` para `text`. O `Down` mantém a capacidade ampliada para preservar textos já gravados. Documentos e modelos antigos com itens continuam legíveis. Nenhuma migration foi executada no banco da clínica durante o desenvolvimento.

## Verificação reproduzível

Na raiz do repositório, com .NET 8 e Python disponíveis:

```text
dotnet build Clinica.sln --no-incremental
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
python tokens/verificar-espelho.py
python tools/verificar-suite.py
python tools/compilar-sombra.py
```

No Windows, a conferência dos controles reais e dos PDFs pode ser repetida com:

```text
dotnet run --project tools/verificar-editor-clinico/Qa.csproj
```

Essa ferramenta utiliza somente dados demonstrativos em SQLite na memória. Exercita sugestões, cancelamento da emissão, preenchimento do endereço, reaproveitamento de evolução e padrões de infusão. Renderiza os controles WPF em uma janela oculta e gera imagens, log de bindings e dois PDFs de 70 parágrafos em `artifacts/qa-prescricao`. Não inicia a aplicação nem utiliza o banco de produção.

A conferência local incluiu a suíte de 2.561 testes, compilação Windows sem erros, compilação-sombra, espelho dos tokens e verificador da suíte. Os PDFs de receita e infusão foram conferidos página por página, com os 70 parágrafos preservados. O CI executa a mesma suíte também no PostgreSQL 16, aplicando as migrations em um banco descartável.
