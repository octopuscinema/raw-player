﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Octopus.Player.Core;
using static JpegLibrary.JpegHuffmanDecodingTable;

namespace Octopus.Player.UI
{
    public struct RecentFileEntry
    {
        [JsonProperty("Path")]
        public string Path { get; private set; }

        [JsonProperty("Type")]
        public string Type { get; private set; }

        [JsonProperty("LastOpened")]
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
        private string JsonPath { get; set; }

        public static readonly uint maxEntries = 10;

        public RecentFiles(PlayerWindow playerWindow, string jsonPath, bool automatic = true)
        {
            entries = new List<RecentFileEntry>();
            if (automatic)
                playerWindow.ClipOpened += Add;
            JsonPath = jsonPath;
            Deserialise();
        }

        public void Clear()
        {
            entries.Clear();
            OnChanged();
        }

        public void Add(IClip clip)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Path == clip.Path)
                {
                    var entry = entries[i];
                    entry.Touch();
                    entries[i] = entry;
                    Sort();
                    OnChanged();
                    return;
                }
            }

            entries.Add(new RecentFileEntry(clip));
            Sort();
            OnChanged();
        }

        public void Remove(IClip clip)
        {
            entries.RemoveAll(e => ( e.Path == clip.Path) );
        }

        public bool Contains(IClip clip)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Path == clip.Path)
                    return true;
            }
            return false;
        }

        private void Serialise()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(JsonPath));
                var json = JsonConvert.SerializeObject(entries);
                File.WriteAllText(JsonPath, json);
            }
            catch
            {
                Trace.WriteLine("Failed to write recent files json to: '" + JsonPath + "'");
            }
        }

        private void Deserialise()
        {
            try
            {
                if (File.Exists(JsonPath))
                {
                    string json = File.ReadAllText(JsonPath);
                    entries = JsonConvert.DeserializeObject<List<RecentFileEntry>>(json);
                    entries.RemoveAll(entry => (string.IsNullOrEmpty(entry.Path) || string.IsNullOrEmpty(entry.Type))) ;
                }
            }
            catch
            {
                Trace.WriteLine("Failed to read recent files json from: '" + JsonPath + "'");
            }
        }

        public void Dispose()
        {
            Serialise();
        }

        private void Sort()
        {
            entries.Sort((a, b) => DateTime.Compare(b.LastOpened, a.LastOpened));
            if ( entries.Count > maxEntries)
                entries.RemoveRange((int)maxEntries, entries.Count - (int)maxEntries);
        }

        private void OnChanged()
        {
            Serialise();
        }
    }
}

