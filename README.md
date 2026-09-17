# ProductionManager

Sistema de gerenciamento de produção industrial desenvolvido em **C# e .NET 10**. O projeto organiza a programação semanal das máquinas e acompanha a produção por meio de **Ordens de Fabricação (OF)**, **PNs** e **Meta Hora**.

O objetivo é centralizar a programação enviada para cada máquina e permitir que o operador acompanhe metas, registre peças boas e refugos e visualize automaticamente os indicadores de **qualidade** e **eficiência**.

> O backend está funcional, persistente e preparado para receber um front-end.

## Visão geral

O sistema representa o fluxo básico de uma operação industrial: o responsável pela programação cadastra e organiza as OFs para cada máquina; o operador acessa a programação, atualiza o status operacional e realiza os apontamentos por Meta Hora.

```text
Programador
    ↓
Programação semanal
    ↓
OF + PN + Máquina
    ↓
Operador
    ↓
Meta Hora
    ↓
Peças boas + Refugo
    ↓
Qualidade + Eficiência
```

A quantidade produzida de uma OF nunca é alterada diretamente. O backend a calcula a partir dos apontamentos feitos nas Metas Hora.

## Principais funcionalidades

- Cadastro de produtos por PN e de máquinas por código
- Criação de Ordens de Fabricação com quantidade planejada
- Programação semanal por máquina, com sequência explícita de OFs
- Controle operacional entre Set-up, Produção e Pausa
- Criação de períodos de Meta Hora, inclusive atravessando a meia-noite
- Registro de peças boas e refugo por período
- Correção explícita de apontamentos, sem duplicar produção
- Cálculo automático de produção acumulada, qualidade e eficiência
- Identificação de metas ainda pendentes
- Finalização explícita e definitiva da OF
- Persistência em SQLite, API HTTP e cliente de Console
- Proteção contra conflitos entre usuários e testes automatizados

## Exemplo de operação

```text
=== ORDEM DE FABRICAÇÃO ===

OF: 254879
PN: 45872-A
Produto: Tampa Superior
Máquina: INJ-04 - Injetora 04

Quantidade planejada: 10000
Quantidade produzida: 4750
Refugo: 250

Qualidade: 95,00%
Eficiência: 97,94%

Status: Produção
```

A produção é apontada pela Meta Hora:

| Período | Meta | Produzido | Refugo | Eficiência |
| --- | ---: | ---: | ---: | ---: |
| 07:00 – 08:00 | 500 | 480 | 10 | 96% |
| 08:00 – 09:00 | 500 | 510 | 5 | 102% |
| 09:00 – 10:00 | 500 | — | — | — |

Uma hora sem apontamento é diferente de uma hora apontada com `0` peças boas e `0` refugos. O primeiro caso representa uma meta pendente; o segundo é um resultado válido de uma hora sem produção.

## Indicadores

### Qualidade

Mede a proporção de peças boas entre tudo que foi produzido no período.

```text
Qualidade = Peças boas / (Peças boas + Refugo)
```

Com 950 peças boas e 50 refugos, a qualidade é de 95%. Quando não há produção nem refugo, o resultado é 0%.

### Eficiência

Compara as peças boas com a meta do período já apontado.

```text
Eficiência = Peças boas / Meta apontada
```

Se a meta for 500 e forem produzidas 525 peças boas, a eficiência será 105%. Superar a meta é uma informação válida e, por isso, a eficiência não é limitada a 100%.

No acompanhamento operacional ao vivo, somente períodos que já possuem apontamento entram no denominador. Isso evita que metas futuras reduzam artificialmente a eficiência atual.

## Regras de negócio

| Regra | Comportamento implementado |
| --- | --- |
| OF, PN e código de máquina | São textos, preservam zeros à esquerda e possuem unicidade no sistema. |
| Quantidade produzida e refugo | São propriedades calculadas, sem edição direta. |
| Status da OF | Pode transitar entre Set-up, Produção e Pausa. |
| Apontamento | Só pode ser criado quando a OF está em Produção. |
| Correção | Atualiza o apontamento existente enquanto a OF está aberta. |
| Finalização | É explícita, requer confirmação na interface e é terminal. |
| Metas pendentes | Bloqueiam a finalização até que sejam resolvidas. |
| Meta Hora | Não aceita períodos inválidos ou sobrepostos. |
| Produção simultânea | A máquina não pode ter duas OFs em Produção ao mesmo tempo. |

Após a finalização, a OF não aceita novos status, metas, apontamentos ou correções.

## Arquitetura

A solução foi separada em camadas para manter as regras de produção independentes da interface e do banco de dados.

```text
Interface
    ↓
API
    ↓
Application
    ↓
Domain
    ↓
Infrastructure
    ↓
Banco de dados
```

| Projeto | Responsabilidade |
| --- | --- |
| `ProductionManager.Domain` | Entidades, estados, cálculos, intervalos, apontamentos, correções e finalização. |
| `ProductionManager.Application` | Casos de uso, DTOs, permissões por perfil, consultas e controle de versão. |
| `ProductionManager.Infrastructure` | Entity Framework Core, SQLite, repositórios, índices e transações. |
| `ProductionManager.Api` | Endpoints HTTP, serialização e tratamento de erros. |
| `ProductionManager.Console` | Cliente temporário que consome a API. |
| `ProductionManager.Tests` | Testes de domínio, aplicação, persistência e API. |

O domínio não depende de Console, API, Entity Framework ou tecnologia de front-end. O Console é apenas um cliente da API e não repete regras de negócio.

## Tecnologias

- C# e .NET 10
- ASP.NET Core Minimal API
- Entity Framework Core
- SQLite
- HTTP/REST
- xUnit para testes automatizados
- Nullable Reference Types
- Concorrência otimista e transações

## Concorrência e segurança dos dados

Cada OF e programação possui uma versão. O cliente consulta o estado atual, envia essa versão ao alterar um recurso e recebe uma nova versão na resposta.

```json
{ "status": "Producao", "versao": 3 }
```

Se duas pessoas tentarem alterar o mesmo dado simultaneamente, a gravação desatualizada recebe `409 Conflict` em vez de sobrescrever a informação mais recente. A aplicação também protege, no banco de dados, o cadastro duplicado de PN, código de máquina, OF, programação semanal e apontamento da mesma Meta Hora.

Os percentuais são entregues pela API como números decimais, por exemplo `0.95` e `1.05`. Cabe ao front-end apresentá-los como `95%` e `105%`.

## Executando o projeto

### Pré-requisito

Instale o **SDK .NET 10**.

Na raiz da solução, restaure as dependências, compile e execute os testes:

```powershell
dotnet restore ProductionManager.sln
dotnet build ProductionManager.sln --no-restore
dotnet test ProductionManager.sln --no-build
```

Inicie a API:

```powershell
dotnet run --project src/ProductionManager.Api
```

No ambiente local, ela inicia em `http://localhost:5080`.

Em outro terminal, execute o Console:

```powershell
dotnet run --project src/ProductionManager.Console
```

Para apontar o Console para outro endereço de API:

```powershell
$env:PRODUCTIONMANAGER_API_URL = 'http://localhost:5080'
dotnet run --project src/ProductionManager.Console
```

## Primeiro uso

O banco inicia vazio. Pelo Console, siga este fluxo:

```text
Programação
    ↓
Cadastrar Produto
    ↓
Cadastrar Máquina
    ↓
Criar OF
    ↓
Liberar OF na programação semanal
    ↓
Operador
    ↓
Selecionar OF
    ↓
Criar Meta Hora
    ↓
Alterar status para Produção
    ↓
Registrar peças boas e refugo
```

O resumo da OF, os totais e os indicadores são calculados pelo backend.

## API e persistência

Exemplos prontos de requisições estão em [docs/ProductionManager.http](docs/ProductionManager.http), e a tabela completa das rotas está em [docs/API.md](docs/API.md).

O banco SQLite padrão fica em `src/ProductionManager.Api/data/productionmanager.db` e preserva os dados após o reinício. A conexão pode ser configurada por `ConnectionStrings:ProductionManager` ou pela variável `ConnectionStrings__ProductionManager`.

No ambiente de desenvolvimento, os perfis podem ser simulados com os cabeçalhos `X-Perfil: Programador` e `X-Perfil: Operador`. Essa simulação existe apenas para testar os fluxos; autenticação e usuários reais são a próxima evolução do projeto.

## Testes e status atual

O projeto possui **69 testes automatizados** cobrindo, entre outros cenários:

- cálculo de qualidade e eficiência, inclusive acima de 100%;
- produção zero, refugo e valores inválidos;
- períodos que atravessam a meia-noite e prevenção de sobreposições;
- apontamentos duplicados e correções;
- finalização e metas pendentes;
- sequência de programação semanal;
- persistência em SQLite;
- conflitos de concorrência entre contextos e requisições HTTP.

Estado atual do backend:

- [x] Domínio e regras de produção
- [x] Programação semanal
- [x] Meta Hora e apontamentos
- [x] API HTTP
- [x] Persistência SQLite
- [x] Concorrência e validações
- [x] Cliente Console
- [x] Testes automatizados
- [ ] Front-end
- [ ] Autenticação, usuários e permissões reais
- [ ] Dashboard e relatórios históricos
- [ ] Migrations para evolução do banco em produção

## Objetivo acadêmico e profissional

O ProductionManager aplica conceitos de orientação a objetos, encapsulamento, regras de domínio, arquitetura em camadas, APIs, persistência, concorrência e testes automatizados a um cenário industrial real.

A próxima etapa é construir o front-end consumindo os contratos HTTP já disponibilizados pelo backend.
