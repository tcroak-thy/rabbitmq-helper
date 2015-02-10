using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Thycotic.Logging;
using Thycotic.MemoryMq.Collections;

namespace Thycotic.MemoryMq.Subsystem
{
    /// <summary>
    /// Exchange that binds topics to queues
    /// </summary>
    public class ExchangeDictionary 
    {
        private readonly ConcurrentDictionary<RoutingSlip, MessageQueue> _data = new ConcurrentDictionary<RoutingSlip, MessageQueue>();

        private readonly ILogWriter _log = Log.Get(typeof(ExchangeDictionary));

        /// <summary>
        /// Gets the mailboxes in the exchange
        /// </summary>
        /// <value>
        /// The mailboxes.
        /// </value>
        public IList<Mailbox> Mailboxes 
        {
            get { return _data.Select(kvp => new Mailbox(kvp.Key, kvp.Value)).ToList(); }
        }

        /// <summary>
        /// Gets a value indicating whether this exchange is empty. This empty mailboxes.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is empty; otherwise, <c>false</c>.
        /// </value>
        public bool IsEmpty {
            get { return _data.Values.All(q => q.IsEmpty); }
        }

        /// <summary>
        /// Publishes the body to the specified routing slip.
        /// </summary>
        /// <param name="routingSlip">The routing slip.</param>
        /// <param name="body">The body.</param>
        public void Publish(RoutingSlip routingSlip, MemoryMqDeliveryEventArgs body)
        {
            _log.Debug(string.Format("Publishing message to {0}", routingSlip));

            _data.GetOrAdd(routingSlip, s => new MessageQueue());

            _data[routingSlip].Enqueue(body);
        }

        /// <summary>
        /// Acknowledges the specified delivery tag.
        /// </summary>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="routingSlip">The routing slip.</param>
        /// <exception cref="System.ApplicationException">Delivery tag was not found</exception>
        public void Acknowledge(ulong deliveryTag, RoutingSlip routingSlip)
        {
           _data[routingSlip].Acknowledge(deliveryTag);
        }

        /// <summary>
        /// Negativelies the acknowledge.
        /// </summary>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="routingSlip">The routing slip.</param>
        /// <exception cref="System.ApplicationException">Delivery tag was not found</exception>
        public void NegativelyAcknowledge(ulong deliveryTag, RoutingSlip routingSlip)
        {
            _data[routingSlip].NegativelyAcknoledge(deliveryTag);
        }
    }
}