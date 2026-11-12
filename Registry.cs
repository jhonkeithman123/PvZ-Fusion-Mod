using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Blooms_Mods
{
    /// <summary>
    /// One toggleable feature shown in the Blooms Mods menu.
    /// </summary>
    public sealed class BloomsFeature
    {
        public string Name;
        public string Description;
        public Func<bool> IsEnabled;
        public Action<bool> SetEnabled;
        public MelonPreferences_Entry<KeyCode> HotkeyPref;

        public BloomsFeature(string name, string description, Func<bool> isEnabled, Action<bool> setEnabled,
                             MelonPreferences_Entry<KeyCode> hotkeyPref = null)
        {
            Name = name;
            Description = description;
            IsEnabled = isEnabled;
            SetEnabled = setEnabled;
            HotkeyPref = hotkeyPref;
        }

        public string HotkeyLabel
        {
            get
            {
                if (HotkeyPref == null || HotkeyPref.Value == KeyCode.None)
                    return string.Empty;
                return " [" + HotkeyPref.Value + "]";
            }
        }
    }

    /// <summary>
    /// A mod listed in the Blooms Mods menu.
    /// </summary>
    public sealed class BloomsModEntry
    {
        public string Name;
        public string Version;
        public string Author;
        public List<BloomsFeature> Features = new List<BloomsFeature>();

        public BloomsModEntry(string name, string version, string author)
        {
            Name = name;
            Version = version;
            Author = author;
        }
    }

    /// <summary>
    /// Registry of the mods shown in the hub. Future mods can call <see cref="Register"/>.
    /// </summary>
    public static class BloomsMods
    {
        public static readonly List<BloomsModEntry> Entries = new List<BloomsModEntry>();

        public static void Register(BloomsModEntry entry)
        {
            if (entry == null)
                return;
            if (!Entries.Contains(entry))
                Entries.Add(entry);
        }
    }
}
