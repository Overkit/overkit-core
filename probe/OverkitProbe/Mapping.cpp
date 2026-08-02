#include "Mapping.hpp"

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#include <chrono>
#include <filesystem>
#include <fstream>

#include <DynamicOutput/Output.hpp>

using namespace RC;

namespace
{
    auto now_ms() -> long long
    {
        return std::chrono::duration_cast<std::chrono::milliseconds>(
                   std::chrono::steady_clock::now().time_since_epoch())
            .count();
    }

    auto widen(const std::string& input) -> std::wstring
    {
        if (input.empty())
        {
            return {};
        }
        const int size = MultiByteToWideChar(CP_UTF8, 0, input.data(), (int)input.size(), nullptr, 0);
        std::wstring out(size, L'\0');
        MultiByteToWideChar(CP_UTF8, 0, input.data(), (int)input.size(), out.data(), size);
        return out;
    }
}

namespace Overkit
{
    auto Mapping::get() -> Mapping&
    {
        static Mapping instance;
        return instance;
    }

    auto Mapping::tick() -> void
    {
        const auto now = now_ms();
        if (now - m_last_check_ms < 2000)
        {
            return;
        }
        m_last_check_ms = now;

        if (!m_path_resolved)
        {
            wchar_t exe_path[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exe_path, MAX_PATH);
            m_file_path = (std::filesystem::path(exe_path).parent_path() /
                           L"ue4ss" / L"Mods" / L"OverkitProbe" / L"mapping.json")
                              .wstring();
            m_path_resolved = true;
        }

        std::error_code ec{};
        const auto mtime = std::filesystem::last_write_time(m_file_path, ec);
        if (ec)
        {
            return; // pas de fichier : les valeurs par défaut compilées servent
        }
        const auto mtime_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                                  mtime.time_since_epoch())
                                  .count();
        if (mtime_ms == m_last_mtime)
        {
            return;
        }
        m_last_mtime = mtime_ms;
        load(m_file_path);
    }

    // Parseur volontairement minimal : mapping.json est PLAT, uniquement des
    // paires "clé": "valeur" (les autres types sont ignorés). Format sous
    // notre contrôle, zéro dépendance.
    auto Mapping::load(const std::wstring& path) -> void
    {
        std::ifstream file(std::filesystem::path(path), std::ios::binary);
        if (!file.is_open())
        {
            return;
        }
        std::string content((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());

        std::unordered_map<std::wstring, std::wstring> values;
        std::size_t i = 0;
        auto read_quoted = [&](std::string& out) -> bool {
            while (i < content.size() && content[i] != '"')
            {
                ++i;
            }
            if (i >= content.size())
            {
                return false;
            }
            ++i;
            out.clear();
            while (i < content.size() && content[i] != '"')
            {
                if (content[i] == '\\' && i + 1 < content.size())
                {
                    ++i;
                }
                out.push_back(content[i]);
                ++i;
            }
            if (i >= content.size())
            {
                return false;
            }
            ++i;
            return true;
        };

        std::string key, value;
        while (read_quoted(key))
        {
            while (i < content.size() && (content[i] == ' ' || content[i] == '\t'))
            {
                ++i;
            }
            if (i >= content.size() || content[i] != ':')
            {
                continue; // pas une paire clé:valeur (virgule, etc.)
            }
            ++i;
            while (i < content.size() && (content[i] == ' ' || content[i] == '\t'))
            {
                ++i;
            }
            if (i < content.size() && content[i] == '"')
            {
                if (read_quoted(value))
                {
                    values[widen(key)] = widen(value);
                }
            }
        }

        m_values = std::move(values);
        Output::send<LogLevel::Verbose>(STR("[OverkitProbe] mapping.json charge : {} entrees (v{})\n"),
                                        m_values.size(), widen(mapping_version()));
    }

    auto Mapping::name(const wchar_t* key, const wchar_t* fallback) -> const wchar_t*
    {
        const auto it = m_values.find(key);
        if (it != m_values.end() && !it->second.empty())
        {
            return it->second.c_str();
        }
        return fallback;
    }

    auto Mapping::mapping_version() const -> std::string
    {
        const auto it = m_values.find(L"mapping_version");
        if (it == m_values.end())
        {
            return "builtin";
        }
        return {it->second.begin(), it->second.end()};
    }

    auto Mapping::game_build() const -> std::string
    {
        const auto it = m_values.find(L"game_build");
        if (it == m_values.end())
        {
            return "unknown";
        }
        return {it->second.begin(), it->second.end()};
    }
}
