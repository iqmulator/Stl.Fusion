using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Owin;
using Stl.Channels;
using Stl.Fusion.Bridge;
using Stl.Fusion.Bridge.Messages;
using Stl.Net;
using Stl.Serialization;

using WebSocketAccept = System.Action<
    System.Collections.Generic.IDictionary<string, object>, // WebSocket Accept parameters
    System.Func< // WebSocketFunc callback
        System.Collections.Generic.IDictionary<string, object>, // WebSocket environment
        System.Threading.Tasks.Task>>;

namespace Stl.Fusion.Server
{
    public class WebSocketServer
    {
        public class Options
        {
            public string RequestPath { get; set; } = "/fusion/ws";
            public string PublisherIdQueryParameterName { get; set; } = "publisherId";
            public string ClientIdQueryParameterName { get; set; } = "clientId";
            public Func<IUtf16Serializer<BridgeMessage>> SerializerFactory { get; set; } =
                DefaultSerializerFactory;

            public static IUtf16Serializer<BridgeMessage> DefaultSerializerFactory()
                => new Utf16Serializer(
                    new TypeDecoratingSerializer(
                        SystemJsonSerializer.Default,
                        t => typeof(ReplicatorRequest).IsAssignableFrom(t)).Reader,
                    new TypeDecoratingSerializer(
                        SystemJsonSerializer.Default,
                        t => typeof(PublisherReply).IsAssignableFrom(t)).Writer
                ).ToTyped<BridgeMessage>();
        }

        protected IPublisher Publisher { get; }
        protected Func<IUtf16Serializer<BridgeMessage>> SerializerFactory { get; }
        protected ILogger Log { get; }

        public string RequestPath { get; }
        public string PublisherIdQueryParameterName { get; }
        public string ClientIdQueryParameterName { get; }

        public WebSocketServer(Options? options, IPublisher publisher, ILogger<WebSocketServer>? log = null)
        {
            options ??= new();
            Log = log ?? NullLogger<WebSocketServer>.Instance;
            RequestPath = options.RequestPath;
            PublisherIdQueryParameterName = options.PublisherIdQueryParameterName;
            ClientIdQueryParameterName = options.ClientIdQueryParameterName;
            Publisher = publisher;
            SerializerFactory = options.SerializerFactory;
        }

        public HttpStatusCode HandleRequest(IOwinContext owinContext)
        {
            // written based on https://stackoverflow.com/questions/41848095/websockets-using-owin

            var acceptToken = owinContext.Get<WebSocketAccept>("websocket.Accept");
            if (acceptToken == null)
                return HttpStatusCode.BadRequest;

            var publisherId = owinContext.Request.Query[PublisherIdQueryParameterName];
            if (Publisher.Id != publisherId)
                return HttpStatusCode.BadRequest;

            var clientId = owinContext.Request.Query[ClientIdQueryParameterName];

            var requestHeaders =
                GetValue<IDictionary<string, string[]>>(owinContext.Environment, "owin.RequestHeaders")
                ?? ImmutableDictionary<string, string[]>.Empty;

            var acceptOptions = new Dictionary<string, object>();
            if (requestHeaders.TryGetValue("Sec-WebSocket-Protocol", out string[]? subProtocols) && subProtocols.Length > 0) {
                // Select the first one from the client
                acceptOptions.Add("websocket.SubProtocol", subProtocols[0].Split(',').First().Trim());
            }

            acceptToken(acceptOptions, wsEnv => {
                var wsContext = (WebSocketContext)wsEnv["System.Net.WebSockets.WebSocketContext"];
                return HandleWebSocket(wsContext, clientId);
            });

            return HttpStatusCode.SwitchingProtocols;
        }

        private async Task HandleWebSocket(WebSocketContext wsContext, string clientId)
        {
            var serializers = SerializerFactory.Invoke();
            var webSocket = wsContext.WebSocket;
            await using var wsChannel = new WebSocketChannel(webSocket);
            var channel = wsChannel
                .WithUtf16Serializer(serializers)
                .WithId(clientId);
            Publisher.ChannelHub.Attach(channel);
            try {
                await wsChannel.WhenCompleted().ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception e) {
                Log.LogWarning(e, "WebSocket connection was closed with an error");
            }
        }

        private static T? GetValue<T>(IDictionary<string, object?> env, string key)
            => env.TryGetValue(key, out var value) && value is T result ? result : default;
    }
}
