using worker;
using config;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ProcessamentoConfig>(
    builder.Configuration.GetSection("ProcessamentoPedidos"));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
