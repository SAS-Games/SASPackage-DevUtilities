using System;

namespace HP.DevUtilities
{
    /// <summary>
    /// Recoverable snapshot of the running game and Unity version.
    /// </summary>
    [Serializable]
    public struct GameInfoSnapshot : IMiniToolSnapshot
    {
        public string GameVersion;
        public string UnityVersion;
    }
}
