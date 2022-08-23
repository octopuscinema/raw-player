using System;
using System.Collections.Generic;
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

        public RecentFileEntry(string jsonData)
        {
            Path = "";
            Type = "";
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

        public static readonly uint maxEntries = 10;

        public RecentFiles(PlayerWindow playerWindow)
        {
            entries = new List<RecentFileEntry>();
            playerWindow.ClipOpened += OnClipOpened;
            //var jsonPath = playerWindow.NativeWindow.PlayerApplication.RecentFilesJsonPath
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
        }

        private void Serialise()
        {
            var json = JsonConvert.SerializeObject(entries);
        }

        private void Deserialise()
        {
            //JsonConvert.DeserializeObject()
        }

        public void Dispose()
        {
            // TODO: Serialise here
            //Serialise();
        }

        private void Sort()
        {
            entries.Sort((a, b) => DateTime.Compare(a.LastOpened, b.LastOpened));
            if ( entries.Count > maxEntries)
                entries.RemoveRange((int)maxEntries, entries.Count - (int)maxEntries);
        }
    }
}

