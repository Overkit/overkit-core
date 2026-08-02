-- Spike Phase 0 (variante Lua) — validation de la lecture par réflexion.
-- Lecture seule stricte (P1) : uniquement des résolutions d'objets par nom et
-- des lectures de propriétés. Aucun appel de fonction de gameplay.
-- Sortie : log UE4SS ([OverkitSpike] ...), en attendant le transport WebSocket
-- de la Sonde C++.

local UEHelpers = require("UEHelpers")

local function log(fmt, ...)
    print(string.format("[OverkitSpike] " .. fmt .. "\n", ...))
end

log("script charge, exploration des objets candidats dans 10 s")

-- Découverte des classes candidates pour l'heure in-game et l'état du monde :
-- on résout par nom (comme le fera mapping.json) et on logge ce qui existe.
local candidates = {
    "PalTimeManager",
    "PalWorldTimeManager",
    "PalGameStateInGame",
    "PalStateController",
    "PalPlayerState",
}

-- Les objets candidats n'existent qu'une fois une partie chargée : on re-scanne
-- toutes les 15 s jusqu'à en trouver au moins un (ou 40 tentatives max).
local scanAttempts = 0
LoopAsync(15000, function()
    local foundAny = false
    ExecuteInGameThread(function()
        scanAttempts = scanAttempts + 1
        for _, name in ipairs(candidates) do
            local ok, err = pcall(function()
                local obj = FindFirstOf(name)
                if obj and obj:IsValid() then
                    log("candidat present : %s -> %s", name, obj:GetFullName())
                    foundAny = true
                else
                    log("candidat absent  : %s (scan %d)", name, scanAttempts)
                end
            end)
            if not ok then
                log("candidat erreur  : %s (%s)", name, tostring(err))
            end
        end
    end)
    return foundAny or scanAttempts >= 40
end)

-- Position joueur toutes les 2 s, par lecture de propriétés
-- (RootComponent.RelativeLocation — pas d'appel de fonction).
LoopAsync(2000, function()
    ExecuteInGameThread(function()
        local ok, err = pcall(function()
            local pawn = UEHelpers.GetPlayer()
            if not pawn or not pawn:IsValid() then
                log("pas de pawn joueur (menu ou chargement)")
                return
            end

            local root = pawn.RootComponent
            if not root or not root:IsValid() then
                log("pawn sans RootComponent")
                return
            end

            local loc = root.RelativeLocation
            log("pawn=%s pos X=%.0f Y=%.0f Z=%.0f",
                pawn:GetClass():GetFName():ToString(), loc.X, loc.Y, loc.Z)
        end)
        if not ok then
            log("erreur lecture : %s", tostring(err))
        end
    end)
    return false -- false = continuer la boucle
end)
