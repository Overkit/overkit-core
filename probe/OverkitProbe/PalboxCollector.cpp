#include "PalboxCollector.hpp"
#include "WorldCollectors.hpp"

#include <chrono>
#include <format>
#include <vector>

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

        auto operator==(const Guid128&) const -> bool = default;
        [[nodiscard]] auto is_zero() const -> bool { return A == 0 && B == 0 && C == 0 && D == 0; }

        [[nodiscard]] auto to_string() const -> std::string
        {
            return std::format("{:08x}-{:08x}-{:08x}-{:08x}",
                               static_cast<std::uint32_t>(A), static_cast<std::uint32_t>(B),
                               static_cast<std::uint32_t>(C), static_cast<std::uint32_t>(D));
        }
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
            else if (wc < 0x20 || wc >= 0x80)
            {
                out += std::format("\\u{:04x}", static_cast<int>(wc));
            }
            else
            {
                out.push_back(static_cast<char>(wc));
            }
        }
        return out;
    }

    auto read_object(Unreal::UObject* owner, const wchar_t* property_name) -> Unreal::UObject*
    {
        auto** value = owner->GetValuePtrByPropertyNameInChain<Unreal::UObject*>(property_name);
        return value ? *value : nullptr;
    }

    // Résout un chemin de structs imbriquées par noms depuis un conteneur, et
    // retourne le pointeur de la struct feuille (nullptr si un maillon manque).
    auto resolve_struct_path(Unreal::UStruct* type, void* container,
                             std::initializer_list<const wchar_t*> path,
                             Unreal::UStruct** leaf_type) -> void*
    {
        for (const auto* name : path)
        {
            auto* property = find_property(type, name);
            if (!property || property->GetClass().GetName() != STR("StructProperty"))
            {
                return nullptr;
            }
            auto* struct_property = static_cast<Unreal::FStructProperty*>(property);
            container = property->ContainerPtrToValuePtr<void>(container);
            type = struct_property->GetStruct().Get();
        }
        *leaf_type = type;
        return container;
    }

    // Lit le Guid du conteneur Palbox : container.ID (PalContainerId) .ID (Guid).
    auto read_container_guid(Unreal::UObject* container) -> Guid128
    {
        Unreal::UStruct* leaf = nullptr;
        void* guid_ptr = resolve_struct_path(container->GetClassPrivate(), container, {STR("ID"), STR("ID")}, &leaf);
        return guid_ptr ? *static_cast<Guid128*>(guid_ptr) : Guid128{};
    }

    // Émet le JSON d'un Pal depuis son PalIndividualCharacterParameter.
    // Retourne une chaîne vide si le paramètre n'est pas exploitable.
    auto emit_pal(Unreal::UObject* parameter, const std::string& instance_id) -> std::string
    {
        auto* save_prop = static_cast<Unreal::FStructProperty*>(
            find_property(parameter->GetClassPrivate(), STR("SaveParameter")));
        if (!save_prop)
        {
            return {};
        }
        void* save = save_prop->ContainerPtrToValuePtr<void>(parameter);
        auto* save_struct = save_prop->GetStruct().Get();

        auto read_byte = [&](const wchar_t* name) -> int {
            auto* p = find_property(save_struct, name);
            return p ? *p->ContainerPtrToValuePtr<std::uint8_t>(save) : -1;
        };

        std::wstring species;
        if (auto* p = find_property(save_struct, STR("CharacterID")))
        {
            species = p->ContainerPtrToValuePtr<Unreal::FName>(save)->ToString();
        }
        if (species.empty() || read_byte(STR("IsPlayer")) > 0)
        {
            return {};
        }

        std::wstring nickname;
        if (auto* p = find_property(save_struct, STR("NickName")))
        {
            const auto& chars = p->ContainerPtrToValuePtr<Unreal::FString>(save)->GetCharArray();
            if (chars.Num() > 0)
            {
                nickname = chars.GetData();
            }
        }

        std::string passives;
        if (auto* p = find_property(save_struct, STR("PassiveSkillList")))
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

        const auto gender_raw = read_byte(STR("Gender"));
        const char* gender = gender_raw == 1 ? "male" : gender_raw == 2 ? "female" : "unknown";

        return std::format(
            R"({{"instance_id":"{}","species_id":"{}","nickname":"{}","gender":"{}","level":{},)"
            R"("passives":[{}],"talents":{{"hp":{},"melee":{},"shot":{},"defense":{}}}}})",
            instance_id, json_escape(species), json_escape(nickname), gender,
            read_byte(STR("Level")), passives,
            read_byte(STR("Talent_HP")), read_byte(STR("Talent_Melee")),
            read_byte(STR("Talent_Shot")), read_byte(STR("Talent_Defense")));
    }

    // Le SlotId d'un SaveParameter pointe-t-il vers le conteneur donné ?
    auto save_belongs_to(Unreal::UObject* parameter, const Guid128& container_guid) -> bool
    {
        auto* save_prop = static_cast<Unreal::FStructProperty*>(
            find_property(parameter->GetClassPrivate(), STR("SaveParameter")));
        if (!save_prop)
        {
            return false;
        }
        Unreal::UStruct* leaf = nullptr;
        void* guid_ptr = resolve_struct_path(save_prop->GetStruct().Get(),
                                             save_prop->ContainerPtrToValuePtr<void>(parameter),
                                             {STR("SlotId"), STR("ContainerId"), STR("ID")}, &leaf);
        return guid_ptr && *static_cast<Guid128*>(guid_ptr) == container_guid;
    }

    // Compte les slots occupés du conteneur via ReplicateHandleID.InstanceId,
    // renseigné pour tout slot occupé même sans réplication du paramètre :
    // donne le vrai total possédé, synchronisé ou non.
    auto count_occupied_slots(Unreal::UObject* container) -> int
    {
        auto* slot_array = container->GetValuePtrByPropertyNameInChain<Unreal::TArray<Unreal::UObject*>>(STR("SlotArray"));
        if (!slot_array)
        {
            return -1;
        }
        int occupied = 0;
        for (std::int32_t i = 0; i < slot_array->Num(); ++i)
        {
            auto* slot = (*slot_array)[i];
            if (!slot)
            {
                continue;
            }
            auto* handle_prop = static_cast<Unreal::FStructProperty*>(
                find_property(slot->GetClassPrivate(), STR("ReplicateHandleID")));
            if (!handle_prop)
            {
                continue;
            }
            void* handle = handle_prop->ContainerPtrToValuePtr<void>(slot);
            if (auto* id_prop = find_property(handle_prop->GetStruct().Get(), STR("InstanceId")))
            {
                if (!id_prop->ContainerPtrToValuePtr<Guid128>(handle)->is_zero())
                {
                    ++occupied;
                }
            }
        }
        return occupied;
    }

    // Instance_ids des slots occupés d'un conteneur (ordre des slots).
    auto container_member_ids(Unreal::UObject* container) -> std::string
    {
        std::string out;
        auto* slot_array = container->GetValuePtrByPropertyNameInChain<Unreal::TArray<Unreal::UObject*>>(STR("SlotArray"));
        if (!slot_array)
        {
            return out;
        }
        for (std::int32_t i = 0; i < slot_array->Num(); ++i)
        {
            auto* slot = (*slot_array)[i];
            if (!slot)
            {
                continue;
            }
            auto* handle_prop = static_cast<Unreal::FStructProperty*>(
                find_property(slot->GetClassPrivate(), STR("ReplicateHandleID")));
            if (!handle_prop)
            {
                continue;
            }
            void* handle = handle_prop->ContainerPtrToValuePtr<void>(slot);
            if (auto* id_prop = find_property(handle_prop->GetStruct().Get(), STR("InstanceId")))
            {
                const auto* guid = id_prop->ContainerPtrToValuePtr<Guid128>(handle);
                if (!guid->is_zero())
                {
                    if (!out.empty())
                    {
                        out += ',';
                    }
                    out += '"' + guid->to_string() + '"';
                }
            }
        }
        return out;
    }

    // Conteneur de l'équipe active : PalOtomoHolderComponentBase.CharacterContainer.
    auto find_party_container() -> Unreal::UObject*
    {
        auto* holder = Unreal::UObjectGlobals::FindFirstOf(STR("PalOtomoHolderComponentBase"));
        return holder ? read_object(holder, STR("CharacterContainer")) : nullptr;
    }

    // Voie principale : IndividualParameterMap du PalCharacterManager (source
    // serveur complète, indépendante de la réplication paresseuse des slots).
    auto collect_from_manager(const std::vector<Guid128>& targets, std::string& pals) -> bool
    {
        auto* manager = Unreal::UObjectGlobals::FindFirstOf(STR("PalCharacterManager"));
        if (!manager)
        {
            return false;
        }
        auto* map_property = static_cast<Unreal::FMapProperty*>(
            find_property(manager->GetClassPrivate(), STR("IndividualParameterMap")));
        if (!map_property)
        {
            return false;
        }
        auto* map = map_property->ContainerPtrToValuePtr<Unreal::FScriptMap>(manager);
        auto& layout = map_property->GetMapLayout();
        auto* key_prop = map_property->GetKeyProp();
        auto* value_prop = map_property->GetValueProp();

        for (std::int32_t i = 0; i < map->GetMaxIndex(); ++i)
        {
            if (!map->IsValidIndex(i))
            {
                continue;
            }
            void* pair = map->GetData(i, layout);
            auto* parameter = *value_prop->ContainerPtrToValuePtr<Unreal::UObject*>(pair);
            if (!parameter)
            {
                continue;
            }
            bool matched = false;
            for (const auto& target : targets)
            {
                if (save_belongs_to(parameter, target))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                continue;
            }

            std::string instance_id = "unknown";
            if (key_prop->GetClass().GetName() == STR("StructProperty"))
            {
                auto* key_struct = static_cast<Unreal::FStructProperty*>(key_prop);
                void* key = key_prop->ContainerPtrToValuePtr<void>(pair);
                if (auto* id_prop = find_property(key_struct->GetStruct().Get(), STR("InstanceId")))
                {
                    instance_id = id_prop->ContainerPtrToValuePtr<Guid128>(key)->to_string();
                }
            }

            const auto pal = emit_pal(parameter, instance_id);
            if (!pal.empty())
            {
                if (!pals.empty())
                {
                    pals += ',';
                }
                pals += pal;
            }
        }
        return true;
    }

    // Voie de secours : slots répliqués uniquement (pages de Palbox déjà
    // affichées). Utilisée si le manager serveur est hors de portée (client
    // d'un serveur distant) — statut `degraded`.
    auto collect_from_slots(Unreal::UObject* container, std::string& pals) -> void
    {
        auto* slot_array = container->GetValuePtrByPropertyNameInChain<Unreal::TArray<Unreal::UObject*>>(STR("SlotArray"));
        if (!slot_array)
        {
            return;
        }
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
                continue;
            }

            std::string instance_id = "unknown";
            if (auto* handle_prop = static_cast<Unreal::FStructProperty*>(
                    find_property(slot->GetClassPrivate(), STR("ReplicateHandleID"))))
            {
                void* handle = handle_prop->ContainerPtrToValuePtr<void>(slot);
                if (auto* id_prop = find_property(handle_prop->GetStruct().Get(), STR("InstanceId")))
                {
                    instance_id = id_prop->ContainerPtrToValuePtr<Guid128>(handle)->to_string();
                }
            }

            const auto pal = emit_pal(parameter, instance_id);
            if (!pal.empty())
            {
                if (!pals.empty())
                {
                    pals += ',';
                }
                pals += pal;
            }
        }
    }
}

namespace Overkit
{
    auto PalboxCollector::collect_if_due(std::string& palbox_json, std::string& party_json) -> bool
    {
        const auto now = std::chrono::steady_clock::now();
        if (now - g_last_scan < std::chrono::seconds(30))
        {
            return false;
        }
        g_last_scan = now;

        palbox_json = R"({"status":"unavailable"})";
        party_json = R"({"status":"unavailable"})";

        try
        {
            auto* player_state = Unreal::UObjectGlobals::FindFirstOf(STR("PalPlayerState"));
            if (!player_state)
            {
                return true;
            }
            auto* storage = read_object(player_state, STR("PalStorage"));
            auto* container = storage ? read_object(storage, STR("TargetContainer")) : nullptr;
            if (!container)
            {
                return true;
            }

            auto* party_container = find_party_container();
            const auto box_guid = read_container_guid(container);

            if (party_container)
            {
                party_json = std::format(R"({{"status":"ok","member_instance_ids":[{}]}})",
                                         container_member_ids(party_container));
            }

            // Cibles : boîte + équipe + travailleurs de toutes les bases —
            // « tous les Pals possédés » (§3.1).
            std::vector<Guid128> targets{box_guid};
            auto owned = count_occupied_slots(container);
            if (party_container)
            {
                targets.push_back(read_container_guid(party_container));
                const auto count = count_occupied_slots(party_container);
                owned += count > 0 ? count : 0;
            }
            const auto worker_containers = WorldCollectors::base_worker_containers();
            for (auto* worker_container : worker_containers)
            {
                targets.push_back(read_container_guid(worker_container));
                const auto count = count_occupied_slots(worker_container);
                owned += count > 0 ? count : 0;
            }
            std::string pals;

            bool via_manager = !box_guid.is_zero() && collect_from_manager(targets, pals);
            if (!via_manager)
            {
                pals.clear();
                collect_from_slots(container, pals);
                if (party_container)
                {
                    collect_from_slots(party_container, pals);
                }
                for (auto* worker_container : worker_containers)
                {
                    collect_from_slots(worker_container, pals);
                }
            }

            // ok = tous les Pals possédés sont lus ; degraded = lecture
            // partielle (pages de Palbox jamais affichées cette session, ou
            // voie de secours par slots répliqués).
            int synced = 0;
            for (std::size_t pos = pals.find(R"("instance_id")"); pos != std::string::npos;
                 pos = pals.find(R"("instance_id")", pos + 1))
            {
                ++synced;
            }
            const bool complete = via_manager && owned >= 0 && synced >= owned;
            palbox_json = std::format(R"({{"status":"{}","owned_count":{},"pals":[{}]}})",
                                      complete ? "ok" : "degraded", owned, pals);
            return true;
        }
        catch (...)
        {
            // EXG-003 : jamais de crash — domaines indisponibles, on réessaiera.
            palbox_json = R"({"status":"unavailable"})";
            party_json = R"({"status":"unavailable"})";
            return true;
        }
    }
}
