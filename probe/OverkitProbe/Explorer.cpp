#include "Explorer.hpp"

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#include <fstream>
#include <string>
#include <vector>

#include <DynamicOutput/Output.hpp>
#include <Unreal/UObjectGlobals.hpp>
#include <Unreal/UObject.hpp>
#include <Unreal/UClass.hpp>
#include <Unreal/CoreUObject/UObject/UnrealType.hpp>
#include <Unreal/NameTypes.hpp>
#include <Unreal/Core/Containers/Array.hpp>
#include <Unreal/Core/Containers/FString.hpp>

using namespace RC;

namespace
{
    constexpr int MaxInstances = 3;
    constexpr int MaxDepth = 2;
    constexpr int MaxArrayPreview = 5;

    auto log_line(const std::wstring& line) -> void
    {
        Output::send<LogLevel::Verbose>(STR("[OverkitExplore] {}\n"), line);
    }

    // Description courte d'une valeur simple/objet ; vide pour les types
    // composites (gérés par récursion).
    auto format_leaf(Unreal::FProperty* property, void* container) -> std::wstring
    {
        const auto type_name = property->GetClass().GetName();
        if (type_name == STR("IntProperty"))
        {
            return std::to_wstring(*property->ContainerPtrToValuePtr<std::int32_t>(container));
        }
        if (type_name == STR("Int64Property"))
        {
            return std::to_wstring(*property->ContainerPtrToValuePtr<std::int64_t>(container));
        }
        if (type_name == STR("FloatProperty"))
        {
            return std::to_wstring(*property->ContainerPtrToValuePtr<float>(container));
        }
        if (type_name == STR("DoubleProperty"))
        {
            return std::to_wstring(*property->ContainerPtrToValuePtr<double>(container));
        }
        if (type_name == STR("ByteProperty") || type_name == STR("EnumProperty"))
        {
            return std::to_wstring(*property->ContainerPtrToValuePtr<std::uint8_t>(container));
        }
        if (type_name == STR("NameProperty"))
        {
            return property->ContainerPtrToValuePtr<Unreal::FName>(container)->ToString();
        }
        if (type_name == STR("StrProperty"))
        {
            const auto& chars = property->ContainerPtrToValuePtr<Unreal::FString>(container)->GetCharArray();
            return L"\"" + std::wstring(chars.Num() > 0 ? chars.GetData() : L"") + L"\"";
        }
        if (type_name == STR("ObjectProperty"))
        {
            auto* value = *property->ContainerPtrToValuePtr<Unreal::UObject*>(container);
            return value ? value->GetClassPrivate()->GetName() + L" '" + value->GetName() + L"'" : L"null";
        }
        return {};
    }
}

namespace Overkit
{
    auto Explorer::tick() -> void
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
            m_file_path = std::filesystem::path(exe_path).parent_path() / L"ue4ss" / L"Mods" / L"OverkitProbe" / L"explore.txt";
            m_path_resolved = true;
            log_line(L"pilotage : " + m_file_path.wstring());
        }

        run_if_file_changed();
    }

    auto Explorer::run_if_file_changed() -> void
    {
        std::error_code ec{};
        const auto mtime = std::filesystem::last_write_time(m_file_path, ec);
        if (ec || mtime == m_last_mtime)
        {
            return;
        }
        m_last_mtime = mtime;

        std::wifstream file(m_file_path);
        if (!file.is_open())
        {
            return;
        }

        log_line(L"===== exploration (fichier modifie) =====");
        std::wstring line;
        while (std::getline(file, line))
        {
            while (!line.empty() && (line.back() == L'\r' || line.back() == L' '))
            {
                line.pop_back();
            }
            if (line.empty() || line.front() == L'#')
            {
                continue;
            }
            try
            {
                dump_class(line);
            }
            catch (...)
            {
                log_line(L"ERREUR pendant le dump de " + line);
            }
        }
        log_line(L"===== fin d'exploration =====");
    }

    auto Explorer::dump_class(const std::wstring& class_name) -> void
    {
        std::vector<Unreal::UObject*> instances{};
        Unreal::UObjectGlobals::FindAllOf(class_name.c_str(), instances);
        log_line(L"--- " + class_name + L" : " + std::to_wstring(instances.size()) + L" instance(s)");

        int shown = 0;
        for (auto* object : instances)
        {
            if (shown++ >= MaxInstances)
            {
                log_line(L"    (instances suivantes tronquees)");
                break;
            }
            log_line(L"  [" + std::to_wstring(shown) + L"] " + object->GetFullName());
            dump_properties(object->GetClassPrivate(), object, L"    ", 0);
        }
    }

    auto Explorer::dump_properties(Unreal::UStruct* type, void* container, const std::wstring& indent, int depth) -> void
    {
        for (auto* property : type->ForEachPropertyInChain())
        {
            const auto type_name = property->GetClass().GetName();
            std::wstring line = indent + property->GetName() + L" (" + type_name + L")";

            if (type_name == STR("IntProperty"))
            {
                line += L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<std::int32_t>(container));
            }
            else if (type_name == STR("Int64Property"))
            {
                line += L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<std::int64_t>(container));
            }
            else if (type_name == STR("UInt32Property"))
            {
                line += L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<std::uint32_t>(container));
            }
            else if (type_name == STR("FloatProperty"))
            {
                line += L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<float>(container));
            }
            else if (type_name == STR("DoubleProperty"))
            {
                line += L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<double>(container));
            }
            else if (type_name == STR("ByteProperty") || type_name == STR("EnumProperty"))
            {
                line += L" = " + std::to_wstring(*property->ContainerPtrToValuePtr<std::uint8_t>(container));
            }
            else if (type_name == STR("NameProperty"))
            {
                line += L" = " + property->ContainerPtrToValuePtr<Unreal::FName>(container)->ToString();
            }
            else if (type_name == STR("StrProperty"))
            {
                const auto& chars = property->ContainerPtrToValuePtr<Unreal::FString>(container)->GetCharArray();
                line += L" = \"" + std::wstring(chars.Num() > 0 ? chars.GetData() : L"") + L"\"";
            }
            else if (type_name == STR("ObjectProperty") || type_name == STR("WeakObjectProperty"))
            {
                if (type_name == STR("ObjectProperty"))
                {
                    auto* value = *property->ContainerPtrToValuePtr<Unreal::UObject*>(container);
                    line += value
                        ? L" -> " + value->GetClassPrivate()->GetName() + L" '" + value->GetName() + L"'"
                        : L" -> null";
                }
            }
            else if (type_name == STR("ArrayProperty"))
            {
                auto* array_property = static_cast<Unreal::FArrayProperty*>(property);
                auto* inner = array_property->GetInner();
                auto* array = property->ContainerPtrToValuePtr<Unreal::TArray<std::uint8_t>>(container);
                line += L" [" + inner->GetClass().GetName() + L"] n=" + std::to_wstring(array->Num());
                log_line(line);

                if (inner->GetClass().GetName() == STR("ObjectProperty") && array->Num() > 0)
                {
                    auto** objects = reinterpret_cast<Unreal::UObject**>(array->GetData());
                    const auto preview = std::min(array->Num(), MaxArrayPreview);
                    for (int i = 0; i < preview; ++i)
                    {
                        auto* element = objects[i];
                        log_line(indent + L"  [" + std::to_wstring(i) + L"] " +
                                 (element ? element->GetClassPrivate()->GetName() + L" '" + element->GetName() + L"'"
                                          : L"null"));
                    }
                }
                continue;
            }

            else if (type_name == STR("MapProperty"))
            {
                auto* map_property = static_cast<Unreal::FMapProperty*>(property);
                auto* key_prop = map_property->GetKeyProp();
                auto* value_prop = map_property->GetValueProp();
                auto& layout = map_property->GetMapLayout();
                auto* map = property->ContainerPtrToValuePtr<Unreal::FScriptMap>(container);

                line += L" <" + key_prop->GetClass().GetName() + L"," + value_prop->GetClass().GetName() +
                        L"> n=" + std::to_wstring(map->Num());
                log_line(line);

                if (depth < MaxDepth)
                {
                    int shown = 0;
                    for (std::int32_t i = 0; i < map->GetMaxIndex() && shown < MaxArrayPreview; ++i)
                    {
                        if (!map->IsValidIndex(i))
                        {
                            continue;
                        }
                        ++shown;
                        void* pair = map->GetData(i, layout);

                        const auto key_leaf = format_leaf(key_prop, pair);
                        log_line(indent + L"  cle[" + std::to_wstring(i) + L"] = " +
                                 (key_leaf.empty() ? L"(" + key_prop->GetClass().GetName() + L")" : key_leaf));
                        if (key_leaf.empty() && key_prop->GetClass().GetName() == STR("StructProperty"))
                        {
                            auto key_struct = static_cast<Unreal::FStructProperty*>(key_prop)->GetStruct();
                            dump_properties(key_struct.Get(), key_prop->ContainerPtrToValuePtr<void>(pair),
                                            indent + L"    ", depth + 1);
                        }

                        const auto value_leaf = format_leaf(value_prop, pair);
                        log_line(indent + L"  val[" + std::to_wstring(i) + L"] = " +
                                 (value_leaf.empty() ? L"(" + value_prop->GetClass().GetName() + L")" : value_leaf));
                        if (value_leaf.empty() && value_prop->GetClass().GetName() == STR("StructProperty"))
                        {
                            auto value_struct = static_cast<Unreal::FStructProperty*>(value_prop)->GetStruct();
                            dump_properties(value_struct.Get(), value_prop->ContainerPtrToValuePtr<void>(pair),
                                            indent + L"    ", depth + 1);
                        }
                    }
                }
                continue;
            }

            log_line(line);

            if (type_name == STR("StructProperty") && depth < MaxDepth)
            {
                auto* struct_property = static_cast<Unreal::FStructProperty*>(property);
                auto inner_struct = struct_property->GetStruct();
                log_line(indent + L"  { struct " + inner_struct->GetName());
                dump_properties(inner_struct.Get(), property->ContainerPtrToValuePtr<void>(container),
                                indent + L"    ", depth + 1);
                log_line(indent + L"  }");
            }
        }
    }
}
