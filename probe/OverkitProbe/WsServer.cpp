// Implémentation WebSocket serveur minimale (RFC 6455, sous-ensemble) :
// - handshake HTTP + Sec-WebSocket-Accept (SHA-1 via BCrypt, Base64 via Crypt32)
// - trames texte sortantes non masquées, gestion de ping/close entrants
// - texte entrant ignoré : le canal est strictement descendant (P1)
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <bcrypt.h>
#include <wincrypt.h>

#include "WsServer.hpp"

#include <array>
#include <cstring>
#include <vector>

namespace
{
    constexpr const char* WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    bool sha1(const std::string& input, std::array<unsigned char, 20>& out)
    {
        BCRYPT_ALG_HANDLE alg = nullptr;
        BCRYPT_HASH_HANDLE hash = nullptr;
        bool ok = false;
        if (BCryptOpenAlgorithmProvider(&alg, BCRYPT_SHA1_ALGORITHM, nullptr, 0) == 0)
        {
            if (BCryptCreateHash(alg, &hash, nullptr, 0, nullptr, 0, 0) == 0)
            {
                ok = BCryptHashData(hash, (PUCHAR)input.data(), (ULONG)input.size(), 0) == 0 &&
                     BCryptFinishHash(hash, out.data(), (ULONG)out.size(), 0) == 0;
                BCryptDestroyHash(hash);
            }
            BCryptCloseAlgorithmProvider(alg, 0);
        }
        return ok;
    }

    std::string base64(const unsigned char* data, DWORD len)
    {
        DWORD chars = 0;
        if (!CryptBinaryToStringA(data, len, CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, nullptr, &chars))
        {
            return {};
        }
        std::string out(chars, '\0');
        if (!CryptBinaryToStringA(data, len, CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, out.data(), &chars))
        {
            return {};
        }
        while (!out.empty() && (out.back() == '\0' || out.back() == '\n' || out.back() == '\r'))
        {
            out.pop_back();
        }
        return out;
    }

    bool send_all(SOCKET s, const char* data, int len)
    {
        int sent = 0;
        while (sent < len)
        {
            const int n = send(s, data + sent, len - sent, 0);
            if (n <= 0)
            {
                return false;
            }
            sent += n;
        }
        return true;
    }

    bool send_frame(SOCKET s, unsigned char opcode, const std::string& payload)
    {
        std::vector<unsigned char> header;
        header.push_back(0x80 | opcode); // FIN + opcode
        const std::size_t n = payload.size();
        if (n < 126)
        {
            header.push_back(static_cast<unsigned char>(n));
        }
        else if (n <= 0xFFFF)
        {
            header.push_back(126);
            header.push_back(static_cast<unsigned char>((n >> 8) & 0xFF));
            header.push_back(static_cast<unsigned char>(n & 0xFF));
        }
        else
        {
            header.push_back(127);
            for (int i = 7; i >= 0; --i)
            {
                header.push_back(static_cast<unsigned char>((n >> (8 * i)) & 0xFF));
            }
        }
        return send_all(s, reinterpret_cast<const char*>(header.data()), static_cast<int>(header.size())) &&
               (n == 0 || send_all(s, payload.data(), static_cast<int>(n)));
    }

    // Traite les trames de contrôle contenues dans un bloc reçu.
    // Retourne false si le client a demandé la fermeture.
    bool handle_incoming(SOCKET s, const unsigned char* buf, int len)
    {
        int pos = 0;
        while (pos + 2 <= len)
        {
            const unsigned char opcode = buf[pos] & 0x0F;
            const bool masked = (buf[pos + 1] & 0x80) != 0;
            std::uint64_t payload_len = buf[pos + 1] & 0x7F;
            int offset = 2;
            if (payload_len == 126)
            {
                if (pos + 4 > len) return true;
                payload_len = (static_cast<std::uint64_t>(buf[pos + 2]) << 8) | buf[pos + 3];
                offset = 4;
            }
            else if (payload_len == 127)
            {
                if (pos + 10 > len) return true;
                payload_len = 0;
                for (int i = 0; i < 8; ++i)
                {
                    payload_len = (payload_len << 8) | buf[pos + 2 + i];
                }
                offset = 10;
            }
            const unsigned char* mask = nullptr;
            if (masked)
            {
                if (pos + offset + 4 > len) return true;
                mask = buf + pos + offset;
                offset += 4;
            }
            if (pos + offset + static_cast<int>(payload_len) > len)
            {
                return true; // trame incomplète : on l'ignore (contrôles localhost = petits)
            }

            if (opcode == 0x8) // close
            {
                send_frame(s, 0x8, {});
                return false;
            }
            if (opcode == 0x9) // ping -> pong
            {
                std::string payload(reinterpret_cast<const char*>(buf + pos + offset), payload_len);
                if (mask)
                {
                    for (std::size_t i = 0; i < payload.size(); ++i)
                    {
                        payload[i] = static_cast<char>(payload[i] ^ mask[i % 4]);
                    }
                }
                send_frame(s, 0xA, payload);
            }
            // texte/binaire entrant : ignoré, canal strictement descendant
            pos += offset + static_cast<int>(payload_len);
        }
        return true;
    }
}

namespace Overkit
{
    WsServer::~WsServer()
    {
        stop();
    }

    void WsServer::log(const std::string& message)
    {
        if (m_logger)
        {
            m_logger(message);
        }
    }

    bool WsServer::start(std::uint16_t port, std::string handshake_message, Logger logger)
    {
        if (m_running.exchange(true))
        {
            return false;
        }
        m_handshake = std::move(handshake_message);
        m_logger = std::move(logger);
        m_thread = std::thread([this, port] { run(port); });
        return true;
    }

    void WsServer::stop()
    {
        if (!m_running.exchange(false))
        {
            return;
        }
        const auto s = static_cast<SOCKET>(m_listen_socket.load());
        if (s != 0)
        {
            closesocket(s);
        }
        if (m_thread.joinable())
        {
            m_thread.join();
        }
    }

    void WsServer::publish(std::string json)
    {
        std::lock_guard lock(m_state_mutex);
        m_latest = std::move(json);
        ++m_latest_seq;
    }

    void WsServer::run(std::uint16_t port)
    {
        WSADATA wsa{};
        if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0)
        {
            log("WSAStartup en echec, serveur arrete");
            return;
        }

        const SOCKET listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (listener == INVALID_SOCKET)
        {
            log("socket() en echec");
            WSACleanup();
            return;
        }
        m_listen_socket = static_cast<std::uintptr_t>(listener);

        sockaddr_in addr{};
        addr.sin_family = AF_INET;
        addr.sin_port = htons(port);
        // Loopback uniquement, sans option de binder ailleurs (EXG-002).
        inet_pton(AF_INET, "127.0.0.1", &addr.sin_addr);

        if (bind(listener, reinterpret_cast<sockaddr*>(&addr), sizeof(addr)) == SOCKET_ERROR ||
            listen(listener, 1) == SOCKET_ERROR)
        {
            log("bind/listen en echec sur 127.0.0.1:" + std::to_string(port) + " (port occupe ?)");
            closesocket(listener);
            WSACleanup();
            return;
        }
        log("en ecoute sur 127.0.0.1:" + std::to_string(port));

        while (m_running)
        {
            fd_set readfds;
            FD_ZERO(&readfds);
            FD_SET(listener, &readfds);
            timeval tv{0, 200'000}; // 200 ms
            const int ready = select(0, &readfds, nullptr, nullptr, &tv);
            if (!m_running)
            {
                break;
            }
            if (ready == SOCKET_ERROR)
            {
                break;
            }
            if (ready > 0)
            {
                const SOCKET client = accept(listener, nullptr, nullptr);
                if (client != INVALID_SOCKET)
                {
                    log("client connecte");
                    serve_client(static_cast<std::uintptr_t>(client));
                    log("client deconnecte");
                }
            }
        }

        closesocket(listener);
        m_listen_socket = 0;
        WSACleanup();
    }

    void WsServer::serve_client(std::uintptr_t client_socket)
    {
        const SOCKET client = static_cast<SOCKET>(client_socket);

        // --- Handshake HTTP ---
        std::string request;
        char buf[4096];
        while (m_running && request.find("\r\n\r\n") == std::string::npos && request.size() < 8192)
        {
            const int n = recv(client, buf, sizeof(buf), 0);
            if (n <= 0)
            {
                closesocket(client);
                return;
            }
            request.append(buf, n);
        }

        const auto key_pos = request.find("Sec-WebSocket-Key:");
        if (key_pos == std::string::npos)
        {
            closesocket(client);
            return;
        }
        auto key_start = key_pos + std::strlen("Sec-WebSocket-Key:");
        while (key_start < request.size() && request[key_start] == ' ')
        {
            ++key_start;
        }
        const auto key_end = request.find('\r', key_start);
        const std::string key = request.substr(key_start, key_end - key_start);

        std::array<unsigned char, 20> digest{};
        if (!sha1(key + WsGuid, digest))
        {
            closesocket(client);
            return;
        }
        const std::string response =
            "HTTP/1.1 101 Switching Protocols\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            "Sec-WebSocket-Accept: " + base64(digest.data(), static_cast<DWORD>(digest.size())) + "\r\n\r\n";
        if (!send_all(client, response.data(), static_cast<int>(response.size())))
        {
            closesocket(client);
            return;
        }

        // --- Annonce initiale (EXG-004) puis boucle de push ---
        if (!send_frame(client, 0x1, m_handshake))
        {
            closesocket(client);
            return;
        }

        std::uint64_t sent_seq = 0;
        while (m_running)
        {
            fd_set readfds;
            FD_ZERO(&readfds);
            FD_SET(client, &readfds);
            timeval tv{0, 50'000}; // 50 ms
            const int ready = select(0, &readfds, nullptr, nullptr, &tv);
            if (ready == SOCKET_ERROR)
            {
                break;
            }
            if (ready > 0)
            {
                const int n = recv(client, buf, sizeof(buf), 0);
                if (n <= 0)
                {
                    break;
                }
                if (!handle_incoming(client, reinterpret_cast<unsigned char*>(buf), n))
                {
                    break;
                }
            }

            std::string to_send;
            {
                std::lock_guard lock(m_state_mutex);
                if (m_latest_seq != sent_seq)
                {
                    to_send = m_latest;
                    sent_seq = m_latest_seq;
                }
            }
            if (!to_send.empty() && !send_frame(client, 0x1, to_send))
            {
                break;
            }
        }
        closesocket(client);
    }
}
