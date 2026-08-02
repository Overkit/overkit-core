#include "WorldCollectors.hpp"

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
            find_property(parameter->GetClassPrivate(), STR("SaveParameter")));
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
            if (!save || guid_at(save_struct, save, {STR("SlotId"), STR("ContainerId"), STR("ID")}) != guid)
            {
                continue;
            }

            std::string instance_id = "unknown";
            void* key = key_prop->ContainerPtrToValuePtr<void>(pair);
            if (auto* id_prop = find_property(key_struct, STR("InstanceId")))
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
        Unreal::UObjectGlobals::FindAllOf(STR("PalBaseCampModel"), camps);
        for (auto* camp : camps)
        {
            auto* director = read_object(camp, STR("WorkerDirector"));
            auto* container = director ? read_object(director, STR("CharacterContainer")) : nullptr;
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
            Unreal::UObjectGlobals::FindAllOf(STR("PalBaseCampModel"), camps);
            if (camps.empty())
            {
                return R"({"status":"unavailable"})";
            }

            std::string list;
            for (auto* camp : camps)
            {
                const auto base_id = guid_at(camp->GetClassPrivate(), camp, {STR("ID")});
                std::string position = "null";
                if (void* translation = resolve_struct_path(camp->GetClassPrivate(), camp,
                                                            {STR("Transform"), STR("Translation")}))
                {
                    const auto* v = static_cast<Vector3d*>(translation);
                    position = std::format(R"({{"x":{:.1f},"y":{:.1f},"z":{:.1f}}})", v->X, v->Y, v->Z);
                }

                std::string workers;
                auto* director = read_object(camp, STR("WorkerDirector"));
                auto* container = director ? read_object(director, STR("CharacterContainer")) : nullptr;
                if (container)
                {
                    const auto guid = container_guid(container);
                    for_each_in_container(guid, [&](const std::string& instance_id, Unreal::UObject*,
                                                    Unreal::UStruct* save_struct, void* save) {
                        const auto stomach = read_save_float(save_struct, save, STR("FullStomach"), -1.f);
                        const auto max_stomach = read_save_float(save_struct, save, STR("MaxFullStomach"), -1.f);
                        const auto sanity = read_save_float(save_struct, save, STR("SanityValue"), -1.f);
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
            auto* player_state = Unreal::UObjectGlobals::FindFirstOf(STR("PalPlayerState"));
            auto* inventory_data = player_state ? read_object(player_state, STR("InventoryData")) : nullptr;
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
                find_property(inventory_data->GetClassPrivate(), STR("MyInventoryInfo")));
            if (!info_prop)
            {
                return R"({"status":"unavailable"})";
            }
            void* info = info_prop->ContainerPtrToValuePtr<void>(inventory_data);
            auto* info_struct = info_prop->GetStruct().Get();
            const std::pair<const wchar_t*, const char*> player_containers[] = {
                {STR("CommonContainerId"), "player"},
                {STR("EssentialContainerId"), "key_items"},
                {STR("FoodEquipContainerId"), "food_box"},
            };
            for (const auto& [name, kind] : player_containers)
            {
                const auto guid = guid_at(info_struct, info, {name, STR("ID")});
                if (!guid.is_zero())
                {
                    wanted.push_back({guid, kind});
                }
            }

            // Coffres : conteneurs appartenant au groupe (guilde) d'une base.
            std::vector<Guid128> guild_ids;
            {
                std::vector<Unreal::UObject*> camps{};
                Unreal::UObjectGlobals::FindAllOf(STR("PalBaseCampModel"), camps);
                for (auto* camp : camps)
                {
                    const auto gid = guid_at(camp->GetClassPrivate(), camp, {STR("GroupIdBelongTo")});
                    if (!gid.is_zero())
                    {
                        guild_ids.push_back(gid);
                    }
                }
            }

            std::vector<Unreal::UObject*> containers{};
            Unreal::UObjectGlobals::FindAllOf(STR("PalItemContainer"), containers);

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
                                               {STR("BelongInfo"), STR("GroupId")});
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

                auto* slots = container->GetValuePtrByPropertyNameInChain<Unreal::TArray<Unreal::UObject*>>(STR("ItemSlotArray"));
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
                    if (void* item = resolve_struct_path(slot->GetClassPrivate(), slot, {STR("ItemId")}))
                    {
                        auto* item_prop = static_cast<Unreal::FStructProperty*>(
                            find_property(slot->GetClassPrivate(), STR("ItemId")));
                        if (auto* static_prop = find_property(item_prop->GetStruct().Get(), STR("StaticId")))
                        {
                            item_id = static_prop->ContainerPtrToValuePtr<Unreal::FName>(item)->ToString();
                        }
                    }
                    const auto* count_prop = find_property(slot->GetClassPrivate(), STR("StackCount"));
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
            Unreal::UObjectGlobals::FindAllOf(STR("PalCharacter"), characters);
            if (characters.empty())
            {
                return R"({"status":"unavailable"})";
            }

            std::string actors;
            for (auto* character : characters)
            {
                auto* component = read_object(character, STR("CharacterParameterComponent"));
                auto* parameter = component ? read_object(component, STR("IndividualParameter")) : nullptr;
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
                if (auto* p = find_property(save_struct, STR("CharacterID")))
                {
                    species = p->ContainerPtrToValuePtr<Unreal::FName>(save)->ToString();
                }
                bool is_player = false;
                if (auto* p = find_property(save_struct, STR("IsPlayer")))
                {
                    is_player = *p->ContainerPtrToValuePtr<std::uint8_t>(save) != 0;
                }
                if (species.empty() || is_player)
                {
                    continue;
                }

                int level = -1;
                if (auto* p = find_property(save_struct, STR("Level")))
                {
                    level = *p->ContainerPtrToValuePtr<std::uint8_t>(save);
                }

                std::string position = "null";
                double distance = -1;
                auto* root = read_object(character, STR("RootComponent"));
                if (root)
                {
                    if (auto* loc = root->GetValuePtrByPropertyNameInChain<Vector3d>(STR("RelativeLocation")))
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
