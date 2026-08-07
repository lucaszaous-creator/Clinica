# SafeID: assinar em nuvem e entrar no sistema com o certificado

Projeto da integração do **SafeID** (Safeweb), o certificado A3 em nuvem que a médica já tem
contratado. Duas metades: **assinar** os documentos com ele e **entrar** no sistema por ele.

Este documento existe porque a integração **não pôde ser escrita ainda** — a documentação
técnica da Safeweb está inacessível deste ambiente (ver a última seção). O que está aqui é o
que foi **verificado no nosso código**, e vale independentemente do que a doc disser. O que
depende dela está marcado como tal, e não foi chutado: chute com aparência de especificação é
pior do que campo vazio, pela mesma razão que `PrecoConvenioService` deixa o preço em branco
quando não há tabela cadastrada.

## O achado que muda o tamanho do trabalho

`docs/apis-integracao-prescricao.md` estimou a refatoração como "não grande, mas estrutural",
supondo que assinar em nuvem obrigaria a mexer no miolo do `AssinaturaDigitalService` —
calcular o hash à mão, remendar o `/Contents` do PDF, recalcular o `/ByteRange`. **Não
obriga.** O PDFsharp 6.2.4 já publica a costura, e ela tem exatamente o formato de uma
assinatura remota:

```csharp
namespace PdfSharp.Pdf.Signatures;

public interface IDigitalSigner
{
    string        CertificateName      { get; }
    Task<int>     GetSignatureSizeAsync();
    Task<byte[]>  GetSignatureAsync(Stream conteudoCoberto);
}
```

(Verificado por reflexão sobre `PdfSharp.System.dll` 6.2.4. `PdfSharpDefaultSigner`, que é o
que usamos hoje, é apenas a implementação local dela, em `PdfSharp.Cryptography.dll`.)

Três coisas importam nessa assinatura de método:

1. **Já é assíncrona.** Uma chamada de rede cabe sem forçar `.Result` em lugar nenhum — e
   `.Result` dentro de assinatura de documento seria travamento de UI no dia em que o PSC
   demorasse.
2. **Recebe o `Stream` do conteúdo coberto pelo `/ByteRange`.** É precisamente o que se
   manda para o PSC assinar. Não precisamos abrir o PDF na mão.
3. **Devolve os bytes do PKCS#7**, que o PDFsharp encaixa no `/Contents` e ainda dimensiona
   o espaço reservado por `GetSignatureSizeAsync`.

Ou seja: **a integração é uma classe nova que implementa `IDigitalSigner`**, e não uma
cirurgia no serviço de assinatura.

## O que muda

| Onde | O que acontece hoje | O que precisa |
|---|---|---|
| `AssinaturaDigitalService.Assinar` (linha 150) | Instancia `PdfSharpDefaultSigner(certificado.Certificado, …)` | Receber o `IDigitalSigner` de fora, em vez de escolher um |
| `AssinaturaDigitalService.Criticar` (linha 243) | Recusa certificado sem `HasPrivateKey` | Em nuvem **não há** chave local; a checagem passa a valer só para o provedor local |
| `CertificadoIcpBrasil.DoRepositorioDoUsuario` | Lê o `X509Store` da máquina | Ganha uma irmã que lista os certificados **do PSC** |
| `EscolherCertificadoViewModel` (linha 93) | Uma fonte só | Duas fontes na mesma lista (máquina e nuvem), com a procedência escrita ao lado |
| `Assinar` | Síncrono | Assíncrono — os **dois** pontos de chamada já estão dentro de método `async` (`AssinaturaDePrescricaoService:143`, `AssinaturaDeDocumentoClinicoService:112`), então isto não se espalha |

Nada disso encosta no faturamento congelado: `AssinaturaDigitalService` nasceu na parcela 42 e
só é referenciado pelos dois serviços de assinatura, pelo `PrescricaoInternaService`, pelo
seletor do shell e pelo DI.

## O que NÃO muda — e é a metade que importa

Isto é a boa notícia, e foi o que a leitura do código estabeleceu com mais firmeza: **todo o
miolo de segurança opera sobre o certificado PÚBLICO**, que um PSC entrega igual ao de um
token. Continuam valendo, sem uma linha alterada:

- **`CertificadoIcpBrasil.CpfDoTitular`** — o ASN.1 lido à mão para tirar o CPF de dentro do
  certificado (OID `2.16.76.1.3.1`). É a metade que faz a assinatura qualificada valer.
- **`TitularDoCertificado.Exigir`** — a regra que impede o e-CPF de outra pessoa assinar pela
  médica. Com SafeID ela fica **mais** necessária, não menos: em nuvem some o gesto físico de
  encaixar o token, e a única barreira que sobra é essa comparação.
- **`Conferir`** e **`ConferirCadeia`** — já são sobre os bytes e sobre a cadeia, nunca sobre
  a posse da chave.
- **`ArquivoAssinado`** e a regra de a reimpressão devolver os **bytes guardados**.

## A segunda metade: entrar no sistema pelo SafeID

`UsuarioSistema` já aponta para `Profissional`, e `Profissional.Cpf` já é obrigatório para
assinar (`TitularDoCertificado.Exigir` recusa quem não o tem). O CPF autenticado pelo SafeID é
**a mesma chave de junção** que a assinatura usa — o login por certificado não inventa
identidade nova, ele reaproveita a que o sistema já exige.

Três decisões que este documento já fixa, porque não dependem da doc:

- **Entra AO LADO do `AcessoService.AutenticarAsync`, nunca no lugar dele.** No balcão duas
  pessoas dividem a mesma máquina e não vão ter e-CPF cada uma; login só por certificado
  trancaria a recepção do lado de fora. Senha PBKDF2 continua sendo o caminho normal.
- **CPF que não casa com nenhum `UsuarioSistema` ativo NÃO cria usuário.** Autenticar prova
  quem a pessoa é, não que ela tem acesso a este sistema — quem concede acesso é a direção,
  em Acessos, como já é hoje.
- **O acesso por certificado grava `EventoAuditoria`**, como toda ação administrativa de
  acesso. Permissão que muda sem rastro é pior do que não ter permissão.

O ponto de desenho que sobra: OAuth2 com PKCE num app WPF exige *redirect* em loopback
(`http://127.0.0.1:<porta livre>`) e abrir o navegador do usuário. É o padrão para app
desktop, e é o que o `code_verifier` do PKCE existe para proteger.

## O que a norma já garante (e por que isso reduz o risco)

A API dos PSCs brasileiros é padronizada pelo **DOC-ICP-17.01** do ITI — "API PSC OAUTH". A
consequência prática vale ser dita: **o cliente que escrevermos não é da Safeweb, é da
norma**. Se um dia a clínica trocar de PSC, ou se o certificado gratuito do CFM (AR-CFM, via
Valid/VIDaaS) entrar na conversa, a mesma implementação atende — muda configuração, não
código.

Do que é público sobre a norma: OAuth 2.0 com PKCE (`code_verifier` de 43 a 128 caracteres) e
dois escopos que interessam — `single_signature` para assinatura avulsa e `signature_session`
para lote (até 100 arquivos na validade do *token*).

**O escopo importa para a experiência da médica.** Com `single_signature`, cada folha
assinada é uma confirmação no celular; com `signature_session`, ela autoriza uma vez e assina
o lote. Numa clínica que emite receita, atestado e folha de infusão na mesma consulta, a
diferença é entre uma autorização e quatro.

## O que falta, e por que não foi escrito

A documentação técnica do SafeID Integração (`pscsafeweb.safewebpss.com.br/Docs/`) está
**bloqueada pela política de egresso** deste ambiente — 403 no CONNECT, junto de todos os
domínios da Safeweb, da Memed e do ITI. O proxy funciona (o GitHub responde 200); a liberação
é que não alcançou a sessão.

Sem ela faltam exatamente estas respostas, e nenhuma delas se adivinha:

1. **URLs base** de autorização, *token* e assinatura (homologação e produção).
2. **Como se obtém `client_id`/`client_secret`** de parceiro Safeweb, e se há homologação
   antes de produção.
3. **O endpoint que lista os certificados** do titular autenticado, e em que formato ele
   devolve o certificado público (é dele que sai o CPF do OID `2.16.76.1.3.1`).
4. **O formato do que se manda assinar**: hash puro, `SignedAttributes` montados por nós, ou
   o documento inteiro — e se o PSC devolve PKCS#7 completo ou só a cifra crua.
5. **O tamanho máximo do PKCS#7 devolvido**, para dimensionar `GetSignatureSizeAsync` (o
   PDFsharp reserva o espaço ANTES de assinar; reservar de menos quebra a assinatura).
6. **Se o carimbo do tempo já vem incluso** — a Safeweb anuncia que sim, e isso pouparia
   contratar ACT à parte, mas o nosso `PedidoAssinatura.CarimbadoraDeTempo` precisa saber se
   deve continuar mandando a URL ou parar.

Deliberadamente **não** foi criada uma classe `ProvedorSafeID` que lance
`NotImplementedException`. Um provedor que aparece no seletor e falha no clique é a mesma
coisa que a parcela 41 corrigiu — botão aceso que não faz nada — com o agravante de estar na
tela que dá valor jurídico ao documento.
