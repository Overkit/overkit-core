#include "WorldCollectors.hpp"
#include "Mapping.hpp"

#include <cmath>
#include <format>

using namespace RC;
using namespace Overkit::Reflect;

namespace
{
    // Lit un float de SaveParameter (certaines jauges y sont en float simple).
    auto read_save_float(Unreal::UStruct* save_struct, void* save, const wchar_t* name, float fallback) -> float
    {
        if (auto* p = find_property(save_struct, name); p && p->GetClass().GetName() == STR("FloatProperty"))
        {
            return *p->ContainerPtrToValuePtr<float>(save);
        }
        return fallback;
    }

    // Accès au SaveParameter d'un PalIndividualCharacterParameter.
    auto save_of(Unreal::UObject* parameter, Unreal::UStruct** save_struct) -> void*
    {
        auto* save_prop = static_cast<Unreal::FStructProperty*>(
            find_property(parameter->GetClassPrivate(), OVKM("prop.save_parameter", "SaveParameter")));
        if (!save_prop)
        {
            return nullptr;
        }
        *save_struct = save_prop->GetStruct().Get();
        return save_prop->ContainerPtrToValuePtr<void>(parameter);
    }

    auto guid_at(Unreal::UStruct* type, void* container, std::initializer_list<const wchar_t*> path) -> Guid128
    {
        void* ptr = resolve_struct_path(type, container, path);
        return ptr ? *static_cast<Guid128*>(ptr) : Guid128{};
    }

    // Parcourt IndividualParameterMap et appelle f(instance_id, parameter)
    // pour chaque entrée dont SaveParameter.SlotId.ContainerId == guid.
    template <typename F>
    auto for_each_in_container(const Guid128& guid, F&& f) -> bool
    {
        auto* manager = Unreal::UObjectGlobals::FindFirstOf(OVKM("class.character_manager", "PalCharacterManager"));
        if (!manager)
        {
            return false;
        }
        auto* map_property = static_cast<Unreal::FMapProperty*>(
            find_property(manager->GetClassPrivate(), OVKM("prop.individual_parameter_map", "IndividualParameterMap")));
        if (!map_property)
        {
            return false;
        }
        auto* map = map_property->ContainerPtrToValuePtr<Unreal::FScriptMap>(manager);
        auto& layout = map_property->GetMapLayout();
        auto* key_prop = map_property->GetKeyProp();
        auto* value_prop = map_property->GetValueProp();
        auto* key_struct = static_cast<Unreal::FStructProperty*>(key_prop)->GetStruct().Get();

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

            Unreal::UStruct* save_struct = nullptr;
            void* save = save_of(parameter, &save_struct);
            if (!save || guid_at(save_struct, save, {OVKM("prop.slot_id", "SlotId"), OVKM("prop.container_id", "ContainerId"), OVKM("prop.id", "ID")}) != guid)
            {
                continue;
            }

            std::string instance_id = "unknown";
            void* key = key_prop->ContainerPtrToValuePtr<void>(pair);
            if (auto* id_prop = find_property(key_struct, OVKM("prop.instance_id", "InstanceId")))
            {
                instance_id = id_prop->ContainerPtrToValuePtr<Guid128>(key)->to_string();
            }
            f(instance_id, parameter, save_struct, save);
        }
        return true;
    }
}

namespace Overkit::WorldCollectors
{
    auto base_worker_containers() -> std::vector<Unreal::UObject*>
    {
        std::vector<Unreal::UObject*> containers;
        std::vector<Unreal::UObject*> camps{};
        Unreal::UObjectGlobals::FindAllOf(OVKM("class.base_camp", "PalBaseCampModel"), camps);
        for (auto* camp : camps)
        {
            auto* director = read_object(camp, OVKM("prop.worker_director", "WorkerDirector"));
            auto* container = director ? read_object(director, OVKM("prop.character_container", "CharacterContainer")) : nullptr;
            if (container && !container_guid(container).is_zero())
            {
                containers.push_back(container);
            }
        }
        return containers;
    }

    auto collect_bases_json() -> std::string
    {
        try
        {
            std::vector<Unreal::UObject*> camps{};
            Unreal::UObjectGlobals::FindAllOf(OVKM("class.base_camp", "PalBaseCampModel"), camps);
            if (camps.empty())
            {
                return R"({"status":"unavailable"})";
            }

            std::string list;
            for (auto* camp : camps)
            {
                const auto base_id = guid_at(camp->GetClassPrivate(), camp, {OVKM("prop.id", "ID")});
                std::string position = "null";
                if (void* translation = resolve_struct_path(camp->GetClassPrivate(), camp,
                                                            {OVKM("prop.transform", "Transform"), OVKM("prop.translation", "Translation")}))
                {
                    const auto* v = static_cast<Vector3d*>(translation);
                    position = std::format(R"({{"x":{:.1f},"y":{:.1f},"z":{:.1f}}})", v->X, v->Y, v->Z);
                }

                std::string workers;
                auto* director = read_object(camp, OVKM("prop.worker_director", "WorkerDirector"));
                auto* container = director ? read_object(director, OVKM("prop.character_container", "CharacterContainer")) : nullptr;
                if (container)
                {
                    const auto guid = container_guid(container);
                    for_each_in_container(guid, [&](const std::string& instance_id, Unreal::UObject*,
                                                    Unreal::UStruct* save_struct, void* save) {
                        const auto stomach = read_save_float(save_struct, save, OVKM("prop.full_stomach", "FullStomach"), -1.f);
                        const auto max_stomach = read_save_float(save_struct, save, OVKM("prop.max_full_stomach", "MaxFullStomach"), -1.f);
                        const auto sanity = read_save_float(save_struct, save, OVKM("prop.sanity_value", "SanityValue"), -1.f);
                        if (!workers.empty())
                        {
                            workers += ',';
                        }
                        workers += std::format(
                            R"({{"instance_id":"{}","hunger":{{"current":{:.1f},"max":{:.1f}}},)"
                            R"("sanity":{{"current":{:.1f},"max":100.0}}}})",
                            instance_id, stomach, max_stomach, sanity);
                    });
                }

                if (!list.empty())
                {
                    list += ',';
                }
                list += std::format(R"({{"base_id":"{}","position":{},"workers":[{}]}})",
                                    base_id.to_string(), position, workers);
            }
            return R"({"status":"ok","list":[)" + list + "]}";
        }
        catch (...)
        {
            return R"({"status":"unavailable"})";
        }
    }

    auto collect_inventory_json() -> std::string
    {
        try
        {
            auto* player_state = Unreal::UObjectGlobals::FindFirstOf(OVKM("class.player_state", "PalPlayerState"));
            auto* inventory_data = player_state ? read_object(player_state, OVKM("prop.inventory_data", "InventoryData")) : nullptr;
            if (!inventory_data)
            {
                return R"({"status":"unavailable"})";
            }

            // Conteneurs du joueur : id -> kind du State Bus.
            struct Wanted
            {
                Guid128 guid;
                const char* kind;
            };
            std::vector<Wanted> wanted;
            auto* info_prop = static_cast<Unreal::FStructProperty*>(
                find_property(inventory_data->GetClassPrivate(), OVKM("prop.my_inventory_info", "MyInventoryInfo")));
            if (!info_prop)
            {
                return R"({"status":"unavailable"})";
            }
            void* info = info_prop->ContainerPtrToValuePtr<void>(inventory_data);
            auto* info_struct = info_prop->GetStruct().Get();
            const std::pair<const wchar_t*, const char*> player_containers[] = {
                {OVKM("prop.common_container_id", "CommonContainerId"), "player"},
                {OVKM("prop.essential_container_id", "EssentialContainerId"), "key_items"},
                {OVKM("prop.food_equip_container_id", "FoodEquipContainerId"), "food_box"},
            };
            for (const auto& [name, kind] : player_containers)
            {
                const auto guid = guid_at(info_struct, info, {name, OVKM("prop.id", "ID")});
                if (!guid.is_zero())
                {
                    wanted.push_back({guid, kind});
                }
            }

            // Coffres : conteneurs appartenant au groupe (guilde) d'une base.
            std::vector<Guid128> guild_ids;
            {
                std::vector<Unreal::UObject*> camps{};
                Unreal::UObjectGlobals::FindAllOf(OVKM("class.base_camp", "PalBaseCampModel"), camps);
                for (auto* camp : camps)
                {
                    const auto gid = guid_at(camp->GetClassPrivate(), camp, {OVKM("prop.group_id_belong_to", "GroupIdBelongTo")});
                    if (!gid.is_zero())
                    {
                        guild_ids.push_back(gid);
                    }
                }
            }

            std::vector<Unreal::UObject*> containers{};
            Unreal::UObjectGlobals::FindAllOf(OVKM("class.item_container", "PalItemContainer"), containers);

            std::string out;
            for (auto* container : containers)
            {
                const auto guid = container_guid(container);
                const char* kind = nullptr;
                for (const auto& w : wanted)
                {
                    if (w.guid == guid)
                    {
                        kind = w.kind;
                        break;
                    }
                }
                if (!kind)
                {
                    const auto group = guid_at(container->GetClassPrivate(), container,
                                               {OVKM("prop.belong_info", "BelongInfo"), OVKM("prop.group_id", "GroupId")});
                    for (const auto& gid : guild_ids)
                    {
                        if (group == gid)
                        {
                            kind = "chest";
                            break;
                        }
                    }
                }
                if (!kind)
                {
                    continue;
                }

                auto* slots = container->GetValuePtrByPropertyNameInChain<Unreal::TArray<Unreal::UObject*>>(OVKM("prop.item_slot_array", "ItemSlotArray"));
                if (!slots)
                {
                    continue;
                }
                std::string items;
                for (std::int32_t i = 0; i < slots->Num(); ++i)
                {
                    auto* slot = (*slots)[i];
                    if (!slot)
                    {
                        continue;
                    }
                    std::wstring item_id;
                    if (void* item = resolve_struct_path(slot->GetClassPrivate(), slot, {OVKM("prop.item_id", "ItemId")}))
                    {
                        auto* item_prop = static_cast<Unreal::FStructProperty*>(
                            find_property(slot->GetClassPrivate(), OVKM("prop.item_id", "ItemId")));
                        if (auto* static_prop = find_property(item_prop->GetStruct().Get(), OVKM("prop.static_id", "StaticId")))
                        {
                            item_id = static_prop->ContainerPtrToValuePtr<Unreal::FName>(item)->ToString();
                        }
                    }
                    const auto* count_prop = find_property(slot->GetClassPrivate(), OVKM("prop.stack_count", "StackCount"));
                    const auto count = count_prop
                        ? *count_prop->ContainerPtrToValuePtr<std::int32_t>(slot)
                        : 0;
                    if (item_id.empty() || item_id == L"None" || count <= 0)
                    {
                        continue;
                    }
                    if (!items.empty())
                    {
                        items += ',';
                    }
                    items += std::format(R"({{"item_id":"{}","count":{}}})", json_escape(item_id), count);
                }

                if (!out.empty())
                {
                    out += ',';
                }
                out += std::format(R"({{"container_id":"{}","kind":"{}","slots":[{}]}})",
                                   guid.to_string(), kind, items);
            }
            return R"({"status":"ok","containers":[)" + out + "]}";
        }
        catch (...)
        {
            return R"({"status":"unavailable"})";
        }
    }

    auto collect_nearby_json(const Vector3d& player_position) -> std::string
    {
        try
        {
            std::vector<Unreal::UObject*> characters{};
            Unreal::UObjectGlobals::FindAllOf(OVKM("class.character", "PalCharacter"), characters);
            if (characters.empty())
            {
                return R"({"status":"unavailable"})";
            }

            std::string actors;
            for (auto* character : characters)
            {
                auto* component = read_object(character, OVKM("prop.character_parameter_component", "CharacterParameterComponent"));
                auto* parameter = component ? read_object(component, OVKM("prop.individual_parameter", "IndividualParameter")) : nullptr;
                if (!parameter)
                {
                    continue;
                }
                Unreal::UStruct* save_struct = nullptr;
                void* save = save_of(parameter, &save_struct);
                if (!save)
                {
                    continue;
                }

                std::wstring species;
                if (auto* p = find_property(save_struct, OVKM("prop.character_id", "CharacterID")))
                {
                    species = p->ContainerPtrToValuePtr<Unreal::FName>(save)->ToString();
                }
                bool is_player = false;
                if (auto* p = find_property(save_struct, OVKM("prop.is_player", "IsPlayer")))
                {
                    is_player = *p->ContainerPtrToValuePtr<std::uint8_t>(save) != 0;
                }
                if (species.empty() || is_player)
                {
                    continue;
                }

                int level = -1;
                if (auto* p = find_property(save_struct, OVKM("prop.level", "Level")))
                {
                    level = *p->ContainerPtrToValuePtr<std::uint8_t>(save);
                }

                std::string position = "null";
                double distance = -1;
                auto* root = read_object(character, OVKM("prop.root_component", "RootComponent"));
                if (root)
                {
                    if (auto* loc = root->GetValuePtrByPropertyNameInChain<Vector3d>(OVKM("prop.relative_location", "RelativeLocation")))
                    {
                        position = std::format(R"({{"x":{:.1f},"y":{:.1f},"z":{:.1f}}})",
                                               loc->X, loc->Y, loc->Z);
                        distance = std::sqrt(std::pow(loc->X - player_position.X, 2) +
                                             std::pow(loc->Y - player_position.Y, 2) +
                                             std::pow(loc->Z - player_position.Z, 2));
                    }
                }

                if (!actors.empty())
                {
                    actors += ',';
                }
                actors += std::format(
                    R"({{"species_id":"{}","level":{},"position":{},"distance":{:.0f}}})",
                    json_escape(species), level, position, distance);
            }
            return R"({"status":"ok","actors":[)" + actors + "]}";
        }
        catch (...)
        {
            return R"({"status":"unavailable"})";
        }
    }
}
