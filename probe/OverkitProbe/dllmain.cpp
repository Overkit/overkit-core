#include <Mod/CppUserModBase.hpp>
#include <DynamicOutput/Output.hpp>

using namespace RC;

// Sonde Overkit — spike Phase 0.
// Étape 1 : valider le chargement du mod C++ et le cycle de vie UE4SS.
// Étape 2 (à venir) : lecture position joueur + heure in-game par réflexion,
// publication sur WebSocket local (127.0.0.1:47800). Lecture seule stricte (P1).
class OverkitProbe : public CppUserModBase
{
public:
    OverkitProbe() : CppUserModBase()
    {
        ModName = STR("OverkitProbe");
        ModVersion = STR("0.0.1");
        ModDescription = STR("Sonde Overkit - lecture seule de l'etat du jeu");
        ModAuthors = STR("Nallraen");

        Output::send<LogLevel::Verbose>(STR("[OverkitProbe] Construit (v0.0.1)\n"));
    }

    ~OverkitProbe() override = default;

    auto on_unreal_init() -> void override
    {
        Output::send<LogLevel::Verbose>(STR("[OverkitProbe] Unreal initialise - reflexion disponible\n"));
    }

    auto on_update() -> void override
    {
        // Appelé à chaque tick UE4SS. Les collecteurs cadencés (position 10 Hz,
        // heure 1 Hz) viendront ici, chacun avec son propre échantillonnage.
    }
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
