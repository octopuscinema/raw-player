using System;

namespace Octopus.Player.Core
{
    public interface IPlayerWindow : IDisposable
    {
        void OnLoad();
        void InvokeOnUIThread(Action action, bool async = true);
    }
}

