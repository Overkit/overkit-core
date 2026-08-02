#include "PalboxCollector.hpp"

#include <chrono>
#include <cstdio>
#include <format>

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
    std::chrono::steady_clock::time_point g_last_scan{};

    struct Guid128
    {
        std::int32_t A, B, C, D;
    };

    auto find_property(Unreal::UStruct* type, const wchar_t* name) -> Unreal::FProperty*
    {
        for (auto* property : type->ForEachPropertyInChain())
        {
            if (property->GetName() == name)
            {
                return property;
            }
        }
        return nullptr;
    }

    // Échappe le strict nécessaire pour émettre du JSON valide.
    auto json_escape(const std::wstring& input) -> std::string
    {
        std::string out;
        out.reserve(input.size());
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
                // Hors ASCII : échappement unicode BMP, suffisant pour des
                // surnoms de Pals.
                out += std::format("\\u{:04x}", static_cast<int>(wc));
            }
        }
        return out;
    }

    auto read_object(Unreal::UObject* owner, const wchar_t* property_name) -> Unreal::UObject*
    {
        auto** value = owner->GetValuePtrByPropertyNameInChain<Unreal::UObject*>(property_name);
        return value ? *value : nullptr;
    }
}

namespace Overkit
{
    auto PalboxCollector::collect_if_due() -> std::string
    {
        const auto now = std::chrono::steady_clock::now();
        if (now - g_last_scan < std::chrono::seconds(30))
        {
            return {};
        }
        g_last_scan = now;

        try
        {
            auto* player_state = Unreal::UObjectGlobals::FindFirstOf(STR("PalPlayerState"));
            if (!player_state)
            {
                return R"({"status":"unavailable"})";
            }
            auto* storage = read_object(player_state, STR("PalStorage"));
            auto* container = storage ? read_object(storage, STR("TargetContainer")) : nullptr;
            if (!container)
            {
                return R"({"status":"unavailable"})";
            }
            auto* slot_array = container->GetValuePtrByPropertyNameInChain<Unreal::TArray<Unreal::UObject*>>(STR("SlotArray"));
            if (!slot_array)
            {
                return R"({"status":"unavailable"})";
            }

            std::string pals;
            for (std::int32_t i = 0; i < slot_array->Num(); ++i)
            {
                auto* slot = (*slot_array)[i];
                if (!slot)
                {
                    continue;
                }
                auto* parameter = read_object(slot, STR("ReplicateIndividualParameter"));
                if (!parameter)
                {
                    continue; // slot vide
                }

                // SaveParameter : résolution par nom, lecture par propriété.
                auto* save_prop = static_cast<Unreal::FStructProperty*>(
                    find_property(parameter->GetClassPrivate(), STR("SaveParameter")));
                if (!save_prop)
                {
                    continue;
                }
                void* save = save_prop->ContainerPtrToValuePtr<void>(parameter);
                auto save_struct = save_prop->GetStruct();

                auto read_byte = [&](const wchar_t* name) -> int {
                    auto* p = find_property(save_struct.Get(), name);
                    return p ? *p->ContainerPtrToValuePtr<std::uint8_t>(save) : -1;
                };

                std::wstring species;
                if (auto* p = find_property(save_struct.Get(), STR("CharacterID")))
                {
                    species = p->ContainerPtrToValuePtr<Unreal::FName>(save)->ToString();
                }
                if (species.empty())
                {
                    continue;
                }

                std::wstring nickname;
                if (auto* p = find_property(save_struct.Get(), STR("NickName")))
                {
                    const auto& chars = p->ContainerPtrToValuePtr<Unreal::FString>(save)->GetCharArray();
                    if (chars.Num() > 0)
                    {
                        nickname = chars.GetData();
                    }
                }

                std::string passives;
                if (auto* p = find_property(save_struct.Get(), STR("PassiveSkillList")))
                {
                    auto* names = p->ContainerPtrToValuePtr<Unreal::TArray<Unreal::FName>>(save);
                    for (std::int32_t n = 0; n < names->Num(); ++n)
                    {
                        if (!passives.empty())
                        {
                            passives += ',';
                        }
                        passives += '"' + json_escape((*names)[n].ToString()) + '"';
                    }
                }

                std::string instance_id = "unknown";
                if (auto* handle_prop = static_cast<Unreal::FStructProperty*>(
                        find_property(slot->GetClassPrivate(), STR("ReplicateHandleID"))))
                {
                    void* handle = handle_prop->ContainerPtrToValuePtr<void>(slot);
                    if (auto* id_prop = find_property(handle_prop->GetStruct().Get(), STR("InstanceId")))
                    {
                        const auto* guid = id_prop->ContainerPtrToValuePtr<Guid128>(handle);
                        instance_id = std::format("{:08x}-{:08x}-{:08x}-{:08x}",
                                                  static_cast<std::uint32_t>(guid->A),
                                                  static_cast<std::uint32_t>(guid->B),
                                                  static_cast<std::uint32_t>(guid->C),
                                                  static_cast<std::uint32_t>(guid->D));
                    }
                }

                const auto gender_raw = read_byte(STR("Gender"));
                const char* gender = gender_raw == 1 ? "male" : gender_raw == 2 ? "female" : "unknown";

                if (!pals.empty())
                {
                    pals += ',';
                }
                pals += std::format(
                    R"({{"instance_id":"{}","species_id":"{}","nickname":"{}","gender":"{}","level":{},)"
                    R"("passives":[{}],"talents":{{"hp":{},"melee":{},"shot":{},"defense":{}}}}})",
                    instance_id, json_escape(species), json_escape(nickname), gender,
                    read_byte(STR("Level")), passives,
                    read_byte(STR("Talent_HP")), read_byte(STR("Talent_Melee")),
                    read_byte(STR("Talent_Shot")), read_byte(STR("Talent_Defense")));
            }

            return R"({"status":"ok","pals":[)" + pals + "]}";
        }
        catch (...)
        {
            // EXG-003 : jamais de crash — domaine indisponible, on réessaiera.
            return R"({"status":"unavailable"})";
        }
    }
}
