using System.Net;
using MyServiceBus.Serialization;
using MyServiceBus.Transports;

namespace MyServiceBus;

public sealed class HttpReceiveTransport : IReceiveTransport
{
    private readonly HttpListener _listener;
    private readonly string _path;
    private readonly Func<ReceiveContext, Task> _handler;
    private readonly bool _hasErrorEndpoint;
    private readonly Func<string?, bool>? _isMessageTypeRegistered;
    private readonly MessageContextFactory _contextFactory = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public HttpReceiveTransport(HttpListener listener, string path, Func<ReceiveContext, Task> handler, bool hasErrorEndpoint,
        Func<string?, bool>? isMessageTypeRegistered)
    {
        _listener = listener;
        _path = path;
        _handler = handler;
        _hasErrorEndpoint = hasErrorEndpoint;
        _isMessageTypeRegistered = isMessageTypeRegistered;
    }

    [Throws(typeof(HttpListenerException))]
    public Task Start(CancellationToken cancellationToken = default)
    {
        _listener.Start();
        _loop = Task.Run(ProcessLoop, _cts.Token);
        return Task.CompletedTask;
    }

    [Throws(typeof(AggregateException))]
    public async Task Stop(CancellationToken cancellationToken = default)
    {
        _cts.Cancel();
        _listener.Stop();
        if (_loop != null)
            await _loop;
    }

    private async Task ProcessLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequest(ctx!), _cts.Token);
        }
    }

    [Throws(typeof(ProtocolViolationException))]
    private async Task HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.HttpMethod != "POST")
            {
                ctx.Response.StatusCode = 405;
                return;
            }

            var requested = ctx.Request.Url!.AbsolutePath.Trim('/');
            if (!(requested.Equals(_path, StringComparison.OrdinalIgnoreCase) ||
                  requested.Equals(_path + "_error", StringComparison.OrdinalIgnoreCase) ||
                  requested.Equals(_path + "_fault", StringComparison.OrdinalIgnoreCase)))
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            using var ms = new MemoryStream();
            await ctx.Request.InputStream.CopyToAsync(ms);
            var payload = ms.ToArray();

            var headers = new Dictionary<string, object>();
            foreach (var key in ctx.Request.Headers.AllKeys)
            {
                if (key != null)
                    headers[key] = ctx.Request.Headers[key]!;
            }

            var baseUri = ctx.Request.Url.GetLeftPart(UriPartial.Authority);
            var isFaultEndpoint = requested.EndsWith("_fault", StringComparison.OrdinalIgnoreCase);
            var isErrorEndpoint = requested.EndsWith("_error", StringComparison.OrdinalIgnoreCase);
            Uri? errorAddress = null;
            if (_hasErrorEndpoint && !isFaultEndpoint && !isErrorEndpoint)
            {
                errorAddress = new Uri($"{baseUri}/{_path}_error");
                if (!headers.ContainsKey(MessageHeaders.FaultAddress))
                    headers[MessageHeaders.FaultAddress] = $"{baseUri}/{_path}_fault";
            }

            var transportMessage = new HttpTransportMessage(headers, payload);
            var msgContext = _contextFactory.CreateMessageContext(transportMessage);
            var receiveContext = new ReceiveContextImpl(msgContext, errorAddress);

            await _handler(receiveContext);
            ctx.Response.StatusCode = 200;
        }
        catch
        {
            ctx.Response.StatusCode = 500;
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    private class HttpTransportMessage : ITransportMessage
    {
        public HttpTransportMessage(IDictionary<string, object> headers, byte[] payload)
        {
            Headers = new Dictionary<string, object>(headers);
            Payload = payload;
        }

        public IDictionary<string, object> Headers { get; }
        public bool IsDurable => true;
        public byte[] Payload { get; }
    }
}
