﻿using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    [TestFixture]
    public class When_subscribing_from_native_pubsub_endpoint_auto : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_deliver_the_message_to_both_subscribers()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface<TestTransport>("A", t => t.BrokerZulu());
                    cfg.AddInterface<TestTransport>("B", t => t.BrokerYankee());

                    cfg.UseStaticRoutingProtocol();
                })
                .WithEndpoint<Publisher>(c =>
                {
                    c.When(x => x.EndpointsStarted, async (s, ctx) =>
                    {
                        //Need to retry sending because there is no reliable way to figure when the router is subscribed
                        while (!ctx.BaseEventDelivered || !ctx.DerivedEventDelivered) 
                        {
                            await s.Publish(new MyDerivedEvent2());
                            await Task.Delay(1000);
                        }
                    });
                })
                .WithEndpoint<BaseEventSubscriber>()
                .WithEndpoint<DerivedEventSubscriber>()
                .Done(c => c.BaseEventDelivered && c.DerivedEventDelivered)
                .Run();

            Assert.IsTrue(result.BaseEventDelivered);
            Assert.IsTrue(result.DerivedEventDelivered);
        }

        class Context : ScenarioContext
        {
            public bool BaseEventDelivered { get; set; }
            public bool DerivedEventDelivered { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.UseTransport<TestTransport>().BrokerZulu();
                });
            }
        }

        class BaseEventSubscriber : EndpointConfigurationBuilder
        {
            public BaseEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerYankee()
                        .Routing();

                    routing.ConnectToRouter("Router", true, false);
                });
            }

            class BaseEventHandler : IHandleMessages<MyBaseEvent2>
            {
                Context scenarioContext;

                public BaseEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyBaseEvent2 message, IMessageHandlerContext context)
                {
                    scenarioContext.BaseEventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class DerivedEventSubscriber : EndpointConfigurationBuilder
        {
            public DerivedEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerYankee()
                        .Routing();

                    routing.ConnectToRouter("Router", true, false);
                });
            }

            class DerivedEventHandler : IHandleMessages<MyDerivedEvent2>
            {
                Context scenarioContext;

                public DerivedEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyDerivedEvent2 message, IMessageHandlerContext context)
                {
                    scenarioContext.DerivedEventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyBaseEvent2 : IEvent
        {
        }

        class MyDerivedEvent2 : MyBaseEvent2
        {
        }
    }
}
