using FireWallService;

var builder = Host.CreateApplicationBuilder(args);

// Добавляем консольное логирование для диагностики и файловое для записи
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFile("firewall.log", LogLevel.Debug);

builder.Services.AddHostedService<FirewallService>();

var host = builder.Build();

Console.WriteLine("\n╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║   FireWallService Started                              ║");
Console.WriteLine("║   Named Pipe: FireWallServicePipe                      ║");
Console.WriteLine("║   Waiting for GUI connections...                       ║");
Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

host.Run();
