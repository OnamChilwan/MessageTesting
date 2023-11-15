// See https://aka.ms/new-console-template for more information

using Azure.Messaging.ServiceBus;
using Example.Host.Commands;
using Example.Host.Events;
using Newtonsoft.Json;

Console.WriteLine("Started..");

const string connectionString = "YOUR_CONNECTION_STRING";
var client = new ServiceBusClient(connectionString);

var processor = client.CreateProcessor("foo-queue");
processor.ProcessMessageAsync += async args =>
{
    var message = JsonConvert.DeserializeObject<FooBarCommand>(args.Message.Body.ToString());
    await args.CompleteMessageAsync(args.Message);
    
    Console.WriteLine($"Processing ID {message.Id} with content {message.Message}");

    var sender = client.CreateSender("foo-topic");
    var @event = new FooBarEvent
    {
        Message = message.Message
    };
    
    await sender.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(@event)));
};

processor.ProcessErrorAsync += eventArgs => Task.CompletedTask;

await processor.StartProcessingAsync();

Console.WriteLine("Press to end..");
Console.Read();