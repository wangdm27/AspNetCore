using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading.Tasks;

namespace AspNetCore.Test2
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
             * Simple Queue Consumer
             * 
            await channel.QueueDeclareAsync(queue: "hello", durable: false, exclusive: false, autoDelete: false, arguments: null);

            Console.WriteLine(" [*] Waiting for message.");

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received {message}");
                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync("hello", autoAck: true, consumer: consumer);
            */

            await channel.QueueDeclareAsync(queue: "task_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            Console.WriteLine(" [*] Waiting for messages.");

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received {message}");

                int dots = message.Split('.').Length - 1;
                await Task.Delay(dots * 1000);

                Console.WriteLine(" [x] Done");

                // here channel could also be accessed as ((AsyncEventingBasicConsumer)sender).Channel
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync("task_queue", autoAck: false, consumer: consumer);


            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}
