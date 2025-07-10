using ConsoleRabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // ✅ Setup DI
        var services = new ServiceCollection();
        services.AddLogging(config =>
        {
            config.AddConsole();
            config.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IRabbitMQConnection, RabbitMQConnection>();
        services.AddSingleton<IConnectionFactory>(sp =>
        {
            return new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
            };
        });
        services.AddSingleton<IEventMessageManager, EventMessageManager>();

        var provider = services.BuildServiceProvider();

        var rabbit = provider.GetRequiredService<IRabbitMQConnection>();
        var publisher = provider.GetRequiredService<IEventMessageManager>();

        Console.WriteLine("🔌 Connecting to RabbitMQ...");
        await rabbit.TryConnectAsync();
        await rabbit.CreateChannelAsync();

        var channel = rabbit.Channel;

        Console.WriteLine("🔥 Declaring + Publishing queues...");

        for (int i = 0; i < 100; i++)
        {
            try
            {
                Console.WriteLine($"🌀 Round {i + 1}");

                var tasks = new Task[10];

                for (int j = 0; j < 10; j++)
                {
                    string queueName = $"q-race-{i}-{j}";
                    string message = $"Hello from {queueName}";

                    tasks[j] = Task.Run(async () =>
                    {
                        // ✅ Step 1: Declare queue
                        await channel.QueueDeclareAsync(queue: queueName,
                            durable: true,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);

                        // ✅ Step 2: Publish message using service
                        await publisher.PublishAsync(channel, queueName, message);
                    });
                }

                await Task.WhenAll(tasks);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 ERROR: {ex.GetType().Name}");
                Console.WriteLine(ex.Message);
                break;
            }
        }

        await rabbit.DisposeAsync();
        Console.WriteLine("👋 Done");
    }
}
