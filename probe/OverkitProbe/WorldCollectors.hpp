#pragma once

#include <string>
#include <vector>

#include "Reflect.hpp"

namespace Overkit
{
    // Collecteurs des domaines bases, inventory et nearby (§3.1).
    // Chaînes validées in-game (2026-08-02, build 1.10.1103.0) :
    //   bases     : PalBaseCampModel (ID, Transform.Translation) +
    //               WorkerDirector.CharacterContainer (slots des travailleurs)
    //   inventory : PalPlayerState.InventoryData.MyInventoryInfo.*ContainerId
    //               -> PalItemContainer.ItemSlotArray (StaticId, StackCount) ;
    //               coffres = conteneurs du groupe de guilde des bases
    //   nearby    : PalCharacter.CharacterParameterComponent.IndividualParameter
    namespace WorldCollectors
    {
        // Conteneurs de travailleurs de toutes les bases — pour que le domaine
        // palbox couvre bien tous les Pals possédés.
        auto base_worker_containers() -> std::vector<RC::Unreal::UObject*>;

        auto collect_bases_json() -> std::string;
        auto collect_inventory_json() -> std::string;
        auto collect_nearby_json(const Reflect::Vector3d& player_position) -> std::string;
    }
}
