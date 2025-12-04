using BinomoBackend.Domain.Events;

namespace BinomoBackend.Application.Interfaces;

public interface IKafkaProducer
{
    Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken ct = default) 
        where TEvent : PaymentEvent;
}