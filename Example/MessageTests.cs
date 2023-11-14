using Azure.Messaging.ServiceBus;
using Example.Commands;
using Example.Events;
using FluentAssertions;
using Newtonsoft.Json;

namespace Example;

public class MessageTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task When_Command_Is_Sent_Expect_Event_To_Be_Published()
    {
        const string connectionString = "YOUR_CONNECTION_STRING_TO_ASB";
        const string queue = "foo-queue";
        const string topic = "foo-topic";
        const string subscription = "foo-topic-test";
        
        var command = new FooBarCommand
        {
            Id = Guid.NewGuid().ToString(),
            Message = "hello world"
        };

        var result = await new MessageAssertionHelper(connectionString)
            .WithMessage(command)
            .SendTo(queue)
            .ShouldReceive<FooBarEvent>(topic, subscription, @event => @event.Message == command.Message);
        
        result!.Message.Should().Be(command.Message);
    }
}

public class MessageAssertionHelper
{
    private readonly ServiceBusClient _client;
    private ServiceBusMessage _message = null!;
    private string _destinationQueue = null!;

    public MessageAssertionHelper(string connectionString)
    {
        _client = new ServiceBusClient(connectionString);
    }

    public MessageAssertionHelper WithMessage<T>(T command)
    {
        _message = new ServiceBusMessage(JsonConvert.SerializeObject(command));
        return this;
    }

    public MessageAssertionHelper SendTo(string destinationQueueName)
    {
        _destinationQueue = destinationQueueName;
        // var sender = _client.CreateSender(destinationQueueName);
        // await sender.SendMessageAsync(_message);
        return this;
    }

    public async Task<T?> ShouldReceive<T>(
        string topic, 
        string subscription, 
        Func<T, bool> matchingPredicate)
    {
        var processor = _client.CreateProcessor(topic, subscription);
        var result = default(T);
        
        processor.ProcessMessageAsync += async args =>
        {
            var msg = JsonConvert.DeserializeObject<T>(args.Message.Body.ToString());
            
            if (matchingPredicate(msg))
            {
                result = msg;
                await args.CompleteMessageAsync(args.Message);
                await processor.StopProcessingAsync();
            }
        };
        
        processor.ProcessErrorAsync += args =>
        {
            throw new Exception($"Message assertion failed {args.Exception}.");
        };
        
        await processor.StartProcessingAsync();
        
        while (result is null) // TODO: timeout control
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        
        return result;
    }
}