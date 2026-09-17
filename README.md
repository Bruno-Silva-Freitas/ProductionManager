# ProductionManager

Backend C#/.NET 10 para programação semanal e apontamento de produção por Meta Hora. A solução implementa o estado definitivo da especificação: produção e refugo vêm exclusivamente de `MetaHora → ApontamentoMetaHora`.

## Executar

Pré-requisito: SDK .NET 10. Os pacotes são restaurados pelo NuGet na primeira execução.

Na raiz do projeto:

```powershell
dotnet restore ProductionManager.sln
dotnet build ProductionManager.sln --no-restore
dotnet test ProductionManager.sln --no-build
dotnet run --project src/ProductionManager.Api
```

A API inicia em `http://localhost:5080` usando o perfil local `Development`. Em outro terminal:

```powershell
dotnet run --project src/ProductionManager.Console
```

O console é cliente HTTP. Ambos os papéis usam o mesmo banco da API. Para outro endereço:

```powershell
$env:PRODUCTIONMANAGER_API_URL = 'http://localhost:5080'
dotnet run --project src/ProductionManager.Console
```

O banco inicial é vazio. No console, entre em **Programação** para cadastrar produto, máquina, OF e liberar a OF em uma programação semanal. Depois entre em **Operador**, selecione a OF, crie os períodos, altere para Produção e registre boas/refugo. A tabela e o resumo são retornados pelo backend. A finalização possui confirmação separada.

## Organização

| Projeto | Responsabilidade |
| --- | --- |
| `ProductionManager.Domain` | Entidades, estados, cálculos, intervalos, apontamento, correção e encerramento. Sem dependência de banco ou interface. |
| `ProductionManager.Application` | Casos de uso de programador/operador, autorização por perfil, DTOs, consultas, controle de versão e interfaces de repositório. |
| `ProductionManager.Infrastructure` | Repositórios EF Core, transações, SQLite, chaves únicas e configuração de concorrência. |
| `ProductionManager.Api` | Rotas HTTP, serialização, tradução de erros e composição dos serviços. |
| `ProductionManager.Console` | Entrada de dados, confirmação e apresentação; chama a API. |
| `ProductionManager.Tests` | Testes de domínio, aplicação, persistência real e API. |

Os antigos arquivos da aplicação de console monolítica foram substituídos por esses projetos. Não há segunda implementação de regras nem `RegistroProducao` paralelo.

## Regras e decisões

- PN, OF e código de máquina são strings, preservam zeros à esquerda e são normalizados com `Trim().ToUpperInvariant()` para unicidade. A unicidade é global e protegida por índices no banco.
- Produto, máquina e identificação da OF permanecem imutáveis após o cadastro; não existe operação de troca estrutural nesta versão.
- Máquina possui `Ativa`. O domínio recusa novas OFs e entrada em Produção quando ela está inativa. Uma futura operação de manutenção poderá expor ativação/desativação na API.
- A OF é a única entrada pública para alterar metas e apontamentos. Os filhos não expõem métodos públicos de gravação. Coleções são somente leitura.
- Quantidade produzida e refugo são somas de apontamentos, sem colunas editáveis para os totais. Totais usam `long` para evitar estouro ao somar vários lançamentos `int`.
- Qualidade e eficiência usam `decimal`. A qualidade é `boas/(boas+refugo)`; eficiência é `boas/meta apontada`. Divisor zero resulta em zero. Eficiência pode ultrapassar 1.
- Somente metas com apontamento entram no denominador operacional. `MetasPendentes` informa quantas ainda faltam. Uma meta sem apontamento devolve `apontamento: null` e indicadores nulos; zero lançado é um resultado real.
- `Inicio` e `Fim` usam `DateTimeOffset`. A validação compara instantes, incluindo fusos distintos. Intervalos adjacentes são permitidos; intervalos sobrepostos na mesma OF são recusados. Meia-noite é representada com a data do dia seguinte.
- `POST` de apontamento cria um único registro. Repetir com a versão antiga gera 409; repetir com versão atual não substitui o lançamento e gera 400. A correção é exclusivamente `PUT` e preserva Id e `RegistradoEm`, preenchendo `AtualizadoEm`.
- Novo apontamento exige Produção. Correção exige OF aberta e perfil Operador, inclusive em SetUp/Interrompida. Não existe reabertura ou correção administrativa de OF finalizada.
- Finalização é explícita, jamais automática por atingir a quantidade planejada. O console pede confirmação; o backend executa a decisão.
- **Todas as metas pendentes bloqueiam a finalização, inclusive futuras.** Como cancelamento não existe nesta versão, resolva cada período com o resultado real; `0/0` é válido quando não houve produção. Uma OF sem metas pode ser finalizada explicitamente.
- Programação semanal normaliza qualquer data informada para a segunda-feira correspondente. Há uma programação por máquina/semana. Cada item guarda a sequência explícita. Reordenar exige todos os IDs de OF da programação, uma única vez cada.
- Somente uma OF por máquina pode estar em Produção. Além da verificação na aplicação, um índice único parcial no SQLite protege contra duas gravações concorrentes de OFs diferentes.

## Contrato HTTP e concorrência

Exemplos utilizáveis estão em `docs/ProductionManager.http`. A tabela completa de rotas está em `docs/API.md`.

Toda alteração de OF, Meta Hora, apontamento e programação existente exige `versao` no corpo. Consulte primeiro o DTO, use a versão recebida e, após cada alteração, passe a usar a nova versão retornada. A versão de um apontamento ou Meta Hora é a versão da sua OF.

```json
{ "status": "Producao", "versao": 3 }
```

A aplicação verifica a versão do cliente e o EF Core configura `Versao` como token de concorrência. Gravação da raiz e dos filhos ocorre em uma transação. Se houver conflito, toda a operação é revertida. O cliente deve consultar novamente e permitir que o usuário revise a alteração; não deve reenviar automaticamente uma escrita com uma versão nova.

Erros conhecidos retornam Problem Details: 400 para regra/dados inválidos, 403 para perfil insuficiente, 404 para registro inexistente e 409 para concorrência/unicidade. Erros inesperados retornam 500 sem detalhes internos ou stack trace.

Percentuais são números JSON (`0.95`, `1.05`), sem localização ou `%`. A interface escolhe como formatá-los. Status JSON é texto: `SetUp`, `Producao`, `Interrompida` ou `Finalizada`.

## Persistência e escopo atual

O arquivo padrão é `src/ProductionManager.Api/data/productionmanager.db`; os dados sobrevivem ao reinício. Pode ser substituído pela configuração `ConnectionStrings:ProductionManager` (ou variável `ConnectionStrings__ProductionManager`).

Nesta primeira versão do esquema, a API cria as tabelas por `EnsureCreated`. Isso inicializa um banco novo, mas **não altera o esquema de um banco existente**. Evoluções de esquema devem receber migrations e uma estratégia de migração dos dados; não apague um banco utilizado para atualizar o sistema.

Autenticação real ficou para a etapa posterior, conforme a especificação. Para exercitar os papéis em `Development`, envie `X-Perfil: Programador` ou `X-Perfil: Operador`. Esse cabeçalho **é uma simulação**, não identifica uma pessoa. Fora de Development ele é ignorado; operações de escrita exigirão uma identidade autenticada com claim de papel fornecida por um futuro adaptador de autenticação. Consultas estão abertas nesta versão. Não há login, sensores, cancelamento de metas ou trilha de auditoria de versões anteriores de apontamento; existem os timestamps de criação e última correção.

O núcleo e os contratos HTTP podem ser consumidos pelo próximo front-end. Autenticação, controle de acesso por máquina e evolução do esquema são etapas de implantação, sem mover regras de produção para a interface.

## Validação

Os testes usam bancos SQLite temporários próprios, sem tocar no banco operacional. Incluem meia-noite/fusos, valores zero/negativos, cálculo acima de 100%, produção acima do planejado, duplicação, correção, pendências, finalização terminal, sequência, papéis, persistência entre conexões, unicidade no banco, conflito real entre contextos e requisições HTTP concorrentes.

```powershell
dotnet test ProductionManager.sln
```

Nullable Reference Types e tratamento de warnings como erros estão ativados em `Directory.Build.props` para todos os projetos.
