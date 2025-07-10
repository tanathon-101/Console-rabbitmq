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

        // 🧩 Add dependencies
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

        // ✅ ส่งข้อความแบบ string
        Console.WriteLine("🚀 Sending message to queue...");
        bool result = await publisher.PublishAsync(channel, "demo-queue", "Hello RabbitMQ!");

        Console.WriteLine(result ? "✅ Publish success" : "❌ Publish failed");

        // ❗ ทดสอบการ pipeline (optional)
        Console.WriteLine("🔥 Declaring 2 queues in parallel...");
        var t1 = channel.QueueDeclareAsync("test-pipeline-1", false, false, false);
        await Task.Delay(1); // ⚠️ จงใจให้ t2 เริ่มก่อน t1 ตอบ
        var t2 = channel.QueueDeclareAsync("test-pipeline-2", false, false, false);

        try
        {
            await Task.WhenAll(t1, t2);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 ERROR: {ex.GetType().Name}");
            Console.WriteLine(ex.Message);
        }

        await rabbit.DisposeAsync();
        Console.WriteLine("👋 Done");
    }
}
