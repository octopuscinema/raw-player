using System;
using static Octopus.Player.Core.Playback.IPlayback;

namespace Octopus.Player.Core
{
    public interface IPlayerWindow : IDisposable
    {
        void OnLoad();
        void InvokeOnUIThread(Action action, bool async = true);

        public delegate void ClipOpenedEventHandler(IClip clip);
        event ClipOpenedEventHandler ClipOpened;
    }
}

