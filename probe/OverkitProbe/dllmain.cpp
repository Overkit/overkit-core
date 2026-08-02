#include <chrono>
#include <cstdio>

#include <Mod/CppUserModBase.hpp>
#include <DynamicOutput/Output.hpp>
#include <Unreal/UObjectGlobals.hpp>
#include <Unreal/UObject.hpp>
#include <Unreal/UClass.hpp>
#include <Unreal/FProperty.hpp>
#include <Unreal/CoreUObject/UObject/UnrealType.hpp>

#include "Explorer.hpp"
#include "PalboxCollector.hpp"
#include "WsServer.hpp"

#include <string>

using namespace RC;

// Version de la Sonde — source unique, reprise par ModVersion, le log et le
// handshake. Règle : mineure = fonctionnalité, patch = modification.
#define OVERKIT_PROBE_VERSION "0.4.0"

namespace
{
    constexpr std::uint16_t ProbePort = 47800;

    // FVector UE5 (doubles). Résolu par nom de propriété via la réflexion ;
    // seule la disposition interne standard X/Y/Z du type moteur est supposée.
    struct Vector3d
    {
        double X;
        double Y;
        double Z;
    };
}

// Sonde Overkit — spike Phase 0.
// Collecte position joueur (10 Hz) par réflexion, lecture seule stricte (P1),
// et publie sur un WebSocket local (127.0.0.1:47800, EXG-002).
// L'heure in-game est annoncée `unavailable` tant que sa classe source n'est
// pas identifiée (travail de mapping en cours, spike Lua).
class OverkitProbe : public CppUserModBase
{
public:
    OverkitProbe() : CppUserModBase()
    {
        ModName = STR("OverkitProbe");
        ModVersion = L"" OVERKIT_PROBE_VERSION;
        ModDescription = STR("Sonde Overkit - lecture seule de l'etat du jeu");
        ModAuthors = STR("Nallraen");

        Output::send<LogLevel::Verbose>(STR("[OverkitProbe] Construit (v{})\n"), L"" OVERKIT_PROBE_VERSION);
    }

    ~OverkitProbe() override
    {
        m_server.stop();
    }

    auto on_unreal_init() -> void override
    {
        Output::send<LogLevel::Verbose>(STR("[OverkitProbe] Unreal initialise - reflexion disponible\n"));

        // EXG-004 : annonce des versions au premier contact.
        const std::string handshake =
            R"({"type":"handshake","probe_version":")" OVERKIT_PROBE_VERSION R"(","schema_version":"0.1-spike",)"
            R"("game_build":"unknown","mapping_version":"none"})";

        m_server.start(ProbePort, handshake, [](const std::string& message) {
            Output::send<LogLevel::Verbose>(STR("[OverkitProbe] [ws] {}\n"), to_wstring(message));
        });
    }

    auto on_update() -> void override
    {
        // Appelé sur le thread jeu : les lectures de réflexion se font ici,
        // cadencées pour rester loin du budget (< 0,5 ms par tick, EXG probe).
        const auto now = std::chrono::steady_clock::now();
        if (now - m_last_push < std::chrono::milliseconds(100)) // 10 Hz
        {
            return;
        }
        m_last_push = now;

        m_explorer.tick();

        // Heure in-game à 1 Hz : PalGameStateInGame.WorldTime (struct
        // GameDateTime, champ unique Ticks en unités de 100 ns).
        if (now - m_last_time_read >= std::chrono::seconds(1))
        {
            m_last_time_read = now;
            m_time_ok = false;
            try
            {
                auto* game_state = Unreal::UObjectGlobals::FindFirstOf(STR("PalGameStateInGame"));
                if (game_state)
                {
                    auto* ticks = game_state->GetValuePtrByPropertyNameInChain<std::int64_t>(STR("WorldTime"));
                    if (ticks && *ticks > 0)
                    {
                        m_world_ticks = *ticks;
                        m_time_ok = true;
                    }
                }
            }
            catch (...)
            {
                m_time_ok = false;
            }
        }

        bool ok = false;
        Vector3d pos{};
        try
        {
            auto* pawn = Unreal::UObjectGlobals::FindFirstOf(STR("PalPlayerCharacter"));
            if (pawn)
            {
                auto** root = pawn->GetValuePtrByPropertyNameInChain<Unreal::UObject*>(STR("RootComponent"));
                if (root && *root)
                {
                    auto* location = (*root)->GetValuePtrByPropertyNameInChain<Vector3d>(STR("RelativeLocation"));
                    if (location)
                    {
                        pos = *location;
                        ok = true;
                    }
                }
            }
        }
        catch (...)
        {
            // EXG-003 : un chemin qui ne se résout pas => champ indisponible,
            // jamais de crash ni d'arrêt global.
            ok = false;
        }

        const auto t_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                              now.time_since_epoch())
                              .count();

        char player_json[192];
        if (ok)
        {
            std::snprintf(player_json, sizeof(player_json),
                          R"({"status":"ok","x":%.1f,"y":%.1f,"z":%.1f})",
                          pos.X, pos.Y, pos.Z);
        }
        else
        {
            std::snprintf(player_json, sizeof(player_json), R"({"status":"unavailable"})");
        }

        char time_json[192];
        if (m_time_ok)
        {
            const auto total_seconds = m_world_ticks / 10'000'000;
            std::snprintf(time_json, sizeof(time_json),
                          R"({"status":"ok","ticks":%lld,"day":%lld,"hour":%lld,"minute":%lld})",
                          static_cast<long long>(m_world_ticks),
                          static_cast<long long>(total_seconds / 86400),
                          static_cast<long long>((total_seconds % 86400) / 3600),
                          static_cast<long long>((total_seconds % 3600) / 60));
        }
        else
        {
            std::snprintf(time_json, sizeof(time_json), R"({"status":"unavailable"})");
        }

        // Palbox + équipe : resync 30 s, embarqués dans le même message que la
        // position (le transport ne garde que le dernier état publié).
        std::string palbox_json, party_json;
        const bool domains_due = m_palbox.collect_if_due(palbox_json, party_json);

        char json[512];
        std::snprintf(json, sizeof(json),
                      R"({"type":"state","t_ms":%lld,"player":%s,"world":{"time":%s})",
                      static_cast<long long>(t_ms), player_json, time_json);
        std::string message(json);
        if (domains_due)
        {
            message += R"(,"palbox":)" + palbox_json;
            message += R"(,"party":)" + party_json;
        }
        message += '}';
        m_server.publish(std::move(message));
    }

private:
    static auto to_wstring(const std::string& input) -> std::wstring
    {
        return {input.begin(), input.end()};
    }

    Overkit::Explorer m_explorer;
    Overkit::PalboxCollector m_palbox;
    Overkit::WsServer m_server;
    std::chrono::steady_clock::time_point m_last_push{};
    std::chrono::steady_clock::time_point m_last_time_read{};
    std::int64_t m_world_ticks{0};
    bool m_time_ok{false};
};

#define OVERKIT_PROBE_API __declspec(dllexport)
extern "C"
{
    OVERKIT_PROBE_API CppUserModBase* start_mod()
    {
        return new OverkitProbe();
    }

    OVERKIT_PROBE_API void uninstall_mod(CppUserModBase* mod)
    {
        delete mod;
    }
}
