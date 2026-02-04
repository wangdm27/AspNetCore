using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace AspNetCore.Test
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //Console.WriteLine("Hello, World!");
            // 如果没有提供命令行参数，则使用默认值
            if (args.Length == 0)
            {
                args = new string[] { "kern.critical", "A critical kernel error" };
            }

            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();
            //消费者要优于创建，否则推送的消息接收不到
            await Task.Delay(1000);

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

            /*
             * Fanout  Publisk/Subscribe
             * 

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

            */

            /*
             * Direct
             * 
            await channel.ExchangeDeclareAsync(exchange: "direct_logs", type: ExchangeType.Direct);

            var serverity = (args.Length > 0) ? args[0] : "info";
            var message = (args.Length > 1) ? string.Join(", ", args.Skip(1).ToArray()) : "Hello World!";
            var body = Encoding.UTF8.GetBytes(message);
            await channel.BasicPublishAsync(exchange: "direct_logs", routingKey: serverity, body: body);
            Console.WriteLine($" [x] Sent '{serverity}' : {message}");
            */

            await channel.ExchangeDeclareAsync(exchange: "topic_logs", type: ExchangeType.Topic);

            var routingKey = (args.Length > 0) ? args[0] : "anonymous.info";
            var message = (args.Length >= 1) ? string.Join(" ", args.Skip(1).ToArray()) : "Hello World!";
            var body = Encoding.UTF8.GetBytes(message);
            await channel.BasicPublishAsync(exchange: "topic_logs", routingKey: routingKey, body: body);
            Console.WriteLine($" [x] Sent '{routingKey}' : {message}");

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