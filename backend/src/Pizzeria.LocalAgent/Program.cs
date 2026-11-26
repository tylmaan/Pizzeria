using Pizzeria.LocalAgent;
using Pizzeria.LocalAgent.Printing;

// Configuration lives next to the executable, whatever the working directory (services start in System32).
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

// Runs as a Windows service when installed as one (sc.exe create ...), as a console app otherwise.
builder.Services.AddWindowsService(options => options.ServiceName = "Pizzeria Local Agent");

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection("Printers:Simulator"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SimulatedPrinter>();

// Printers:Receipt / Printers:Fiscal = "Simulator" or "None". Real drivers are added here later.
builder.Services.AddSingleton<IReceiptPrinter>(sp => builder.Configuration["Printers:Receipt"] == "Simulator"
    ? sp.GetRequiredService<SimulatedPrinter>()
    : new NoReceiptPrinter());
builder.Services.AddSingleton<IFiscalPrinter>(sp => builder.Configuration["Printers:Fiscal"] == "Simulator"
    ? sp.GetRequiredService<SimulatedPrinter>()
    : new NoFiscalPrinter());

builder.Services.AddSingleton<PrintJobProcessor>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
