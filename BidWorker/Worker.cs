namespace BidWorker;
using System.IO;
using System.Text;
using System.Text.Json;
using BidWorker.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly string _hostName;

    private readonly string _connectionURI;
    private readonly string _auctionsDatabase;
    private readonly string _usersDatabase;

    private readonly string _listingsCollectionName;
    private readonly string _userCollectionName;


    private readonly IMongoCollection<Auction> _listingsCollection;
    private readonly IMongoCollection<User> _userCollection;


    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;

        _logger.LogInformation($"Connection: {_hostName}");

        try
        {
            _connectionURI = config["ConnectionURI"] ?? "ConnectionURI missing";
            _hostName = config["HostnameRabbit"];
            _auctionsDatabase = "Auctions";
            _usersDatabase = "Users";
            _listingsCollectionName = "listing";
            _userCollectionName = "user";

        }
        catch (Exception ex)
        {
            _logger.LogError("Error retrieving enviroment variables");
        }

        try
        {
            // Sets MongoDB client
            var mongoClient = new MongoClient(_connectionURI);

            // Sets MongoDB Database
            var auctionsDatabase = mongoClient.GetDatabase(_auctionsDatabase);
            var usersDatabase = mongoClient.GetDatabase(_usersDatabase);


            // Sets MongoDB Collection
            _listingsCollection = auctionsDatabase.GetCollection<Auction>(_listingsCollectionName);
            _userCollection = usersDatabase.GetCollection<User>(_userCollectionName);


        }
        catch (Exception ex)
        {
            _logger.LogError($"Error trying to connect to database: {ex.Message}");

            throw;
        }

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

            try
            {
                User bidder = new User();
                Auction currentAuction = new Auction();

                currentAuction = _listingsCollection.Find(x => x.AuctionID == bid.AuctionID).FirstOrDefault();
                bidder = _userCollection.Find(x => x.UserID == bid.BidderID).FirstOrDefault();

                var filter = Builders<Auction>.Filter.Eq("AuctionID", bid.AuctionID);

                var updateHighestBid = Builders<Auction>.Update.Set("HighestBid", bid.Price);
                var updateBids = Builders<Auction>.Update.Push("Bids", new Bid
                {
                    BidID = ObjectId.GenerateNewId().ToString(),
                    Date = DateTime.Now,
                    Price = bid.Price,
                    Bidder = bidder
                });


                _listingsCollection.UpdateOne(filter, updateHighestBid);
                _listingsCollection.UpdateOne(filter, updateBids);

                _logger.LogInformation("Bid added to auction!");

            }
            catch (Exception ex)
            {
                _logger.LogError($"Eror while adding bid to auction: {ex.Message}");
            }

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