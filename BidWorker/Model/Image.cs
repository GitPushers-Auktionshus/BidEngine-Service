using System.Globalization;
using System.Runtime.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BidWorker.Model
{
    public class EnvVariables
    {
        public Dictionary<string, string> dictionary { get; set; }

        public EnvVariables()
        {
        }
    }
}
