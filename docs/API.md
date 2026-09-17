# Contrato da API

Base local: `http://localhost:5080`. Corpos JSON; datas de evento em ISO 8601 com offset, por exemplo `2026-09-16T23:00:00-03:00`. Datas de semana usam `yyyy-MM-dd`.

Os perfis abaixo são aplicados nos serviços da Application. No ambiente Development, simule-os com o cabeçalho `X-Perfil`. Isso não é autenticação.

| Método/rota | Perfil para escrita | Corpo/observação |
| --- | --- | --- |
| GET `/api/produtos` | — | Lista de PN, nome e Id. |
| POST `/api/produtos` | Programador | `{ "pn": "00045872", "nome": "Tampa" }` |
| GET `/api/maquinas` | — | Lista incluindo `ativa`. |
| POST `/api/maquinas` | Programador | `{ "codigo": "INJ-04", "nome": "Injetora 04" }` |
| POST `/api/ordens` | Programador | `{ "of": "000254879", "produtoId": 1, "maquinaId": 1, "quantidadePlanejada": 10000 }` |
| GET `/api/ordens/{id}` | — | Resumo calculado e versão da OF. |
| GET `/api/ordens/por-of/{of}` | — | Mesmo resumo, pela identificação textual da OF. |
| PATCH `/api/ordens/{id}/status` | Operador | `{ "status": "Producao", "versao": 1 }` |
| POST `/api/ordens/{id}/finalizar` | Operador | `{ "versao": 2 }`; a interface deve confirmar antes do envio. |
| GET `/api/ordens/{id}/metas-hora` | — | `{ "ordemId": 1, "versao": 2, "metas": [...] }`. |
| POST `/api/ordens/{id}/metas-hora` | Operador | `{ "inicio": "2026-09-16T23:00:00-03:00", "fim": "2026-09-17T00:00:00-03:00", "metaPlanejada": 500, "versao": 2 }` |
| POST `/api/metas-hora/{id}/apontamento` | Operador | `{ "quantidadeBoa": 480, "refugo": 10, "versao": 3 }` |
| PUT `/api/metas-hora/{id}/apontamento` | Operador | Mesmo corpo; substitui resultado existente, sem criar outro. |
| GET `/api/maquinas/{id}/programacao` | — | Lista de semanas, com itens na sequência. Filtro opcional `?semana=2026-09-16`. |
| POST `/api/maquinas/{id}/programacao` | Programador | `{ "inicioSemana": "2026-09-16", "ordemIds": [1, 2] }` |
| POST `/api/programacoes/{id}/itens` | Programador | `{ "ordemId": 3, "versao": 3 }`; acrescenta no fim. |
| PUT `/api/programacoes/{id}/sequencia` | Programador | `{ "ordemIds": [3, 1, 2], "versao": 4 }`; lista completa de IDs de OF. |
| GET `/health` | — | Verificação de disponibilidade do processo. |

Cadastros retornam 201; alterações retornam 200 com o DTO atualizado, incluindo a nova versão. Alterações de metas e apontamentos retornam o resumo da OF; consulte novamente a lista de metas para obter os IDs recém-criados.

Resumo da OF contém `id`, `of`, `pn`, `produto`, `maquinaId`, `maquinaCodigo`, `maquinaNome`, `quantidadePlanejada`, `quantidadeProduzida`, `refugo`, `metaApontada`, `qualidade`, `eficiencia`, `status`, `metasPendentes`, `criadaEm`, `finalizadaEm` e `versao`.

Meta Hora contém `id`, `ordemProducaoId`, `inicio`, `fim`, `metaPlanejada`, `apontamento`, `eficiencia` e `qualidade`. Quando presente, o apontamento contém `id`, `metaHoraId`, `quantidadeBoa`, `refugo`, `registradoEm` e `atualizadoEm`.

Em um 409, consulte novamente e mostre o conflito ao usuário antes de uma nova decisão. Em falha de conexão após escrita, consulte o estado antes de repetir: o servidor pode já ter concluído a gravação.
