// Dumper Overkit (§4.1) — outil de dev, jamais livré aux joueurs.
// Pilotage par fichier dump.txt à côté du mod, relu dès qu'il change :
//   list            -> écrit out/_tables.txt (toutes les DataTables + nb lignes)
//   DT_XxxYyy       -> écrit out/DT_XxxYyy.json (toutes les lignes, générique)
// Lecture seule stricte ; l'écriture se limite aux fichiers de sortie du mod.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#include <chrono>
#include <filesystem>
#include <format>
#include <fstream>
#include <string>
#include <vector>

#include <Mod/CppUserModBase.hpp>
#include <DynamicOutput/Output.hpp>
#include <Unreal/UObjectGlobals.hpp>
#include <Unreal/UObject.hpp>
#include <Unreal/UClass.hpp>
#include <Unreal/CoreUObject/UObject/UnrealType.hpp>
#include <Unreal/NameTypes.hpp>
#include <Unreal/FText.hpp>
#include <Unreal/Engine/UDataTable.hpp>
#include <Unreal/Core/Containers/Array.hpp>
#include <Unreal/Core/Containers/FString.hpp>

using namespace RC;

#define OVERKIT_DUMPER_VERSION "0.1.0"

namespace
{
    constexpr int MaxDepth = 4;

    auto log_line(const std::wstring& line) -> void
    {
        Output::send<LogLevel::Verbose>(STR("[OverkitDumper] {}\n"), line);
    }

    auto to_utf8(const std::wstring& input) -> std::string
    {
        if (input.empty())
        {
            return {};
        }
        const int size = WideCharToMultiByte(CP_UTF8, 0, input.data(), (int)input.size(), nullptr, 0, nullptr, nullptr);
        std::string out(size, '\0');
        WideCharToMultiByte(CP_UTF8, 0, input.data(), (int)input.size(), out.data(), size, nullptr, nullptr);
        return out;
    }

    auto json_escape(const std::wstring& input) -> std::string
    {
        std::string out;
        for (const auto wc : input)
        {
            if (wc == L'"' || wc == L'\\')
            {
                out.push_back('\\');
                out.push_back(static_cast<char>(wc));
            }
            else if (wc < 0x20)
            {
                out += std::format("\\u{:04x}", static_cast<int>(wc));
            }
            else if (wc < 0x80)
            {
                out.push_back(static_cast<char>(wc));
            }
            else
            {
                out += to_utf8(std::wstring(1, wc));
            }
        }
        return out;
    }

    auto struct_to_json(Unreal::UStruct* type, void* container, int depth) -> std::string;

    // Sérialise la valeur d'une propriété en fragment JSON ("null" si type
    // non géré). Pour un élément de tableau, container = pointeur d'élément
    // (l'offset interne de la propriété est alors 0).
    auto value_to_json(Unreal::FProperty* property, void* container, int depth) -> std::string
    {
        const auto type_name = property->GetClass().GetName();

        if (type_name == STR("IntProperty"))
        {
            return std::to_string(*property->ContainerPtrToValuePtr<std::int32_t>(container));
        }
        if (type_name == STR("Int64Property"))
        {
            return std::to_string(*property->ContainerPtrToValuePtr<std::int64_t>(container));
        }
        if (type_name == STR("UInt32Property"))
        {
            return std::to_string(*property->ContainerPtrToValuePtr<std::uint32_t>(container));
        }
        if (type_name == STR("UInt16Property"))
        {
            return std::to_string(*property->ContainerPtrToValuePtr<std::uint16_t>(container));
        }
        if (type_name == STR("Int16Property"))
        {
            return std::to_string(*property->ContainerPtrToValuePtr<std::int16_t>(container));
        }
        if (type_name == STR("FloatProperty"))
        {
            return std::format("{}", *property->ContainerPtrToValuePtr<float>(container));
        }
        if (type_name == STR("DoubleProperty"))
        {
            return std::format("{}", *property->ContainerPtrToValuePtr<double>(container));
        }
        if (type_name == STR("ByteProperty") || type_name == STR("EnumProperty"))
        {
            return std::to_string(*property->ContainerPtrToValuePtr<std::uint8_t>(container));
        }
        if (type_name == STR("BoolProperty"))
        {
            auto* bool_property = static_cast<Unreal::FBoolProperty*>(property);
            const auto* bytes = property->ContainerPtrToValuePtr<std::uint8_t>(container);
            return (bytes[bool_property->GetByteOffset()] & bool_property->GetFieldMask()) ? "true" : "false";
        }
        if (type_name == STR("NameProperty"))
        {
            return '"' + json_escape(property->ContainerPtrToValuePtr<Unreal::FName>(container)->ToString()) + '"';
        }
        if (type_name == STR("StrProperty"))
        {
            const auto& chars = property->ContainerPtrToValuePtr<Unreal::FString>(container)->GetCharArray();
            return '"' + json_escape(chars.Num() > 0 ? chars.GetData() : L"") + '"';
        }
        if (type_name == STR("TextProperty"))
        {
            auto* text = property->ContainerPtrToValuePtr<Unreal::FText>(container);
            return '"' + json_escape(text->Data ? text->ToString() : L"") + '"';
        }
        if (type_name == STR("ObjectProperty") || type_name == STR("SoftObjectProperty") ||
            type_name == STR("ClassProperty") || type_name == STR("SoftClassProperty"))
        {
            if (type_name == STR("ObjectProperty"))
            {
                auto* value = *property->ContainerPtrToValuePtr<Unreal::UObject*>(container);
                return value ? '"' + json_escape(value->GetName()) + '"' : "null";
            }
            return "null";
        }
        if (type_name == STR("StructProperty") && depth < MaxDepth)
        {
            auto* struct_property = static_cast<Unreal::FStructProperty*>(property);
            return struct_to_json(struct_property->GetStruct().Get(),
                                  property->ContainerPtrToValuePtr<void>(container), depth + 1);
        }
        if (type_name == STR("ArrayProperty") && depth < MaxDepth)
        {
            auto* array_property = static_cast<Unreal::FArrayProperty*>(property);
            auto* inner = array_property->GetInner();
            auto* array = property->ContainerPtrToValuePtr<Unreal::TArray<std::uint8_t>>(container);
            const auto stride = inner->GetElementSize();
            std::string out = "[";
            for (std::int32_t i = 0; i < array->Num(); ++i)
            {
                if (i > 0)
                {
                    out += ',';
                }
                out += value_to_json(inner, array->GetData() + static_cast<std::ptrdiff_t>(i) * stride, depth + 1);
            }
            return out + ']';
        }
        return "null";
    }

    auto struct_to_json(Unreal::UStruct* type, void* container, int depth) -> std::string
    {
        std::string out = "{";
        bool first = true;
        for (auto* property : type->ForEachPropertyInChain())
        {
            if (!first)
            {
                out += ',';
            }
            first = false;
            out += '"' + json_escape(property->GetName()) + "\":" + value_to_json(property, container, depth);
        }
        return out + '}';
    }
}

// Mod Dumper : cycle de vie identique à la Sonde, mais aucun serveur — juste
// le pilotage par fichier et l'écriture des JSON.
class OverkitDumper : public CppUserModBase
{
public:
    OverkitDumper() : CppUserModBase()
    {
        ModName = STR("OverkitDumper");
        ModVersion = L"" OVERKIT_DUMPER_VERSION;
        ModDescription = STR("Dumper Overkit - extraction des DataTables (outil de dev)");
        ModAuthors = STR("Nallraen");
        Output::send<LogLevel::Verbose>(STR("[OverkitDumper] Construit (v{})\n"), L"" OVERKIT_DUMPER_VERSION);
    }

    auto on_update() -> void override
    {
        const auto now = std::chrono::steady_clock::now();
        if (now - m_last_check < std::chrono::seconds(2))
        {
            return;
        }
        m_last_check = now;

        if (!m_path_resolved)
        {
            wchar_t exe_path[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exe_path, MAX_PATH);
            const auto mod_dir = std::filesystem::path(exe_path).parent_path() / L"ue4ss" / L"Mods" / L"OverkitDumper";
            m_control_file = mod_dir / L"dump.txt";
            m_out_dir = mod_dir / L"out";
            m_path_resolved = true;
            log_line(L"pilotage : " + m_control_file.wstring());
        }

        std::error_code ec{};
        const auto mtime = std::filesystem::last_write_time(m_control_file, ec);
        if (ec || mtime == m_last_mtime)
        {
            return;
        }
        m_last_mtime = mtime;
        run_control_file();
    }

private:
    auto run_control_file() -> void
    {
        std::wifstream file(m_control_file);
        if (!file.is_open())
        {
            return;
        }
        std::error_code ec{};
        std::filesystem::create_directories(m_out_dir, ec);

        log_line(L"===== dump (fichier modifie) =====");
        std::wstring line;
        while (std::getline(file, line))
        {
            while (!line.empty() && (line.back() == L'\r' || line.back() == L' '))
            {
                line.pop_back();
            }
            while (!line.empty() && (line.front() == L'\xFEFF' || line.front() == L' ' || line.front() > 0x7F))
            {
                line.erase(0, 1);
            }
            if (line.empty() || line.front() == L'#')
            {
                continue;
            }
            try
            {
                if (line == L"list")
                {
                    list_tables();
                }
                else
                {
                    dump_table(line);
                }
            }
            catch (...)
            {
                log_line(L"ERREUR pendant le traitement de " + line);
            }
        }
        log_line(L"===== fin de dump =====");
    }

    auto list_tables() -> void
    {
        std::vector<Unreal::UObject*> tables{};
        Unreal::UObjectGlobals::FindAllOf(STR("DataTable"), tables);

        std::ofstream out(m_out_dir / L"_tables.txt", std::ios::binary);
        int count = 0;
        for (auto* object : tables)
        {
            auto* table = static_cast<Unreal::UDataTable*>(object);
            const auto row_struct = table->GetRowStruct();
            out << to_utf8(std::format(L"{}\t{} lignes\t[{}]\t{}\n",
                                       table->GetName(),
                                       table->GetRowMap().Num(),
                                       row_struct ? row_struct->GetName() : L"?",
                                       table->GetPathName()));
            ++count;
        }
        log_line(std::format(L"liste ecrite : {} tables -> out/_tables.txt", count));
    }

    auto dump_table(const std::wstring& table_name) -> void
    {
        std::vector<Unreal::UObject*> tables{};
        Unreal::UObjectGlobals::FindAllOf(STR("DataTable"), tables);

        for (auto* object : tables)
        {
            if (object->GetName() != table_name)
            {
                continue;
            }
            auto* table = static_cast<Unreal::UDataTable*>(object);
            auto row_struct = table->GetRowStruct();
            if (!row_struct)
            {
                log_line(table_name + L" : pas de RowStruct, ignoree");
                return;
            }

            std::string json = "{\n";
            json += "\"$table\":\"" + json_escape(table_name) + "\",";
            json += "\"$row_struct\":\"" + json_escape(row_struct->GetName()) + "\",";
            json += "\"rows\":{";
            bool first = true;
            int rows = 0;
            for (auto& pair : table->GetRowMap())
            {
                if (!first)
                {
                    json += ',';
                }
                first = false;
                json += '\n';
                json += '"' + json_escape(pair.Key.ToString()) + "\":" +
                        struct_to_json(row_struct.Get(), pair.Value, 0);
                ++rows;
            }
            json += "\n}}\n";

            std::ofstream out(m_out_dir / (table_name + L".json"), std::ios::binary);
            out << json;
            log_line(std::format(L"{} : {} lignes -> out/{}.json", table_name, rows, table_name));
            return;
        }
        log_line(table_name + L" : table introuvable (pas encore chargee ?)");
    }

    std::filesystem::path m_control_file;
    std::filesystem::path m_out_dir;
    std::filesystem::file_time_type m_last_mtime{};
    std::chrono::steady_clock::time_point m_last_check{};
    bool m_path_resolved{false};
};

#define OVERKIT_DUMPER_API __declspec(dllexport)
extern "C"
{
    OVERKIT_DUMPER_API CppUserModBase* start_mod()
    {
        return new OverkitDumper();
    }

    OVERKIT_DUMPER_API void uninstall_mod(CppUserModBase* mod)
    {
        delete mod;
    }
}
