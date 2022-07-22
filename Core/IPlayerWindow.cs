using System;

namespace Octopus.Player.Core
{
    public interface IPlayerWindow
    {
        void InvokeOnUIThread(Action action, bool async = true);
    }
}

