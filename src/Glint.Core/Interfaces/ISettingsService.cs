using Glint.Core.Models;

namespace Glint.Core.Interfaces;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> from %AppData%\Glint\settings.json.
/// A single in-memory instance is shared (via DI singleton) and mutated directly by
/// ViewModels; call <see cref="Save"/> after any change that should persist.
/// </summary>
public interface ISettingsService
{
    /// <summary>The live settings object. Mutate properties directly, then call <see cref="Save"/>.</summary>
    AppSettings Current { get; }

    /// <summary>
    /// Writes <see cref="Current"/> to disk. Safe to call frequently — writes are atomic
    /// (write to temp file, then replace) to avoid corrupting settings if the app is killed mid-write.
    /// </summary>
    void Save();

    /// <summary>
    /// Re-reads settings from disk, replacing <see cref="Current"/>. If the file is missing
    /// or corrupt, falls back to defaults and logs a warning — never throws.
    /// </summary>
    void Reload();
}
