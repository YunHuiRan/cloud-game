using System;
using System.Windows;

namespace Ludens
{
    public sealed class GameOpenEventArgs : EventArgs
    {
        public GameEntry Game { get; private set; }
        public Rect SourceRect { get; private set; }

        public GameOpenEventArgs(GameEntry game, Rect sourceRect)
        {
            Game = game;
            SourceRect = sourceRect;
        }
    }

    public sealed class GameActionEventArgs : EventArgs
    {
        public GameEntry Game { get; private set; }

        public GameActionEventArgs(GameEntry game)
        {
            Game = game;
        }
    }
}
