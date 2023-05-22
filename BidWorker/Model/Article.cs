﻿using BidWorker.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BidWorker.Model
{
    public class Article
    {
        [BsonId]
        public string ArticleID { get; set; }
        public string? Name { get; set; }
        public bool NoReserve { get; set; }
        public double EstimatedPrice { get; set; }
        public string? Description { get; set; }
        public List<Image> Images { get; set; }
        public string? Category { get; set; }
        public bool Sold { get; set; }
        public Auctionhouse Auctionhouse { get; set; }
        public User Seller { get; set; }
        public double MinPrice { get; set; }
        public User Buyer { get; set; }

        public Article()
        {

        }

    }
}
