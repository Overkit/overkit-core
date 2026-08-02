# Client de test WebSocket : se connecte à la Sonde et affiche les N premiers messages.
param(
    [int]$MessageCount = 8,
    [int]$TimeoutSeconds = 15
)

$ws = New-Object System.Net.WebSockets.ClientWebSocket
$cts = New-Object System.Threading.CancellationTokenSource
$cts.CancelAfter([TimeSpan]::FromSeconds($TimeoutSeconds))

try {
    $uri = [Uri]'ws://127.0.0.1:47800'
    $ws.ConnectAsync($uri, $cts.Token).GetAwaiter().GetResult()
    Write-Host "CONNECTE (etat: $($ws.State))"

    $buffer = New-Object byte[] 8192
    for ($i = 0; $i -lt $MessageCount; $i++) {
        $segment = New-Object System.ArraySegment[byte] -ArgumentList @(,$buffer)
        $result = $ws.ReceiveAsync($segment, $cts.Token).GetAwaiter().GetResult()
        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
            Write-Host "FERMETURE demandee par le serveur"
            break
        }
        $text = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
        Write-Host "MSG $($i + 1): $text"
    }

    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', $cts.Token).GetAwaiter().GetResult()
    Write-Host "DECONNECTE proprement"
} catch {
    $msg = $_.Exception.Message
    if ($_.Exception.InnerException) { $msg = $_.Exception.InnerException.Message }
    Write-Host "ECHEC: $msg"
} finally {
    $ws.Dispose()
}
