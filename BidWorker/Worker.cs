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

    // Initializes enviroment variables
    private readonly string _hostName;

    private readonly string _connectionURI;
    private readonly string _auctionsDatabase;
    private readonly string _usersDatabase;

    private readonly string _listingsCollectionName;
    private readonly string _userCollectionName;

    // Initializes MongoDB database collection
    private readonly IMongoCollection<Auction> _listingsCollection;
    private readonly IMongoCollection<User> _userCollection;


    public Worker(ILogger<Worker> logger, IConfiguration config, EnvVariables vaultSecrets)
    {
        _logger = logger;
        _config = config;

        _logger.LogInformation($"Connection: {_hostName}");

        try
        {
            // Retrieves enviroment variables from program.cs, from injected EnvVariables class
            _connectionURI = vaultSecrets.dictionary["ConnectionURI"];

            // Retrieves the RabbitMQ hostname from the docker file
            _hostName = config["HostnameRabbit"] ?? "HostnameRabbit missing";

            // Retrieves User and Auction database
            _auctionsDatabase = config["AuctionsDatabase"] ?? "AuctionsDatabase missing";
            _usersDatabase = config["UsersDatabase"] ?? "UsersDatabase missing";

            // Retrieves listing and user collection
            _listingsCollectionName = config["AuctionCollection"] ?? "AuctionCollection missing";
            _userCollectionName = config["UserCollection"] ?? "UserCollection missing";

            _logger.LogInformation($"BidWorker secrets: ConnectionURI: {_connectionURI}");
            _logger.LogInformation($"Bidworker Database and Collections: AuctionDatabase: {_auctionsDatabase}, UsersDatabase: {_usersDatabase}, AuctionCollection: {_listingsCollectionName}, UserCollection: {_userCollectionName}");


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
        // Connects to RabbitMQ
        var factory = new ConnectionFactory
        {
            HostName = _hostName
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Declares the topic exchange "AuctionHouse"
        channel.ExchangeDeclare(exchange: "AuctionHouse", type: ExchangeType.Topic);

        var queueName = channel.QueueDeclare().QueueName;

        // Binds the queue to the routingkey AuctionBid
        channel.QueueBind(queue: queueName,
                    exchange: "AuctionHouse",
                    routingKey: "AuctionBid");

        _logger.LogInformation("[*] Waiting for messages.");

        var consumer = new EventingBasicConsumer(channel);

        // Delegate method
        consumer.Received += (model, ea) =>
        {
            // Retrieves the data from the body
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            _logger.LogInformation($"Routingkey for received message: {ea.RoutingKey}");

            // Deserializes the received data to a BidDTO object
            BidDTO? bid = JsonSerializer.Deserialize<BidDTO>(message);

            try
            {
                User bidder = new User();
                Auction currentAuction = new Auction();

                // Finds the auction that's going to the receive the new bid, and finds the user that made the bid
                currentAuction = _listingsCollection.Find(x => x.AuctionID == bid.AuctionID).FirstOrDefault();
                bidder = _userCollection.Find(x => x.UserID == bid.BidderID).FirstOrDefault();

                // Creates a filter that finds the auction with a matching Auction ID
                var filter = Builders<Auction>.Filter.Eq("AuctionID", bid.AuctionID);

                // Creates two update definitions to change the "HighestBid" property and push a new bid to the list of bids in a auction
                var updateHighestBid = Builders<Auction>.Update.Set("HighestBid", bid.Price);
                var updateBids = Builders<Auction>.Update.Push("Bids", new Bid
                {
                    BidID = ObjectId.GenerateNewId().ToString(),
                    Date = DateTime.Now,
                    Price = bid.Price,
                    Bidder = bidder
                });

                // Updates the auction in the listing collection using the filter and the two update definitions
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

        // Consumes the queue
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