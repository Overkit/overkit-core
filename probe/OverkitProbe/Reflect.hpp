#pragma once

// Helpers de réflexion et de JSON partagés par les collecteurs de la Sonde.
// Lecture seule stricte (P1) : uniquement résolution par nom et lecture.

#include <format>
#include <initializer_list>
#include <string>
#include <vector>

#include <Unreal/UObjectGlobals.hpp>
#include <Unreal/UObject.hpp>
#include <Unreal/UClass.hpp>
#include <Unreal/CoreUObject/UObject/UnrealType.hpp>
#include <Unreal/NameTypes.hpp>
#include <Unreal/Core/Containers/Array.hpp>
#include <Unreal/Core/Containers/FString.hpp>

namespace Overkit::Reflect
{
    namespace U = RC::Unreal;

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

    struct Vector3d
    {
        double X, Y, Z;
    };

    inline auto find_property(U::UStruct* type, const wchar_t* name) -> U::FProperty*
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

    inline auto read_object(U::UObject* owner, const wchar_t* property_name) -> U::UObject*
    {
        auto** value = owner->GetValuePtrByPropertyNameInChain<U::UObject*>(property_name);
        return value ? *value : nullptr;
    }

    // Le jeu garde parfois plusieurs instances d'une même classe, dont des
    // coquilles vides (ex. deux PalPlayerState, un seul avec sa PalStorage).
    // FindFirstOf n'a aucune raison de tomber sur la bonne : on cherche la
    // première instance dont la propriété attendue est renseignée.
    inline auto find_first_with(const wchar_t* class_name, const wchar_t* property_name) -> U::UObject*
    {
        std::vector<U::UObject*> instances{};
        U::UObjectGlobals::FindAllOf(class_name, instances);
        for (auto* instance : instances)
        {
            if (instance && read_object(instance, property_name))
            {
                return instance;
            }
        }
        return nullptr;
    }

    // Résout un chemin de structs imbriquées par noms et retourne le pointeur
    // de la struct feuille (nullptr si un maillon manque).
    inline auto resolve_struct_path(U::UStruct* type, void* container,
                                    std::initializer_list<const wchar_t*> path) -> void*
    {
        for (const auto* name : path)
        {
            auto* property = find_property(type, name);
            if (!property || property->GetClass().GetName() != STR("StructProperty"))
            {
                return nullptr;
            }
            auto* struct_property = static_cast<U::FStructProperty*>(property);
            container = property->ContainerPtrToValuePtr<void>(container);
            type = struct_property->GetStruct().Get();
        }
        return container;
    }

    // Guid d'un conteneur (palbox, party, workers, items) : <obj>.ID.ID.
    inline auto container_guid(U::UObject* container) -> Guid128
    {
        void* guid = resolve_struct_path(container->GetClassPrivate(), container, {STR("ID"), STR("ID")});
        return guid ? *static_cast<Guid128*>(guid) : Guid128{};
    }

    inline auto json_escape(const std::wstring& input) -> std::string
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
}
