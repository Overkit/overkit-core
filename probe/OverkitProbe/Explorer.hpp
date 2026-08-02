#pragma once

#include <chrono>
#include <filesystem>

namespace RC::Unreal
{
    class UObject;
    class UStruct;
}

namespace Overkit
{
    // Outil de découverte du mapping : lit un fichier explore.txt (un nom de
    // classe Unreal par ligne, # pour commenter) à côté du mod et dumpe dans le
    // log UE4SS les propriétés + valeurs des instances trouvées, structs et
    // objets inclus. Le fichier est relu dès qu'il change : l'exploration se
    // pilote sans redémarrer le jeu. Lecture seule stricte (P1).
    class Explorer
    {
    public:
        // À appeler sur le thread jeu ; throttle interne.
        void tick();

    private:
        void run_if_file_changed();
        void dump_class(const std::wstring& class_name);
        void dump_properties(RC::Unreal::UStruct* type, void* container, const std::wstring& indent, int depth);

        std::filesystem::path m_file_path;
        std::filesystem::file_time_type m_last_mtime{};
        std::chrono::steady_clock::time_point m_last_check{};
        bool m_path_resolved{false};
    };
}
