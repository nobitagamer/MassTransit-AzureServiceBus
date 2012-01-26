using System;
using System.IO;
using System.Threading;
using Magnum.Extensions;
using MassTransit.Context;
using MassTransit.Util;
using Microsoft.ServiceBus.Messaging;

namespace MassTransit.Transports.ServiceBusQueues
{
	public class InboundServiceBusQueuesTransport
		: IInboundTransport
	{
		private readonly ConnectionHandler<ServiceBusQueuesConnection> _connectionHandler;
		private readonly IEndpointAddress _address;

		private bool _disposed;
		private ServiceBusQueueSubsciption _subsciption;

		public InboundServiceBusQueuesTransport(IEndpointAddress address,
		                                        ConnectionHandler<ServiceBusQueuesConnection> connectionHandler)
		{
			_connectionHandler = connectionHandler;
			_address = address;
		}

		public IEndpointAddress Address
		{
			get { return _address; }
		}

		public void Receive(Func<IReceiveContext, Action<IReceiveContext>> callback, TimeSpan timeout)
		{
			AddConsumerBinding();

			_connectionHandler.Use(connection =>
				{
					BrokeredMessage message;
					if ((message = connection.Queue.Receive(50.Milliseconds())) == null)
					{
						Thread.Sleep(10);
						return;
					}

					using (var body = new MemoryStream(message.GetBody<MessageEnvelope>().ActualBody, false))
					{
						var context = ReceiveContext.FromBodyStream(body);
						context.SetMessageId(message.MessageId);
						context.SetInputAddress(Address);
						context.SetCorrelationId(message.CorrelationId);

						var receive = callback(context);
						if (receive == null)
						{
							if (SpecialLoggers.Messages.IsInfoEnabled)
								SpecialLoggers.Messages.InfoFormat("SKIP:{0}:{1}", Address, context.MessageId);
						}
						else
						{
							receive(context);
						}
					}
				});
		}

		private void AddConsumerBinding()
		{
			if (_subsciption != null)
				return;

			_subsciption = new ServiceBusQueueSubsciption(_address);
			_connectionHandler.AddBinding(_subsciption);
		}

		private void RemoveConsumer()
		{
			if (_subsciption != null)
			{
				_connectionHandler.RemoveBinding(_subsciption);
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				RemoveConsumer();
			}

			_disposed = true;
		}

		~InboundServiceBusQueuesTransport()
		{
			Dispose(false);
		}
	}
}