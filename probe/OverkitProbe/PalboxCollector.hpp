#pragma once

#include <string>

namespace Overkit
{
    // Collecteur Palbox (§3.1) : parcourt le conteneur de 960 slots du joueur
    // par réflexion pure et produit le domaine JSON `palbox`. Chaîne validée
    // in-game (2026-08-02, build 1.10.1103.0) :
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
        // À appeler sur le thread jeu. Retourne le JSON du domaine palbox
        // ({"status":...}) ou une chaîne vide si rien de neuf à publier.
        // Cadence : scan complet toutes les 30 s (resync du cahier des
        // charges ; l'event-driven viendra avec les hooks).
        auto collect_if_due() -> std::string;
    };
}
