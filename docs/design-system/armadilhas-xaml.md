# Armadilhas do compilador de XAML

Erros que **não** aparecem em revisão de código nem em XAML bem-formado: só o compilador
de marcação (`MarkupCompilePass`) os pega, e como `Clinica.Desktop` só compila no Windows,
eles costumam estourar direto no CI. Todos os casos abaixo já quebraram um build deste repo.

Vale lembrar que o compilador **aborta no primeiro arquivo com erro** — build verde depois de
uma correção não significa que o restante estava limpo; pode só não ter chegado lá ainda.

## MC3024 — `Style` definido duas vezes

Definir `Style` como atributo **e** como elemento-propriedade no mesmo elemento.
É fácil cair nisso ao acrescentar um gatilho a um controle que já usava um estilo do DS.

```xml
<!-- ERRADO: 'Style' property has already been set and can be set only once -->
<TextBlock Text="Observações" Style="{StaticResource FichaRotulo}">
    <TextBlock.Style>
        <Style TargetType="TextBlock" BasedOn="{StaticResource FichaRotulo}"> …
```

```xml
<!-- CERTO: só o elemento-propriedade; o BasedOn traz o estilo do DS -->
<TextBlock Text="Observações">
    <TextBlock.Style>
        <Style TargetType="TextBlock" BasedOn="{StaticResource FichaRotulo}"> …
```

## MC4102 — propriedade de tipo array dentro de template

Uma propriedade de dependência cujo tipo é um **array** não pode ser atribuída dentro de uma
seção de template (`DataTemplate`, `ControlTemplate`, `CellTemplate`): o compilador emite
*"Tags of type 'PropertyArrayStart' are not supported in template sections"*.

O sintoma engana, porque o mesmo controle funciona no visual tree normal e só quebra na lista:

```csharp
// ERRADO: quebra em <ctrl:Avatar Foto="{Binding FotoMiniatura}" /> dentro de DataTemplate
DependencyProperty.Register(nameof(Foto), typeof(byte[]), typeof(Avatar), …)

// CERTO: declare como object e converta na leitura
DependencyProperty.Register(nameof(Foto), typeof(object), typeof(Avatar), …)
// …
var imagem = Retrato.Carregar(Foto as byte[]);
```

## O que dá para checar antes do push (sem SDK)

Para a **suíte multi-exe** isso já está escrito e roda no CI:

```bash
python3 tools/verificar-suite.py
```

Ele cobre XAML bem-formado, `{StaticResource}` sem chave, pack URI quebrado, `x:Class` sem
code-behind, `x:Key` repetido, evento sem handler, `Style` duplicado (MC3024) e o
`Application` sem qualificar (CS0118 — dentro de `Clinica.*` esse nome é o namespace
`Clinica.Application`, não o tipo do WPF). Ainda **não** cobre `Clinica.Desktop`, que entra
quando o faturamento virar módulo (Fase 4).

Para o faturamento, por enquanto, as checagens continuam manuais. Nenhuma delas substitui o
compilador, mas pegam a maior parte dos erros de digitação e evitam ciclos de CI:

- **XAML bem-formado**: passar cada arquivo por um parser de XML.
- **Recursos**: extrair os `x:Key` de `Styles/**/*.xaml` + `App.xaml` e conferir que todo
  `{StaticResource X}` das views tem chave correspondente.
- **Bindings**: extrair as raízes de `{Binding X}` das views e conferir contra as propriedades
  do ViewModel (`[ObservableProperty]`, propriedades públicas e comandos `[RelayCommand]`).
  As raízes que sobram devem ser as de item de lista — confira-as contra a entidade do template.
- **`Style` duplicado**: procurar elementos que tenham `Style="…"` no atributo e `<Tag.Style>`
  logo abaixo (cuidado com falso positivo de irmão autofechado).

## Padrões que **são** seguros

Confirmados em build, para não virarem suspeita à toa:

- `<Style.Triggers>` (e coleções em geral) dentro de `DataTemplate`.
- `{Binding …, RelativeSource={RelativeSource TemplatedParent}}` dentro de um `Freezable`
  em template — ao contrário de `{TemplateBinding}`, que falha nessa posição.
