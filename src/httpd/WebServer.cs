using System.Net;
using System.Net.Mime;
using System.Text.RegularExpressions;
using livemap.util;
using MimeTypes;

namespace livemap.httpd;

public partial class WebServer(LiveMap server)
{
    [GeneratedRegex(@"^(.*\/)?(.+)\/([+-]?\d+)\/([+-]?\d+)\/([+-]?\d+)(\/.*)?")]
    private static partial Regex FriendlyUrlRegex();

    private HttpListener? _listener;
    private Thread? _thread;
    private bool _running;
    private bool _stopped;
    private bool _reload;

    public void Reload()
    {
        _reload = true;
        Dispose();
        _stopped = false;
    }

    public void Run()
    {
        if (!server.Config.Httpd.Enabled)
        {
            return;
        }

        if (!_stopped && !_running)
        {
            RunAsyncInfiniteLoop();
        }
    }

    private void RunAsyncInfiniteLoop()
    {
        _running = true;

        (_thread = new Thread(_ =>
        {
            int port = server.Config.Httpd.Port;

            if (_listener == null)
            {
                Logger.Info($"Internal webserver starting on port {port}");
            }

                string[] prefixes = { $"http://*:{port}/", $"http://+:{port}/", $"http://0.0.0.0:{port}/" };
                bool bound = false;

                foreach (var prefix in prefixes)
                {
                    try
                    {
                        Logger.Info($"Attempting to bind to {prefix}");
                        (_listener = new HttpListener { Prefixes = { prefix } }).Start();
                        bound = true;
                        
                        Logger.Info($"Internal webserver successfully started on {prefix}");
                        if (prefix.Contains("*") || prefix.Contains("+") || prefix.Contains("0.0.0.0"))
                        {
                            try 
                            {
                                var host = Dns.GetHostEntry(Dns.GetHostName());
                                foreach (var ip in host.AddressList)
                                {
                                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                    {
                                        Logger.Info($"You should be able to access the map at: http://{ip}:{port}/");
                                    }
                                }
                            }
                            catch 
                            {
                                // ignore DNS errors
                            }
                        }
                        break;
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Failed to bind to {prefix}: {e.Message}");
                    }
                }

                if (!bound)
                {
                    Logger.Warn($"Internal webserver failed to bind to all interfaces on port {port}. Falling back to localhost.");
                    Logger.Warn("If you want to access the map from other devices, please check the wiki: https://vs.pl3x.net/livemap-access-denied");

                    try
                    {
                        (_listener = new HttpListener { Prefixes = { $"http://localhost:{port}/" } }).Start();
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Internal webserver has failed to start");
                        if (e is HttpListenerException { ErrorCode: 5 })
                        {
                            Logger.Error("Check LiveMap Wiki for possible fixes: https://vs.pl3x.net/livemap-access-denied");
                        }

                        Logger.Error(e.ToString());
                        _running = false;
                        _stopped = true;
                        Thread.CurrentThread.Interrupt();
                        return;
                    }
                }

            while (_running)
            {
                try
                {
                    HandleRequest(_listener!.GetContext());
                }
                catch (Exception)
                {
                    if (_stopped)
                    {
                        Logger.Info("Internal webserver has stopped");
                        if (_reload)
                        {
                            _reload = false;
                            _stopped = false;
                        }
                    }

                    try
                    {
                        _listener?.Stop();
                    }
                    catch (Exception)
                    {
                        // ignore
                    }

                    _running = false;
                    Thread.CurrentThread.Interrupt();
                }
            }
        })).Start();
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        string urlLoc = context.Request.Url?.LocalPath[1..] ?? "";

        try
        {
            // friendly urls
            MatchCollection matches = FriendlyUrlRegex().Matches(urlLoc);
            if (matches.Count > 0)
            {
                string group6 = matches[0].Groups[6].Value;
                if (group6.Length == 0 && !matches[0].Value.EndsWith('/'))
                {
                    context.Response.Redirect($"{context.Request.Url?.OriginalString}/");
                    context.Response.Close();
                    return;
                }

                urlLoc = group6[1..];
            }
        }
        catch (Exception)
        {
            // ignore
        }

        if (string.IsNullOrEmpty(urlLoc))
        {
            urlLoc = "index.html";
        }

        using HttpListenerResponse response = context.Response;
        string filePath = Path.Combine(Files.WebDir, urlLoc);

        byte[] buffer;
        if (File.Exists(filePath))
        {
            response.ContentType = MimeTypeMap.GetMimeType(new FileInfo(filePath).Extension) ?? MediaTypeNames.Text.Plain;
            buffer = File.ReadAllBytes(filePath);
            response.StatusCode = 200;
        }
        else
        {
            response.ContentType = MediaTypeNames.Text.Html;
            buffer = File.ReadAllBytes(Path.Combine(Files.WebDir, "404.html"));
            response.StatusCode = 404;
        }

        response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
        response.AddHeader("Access-Control-Allow-Methods", "GET,POST");
        response.AddHeader("Access-Control-Allow-Origin", "*");

        try
        {
            TimeSpan time = File.GetLastWriteTimeUtc(filePath) - DateTime.UnixEpoch;
            response.AddHeader("ETag", ((long)time.TotalMilliseconds).ToString());
        }
        catch (Exception)
        {
            // ignore
        }

        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    public void Dispose()
    {
        _stopped = true;

        try
        {
            _listener?.Stop();
        }
        catch (ObjectDisposedException)
        {
        }

        _listener = null;

        _thread?.Interrupt();
        _thread = null;
    }
}
