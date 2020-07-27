// Created by Ben Sims 23/07/20

using System;

namespace FoxieGames
{
    [Flags]
    public enum NeighborFlags
    {
        None = 0,
        Right = 1,
        Up = 2,
        UpperRight = 3,
        Left = 4,
        LeftRight = 5,
        UpperLeft = 6,
        UpLeftRight = 7,
        Down = 8,
        DownRight = 9,
        UpDown = 10,
        UpDownRight = 11,
        DownLeft = 12,
        DownLeftRight = 13,
        UpDownLeft = 14,
        All = 15
    }
}