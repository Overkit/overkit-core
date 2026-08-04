using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Overkit.Contracts;
using Overkit.Host.Core;

namespace Overkit.Host.Probe;

/// <summary>
/// Connexion à la Sonde : WebSocket local, reconnexion automatique avec
/// backoff 1 s → 30 s (EXG-011), transitions live↔statique sans redémarrage.
/// Les messages sont désérialisés vers les types générés du State Bus ; tout
/// message invalide est ignoré (journalisé), jamais fatal.
/// </summary>
public sealed class ProbeConnection : IDisposable
{
    private readonly Uri _uri;
    private readonly StateBus _bus;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _cts = new();
    // Le convertisseur global d'enums couvre les valeurs de dictionnaires
    // (ex. collectors : map nom -> FieldStatus), que les attributs générés
    // par propriété ne couvrent pas.
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public ProbeConnection(Uri uri, StateBus bus, Action<string> log)
    {
        _uri = uri;
        _bus = bus;
        _log = log;
        _ = Task.Run(RunAsync);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task RunAsync()
    {
        var backoffSeconds = 1;
        var buffer = new byte[64 * 1024];

        while (!_cts.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(_uri, _cts.Token);
                backoffSeconds = 1;
                _log($"Sonde connectée ({_uri})");

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
            catch (Exception ex) when (ex is WebSocketException or IOException)
            {
                // Sonde absente : cas nominal du mode statique, pas une erreur.
            }
            catch (Exception ex)
            {
                _log($"Connexion Sonde : {ex.Message}");
            }

            _bus.EnterStaticMode();
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
            var type = doc.RootElement.GetProperty("type").GetString();
            switch (type)
            {
                case "handshake":
                    var handshake = doc.RootElement.Deserialize<HandshakeMessage>(_json);
                    if (handshake is null)
                    {
                        return;
                    }
                    _log($"Handshake Sonde v{handshake.Probe_version}, schéma {handshake.Schema_version}, " +
                         $"jeu {handshake.Game_build}, mapping {handshake.Mapping_version}");
                    // TODO (EXG-004) : comparer schema_version (majeur) et
                    // refuser ou dégrader ici.
                    _bus.SetHandshake(handshake);
                    break;

                case "state":
                    var state = doc.RootElement.Deserialize<StateMessage>(_json);
                    if (state is not null)
                    {
                        _bus.Apply(state);
                    }
                    break;

                default:
                    _log($"Message Sonde inconnu : type={type}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            _log($"Message Sonde invalide : {ex.Message}");
        }
    }
}
