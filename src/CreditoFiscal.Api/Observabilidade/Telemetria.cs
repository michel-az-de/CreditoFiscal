using System.Diagnostics;

namespace CreditoFiscal.Api.Observabilidade;

// fonte unica de spans do servico, registrada no OpenTelemetry via AddSource
public static class Telemetria
{
    public const string Nome = "CreditoFiscal";

    public static readonly ActivitySource Fonte = new ActivitySource(Nome);
}
