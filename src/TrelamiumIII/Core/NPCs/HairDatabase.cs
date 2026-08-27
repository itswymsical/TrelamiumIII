using System.Linq;

namespace TrelamiumIII.Core.NPCs;

public static class HairDatabase
{
    public static readonly int[] Masculine = [0, 1, 2, 75, ];
    public static readonly int[] Feminine = [/*173, 174, 195, 196, */];
    public static readonly int[] Neutral = [];

    public static class Tags // I'll have to come back in 1.4.5 and update this :C
    {
        public static readonly int[] Beards = [2, 17, 56, 57, 58, 59, 60, 61, 62, 63, 137, 147, 148, 149, 150, 151, 152,
            153, 154, 155, 156, 157, 158, 159, 161, 162, /*, 165, 201, 202, 203, 204, 205, 206, 207, 208*/];
        public static readonly int[] Mustaches = [60, 61, 74, 75, 76, 77, 147, 148, 149, 150, 151, 152,];

        public static readonly int[] LongHair = [64, 65, 66, 67, 68, 69, 71, 72, 73, 80, 81, 85, 86, 87, 88, 89, 101, 106, 133, 146, 162];
        public static readonly int[] Ponytails = [5, 9, 18, 21, 22, 26, 29, 32, 38, 46, 47, 50, 81, 82, 86];
        public static readonly int[] Buns = [11, 83, 84];

        public static readonly int[] Stylistic = [10, 16, 23, 25, 52, 92, 94, 109, 123, 124, 125, 126, 127, 128, 129, 131, 132, 163];

        public static readonly int[] Shinobi = [47, 78, 79, 94, 109 /*, 180, 190, 193, 197, 199, 209, 210, 211, 212, 213, 214, 215*/];
    }

}

public static class HairQuery
{
    public static int[] Intersect(int[] a, int[] b)
    {
        return a.Intersect(b).ToArray();
    }
}

public static class HairstyleID // work in progress, im doing this manually lol.
{
    public const int Default = 0;
    public const int Default2 = 1;
    public const int DefaultBeard = 2;
    public const int TrapezoidCut = 3;
    public const int EmoSwoop = 4;
    public const int DefaultPonytail_1 = 5;
    public const int LongBraids = 6;
    public const int DefaultShortCut = 7;
    public const int CurlingForwardHair = 8;
    public const int DefaultPonytail_2 = 9;

    public const int Pigtails = 32;
    public const int Bald = 15;
    public const int Afro = 16;
    public const int Mohawk = 10;
    public const int LongDoublePonytail = 82;
    public const int LongHairBabyHairStickingOut = 133;
}