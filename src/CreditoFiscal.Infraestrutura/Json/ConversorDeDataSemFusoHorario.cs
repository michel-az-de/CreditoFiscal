using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreditoFiscal.Infraestrutura.Json;

// le so o dia e forca Kind=Unspecified: Npgsql 6 quebra com DateTime Utc em coluna date
public sealed class ConversorDeDataSemFusoHorario : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();
        var data = DateTimeOffset.Parse(texto!, CultureInfo.InvariantCulture);
        return DateTime.SpecifyKind(data.Date, DateTimeKind.Unspecified);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
