﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoQueue.Core;
using MongoQueue.Core.AgentAbstractions;
using MongoQueue.Core.Entities;

namespace MongoQueue.Legacy
{
    public sealed class LegacyDeadLettersAgent : IDeadLettersAgent
    {
        private readonly LegacyMongoAgent _mongoAgent;

        public LegacyDeadLettersAgent(LegacyMongoAgent mongoAgent)
        {
            _mongoAgent = mongoAgent;
        }
        public Task<DeadLetter[]> GetDeadLetters(string route, string topic, CancellationToken cancellationToken)
        {
            var find = Query.And(Query<DeadLetter>.EQ(l => l.Topic, topic),
                Query<DeadLetter>.NotIn(l => l.ResentTo, new[] { route }));
            var update = Update<DeadLetter>.Push(x => x.ResentTo, route);
            var collection = _mongoAgent.GetCollection<DeadLetter>();
            var deadLetters = collection.Find(find).ToList();
            var ids = deadLetters.Select(l => l.Id).ToArray();
            collection.Update(Query<DeadLetter>.In(l => l.Id, ids), update, UpdateFlags.Multi);
            return Task.FromResult(deadLetters.ToArray());
        }

        public Task PublishAsync(string topic, string payload, Ack ack = Ack.Master)
        {
            Publish(topic, payload, ack);
            return Task.CompletedTask;
        }

        public void Publish(string topic, string payload, Ack ack = Ack.Master)
        {
            var collection = _mongoAgent.GetCollection<DeadLetter>();
            collection.Insert(new DeadLetter
            {
                Id = IdGenerator.GetId(),
                ResentTo = new string[0],
                Payload = payload,
                Topic = topic
            }, ack.ToWriteConcern());
        }
    }
}