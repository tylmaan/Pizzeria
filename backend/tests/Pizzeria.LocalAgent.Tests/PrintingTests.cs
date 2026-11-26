using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pizzeria.LocalAgent.Printing;

namespace Pizzeria.LocalAgent.Tests;

public sealed class PrintingTests : IDisposable
{
    private const string TicketJson = """
        {"number":12,"placedAt":"2026-11-26T17:05:00+00:00","fulfillment":"Delivery",
         "lines":[{"quantity":2,"name":"Margherita / Pepperoni","size":"42 cm","modifiers":["Czosnkowy"],"notes":"Bez oregano"}],
         "notes":"Domofon nie działa"}
        """;

    private readonly string _output = Path.Combine(Path.GetTempPath(), "pizzeria-agent-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_output))
        {
            Directory.Delete(_output, recursive: true);
        }
    }

    [Fact]
    public void KitchenTicket_ShowsNumberItemsModifiersAndNotes_InLocalTime()
    {
        var ticket = System.Text.Json.JsonSerializer.Deserialize<KitchenTicketPayload>(TicketJson, System.Text.Json.JsonSerializerOptions.Web)!;
        var warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

        var text = TicketFormatter.KitchenTicket(ticket, warsaw);

        Assert.Contains("Zamówienie #12", text, StringComparison.Ordinal);
        Assert.Contains("DOSTAWA", text, StringComparison.Ordinal);
        Assert.Contains("26.11.2026 18:05", text, StringComparison.Ordinal);
        Assert.Contains("2x Margherita / Pepperoni 42 cm", text, StringComparison.Ordinal);
        Assert.Contains("   + Czosnkowy", text, StringComparison.Ordinal);
        Assert.Contains("   ! Bez oregano", text, StringComparison.Ordinal);
        Assert.Contains("Uwagi: Domofon nie działa", text, StringComparison.Ordinal);
        Assert.All(text.Split(Environment.NewLine), line => Assert.True(line.Length <= TicketFormatter.Width, line));
    }

    [Fact]
    public void FiscalReceipt_ListsLinesDeliveryAndTotal()
    {
        var receipt = new FiscalReceiptPayload(7, [new FiscalReceiptLine("Margherita 42 cm", 2, 34m, 68m)], 6m, 74m, "Cash");

        var text = TicketFormatter.FiscalReceipt(receipt);

        Assert.Contains("  2 x 34,00", text, StringComparison.Ordinal);
        Assert.Contains("Dostawa", text, StringComparison.Ordinal);
        Assert.Matches(@"SUMA PLN\s+74,00", text);
    }

    [Fact]
    public async Task Processor_PrintsKitchenTicketWithSimulator()
    {
        var printer = Simulator();
        var processor = new PrintJobProcessor(printer, printer, NullLogger<PrintJobProcessor>.Instance);

        var result = await processor.ProcessAsync(new AgentJob(Guid.NewGuid(), PrintJobKinds.KitchenTicket, TicketJson), CancellationToken.None);

        Assert.Equal(PrintResult.Ok, result);
        var file = Assert.Single(Directory.GetFiles(_output));
        Assert.EndsWith("-kitchen-12.txt", file, StringComparison.Ordinal);
        Assert.Contains("Margherita / Pepperoni", await File.ReadAllTextAsync(file), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Processor_TurnsProblemsIntoFailedJobs()
    {
        var processor = new PrintJobProcessor(Simulator(), new NoFiscalPrinter(), NullLogger<PrintJobProcessor>.Instance);

        var noPrinter = await processor.ProcessAsync(new AgentJob(Guid.NewGuid(), PrintJobKinds.FiscalReceipt, "{}"), CancellationToken.None);
        var badPayload = await processor.ProcessAsync(new AgentJob(Guid.NewGuid(), PrintJobKinds.KitchenTicket, "not json"), CancellationToken.None);
        var unknownKind = await processor.ProcessAsync(new AgentJob(Guid.NewGuid(), "Invoice", "{}"), CancellationToken.None);

        Assert.Equal(PrintResult.Failed("No fiscal printer configured"), noPrinter);
        Assert.Equal(PrintResult.Failed("Invalid print data"), badPayload);
        Assert.False(unknownKind.Printed);
        Assert.Contains("Invoice", unknownKind.Error, StringComparison.Ordinal);
    }

    private SimulatedPrinter Simulator() =>
        new(Options.Create(new SimulatorOptions { OutputDirectory = _output }), TimeProvider.System, NullLogger<SimulatedPrinter>.Instance);
}
