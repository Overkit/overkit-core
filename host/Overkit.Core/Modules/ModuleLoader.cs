using System.Reflection;
using System.Runtime.Loader;
using Overkit.Sdk;

namespace Overkit.Host.Core;

/// <summary>État d'un module chargé, tel qu'affiché à l'utilisateur.</summary>
public enum ModuleStatus
{
    Active,

    /// <summary>Compatibilité refusée au chargement (schéma, domaine requis) — EXG-070.</summary>
    Incompatible,

    /// <summary>Le module a levé une exception : désactivé, le host continue — EXG-060.</summary>
    Faulted,
}

public sealed class LoadedModule
{
    public required ModuleManifest Manifest { get; init; }
    public required IOverkitModule Instance { get; init; }
    public required string SourcePath { get; init; }
    public ModuleStatus Status { get; internal set; } = ModuleStatus.Active;

    /// <summary>Raison affichée quand le module n'est pas actif.</summary>
    public string? Reason { get; internal set; }
}

/// <summary>
/// Chargeur de modules tiers (§5.3) : chaque assembly est chargée dans son
/// propre <see cref="AssemblyLoadContext"/> collectible (isolation, déchargement
/// à chaud possible). Un module qui refuse la compatibilité ou qui lève une
/// exception est désactivé avec une raison affichée — jamais de crash du host
/// ni des autres modules (EXG-060, EXG-070).
/// </summary>
public sealed class ModuleLoader
{
    private readonly List<LoadedModule> _modules = [];
    private readonly List<(string Path, string Reason)> _rejected = [];
    private readonly Action<string> _log;
    private readonly RefData _refData;

    public ModuleLoader(RefData refData, Action<string> log)
    {
        _refData = refData;
        _log = log;
    }

    public IReadOnlyList<LoadedModule> Modules => _modules;

    /// <summary>Modules refusés avant instanciation (assembly illisible, contrat absent…).</summary>
    public IReadOnlyList<(string Path, string Reason)> Rejected => _rejected;

    /// <summary>
    /// Charge tous les modules du dossier « Modules/ » à côté de l'exécutable :
    /// un sous-dossier par module, contenant sa DLL.
    /// </summary>
    public void LoadAll(string modulesDirectory)
    {
        if (!Directory.Exists(modulesDirectory))
        {
            Directory.CreateDirectory(modulesDirectory);
            _log($"Dossier de modules créé : {modulesDirectory}");
            return;
        }

        var dlls = Directory.EnumerateFiles(modulesDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(path => !HostOwnedAssemblies.Contains(Path.GetFileNameWithoutExtension(path),
                                                         StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var dll in dlls)
        {
            TryLoad(dll);
        }

        _log($"Modules : {_modules.Count(m => m.Status == ModuleStatus.Active)} actif(s), " +
             $"{_modules.Count(m => m.Status != ModuleStatus.Active) + _rejected.Count} inactif(s)");
    }

    /// <summary>
    /// Enregistre un module livré avec Overkit. Il passe par le même contrat et
    /// le même contrôle de compatibilité qu'un module tiers : ce qu'un module
    /// interne sait faire, un module externe le sait aussi.
    /// </summary>
    public LoadedModule Register(IOverkitModule instance)
    {
        var module = new LoadedModule
        {
            Manifest = instance.Manifest,
            Instance = instance,
            SourcePath = "",
        };

        if (CheckCompatibility(module.Manifest) is { } reason)
        {
            module.Status = ModuleStatus.Incompatible;
            module.Reason = reason;
            _log($"Module interne '{module.Manifest.Name}' inactif : {reason}");
        }
        else
        {
            instance.Initialize(new ModuleContext(module.Manifest, _refData, _log));
        }

        _modules.Add(module);
        return module;
    }

    private void TryLoad(string dllPath)
    {
        try
        {
            // Contexte collectible et isolé : les types du SDK viennent du host
            // (resolver par défaut), le reste est privé au module.
            var context = new AssemblyLoadContext($"Overkit.Module.{Path.GetFileNameWithoutExtension(dllPath)}",
                                                  isCollectible: true);
            context.Resolving += (ctx, name) =>
            {
                // Le SDK et les contrats viennent TOUJOURS du host : si un
                // module embarquait sa propre copie, ses types ne seraient pas
                // ceux du host et les échanges casseraient silencieusement.
                if (HostOwnedAssemblies.Contains(name.Name ?? "", StringComparer.OrdinalIgnoreCase))
                {
                    return null; // repli sur le contexte par défaut
                }
                var sibling = Path.Combine(Path.GetDirectoryName(dllPath)!, name.Name + ".dll");
                return File.Exists(sibling) ? ctx.LoadFromAssemblyPath(sibling) : null;
            };

            using var stream = File.OpenRead(dllPath);
            var assembly = context.LoadFromStream(stream);

            var types = assembly.GetTypes()
                .Where(t => typeof(IOverkitModule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                .ToList();

            if (types.Count == 0)
            {
                _rejected.Add((dllPath, "aucune implémentation d'IOverkitModule"));
                return;
            }

            foreach (var type in types)
            {
                if (Activator.CreateInstance(type) is not IOverkitModule instance)
                {
                    _rejected.Add((dllPath, $"{type.Name} : instanciation impossible"));
                    continue;
                }

                var module = new LoadedModule
                {
                    Manifest = instance.Manifest,
                    Instance = instance,
                    SourcePath = dllPath,
                };

                if (CheckCompatibility(module.Manifest) is { } reason)
                {
                    module.Status = ModuleStatus.Incompatible;
                    module.Reason = reason;
                    _modules.Add(module);
                    _log($"Module '{module.Manifest.Name}' inactif : {reason}");
                    continue;
                }

                instance.Initialize(new ModuleContext(module.Manifest, _refData, _log));
                _modules.Add(module);
                _log($"Module chargé : {module.Manifest.Name} v{module.Manifest.Version} ({module.Manifest.Id})");
            }
        }
        catch (Exception ex)
        {
            // Assembly corrompue, dépendance manquante, constructeur explosif…
            _rejected.Add((dllPath, ex.GetBaseException().Message));
            _log($"Module ignoré ({Path.GetFileName(dllPath)}) : {ex.GetBaseException().Message}");
        }
    }

    /// <summary>Retourne la raison du refus, ou null si le module est compatible (EXG-070).</summary>
    private static string? CheckCompatibility(ModuleManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Name))
        {
            return "manifeste incomplet (id ou nom manquant)";
        }

        if (!Version.TryParse(manifest.MinSchema, out var min))
        {
            return $"min_schema illisible : « {manifest.MinSchema} »";
        }
        if (min > SchemaVersion)
        {
            return $"nécessite le schéma {manifest.MinSchema}, le host fournit {SchemaVersion}";
        }

        var unknownDomains = manifest.StateRequires
            .Concat(manifest.StateOptional)
            .Where(domain => !KnownDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (unknownDomains.Count > 0)
        {
            return $"domaine(s) inconnu(s) : {string.Join(", ", unknownDomains)}";
        }

        var unknownCapabilities = manifest.Capabilities
            .Where(cap => !KnownCapabilities.Contains(cap, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return unknownCapabilities.Count > 0
            ? $"capacité(s) non supportée(s) : {string.Join(", ", unknownCapabilities)}"
            : null;
    }

    /// <summary>
    /// Distribue le snapshot à tous les modules actifs. Un module qui lève est
    /// désactivé et signalé ; les autres continuent (EXG-060).
    /// </summary>
    public void Dispatch(GameStateSnapshot snapshot)
    {
        foreach (var module in _modules)
        {
            if (module.Status != ModuleStatus.Active)
            {
                continue;
            }
            try
            {
                module.Instance.OnStateUpdated(snapshot);
            }
            catch (Exception ex)
            {
                Fault(module, ex);
            }
        }
    }

    /// <summary>Construit la vue d'un module, en absorbant ses pannes.</summary>
    public ModuleView BuildView(LoadedModule module)
    {
        if (module.Status != ModuleStatus.Active)
        {
            return new ModuleView(module.Manifest.Name,
                [new EmptySection(module.Reason ?? "module inactif")]);
        }
        try
        {
            return module.Instance.BuildView();
        }
        catch (Exception ex)
        {
            Fault(module, ex);
            return new ModuleView(module.Manifest.Name, [new EmptySection(module.Reason!)]);
        }
    }

    /// <summary>
    /// Transmet une action de l'utilisateur au module, en absorbant ses pannes :
    /// un champ mal géré désactive le module, pas le panneau (EXG-060).
    /// </summary>
    public void Interact(LoadedModule module, ViewInteraction interaction)
    {
        if (module.Status != ModuleStatus.Active)
        {
            return;
        }
        try
        {
            module.Instance.OnInteraction(interaction);
        }
        catch (Exception ex)
        {
            Fault(module, ex);
        }
    }

    private void Fault(LoadedModule module, Exception ex)
    {
        module.Status = ModuleStatus.Faulted;
        module.Reason = $"désactivé après une erreur : {ex.GetBaseException().Message}";
        _log($"Module '{module.Manifest.Name}' {module.Reason}");
    }

    private static readonly Version SchemaVersion = new(1, 0);

    /// <summary>Assemblies fournies par le host : un module ne doit jamais en charger sa propre copie.</summary>
    private static readonly string[] HostOwnedAssemblies = ["Overkit.Sdk", "Overkit.Contracts"];

    private static readonly string[] KnownDomains =
        ["player", "world", "inventory", "palbox", "party", "bases", "nearby", "collectors"];

    private static readonly string[] KnownCapabilities = ["refdata", "storage"];

    private sealed class ModuleContext(ModuleManifest manifest, RefData refData, Action<string> log) : IModuleContext
    {
        public IRefData? RefData { get; } =
            manifest.Capabilities.Contains("refdata", StringComparer.OrdinalIgnoreCase) ? refData : null;

        public void Log(string message) => log($"[{manifest.Id}] {message}");
    }
}
