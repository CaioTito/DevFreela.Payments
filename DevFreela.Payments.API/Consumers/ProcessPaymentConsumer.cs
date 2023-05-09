using DevFreela.Payments.API.Models;
using DevFreela.Payments.API.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DevFreela.Payments.API.Consumers;

public class ProcessPaymentConsumer : BackgroundService
{
    private const string PAYMENT_QUEUE = "Payments";
    private const string PAYMENT_APPROVED_QUEUE = "PaymentsApproved";
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;

    public ProcessPaymentConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        var factory = new ConnectionFactory
        {
            HostName = "localhost"
        };

        _connection = factory.CreateConnection();
        
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: PAYMENT_QUEUE,
            durable: false,
            exclusive: false, 
            autoDelete: false,
            arguments: null);

        _channel.QueueDeclare(
            queue: PAYMENT_APPROVED_QUEUE,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (sender, eventArgs) =>
        {
            var byteArray = eventArgs.Body.ToArray();
            var paymentInfoJson = Encoding.UTF8.GetString(byteArray);

            var paymentInfo = JsonSerializer.Deserialize<PaymentInfoInputModel>(paymentInfoJson);

            ProcessPayment(paymentInfo);

            var paymentApproved = new PaymentsApprovedIntegrationEvent(paymentInfo.IdProject);
            var paymentApprovedJson = JsonSerializer.Serialize(paymentApproved);
            var paymentApprovedBytes = Encoding.UTF8.GetBytes(paymentApprovedJson);

            _channel.BasicPublish(
                exchange: "",
                routingKey: PAYMENT_APPROVED_QUEUE,
                basicProperties: null,
                body: paymentApprovedBytes
                );

            _channel.BasicAck(eventArgs.DeliveryTag, false);
        };

        _channel.BasicConsume(PAYMENT_QUEUE, false, consumer);

        return Task.CompletedTask;
    }

    public void ProcessPayment(PaymentInfoInputModel paymentInfo)
    {
        using(var scope = _serviceProvider.CreateScope())
        {
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            paymentService.Process(paymentInfo);
        }
    }
}
