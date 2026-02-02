using RabbitMQ.Client;
using System.Text;

namespace AspNetCore.Test
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //Console.WriteLine("Hello, World!");

            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            /*
             * Simple Queue Productor

            await channel.QueueDeclareAsync(queue: "hello", durable: false, exclusive: false, autoDelete: false, arguments: null);

            const string message = "Hello World!";
            var body = Encoding.UTF8.GetBytes(message);

            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "hello", body: body);
            Console.WriteLine($" [x] Sent {message}");
            */

            /*
             * Work Queue
             * 
            await channel.QueueDeclareAsync(queue: "task_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
            for (int i = 0; i < 5; i++)
            {
                var message = GetMessage(i);
                var body = Encoding.UTF8.GetBytes(message);

                var properties = new BasicProperties
                {
                    Persistent = true,
                };

                await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "task_queue", mandatory: true, basicProperties: properties, body: body);
                Console.WriteLine($" [x] Sent {message}");
            }
            */
            //消费者要优于创建，否则推送的消息接收不到
            await Task.Delay(1000);

            await channel.ExchangeDeclareAsync(exchange: "logs", type: ExchangeType.Fanout);

            for (int i = 0; i < 5; i++)
            {
                var message = GetMessage(i);
                var body = Encoding.UTF8.GetBytes(message);
                await channel.BasicPublishAsync(exchange: "logs", routingKey: string.Empty, body: body);
                Console.WriteLine($" [x] Sent {message}");
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        static string GetMessage(int i)
        {
            // 根据索引生成包含不同数量点号的消息，模拟不同处理时间
            int dots = i % 3 + 1; // 1-3个点
            return "Hello World!" + i + new string('.', dots);
        }
    }
}