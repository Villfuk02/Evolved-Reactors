using System;

namespace Fission
{
    internal record class Block(string Name, char Symbol, int Cooling, Func<Neighbors, bool> IsValid)
    {
        // ALL VALUES ARE FROM ENIGMATICA 2 EXPERT
        public static readonly Block CASING = new("Casing", '#', 0, n => false);
        public static readonly Block AIR = new("Air", ' ', 0, n => true);
        public static readonly Block REACTOR_CELL = new("Reactor Cell", 'X', 0, n => true);
        public static readonly Block MODERATOR = new("Moderator", '+', 0, n => n.Count(ACTIVE_MODERATOR) + n.Count(MODERATOR) > 0 && n.Count(REACTOR_CELL) == 0);
        public static readonly Block ACTIVE_MODERATOR = new("Moderator", '+', 0, n => n.Count(REACTOR_CELL) > 0);
        public static readonly Block WATER = new("Water Cooler", 'W', 20, n => n.Count(ACTIVE_MODERATOR) + n.Count(REACTOR_CELL) > 0);
        public static readonly Block REDSTONE = new("Redstone Cooler", 'R', 80, n => n.Count(REACTOR_CELL) > 0);
        public static readonly Block QUARTZ = new("Quartz Cooler", 'Q', 80, n => n.Count(ACTIVE_MODERATOR) > 0);
        public static readonly Block GOLD = new("Gold Cooler", 'G', 120, n => n.Count(WATER) > 0 && n.Count(REDSTONE) > 0);
        public static readonly Block GLOWSTONE = new("Glowstone Cooler", 'g', 120, n => n.Count(ACTIVE_MODERATOR) >= 2);
        public static readonly Block LAPIS = new("Lapis Cooler", 'L', 100, n => n.Count(REACTOR_CELL) > 0 && n.Count(CASING) > 0);
        public static readonly Block DIAMOND = new("Diamond Cooler", 'D', 120, n => n.Count(WATER) > 0 && n.Count(QUARTZ) > 0);
        public static readonly Block HELIUM = new("Liquid Helium Cooler", 'H', 120, n => n.Count(REDSTONE) == 1 && n.Count(CASING) > 0);
        public static readonly Block ENDERIUM = new("Enderium Cooler", 'e', 140, n => n.Count(CASING) == 3 && n.T != n.B && n.N != n.S && n.E != n.W);
        public static readonly Block CRYOTHEUM = new("Cryotheum Cooler", 'c', 140, n => n.Count(REACTOR_CELL) >= 2);
        public static readonly Block IRON = new("Iron Cooler", 'I', 60, n => n.Count(GOLD) > 0);
        public static readonly Block EMERALD = new("Emerald Cooler", 'E', 140, n => n.Count(ACTIVE_MODERATOR) > 0 && n.Count(REACTOR_CELL) > 0);
        public static readonly Block COPPER = new("Copper Cooler", 'C', 60, n => n.Count(GLOWSTONE) > 0);
        public static readonly Block TIN = new("Tin Cooler", 'T', 80, n => (n.T == LAPIS && n.B == LAPIS) || (n.N == LAPIS && n.S == LAPIS) || (n.E == LAPIS && n.W == LAPIS));
        public static readonly Block MAGNESIUM = new("Magnesium Cooler", 'M', 100, n => n.Count(ACTIVE_MODERATOR) > 0 && n.Count(CASING) > 0);

        static readonly Block[] ALL = new Block[] { CASING, AIR, REACTOR_CELL, MODERATOR, ACTIVE_MODERATOR, WATER, REDSTONE, QUARTZ, GOLD, GLOWSTONE, LAPIS, DIAMOND, HELIUM, ENDERIUM, CRYOTHEUM, IRON, EMERALD, COPPER, TIN, MAGNESIUM };

        public static Block FromSymbol(char s)
        {
            foreach (Block block in ALL)
            {
                if (block.Symbol == s)
                    return block;
            }
            return AIR;
        }

        public override string ToString()
        {
            return $"{Name} ({Symbol})";
        }
    }

    internal readonly record struct Neighbors(Block T, Block B, Block N, Block S, Block E, Block W)
    {
        public int Count(Block b)
        {
            int count = 0;
            if (T == b) count++;
            if (B == b) count++;
            if (N == b) count++;
            if (S == b) count++;
            if (E == b) count++;
            if (W == b) count++;
            return count;
        }
    }
}
