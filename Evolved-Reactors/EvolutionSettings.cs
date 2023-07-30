using System;
using System.Collections.Generic;
using System.Linq;

namespace Fission
{
    internal class EvolutionSettings
    {
        static readonly Block[] AllowedBlocks = { Block.ACTIVE_MODERATOR, Block.WATER, Block.REDSTONE, Block.QUARTZ, Block.GOLD, Block.GLOWSTONE, Block.LAPIS, Block.DIAMOND, Block.HELIUM, Block.ENDERIUM, Block.CRYOTHEUM, Block.IRON, Block.EMERALD, Block.COPPER, Block.TIN, Block.MAGNESIUM };

        public DoubleOption powerWeight = new(0, "Points per RF/t of effective power");
        public DoubleOption efficiencyWeight = new(0, "Points per % of efficiency");
        public DoubleOption cellWeight = new(0, "Points per Reactor Fuel Cell");
        public DoubleOption airWeight = new(0, "Points per block of Air");
        public DoubleOption fuelUsageWeight = new(0, "Points per fuel usage rate");
        public DoubleOption unsafePenalty = new(0, "Penalty for being unsafe");
        public BlockSetOption blocks = new(AllowedBlocks, "Allowed components");
        public bool allowModerators;
        public Block[] placeableNonCore;
        public void LoadFromInput()
        {
            Console.WriteLine();
            Console.WriteLine("  Fitness function parameters");
            powerWeight.LoadFromInput();
            efficiencyWeight.LoadFromInput();
            cellWeight.LoadFromInput();
            airWeight.LoadFromInput();
            fuelUsageWeight.LoadFromInput();
            unsafePenalty.LoadFromInput();
            Console.WriteLine();
            blocks.LoadFromInput();
            allowModerators = ((HashSet<Block>)blocks).Contains(Block.ACTIVE_MODERATOR);
            placeableNonCore = ((HashSet<Block>)blocks).Append(Block.AIR).ToArray();
        }

        public void Clone(EvolutionSettings original)
        {
            powerWeight.value = original.powerWeight;
            efficiencyWeight.value = original.efficiencyWeight;
            cellWeight.value = original.cellWeight;
            airWeight.value = original.airWeight;
            fuelUsageWeight.value = original.fuelUsageWeight;
            unsafePenalty.value = original.unsafePenalty;
            allowModerators = original.allowModerators;
            placeableNonCore = original.placeableNonCore.ToArray();
        }
    }
}
