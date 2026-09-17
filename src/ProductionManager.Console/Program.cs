using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProductionManager.Application.Commands;
using ProductionManager.Application.DTOs;
using ProductionManager.Domain.Enums;

namespace ProductionManager.ConsoleClient;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly HttpClient Api = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("PRODUCTIONMANAGER_API_URL") ?? "http://localhost:5080")
    };

    private static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        Console.WriteLine("ProductionManager — cliente temporário da API");
        Console.WriteLine($"Servidor: {Api.BaseAddress}");
        Console.WriteLine("Os dados são gravados no banco da API. Perfis simulados somente em Development.");
        try
        {
            while (true)
            {
                Console.WriteLine("\n1 - Programação\n2 - Operador\n0 - Sair");
                var opcao = Inteiro("Opção: ", 0, 2);
                if (opcao == 0) return;
                Api.DefaultRequestHeaders.Remove("X-Perfil");
                Api.DefaultRequestHeaders.Add("X-Perfil", opcao == 1 ? "Programador" : "Operador");
                if (opcao == 1) await Programacao(); else await Operacao();
            }
        }
        catch (EndOfStreamException) { Console.WriteLine("\nEntrada encerrada."); }
        finally { Api.Dispose(); }
    }

    private static async Task Programacao()
    {
        while (true)
        {
            Console.WriteLine("\n=== PROGRAMAÇÃO ===\n1 - Cadastrar produto/PN\n2 - Cadastrar máquina\n3 - Cadastrar OF\n4 - Criar programação semanal/liberar OFs\n5 - Consultar programação\n6 - Adicionar OF à semana existente\n7 - Alterar sequência\n0 - Voltar");
            var opcao = Inteiro("Opção: ", 0, 7);
            if (opcao == 0) return;
            await Executar(async () =>
            {
                switch (opcao)
                {
                    case 1:
                        var produto = await Enviar<ProdutoDto>(HttpMethod.Post, "/api/produtos", new CriarProduto(Texto("PN: "), Texto("Nome: ")));
                        Console.WriteLine($"Produto {produto.Id}: {produto.PN} - {produto.Nome}");
                        break;
                    case 2:
                        var maquina = await Enviar<MaquinaDto>(HttpMethod.Post, "/api/maquinas", new CriarMaquina(Texto("Código: "), Texto("Nome: ")));
                        Console.WriteLine($"Máquina {maquina.Id}: {maquina.Codigo}");
                        break;
                    case 3:
                        var produtos = await Obter<List<ProdutoDto>>("/api/produtos");
                        foreach (var p in produtos) Console.WriteLine($"{p.Id} - {p.PN} | {p.Nome}");
                        var produtoId = Inteiro("ID do produto: ", 1);
                        var maquinaId = await EscolherMaquina();
                        var ordem = await Enviar<OrdemDto>(HttpMethod.Post, "/api/ordens",
                            new CriarOrdem(Texto("OF (preserva zeros à esquerda): "), produtoId, maquinaId, Inteiro("Quantidade planejada: ", 1)));
                        Resumo(ordem);
                        Console.WriteLine("OF cadastrada. Libere-a em uma programação semanal pela opção 4 ou 6.");
                        break;
                    case 4:
                        var id = await EscolherMaquina();
                        var pSemana = await Enviar<ProgramacaoDto>(HttpMethod.Post, $"/api/maquinas/{id}/programacao",
                            new CriarProgramacao(Data("Data da semana (dd/MM/aaaa): "), Ids("IDs das OFs em ordem, separados por vírgula (vazio = semana sem OFs): ")));
                        ExibirProgramacao([pSemana]);
                        break;
                    case 5: ExibirProgramacao(await ConsultarProgramacao()); break;
                    case 6:
                    case 7:
                        var programacoes = await ConsultarProgramacao();
                        ExibirProgramacao(programacoes);
                        var programacaoId = Inteiro("ID da programação: ", 1);
                        var atual = programacoes.SingleOrDefault(p => p.Id == programacaoId)
                            ?? throw new InvalidOperationException("Selecione uma programação exibida.");
                        var alterada = opcao == 6
                            ? await Enviar<ProgramacaoDto>(HttpMethod.Post, $"/api/programacoes/{atual.Id}/itens",
                                new AdicionarOrdemProgramacao(Inteiro("ID da OF: ", 1), atual.Versao))
                            : await Enviar<ProgramacaoDto>(HttpMethod.Put, $"/api/programacoes/{atual.Id}/sequencia",
                                new ReordenarProgramacao(Ids("Todos os IDs de OF na nova sequência: "), atual.Versao));
                        ExibirProgramacao([alterada]);
                        break;
                }
            });
        }
    }

    private static async Task Operacao()
    {
        while (true)
        {
            Console.WriteLine("\n=== OPERADOR ===\n1 - Ver programação da máquina\n2 - Acessar pelo número da OF\n0 - Voltar");
            var opcao = Inteiro("Opção: ", 0, 2);
            if (opcao == 0) return;
            await Executar(async () =>
            {
                OrdemDto ordem;
                if (opcao == 1)
                {
                    var programacoes = await ConsultarProgramacao();
                    ExibirProgramacao(programacoes);
                    if (programacoes.All(p => p.Itens.Count == 0)) return;
                    var id = Inteiro("ID da OF exibida: ", 1);
                    ordem = programacoes.SelectMany(p => p.Itens).Select(i => i.Ordem).FirstOrDefault(o => o.Id == id)
                        ?? throw new InvalidOperationException("Selecione uma OF exibida.");
                }
                else ordem = await Obter<OrdemDto>($"/api/ordens/por-of/{Uri.EscapeDataString(Texto("OF: "))}");
                await MenuOrdem(ordem.Id);
            });
        }
    }

    private static async Task MenuOrdem(int id)
    {
        while (true)
        {
            var ordem = await Obter<OrdemDto>($"/api/ordens/{id}");
            Resumo(ordem);
            Console.WriteLine("\n1 - Alterar status operacional\n2 - Meta Hora\n3 - Finalizar OF\n0 - Voltar");
            var opcao = Inteiro("Opção: ", 0, 3);
            if (opcao == 0) return;
            await Executar(async () =>
            {
                if (opcao == 1)
                {
                    Console.WriteLine("1 - Set-up\n2 - Produção\n3 - Pausa\n0 - Cancelar");
                    var status = Inteiro("Status: ", 0, 3);
                    if (status == 0) return;
                    await Enviar<OrdemDto>(HttpMethod.Patch, $"/api/ordens/{id}/status",
                        new AlterarStatus(status switch { 1 => StatusOrdem.SetUp, 2 => StatusOrdem.Producao, _ => StatusOrdem.Interrompida }, ordem.Versao));
                }
                else if (opcao == 2) await MenuMetas(id);
                else
                {
                    Console.WriteLine($"Metas pendentes: {ordem.MetasPendentes}. Resolva todas antes de finalizar.");
                    Console.WriteLine("Após finalizar, a OF não poderá mais receber alterações.");
                    if (Texto("Deseja realmente finalizar esta OF? [S/N]: ").Equals("S", StringComparison.OrdinalIgnoreCase))
                        await Enviar<OrdemDto>(HttpMethod.Post, $"/api/ordens/{id}/finalizar", new FinalizarOrdem(ordem.Versao));
                    else Console.WriteLine("Finalização cancelada.");
                }
            });
        }
    }

    private static async Task MenuMetas(int ordemId)
    {
        while (true)
        {
            var metas = await Obter<MetasHoraDto>($"/api/ordens/{ordemId}/metas-hora");
            Console.WriteLine("\n=== META HORA ===\nID | Período (data/hora/fuso) | Meta | Boas | Refugo | Eficiência");
            foreach (var m in metas.Metas)
                Console.WriteLine($"{m.Id} | {m.Inicio:dd/MM HH:mm zzz} → {m.Fim:dd/MM HH:mm zzz} | {m.MetaPlanejada} | {m.Apontamento?.QuantidadeBoa.ToString() ?? "--"} | {m.Apontamento?.Refugo.ToString() ?? "--"} | {m.Eficiencia?.ToString("P2") ?? "--"}");
            Console.WriteLine("1 - Criar Meta Hora\n2 - Registrar apontamento\n3 - Corrigir apontamento existente\n0 - Voltar");
            var opcao = Inteiro("Opção: ", 0, 3);
            if (opcao == 0) return;
            await Executar(async () =>
            {
                OrdemDto atualizada;
                if (opcao == 1)
                {
                    var inicio = Instante("Início (dd/MM/aaaa HH:mm -03:00): ");
                    var fim = Instante("Fim (dd/MM/aaaa HH:mm -03:00): ");
                    atualizada = await Enviar<OrdemDto>(HttpMethod.Post, $"/api/ordens/{ordemId}/metas-hora",
                        new CriarMetaHora(inicio, fim, Inteiro("Meta: ", 1), metas.Versao));
                }
                else
                {
                    var id = Inteiro("ID da Meta Hora exibida: ", 1);
                    if (!metas.Metas.Any(m => m.Id == id)) throw new InvalidOperationException("Selecione uma Meta Hora exibida.");
                    var boas = Inteiro("Quantidade boa: ", 0);
                    var refugo = Inteiro("Refugo: ", 0);
                    if (opcao == 3 && !Texto("Substituir o resultado anterior? [S/N]: ").Equals("S", StringComparison.OrdinalIgnoreCase)) return;
                    atualizada = await Enviar<OrdemDto>(opcao == 2 ? HttpMethod.Post : HttpMethod.Put,
                        $"/api/metas-hora/{id}/apontamento", new ApontarMetaHora(boas, refugo, metas.Versao));
                }
                Console.WriteLine("Operação salva.");
                Resumo(atualizada);
            });
        }
    }

    private static void Resumo(OrdemDto o)
    {
        Console.WriteLine($"\n=== ORDEM DE FABRICAÇÃO ===\nOF: {o.OF}\nPN: {o.PN}\nProduto: {o.Produto}\nMáquina: {o.MaquinaCodigo} - {o.MaquinaNome}\n");
        Console.WriteLine($"Quantidade planejada: {o.QuantidadePlanejada}\nQuantidade produzida: {o.QuantidadeProduzida}\nRefugo: {o.Refugo}\n");
        Console.WriteLine($"Qualidade: {o.Qualidade:P2}\nEficiência: {o.Eficiencia:P2}\nMetas pendentes: {o.MetasPendentes}\nStatus: {TextoStatus(o.Status)}");
    }
    private static void ExibirProgramacao(IEnumerable<ProgramacaoDto> programacoes)
    {
        foreach (var p in programacoes)
        {
            Console.WriteLine($"\nProgramação {p.Id} | {p.MaquinaCodigo} | Semana: {p.InicioSemana:dd/MM/yyyy}");
            foreach (var item in p.Itens)
                Console.WriteLine($"{item.Sequencia} - ID {item.Ordem.Id} | OF {item.Ordem.OF} | PN {item.Ordem.PN} | {TextoStatus(item.Ordem.Status)}");
        }
    }
    private static string TextoStatus(string status) => status switch
    {
        "SetUp" => "Set-up", "Producao" => "Produção", "Interrompida" => "Pausa", _ => status
    };
    private static async Task<List<ProgramacaoDto>> ConsultarProgramacao() =>
        await Obter<List<ProgramacaoDto>>($"/api/maquinas/{await EscolherMaquina()}/programacao");
    private static async Task<int> EscolherMaquina()
    {
        foreach (var m in await Obter<List<MaquinaDto>>("/api/maquinas")) Console.WriteLine($"{m.Id} - {m.Codigo} | {m.Nome} | {(m.Ativa ? "Ativa" : "Inativa")}");
        return Inteiro("ID da máquina: ", 1);
    }
    private static async Task<T> Obter<T>(string rota) => await Enviar<T>(HttpMethod.Get, rota, null);
    private static async Task<T> Enviar<T>(HttpMethod metodo, string rota, object? corpo)
    {
        using var request = new HttpRequestMessage(metodo, rota);
        if (corpo is not null) request.Content = JsonContent.Create(corpo, options: Json);
        using var response = await Api.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var texto = await response.Content.ReadAsStringAsync();
            string? detalhe = null;
            try { using var problem = JsonDocument.Parse(texto); if (problem.RootElement.TryGetProperty("detail", out var d)) detalhe = d.GetString(); }
            catch (JsonException) { }
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {detalhe ?? response.ReasonPhrase}");
        }
        return await response.Content.ReadFromJsonAsync<T>(Json) ?? throw new InvalidOperationException("Resposta vazia da API.");
    }
    private static async Task Executar(Func<Task> acao)
    {
        try { await acao(); }
        catch (InvalidOperationException ex) { Console.WriteLine(ex.Message); }
        catch (HttpRequestException) { Console.WriteLine("Não foi possível acessar a API. Confira se o servidor está em execução."); }
        catch (TaskCanceledException) { Console.WriteLine("A API não respondeu a tempo. Consulte o resultado antes de repetir a operação."); }
    }
    private static string Linha(string prompt) { Console.Write(prompt); return Console.ReadLine()?.Trim() ?? throw new EndOfStreamException(); }
    private static string Texto(string prompt)
    {
        while (true) { var valor = Linha(prompt); if (valor.Length > 0) return valor; Console.WriteLine("Preencha o campo."); }
    }
    private static int Inteiro(string prompt, int min, int max = int.MaxValue)
    {
        while (true) { if (int.TryParse(Linha(prompt), out var i) && i >= min && i <= max) return i; Console.WriteLine("Informe um número válido."); }
    }
    private static DateOnly Data(string prompt)
    {
        while (true) { if (DateOnly.TryParseExact(Linha(prompt), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d; Console.WriteLine("Use dd/MM/aaaa."); }
    }
    private static DateTimeOffset Instante(string prompt)
    {
        while (true) { if (DateTimeOffset.TryParseExact(Linha(prompt), "dd/MM/yyyy HH:mm zzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d; Console.WriteLine("Informe data, hora e fuso, por exemplo 16/09/2026 23:00 -03:00."); }
    }
    private static int[] Ids(string prompt)
    {
        while (true)
        {
            var texto = Linha(prompt);
            if (texto.Length == 0) return [];
            var partes = texto.Split(',', StringSplitOptions.TrimEntries);
            if (partes.All(p => int.TryParse(p, out var id) && id > 0)) return partes.Select(int.Parse).ToArray();
            Console.WriteLine("Use IDs numéricos separados por vírgula.");
        }
    }
}
