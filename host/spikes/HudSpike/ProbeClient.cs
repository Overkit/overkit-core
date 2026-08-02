using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HudSpike;

/// <summary>
/// Client WebSocket de la Sonde : connexion à ws://127.0.0.1:47800,
/// reconnexion automatique avec backoff (1 s → 30 s, esprit EXG-011),
/// dernier état exposé pour lecture par le timer UI.
/// </summary>
public sealed class ProbeClient : IDisposable
{
    private const string ProbeUri = "ws://127.0.0.1:47800";

    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();

    private string _connection = "connexion…";
    private string _playerStatus = "-";
    private double _x, _y, _z;
    private string _probeVersion = "?";
    private string _timeStatus = "-";
    private long _day, _hour, _minute;

    public ProbeClient()
    {
        _ = Task.Run(RunAsync);
    }

    public (string Connection, string PlayerStatus, double X, double Y, double Z, string ProbeVersion,
            string TimeStatus, long Day, long Hour, long Minute) Read()
    {
        lock (_lock)
        {
            return (_connection, _playerStatus, _x, _y, _z, _probeVersion, _timeStatus, _day, _hour, _minute);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task RunAsync()
    {
        var backoffSeconds = 1;
        var buffer = new byte[16 * 1024];

        while (!_cts.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(new Uri(ProbeUri), _cts.Token);
                backoffSeconds = 1;
                SetConnection("live");

                while (ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    HandleMessage(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Sonde absente ou tombée : on bascule en mode dégradé et on retente.
            }

            SetConnection($"statique — reconnexion dans {backoffSeconds} s");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            backoffSeconds = Math.Min(backoffSeconds * 2, 30);
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            if (type == "handshake")
            {
                var version = root.GetProperty("probe_version").GetString() ?? "?";
                lock (_lock)
                {
                    _probeVersion = version;
                }
                return;
            }

            if (type == "state")
            {
                var player = root.GetProperty("player");
                var status = player.GetProperty("status").GetString() ?? "-";
                var time = root.GetProperty("world").GetProperty("time");
                var timeStatus = time.GetProperty("status").GetString() ?? "-";
                lock (_lock)
                {
                    _playerStatus = status;
                    if (status == "ok")
                    {
                        _x = player.GetProperty("x").GetDouble();
                        _y = player.GetProperty("y").GetDouble();
                        _z = player.GetProperty("z").GetDouble();
                    }
                    _timeStatus = timeStatus;
                    if (timeStatus == "ok")
                    {
                        _day = time.GetProperty("day").GetInt64();
                        _hour = time.GetProperty("hour").GetInt64();
                        _minute = time.GetProperty("minute").GetInt64();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Message inattendu : ignoré, le flux continue.
        }
    }

    private void SetConnection(string value)
    {
        lock (_lock)
        {
            _connection = value;
        }
    }
}
