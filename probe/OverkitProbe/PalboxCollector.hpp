#pragma once

#include <string>

namespace Overkit
{
    // Collecteur Palbox (§3.1) : parcourt le conteneur de 960 slots du joueur
    // par réflexion pure et produit le domaine JSON `palbox`. Chaîne de
    // résolution (vérifiée sur le build 1.10.1103.0) :
    //   PalPlayerState.PalStorage → PalPlayerDataPalStorage
    //     .TargetContainer → PalIndividualCharacterContainer
    //       .SlotArray[] → PalIndividualCharacterSlot
    //         .ReplicateHandleID.InstanceId (Guid)
    //         .ReplicateIndividualParameter → PalIndividualCharacterParameter
    //           .SaveParameter : CharacterID, Gender, Level, NickName,
    //                            Talent_HP/Melee/Shot/Defense, PassiveSkillList
    class PalboxCollector
    {
    public:
        // À appeler sur le thread jeu. Remplit les JSON des domaines palbox
        // (tous les Pals possédés : boîte + équipe) et party (instance_ids de
        // l'équipe active, via PalOtomoHolderComponentBase.CharacterContainer)
        // et retourne true quand un scan a eu lieu. Cadence : 30 s (resync du
        // cahier des charges ; l'event-driven viendra avec les hooks).
        auto collect_if_due(std::string& palbox_json, std::string& party_json) -> bool;
    };
}
