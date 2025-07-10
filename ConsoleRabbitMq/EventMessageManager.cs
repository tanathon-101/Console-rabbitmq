using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleRabbitMq
{
    public interface IEventMessageManager
    {
        Task<bool> PublishAsync(IChannel channel, string queueName, string message);

    }
    public class EventMessageManager : IEventMessageManager
    {
        private readonly IRabbitMQConnection _rabbitMQConnection;
        private readonly ILogger<EventMessageManager> _logger;
        private AsyncRetryPolicy _retryPolicyAsync;

        public EventMessageManager(IRabbitMQConnection rabbitMQConnection, ILogger<EventMessageManager> logger)
        {
            _rabbitMQConnection = rabbitMQConnection;
            InitialRetryPolicy();
            _logger = logger;
        }

       
        public async Task<bool> PublishAsync(IChannel channel, string queueName, string message)

        {
            try
            {
                if (!_rabbitMQConnection.IsConnected)
                {
                    await _rabbitMQConnection.TryConnectAsync();
                }

                if (!_rabbitMQConnection.IsChannelOpen)
                {
                    await _rabbitMQConnection.CreateChannelAsync();
                }

                await _rabbitMQConnection.Channel.QueueDeclareAsync(queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

               byte[] publishBody = Encoding.UTF8.GetBytes(message);


                BasicProperties props = CreateBasicProperties();



                
                    await _rabbitMQConnection.Channel.BasicPublishAsync(exchange: "",
                                                routingKey: queueName,
                                                mandatory: true,
                                                basicProperties: props,
                                                body: publishBody,
                                                cancellationToken: CancellationToken.None);
                

                return true;
            }
            catch (Exception ex)
            {
                    return false;
            }
            finally
            {
                await _rabbitMQConnection.DisposeAsync();
            }
        }


        private BasicProperties CreateBasicProperties()
        {
            BasicProperties props = new BasicProperties
            {
                //ContentType = "application/json",
                //ContentType = "text/plain",
                DeliveryMode = DeliveryModes.Persistent,
            };
            return props;
        }

        private void InitialRetryPolicy()
        {
            _retryPolicyAsync = Policy
                .Handle<SocketException>()
                .Or<BrokerUnreachableException>()
                .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(2),
                onRetryAsync: (exception, timespan, attempt, context) =>
                {
                    _logger.LogWarning($"[ERROR] Attempt retry {attempt} -> Message:{exception.ToString()}");
                    return Task.CompletedTask;
                });
        }

    }
}