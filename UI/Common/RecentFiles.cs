using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Octopus.Player.Core;

namespace Octopus.Player.UI
{
    public struct RecentFileEntry
    {
        public string Path { get; private set; }
        public string Type { get; private set; }
        public DateTime LastOpened { get; private set; }

        public RecentFileEntry(IClip clip)
        {
            Path = clip.Path;
            Type = clip.GetType().ToString();
            LastOpened = DateTime.Now;
        }

        public void Touch(DateTime? touchTime = null)
        {
            LastOpened = touchTime.HasValue ? touchTime.Value : DateTime.Now;
        }
    }

    public class RecentFiles : IDisposable
    {
        public IReadOnlyCollection<RecentFileEntry> Entries { get { return entries; } }
        private List<RecentFileEntry> entries;
        private string jsonPath;

        public static readonly uint maxEntries = 10;

        public RecentFiles(PlayerWindow playerWindow)
        {
            entries = new List<RecentFileEntry>();
            playerWindow.ClipOpened += OnClipOpened;
            jsonPath = playerWindow.NativeWindow.PlayerApplication.RecentFilesJsonPath;
            Deserialise();
        }

        public RecentFileEntry? FindEntry(IClip clip)
        {
            foreach(var entry in Entries)
            {
                if (entry.Path == clip.Path)
                    return entry;
            }

            return null;
        }

        public void OnClipOpened(IClip clip)
        {
            var entry = FindEntry(clip);
            if (entry != null)
                entry.Value.Touch();
            else
                entries.Add(new RecentFileEntry(clip));
            Sort();
            OnChanged();
        }

        private void Serialise()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
                var json = JsonConvert.SerializeObject(entries);
                File.WriteAllText(jsonPath, json);
            }
            catch
            {
                Trace.WriteLine("Failed to write recent files json to: '" + jsonPath + "'");
            }
        }

        private void Deserialise()
        {
            try
            {
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    entries = JsonConvert.DeserializeObject<List<RecentFileEntry>>(json);
                }
            }
            catch
            {
                Trace.WriteLine("Failed to read recent files json from: '" + jsonPath + "'");
            }
        }

        public void Dispose()
        {
            Serialise();
        }

        private void Sort()
        {
            entries.Sort((a, b) => DateTime.Compare(a.LastOpened, b.LastOpened));
            if ( entries.Count > maxEntries)
                entries.RemoveRange((int)maxEntries, entries.Count - (int)maxEntries);
        }

        private void OnChanged()
        {
            Serialise();
        }
    }
}

