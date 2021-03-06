﻿using Bittrex.Net.Interfaces;
using Bittrex.Net.Objects;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Hubs;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Bittrex.Net.UnitTests.Core
{
    public class BittrexSocketClientTests
    {
        Subscription sub;
        Mock<Interfaces.IHubConnection> socket;
        Mock<ICloudFlareAuthenticator> cloud;
        Mock<IHubProxy> proxy;

        [TestCase()]
        public void SubscribingToMarketDeltaStream_Should_TriggerWhenDeltaMessageIsReceived()
        {
            // arrange
            var client = PrepareClient();

            BittrexMarketSummary result = null;
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket", (test) => result = test);

            var expected = new BittrexStreamDeltas()
            {
                Deltas = new List<BittrexMarketSummary>()
                {
                    new BittrexMarketSummary()
                    {
                        Ask = 1.1m,
                        BaseVolume = 2.2m,
                        Bid = 3.3m,
                        Created = new DateTime(2017, 1, 1),
                        DisplayMarketName = null,
                        High = 4.4m,
                        Last = 5.5m,
                        Low = 6.6m,
                        MarketName = "TestMarket",
                        OpenBuyOrders = 10,
                        OpenSellOrders = 20,
                        PrevDay = 7.7m,
                        TimeStamp = new DateTime(2016, 1, 1),
                        Volume = 8.8m
                    }
                }
            };

            // act
            TriggerSub(sub, expected);

            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsTrue(ObjectComparer.PublicInstancePropertiesEqual(result, expected.Deltas[0]));
        }

        [TestCase()]
        public void SubscribingToAllMarketDeltaStream_Should_TriggerWhenAnyDeltaMessageIsReceived()
        {
            // arrange
            var client = PrepareClient();

            int calls = 0;
            var subscription = client.SubscribeToAllMarketDeltaStream(data => calls++);

            var expected = new BittrexStreamDeltas()
            {
                Deltas = new List<BittrexMarketSummary>()
                {
                    new BittrexMarketSummary()
                    {
                        Ask = 1.1m,
                        BaseVolume = 2.2m,
                        Bid = 3.3m,
                        Created = new DateTime(2017, 1, 1),
                        DisplayMarketName = null,
                        High = 4.4m,
                        Last = 5.5m,
                        Low = 6.6m,
                        MarketName = "TestMarket",
                        OpenBuyOrders = 10,
                        OpenSellOrders = 20,
                        PrevDay = 7.7m,
                        TimeStamp = new DateTime(2016, 1, 1),
                        Volume = 8.8m
                    }
                }
            };

            var expected2 = new BittrexStreamDeltas()
            {
                Deltas = new List<BittrexMarketSummary>()
                {
                    new BittrexMarketSummary()
                    {
                        Ask = 1.1m,
                        BaseVolume = 2.2m,
                        Bid = 3.3m,
                        Created = new DateTime(2017, 1, 1),
                        DisplayMarketName = null,
                        High = 4.4m,
                        Last = 5.5m,
                        Low = 6.6m,
                        MarketName = "TestMarket2",
                        OpenBuyOrders = 10,
                        OpenSellOrders = 20,
                        PrevDay = 7.7m,
                        TimeStamp = new DateTime(2016, 1, 1),
                        Volume = 8.8m
                    }
                }
            };

            // act
            TriggerSub(sub, expected);
            TriggerSub(sub, expected2);

            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsTrue(calls == 2);
        }

        [TestCase()]
        public void SubscribingToMarketDeltaStream_Should_NotTriggerWhenDeltaMessageForOtherMarketIsReceived()
        {
            // arrange
            var client = PrepareClient();

            BittrexMarketSummary result = null;
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", (test) => result = test);

            var expected = new BittrexStreamDeltas()
            {
                Deltas = new List<BittrexMarketSummary>()
                {
                    new BittrexMarketSummary()
                    {
                        Ask = 1.1m,
                        BaseVolume = 2.2m,
                        Bid = 3.3m,
                        Created = new DateTime(2017, 1, 1),
                        DisplayMarketName = null,
                        High = 4.4m,
                        Last = 5.5m,
                        Low = 6.6m,
                        MarketName = "TestMarket2",
                        OpenBuyOrders = 10,
                        OpenSellOrders = 20,
                        PrevDay = 7.7m,
                        TimeStamp = new DateTime(2016, 1, 1),
                        Volume = 8.8m
                    }
                }
            };

            // act
            TriggerSub(sub, expected);

            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsNull(result);
        }

        [TestCase()]
        public void Subscribing_Should_InvokeSubscribeDeltas()
        {
            // arrange
            var client = PrepareClient();

            // act
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", (test) => { });

            // assert
            proxy.Verify(p => p.Invoke("SubscribeToSummaryDeltas"), Times.Once);
        }

        [TestCase()]
        public void WhenTheSocketReturnsEmptyDataTheEvent_Should_NotTrigger()
        {
            // arrange
            var client = PrepareClient();

            BittrexMarketSummary result = null;
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", (test) => result = test);

            var expected = "";

            // act
            TriggerSub(sub, expected);

            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsNull(result);
        }

        [TestCase()]
        public void WhenUnsubscribingTheLastSubscriptionTheSocket_Should_Close()
        {
            // arrange
            bool stopCalled = false;
            var client = PrepareClient();
            socket.Setup(s => s.Stop(It.IsAny<TimeSpan>())).Callback(() => stopCalled = true);
            
            BittrexMarketSummary result = null;
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", (test) => result = test);
            
            // act
            client.UnsubscribeFromStream(subscription.Result);
            Thread.Sleep(10);
            
            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsTrue(stopCalled);
        }

        [TestCase()]
        public void WhenUnsubscribingNotTheLastSubscriptionTheSocket_Should_NotClose()
        {
            // arrange
            bool stopCalled = false;
            var client = PrepareClient();
            socket.Setup(s => s.Stop(It.IsAny<TimeSpan>())).Callback(() => stopCalled = true);
            
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", null);
            var subscription2 = client.SubscribeToMarketDeltaStream("TestMarket2", null);

            // act
            client.UnsubscribeFromStream(subscription.Result);

            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsTrue(subscription2.Success);
            Assert.IsFalse(stopCalled);
        }

        [TestCase()]
        public void WhenUnsubscribingAllTheSocket_Should_Close()
        {
            // arrange
            bool stopCalled = false;
            var client = PrepareClient();
            socket.Setup(s => s.Stop(It.IsAny<TimeSpan>())).Callback(() => stopCalled = true);
            
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", null);
            var subscription2 = client.SubscribeToMarketDeltaStream("TestMarket2", null);

            // act
            client.UnsubscribeAllStreams();
            Thread.Sleep(10);

            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsTrue(subscription2.Success);
            Assert.IsTrue(stopCalled);
        }

        [TestCase()]
        public void WhenConnectionFailsCloudFlareBypass_Should_BeTried()
        {
            // arrange
            bool cloudFlareCalled = false;
            var client = PrepareClient();
            socket.Setup(s => s.State).Returns(ConnectionState.Disconnected);
            socket.Setup(s => s.Start()).Callback(() => { socket.Raise(s => s.StateChanged += null, new StateChange(ConnectionState.Connecting, ConnectionState.Disconnected)); });

            cloud.Setup(c => c.GetCloudFlareCookies(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Returns(new CookieContainer()).Callback(() =>
            {
                cloudFlareCalled = true;
                socket.Setup(s => s.State).Returns(ConnectionState.Connected);
                socket.Setup(s => s.Start()).Callback(() => { socket.Raise(s => s.StateChanged += null, new StateChange(ConnectionState.Connecting, ConnectionState.Connected)); });
            });

            BittrexMarketSummary result = null;

            // act
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", (test) => result = test);

            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsTrue(cloudFlareCalled);
        }

        [TestCase()]
        public void WhenCloudFlareFailsSubscription_Should_Fail()
        {
            // arrange
            var client = PrepareClient();
            socket.Setup(s => s.State).Returns(ConnectionState.Disconnected);
            cloud.Setup(c => c.GetCloudFlareCookies(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Returns<CookieContainer>(null);
            socket.Setup(s => s.Start()).Callback(() => { socket.Raise(s => s.StateChanged += null, new StateChange(ConnectionState.Connecting, ConnectionState.Disconnected)); });

            BittrexMarketSummary result = null;

            // act
            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", (test) => result = test);

            // assert
            Assert.IsFalse(subscription.Success);
        }

        [TestCase()]
        public void Dispose_Should_ClearSubscriptions()
        {
            // arrange
            bool stopCalled = false;
            var client = PrepareClient();
            socket.Setup(s => s.Stop(It.IsAny<TimeSpan>())).Callback(() => stopCalled = true);

            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", null);
            var subscription2 = client.SubscribeToMarketDeltaStream("TestMarket2", null);
            
            // act
            client.Dispose();
            Thread.Sleep(100);

            // assert 
            Assert.IsTrue(subscription.Success);
            Assert.IsTrue(subscription2.Success);
            Assert.IsTrue(stopCalled);
        }

        [TestCase()]
        public void WhenSocketDisconnectsWithSubscriptions_Should_Reconnect()
        {
            // arrange
            bool reconnectDone = false;
            bool closeEventDone = false;
            
            var client = PrepareClient();
            socket.Setup(s => s.State).Returns(ConnectionState.Connected);
            socket.Setup(s => s.Start()).Callback(() =>
            {
                socket.Raise(s => s.StateChanged += null, new StateChange(ConnectionState.Connecting, ConnectionState.Connected));
                if (!closeEventDone)
                {
                    closeEventDone = true;
                    Task.Run(() =>
                    {
                        Thread.Sleep(2000);
                        socket.Setup(s => s.Start()).Callback(() =>
                        {
                            socket.Raise(s => s.StateChanged += null, new StateChange(ConnectionState.Connecting, ConnectionState.Connected));
                            reconnectDone = true;
                        });
                        socket.Raise(s => s.Closed += null);
                    });
                }
            });

            var subscription = client.SubscribeToMarketDeltaStream("TestMarket1", null);

            // act
            Thread.Sleep(3000);

            // assert
            Assert.IsTrue(subscription.Success);
            Assert.IsTrue(reconnectDone);
        }

        private BittrexSocketClient PrepareClient()
        {
            sub = new Subscription();

            proxy = new Mock<IHubProxy>();
            proxy.Setup(r => r.Subscribe(It.IsAny<string>())).Returns(sub);

            socket = new Mock<Interfaces.IHubConnection>();
            socket.Setup(s => s.Stop(It.IsAny<TimeSpan>()));
            socket.Setup(s => s.Start()).Callback(() => { socket.Raise(s => s.StateChanged += null, new StateChange(ConnectionState.Connecting, ConnectionState.Connected)); });
            socket.Setup(s => s.State).Returns(ConnectionState.Connected);
            socket.Setup(s => s.CreateHubProxy(It.IsAny<string>())).Returns(proxy.Object);

            var factory = new Mock<IConnectionFactory>();
            factory.Setup(s => s.Create(It.IsAny<string>())).Returns(socket.Object);

            cloud = new Mock<ICloudFlareAuthenticator>();
            cloud.Setup(c => c.GetCloudFlareCookies(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Returns(new CookieContainer());
            
            var client = new BittrexSocketClient { ConnectionFactory = factory.Object, CloudFlareAuthenticator = cloud.Object };
            client.GetType().GetField("connection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).SetValue(client, null);
            client.GetType().GetField("registrations", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).SetValue(client, new List<BittrexRegistration>());
            client.GetType().GetField("reconnecting", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).SetValue(client, false);

            return client;
        }

        private void TriggerSub(Subscription sub, object expected)
        {
            var ex = JToken.FromObject(expected);
            var field = sub.GetType().GetField("Received", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            var eventDelegate = (MulticastDelegate)field.GetValue(sub);

            foreach (var handler in eventDelegate.GetInvocationList())
                handler.Method.Invoke(handler.Target, new object[] { new List<JToken>() { ex } });
        }
    }
}
