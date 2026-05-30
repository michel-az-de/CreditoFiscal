using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Infraestrutura.Mensageria;

public sealed class AdaptadorServiceBus : IMensagemPublisher, IMensagemConsumer
{
    private readonly ServiceBusClient _cliente;

    public AdaptadorServiceBus(ServiceBusClient cliente)
    {
        _cliente = cliente;
    }

    public async Task PublicarAsync<T>(string fila, T mensagem, CancellationToken ct)
    {
        var remetente = _cliente.CreateSender(fila);
        try
        {
            var corpo = JsonSerializer.SerializeToUtf8Bytes(mensagem, OpcoesJsonMensageria.Padrao);
            var mensagemServiceBus = new ServiceBusMessage(corpo) { ContentType = "application/json" };
            await remetente.SendMessageAsync(mensagemServiceBus, ct);
        }
        finally
        {
            await remetente.DisposeAsync();
        }
    }

    public async Task<IConsumerSession<T>> AbrirSessaoAsync<T>(string fila, int maximo, TimeSpan timeout, CancellationToken ct)
    {
        // PeekLock (padrao): a mensagem fica travada ate complete/abandon
        var receptor = _cliente.CreateReceiver(fila);
        try
        {
            var recebidas = await receptor.ReceiveMessagesAsync(maximo, timeout, ct);
            return new ServiceBusConsumerSession<T>(receptor, recebidas, OpcoesJsonMensageria.Padrao);
        }
        catch
        {
            await receptor.DisposeAsync();
            throw;
        }
    }
}
