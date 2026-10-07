namespace worker;
using config;
using pedido;
using Microsoft.Extensions.Options;

public class Worker : BackgroundService
{
    private static readonly string[] MotivosErro =
    {
        "Pagamento recusado",
        "Estoque insuficiente",
        "Endereço de entrega inválido",
        "Cartão expirado",
        "Falha ao comunicar com a transportadora"
    };

    private readonly ProcessamentoConfig _config;
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly object _locker = new object();

    public Worker(ILogger<Worker> logger, IOptions<ProcessamentoConfig> config, IHostApplicationLifetime lifetime)
    {
        _config = config.Value;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            List<Pedido> pedidos = GerarPedidos(_config.TotalPedidos);
            _logger.LogInformation($"BLACK FRIDAY: {pedidos.Count} pedidos recebidos de uma vez! (Máximo de {_config.GrauConcorrencia} simultâneos)\n");

            decimal totalFaturado = 0;
            int processados = 0;
            List<PedidoComErro> erros = new List<PedidoComErro>();

            using SemaphoreSlim semaforo = new SemaphoreSlim(_config.GrauConcorrencia);
            var cronometro = System.Diagnostics.Stopwatch.StartNew();

            IEnumerable<Task> tarefas = pedidos.Select(pedido => Task.Run(async () =>
            {
                await semaforo.WaitAsync(stoppingToken);
                try
                {
                    await ProcessarPedidoAsync(pedido, stoppingToken);

                    lock (_locker)
                    {
                        totalFaturado += pedido.Valor;
                        processados++;
                    }
                    _logger.LogInformation($"[Thread {Environment.CurrentManagedThreadId}] Pedido {pedido.Id} OK - {pedido.Cliente} - {pedido.Valor:C}");
                }
                catch (PedidoException ex)
                {
                    lock (_locker)
                    {
                        erros.Add(new PedidoComErro { Pedido = pedido, Motivo = ex.Message });
                    }
                    _logger.LogWarning($"[Thread {Environment.CurrentManagedThreadId}] Pedido {pedido.Id} COM ERRO - {ex.Message}");
                }
                finally
                {
                    semaforo.Release();
                }
            }, stoppingToken));

            await Task.WhenAll(tarefas.ToList());
            cronometro.Stop();

            ExibirResumo(pedidos.Count, processados, totalFaturado, erros, cronometro.Elapsed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Processamento cancelado.");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private static List<Pedido> GerarPedidos(int quantidade)
    {
        return Enumerable.Range(1, quantidade).Select(id => new Pedido
        {
            Id = id,
            Cliente = $"Cliente {id:0000}",
            Valor = Math.Round((decimal)(Random.Shared.NextDouble() * 1950 + 50), 2) // R$ 50 a R$ 2000
        }).ToList();
    }

    private async Task ProcessarPedidoAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        // Simula pagamento, estoque e logística
        await Task.Delay(Random.Shared.Next(10, 150), cancellationToken);

        if (Random.Shared.NextDouble() < _config.TaxaErro)
            throw new PedidoException(MotivosErro[Random.Shared.Next(MotivosErro.Length)]);
    }

    private void ExibirResumo(int total, int processados, decimal totalFaturado, List<PedidoComErro> erros, TimeSpan duracao)
    {
        _logger.LogInformation("");
        _logger.LogInformation("========== RESUMO BLACK FRIDAY ==========");
        _logger.LogInformation($"Pedidos recebidos:     {total}");
        _logger.LogInformation($"Pedidos processados:   {processados}");
        _logger.LogInformation($"Pedidos com erro:      {erros.Count}");
        _logger.LogInformation($"Total faturado:        {totalFaturado:C}");
        _logger.LogInformation($"Tempo de processamento: {duracao.TotalSeconds:F2}s");

        if (erros.Count == 0) return;

        _logger.LogInformation("--- Erros por motivo ---");
        foreach (var grupo in erros.GroupBy(e => e.Motivo).OrderByDescending(g => g.Count()))
            _logger.LogInformation($"{grupo.Key}: {grupo.Count()}");

        _logger.LogInformation($"--- Pedidos com erro (Ids): {string.Join(", ", erros.Select(e => e.Pedido.Id).OrderBy(id => id))}");
        _logger.LogInformation($"Valor não faturado:    {erros.Sum(e => e.Pedido.Valor):C}");
    }
}
