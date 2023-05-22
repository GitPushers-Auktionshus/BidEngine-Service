using System;
using MongoDB.Bson.Serialization.Attributes;

namespace BidWorker.Model
{
    public class Comment
    {
        [BsonId]
        public string CommentID { get; set; }
        public string UserID { get; set; }
        public string Username { get; set; }
        public DateTime? DateCreated { get; set; }
        public string Message { get; set; }

        public Comment()
        {
        }
    }
}
