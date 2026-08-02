#pragma once

#include <atomic>
#include <cstdint>
#include <functional>
#include <mutex>
#include <string>
#include <thread>

namespace Overkit
{
    // Serveur WebSocket minimal, sans dépendance, loopback uniquement (EXG-002).
    // Modèle « dernier état » : publish() remplace le snapshot courant ; le
    // thread serveur pousse au client connecté dès qu'un nouvel état existe.
    // Un seul client servi à la fois (le host Overkit) ; reconnexion possible.
    class WsServer
    {
    public:
        using Logger = std::function<void(const std::string&)>;

        WsServer() = default;
        ~WsServer();
        WsServer(const WsServer&) = delete;
        WsServer& operator=(const WsServer&) = delete;

        // handshake_message : premier message envoyé à chaque client (EXG-004).
        bool start(std::uint16_t port, std::string handshake_message, Logger logger);
        void stop();
        void publish(std::string json);

    private:
        void run(std::uint16_t port);
        void serve_client(std::uintptr_t client_socket);
        void log(const std::string& message);

        std::thread m_thread;
        std::atomic<bool> m_running{false};
        std::atomic<std::uintptr_t> m_listen_socket{0};
        std::mutex m_state_mutex;
        std::string m_latest;
        std::uint64_t m_latest_seq{0};
        std::string m_handshake;
        Logger m_logger;
    };
}
