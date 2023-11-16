# Message Testing

This project demonstrates how to test messaging based endpoints. Unlike APIs where you ultimately send a request and expect a response almost immediately, message based testing is a little more complicated. There is no immediate response to assert against, you ultimately need to poll against something such as a database. 

&nbsp;

# Scenario

We have a very simple scenario here. There is an endpoint that handles a command and at the back of this it publishes an event. 

> Typically, in a real system you would handle the command and perform some sort of action like write to a database. However for brevity we don't do anything like that here, we simply have the host who handles the command and publishes an event.

This is essentially a black box test. We feed the system with a valid input and expect some sort of output.

&nbsp;

# The Concept

There are a couple of components that are of interest:
- We have a test that simply sends a command to a queue
- The SUT (subject under test) *should* handle this command and publish an event
- The test subscribes to the event and asserts it is the message we are expecting

> The SUT in this instance is our console app entitled Example.Host. This mimics an endpoint that essentially handles the command. Obviously this would be a real endpoint you would target.

``` mermaid
flowchart TD
    A[Test] --> |Sends Command| B(SUT Handles)
    B --> |Produces| C[Publish Event]
    C --> |Subscription| A
```

&nbsp;

# Competing Consumer Problem

There is a caviat with testing message based systems and that is [Competing Consumers](https://learn.microsoft.com/en-us/azure/architecture/patterns/competing-consumers) where two or more services are *competing* for the same message. For example:
- Typically you would have a service deployed that is listening for `FooBarEventV1`
- Our test is also listening for `FooBarEventV1`

The issue we have here is, intermittent faliures. You'll find these tests pass occassionally because the deployed service may pick this message up first, handle it, complete it resulting in a test failure.

``` mermaid
flowchart TD
    A[Deployed Service] --> |Subscribes| B(FooBarEventV1)
    C[Test] --> |Subscribes| B
```

I've seen multiple approaches to handling this situation such as:

## Test Setup
Have setup within the tests to create a test subscription then tear it down after the tests have executed. I personally do not like this approach as your tests are now also changing infrastructure. It also needs to run as an elevated user in order to do such things.

&nbsp;

## Stop Services
Stop the deployed endpoint temporarily until tests run. This fixes the competing aspect because now only one thing (tests) will be subscribing to the topic thus eliminating the race condition. However, again this may require the test to run as an elevated user and this becomes more troublesome when it comes to cloud resources. Doing such things as stopping an Azure Function, APIs in a Service Fabric cluster becomes a lot more complicated.

&nbsp;

## Provisioning
Provision a test subscription. This is my go to approach as the provisioning of the subscription is done by the pipeline so it doesn't require the test to run as an elevated user. We eliminate the race condition as the message is forwarded to both subscriptions:
- One which the application will handle
- Second which the test will handle

We mitigate the flakiness as copies of the message are sent to both subscriptions (virtual queues).

``` mermaid
flowchart TD
    A[Deployed Service] --> |Subscribes| D
    C[Test] --> |Subscribes| E
    D(Sub1) --> B(Topic)
    E(Sub2) --> B
```
