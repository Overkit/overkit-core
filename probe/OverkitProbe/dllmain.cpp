#include <chrono>
#include <cstdio>

#include <Mod/CppUserModBase.hpp>
#include <DynamicOutput/Output.hpp>
#include <Unreal/UObjectGlobals.hpp>
#include <Unreal/UObject.hpp>
#include <Unreal/UClass.hpp>
#include <Unreal/FProperty.hpp>
#include <Unreal/CoreUObject/UObject/UnrealType.hpp>

#include "WsServer.hpp"

using namespace RC;

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
        ModVersion = STR("0.1.0");
        ModDescription = STR("Sonde Overkit - lecture seule de l'etat du jeu");
        ModAuthors = STR("Nallraen");

        Output::send<LogLevel::Verbose>(STR("[OverkitProbe] Construit (v0.1.0)\n"));
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
            R"({"type":"handshake","probe_version":"0.1.0","schema_version":"0.1-spike",)"
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

        dump_time_sources_once();

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

        char json[512];
        std::snprintf(json, sizeof(json),
                      R"({"type":"state","t_ms":%lld,"player":%s,"world":{"time":%s}})",
                      static_cast<long long>(t_ms), player_json, time_json);
        m_server.publish(json);
    }

private:
    // Exploration ponctuelle : liste propriétés ET valeurs simples des classes
    // candidates pour l'heure in-game (travail de mapping). Déclenchée quand
    // PalGameStateInGame existe (= partie chargée), pour éviter les objets
    // modèles des mondes temporaires. Retiré une fois mapping.json en place.
    auto dump_time_sources_once() -> void
    {
        if (m_time_dump_done)
        {
            return;
        }
        try
        {
            if (!Unreal::UObjectGlobals::FindFirstOf(STR("PalGameStateInGame")))
            {
                return; // pas encore en partie
            }
            m_time_dump_done = true;

            for (const auto* class_name : {STR("PalTimeManager"), STR("PalGameStateInGame")})
            {
                std::vector<Unreal::UObject*> instances{};
                Unreal::UObjectGlobals::FindAllOf(class_name, instances);
                for (auto* object : instances)
                {
                    Output::send<LogLevel::Verbose>(STR("[OverkitProbe] Proprietes de {} :\n"),
                                                    object->GetFullName());
                    for (auto* property : object->GetClassPrivate()->ForEachPropertyInChain())
                    {
                        const auto type_name = property->GetClass().GetName();
                        std::wstring value = L"";
                        if (type_name == STR("IntProperty"))
                        {
                            value = L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<int32_t>(object));
                        }
                        else if (type_name == STR("DoubleProperty"))
                        {
                            value = L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<double>(object));
                        }
                        else if (type_name == STR("FloatProperty"))
                        {
                            value = L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<float>(object));
                        }
                        else if (type_name == STR("ByteProperty"))
                        {
                            value = L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<std::uint8_t>(object));
                        }
                        Output::send<LogLevel::Verbose>(STR("[OverkitProbe]   {} ({}){}\n"),
                                                        property->GetName(), type_name, value);

                        // Descente d'un niveau dans les structs liées au temps.
                        if (type_name == STR("StructProperty") &&
                            property->GetName().find(STR("Time")) != std::wstring::npos)
                        {
                            auto* struct_property = static_cast<Unreal::FStructProperty*>(property);
                            auto inner_struct = struct_property->GetStruct();
                            void* struct_ptr = property->ContainerPtrToValuePtr<void>(object);
                            Output::send<LogLevel::Verbose>(STR("[OverkitProbe]     -> struct {} :\n"),
                                                            inner_struct->GetName());
                            for (auto* inner : inner_struct->ForEachPropertyInChain())
                            {
                                const auto inner_type = inner->GetClass().GetName();
                                std::wstring inner_value = L"";
                                if (inner_type == STR("IntProperty"))
                                {
                                    inner_value = L" = " + std::to_wstring(*inner->ContainerPtrToValuePtr<int32_t>(struct_ptr));
                                }
                                else if (inner_type == STR("Int64Property"))
                                {
                                    inner_value = L" = " + std::to_wstring(*inner->ContainerPtrToValuePtr<std::int64_t>(struct_ptr));
                                }
                                else if (inner_type == STR("DoubleProperty"))
                                {
                                    inner_value = L" = " + std::to_wstring(*inner->ContainerPtrToValuePtr<double>(struct_ptr));
                                }
                                else if (inner_type == STR("FloatProperty"))
                                {
                                    inner_value = L" = " + std::to_wstring(*inner->ContainerPtrToValuePtr<float>(struct_ptr));
                                }
                                else if (inner_type == STR("ByteProperty"))
                                {
                                    inner_value = L" = " + std::to_wstring(*inner->ContainerPtrToValuePtr<std::uint8_t>(struct_ptr));
                                }
                                Output::send<LogLevel::Verbose>(STR("[OverkitProbe]     {} ({}){}\n"),
                                                                inner->GetName(), inner_type, inner_value);
                            }
                        }
                    }
                }
            }
        }
        catch (...)
        {
            m_time_dump_done = true;
        }
    }

    static auto to_wstring(const std::string& input) -> std::wstring
    {
        return {input.begin(), input.end()};
    }

    Overkit::WsServer m_server;
    std::chrono::steady_clock::time_point m_last_push{};
    std::chrono::steady_clock::time_point m_last_time_read{};
    std::int64_t m_world_ticks{0};
    bool m_time_ok{false};
    bool m_time_dump_done{false};
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
