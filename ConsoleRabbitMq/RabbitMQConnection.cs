using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

using System.Net.Sockets;

namespace ConsoleRabbitMq
{

    public interface IRabbitMQConnection : IAsyncDisposable
    {

        Task<bool> TryConnectAsync();
        Task<bool> CreateConnectionAsync();
        Task<bool> CreateChannelAsync();
        bool IsConnected { get; }
        bool IsChannelOpen { get; } 
        IConnection Connection { get; }
        IChannel Channel { get; }
    }
    public class RabbitMQConnection : IRabbitMQConnection
    {
        private AsyncRetryPolicy _retryPolicyAsync;
        private List<AmqpTcpEndpoint> _rabbitEndpoints;

        private IConnectionFactory _factory;
        private IConnection _connection;
        private IChannel _channel;
        private readonly ILogger<RabbitMQConnection> _logger;
        private bool _disposed;
        private readonly object sync_root = new object();
        public bool IsConnected => _connection != null && _connection.IsOpen ? true : false;
        public bool IsChannelOpen => _channel != null && _channel.IsOpen ? true : false;
        public IConnection Connection => _connection;
        public IChannel Channel => _channel;

        public RabbitMQConnection(ILogger<RabbitMQConnection> logger,
            IConnectionFactory connectionFactory)
        {
            _logger = logger;
            _factory = connectionFactory;
            InitialRabbitMQSettings();
            InitialRabbitEndpoints();
            InitialRetryPolicy();
        }

        private void InitialRabbitEndpoints()
        {
            _rabbitEndpoints = new List<AmqpTcpEndpoint>
            {
                  new AmqpTcpEndpoint("localhost", 5672)
            };
        }


        public async Task<bool> TryConnectAsync()
        {
            try
            {
                _logger.LogInformation("RabbitMQ Client is trying to connect...");
                await _retryPolicyAsync.ExecuteAsync(async () =>
                {
                    try
                    {
                        _connection = await _factory.CreateConnectionAsync(_rabbitEndpoints);
                    }
                    catch
                    {
                        throw;
                    }
                });

                if (IsConnected)
                {
                    _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                    _connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                    _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
                    _logger.LogInformation($"RabbitMQ persistent connection acquired a connection {_connection.Endpoint.HostName} and is subscribed to failure events");
                    return true;
                }
                else
                {
                    _logger.LogCritical("FATAL ERROR: RabbitMQ connections could not be created and opened");
                    return false;
                }

            }
            catch { return false; }
        }

        public async Task<bool> CreateConnectionAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    _connection = await _factory.CreateConnectionAsync(_rabbitEndpoints);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to create the rabbitmq connections because {ex.Message}");
            }
            return IsConnected;
        }


        public async Task<bool> CreateChannelAsync()
        {
            try
            {
                if (!IsChannelOpen)
                {
                    _channel = await _connection.CreateChannelAsync();
                }
            }
            catch
            {
                throw new Exception("Unable to create the rabbitmq channel when connections is null.");
            }
            return IsChannelOpen;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed) return;
            try
            {
                await _connection.CloseAsync();
                await _channel.CloseAsync();
                await _connection.DisposeAsync();
                await _channel.DisposeAsync();
            }
            catch (IOException ex)
            {
                _disposed = true;
                _logger.LogCritical(ex.ToString());
            }
            GC.SuppressFinalize(this);
        }

        #region Private Method

        private void InitialRabbitMQSettings()
        {
            _factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                VirtualHost = "/",
                AutomaticRecoveryEnabled = true,
            };
        }

        private void InitialRetryPolicy()
        {
            _retryPolicyAsync = Policy
                .Handle<SocketException>()
                .Or<BrokerUnreachableException>()
                .WaitAndRetryAsync(
                retryCount:3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(3),
                onRetryAsync: (exception, timespan, attempt, context) =>
                {
                    _logger.LogCritical($"[ERROR] Attempt retry {attempt} -> Message:{exception.ToString()}");
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// Event handler for re-connect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
        {
            if (_disposed) return;
            _logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");
            await TryConnectAsync();
        }

        /// <summary>
        /// Event handler for re-connect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        private async Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs reason)
        {
            if (_disposed) return;
            _logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");
            await TryConnectAsync();
        }

        /// <summary>
        /// Event handler for re-connect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed) return;
            _logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");
            await TryConnectAsync();
        }

        #endregion
    }
}