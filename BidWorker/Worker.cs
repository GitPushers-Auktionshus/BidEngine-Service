namespace BidWorker;
using System.IO;
using System.Text;
using System.Text.Json;
using BidWorker.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly string _hostName;

    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _hostName = config["HostnameRabbit"];

        _logger.LogInformation($"Connection: {_hostName}");

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var factory = new ConnectionFactory
        {
            HostName = _hostName
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(exchange: "AuctionHouse", type: ExchangeType.Topic);

        var queueName = channel.QueueDeclare().QueueName;

        channel.QueueBind(queue: queueName,
                    exchange: "AuctionHouse",
                    routingKey: "AuctionBid");

        _logger.LogInformation("[*] Waiting for messages.");

        var consumer = new EventingBasicConsumer(channel);

        // Delegate method
        consumer.Received += (model, ea) =>
        {
            // Henter data ned fra køen
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            _logger.LogInformation($"Routingkey for modtaget besked: {ea.RoutingKey}");

            // Deserialiserer det indsendte data om til C# objekt
            BidDTO? bid = JsonSerializer.Deserialize<BidDTO>(message);

            _logger.LogInformation($"[*] BidDTO modtaget:\n\tPrice: {bid.Price}\n\tBidderID: {bid.BidderID}\n\tAuctionID: {bid.AuctionID}\n\t");

        };

        channel.BasicConsume(queue: queueName,
                                autoAck: true,
                                consumer: consumer);


        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }

    }
}