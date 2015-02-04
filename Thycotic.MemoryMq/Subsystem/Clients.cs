﻿using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;
using Thycotic.Logging;

namespace Thycotic.MemoryMq.Subsystem
{
    public class Clients
    {
        private readonly ConcurrentDictionary<string, ClientList> _data =
            new ConcurrentDictionary<string, ClientList>();

        private readonly ILogWriter _log = Log.Get(typeof(Clients));

        public void AddConsumer(string queueName)
        {
            var callback = OperationContext.Current.GetCallbackChannel<IMemoryMqServerCallback>();

            var client = new MemoryMqServerClient
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                Channel = (IContextChannel)callback,
                Callback = callback
            };

            //have the consumer remove itself when it disconnects
            client.Channel.Closed += (sender, args) =>
            {
                _log.Debug("Detaching consumer");
                GetConsumerList(queueName).RemoveConsumer(client);
            };

            GetConsumerList(queueName).AddConsumer(client);
        }

        public bool TryGetClient(string queueName, out MemoryMqServerClient client)
        {
            return GetConsumerList(queueName).TryGetConsumer(out client);
        }

        private ClientList GetConsumerList(string queueName)
        {
            return _data.GetOrAdd(queueName, s => new ClientList());
        }


        private class ClientList
        {
            private readonly ConcurrentDictionary<string, MemoryMqServerClient> _data = new ConcurrentDictionary<string, MemoryMqServerClient>();

            private int _robin;

            private readonly ILogWriter _log = Log.Get(typeof(ClientList));

            public void AddConsumer(MemoryMqServerClient client)
            {
                _log.Debug(string.Format("Adding consumer with session ID {0}", client.Channel.SessionId));

                _data.TryAdd(client.Channel.SessionId, client);
            }

            public void RemoveConsumer(MemoryMqServerClient client)
            {
                MemoryMqServerClient temp;
                _data.TryRemove(client.Channel.SessionId, out temp);
            }

            public bool TryGetConsumer(out MemoryMqServerClient client)
            {
                if (_data.Any())
                {
                    _robin = _robin % _data.Count;
                    client = _data.Values.Skip(_robin).Single();
                    return true;
                }
                
                client = null;
                _robin = 0;
                return false;
            }
        }
    }
}