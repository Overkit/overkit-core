#pragma once

#include <string>
#include <unordered_map>

namespace Overkit
{
    // Mapping des noms de réflexion (§2.1, EXG-003) : chaque nom de classe ou
    // de propriété spécifique au jeu est résolu via mapping.json (déployé à
    // côté du mod, versionné avec le dataset). Un patch qui renomme une
    // propriété se corrige en éditant ce fichier, sans recompiler. Le fichier
    // est relu dès qu'il change ; un nom absent retombe sur la valeur par
    // défaut compilée.
    class Mapping
    {
    public:
        static auto get() -> Mapping&;

        // À appeler sur le thread jeu (throttle interne) : charge/recharge le
        // fichier s'il a changé.
        auto tick() -> void;

        // Nom effectif pour une clé ; le pointeur reste valide jusqu'au
        // prochain reload (usage immédiat uniquement, thread jeu).
        auto name(const wchar_t* key, const wchar_t* fallback) -> const wchar_t*;

        // Valeurs de l'en-tête pour le handshake (EXG-004).
        auto mapping_version() const -> std::string;
        auto game_build() const -> std::string;

    private:
        auto load(const std::wstring& path) -> void;

        std::unordered_map<std::wstring, std::wstring> m_values;
        long long m_last_check_ms{0};
        long long m_last_mtime{0};
        bool m_path_resolved{false};
        std::wstring m_file_path;
    };
}

// Raccourci de résolution : OVKM("prop.character_id", "CharacterID")
#define OVKM(key, fallback) (Overkit::Mapping::get().name(L##key, L##fallback))
