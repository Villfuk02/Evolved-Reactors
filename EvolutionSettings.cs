using System.Collections.Generic;
using System.Linq;

namespace Fission
{
    internal class EvolutionSettings
    {
        static readonly Block[] ALLOWED_BLOCKS = new Block[] { Block.ACTIVE_MODERATOR, Block.WATER, Block.REDSTONE, Block.QUARTZ, Block.GOLD, Block.GLOWSTONE, Block.LAPIS, Block.DIAMOND, Block.HELIUM, Block.ENDERIUM, Block.CRYOTHEUM, Block.IRON, Block.EMERALD, Block.COPPER, Block.TIN, Block.MAGNESIUM };

        public DoubleOption powerWeight = new(0, "Points per RF/t of effective power");
        public DoubleOption efficiencyWeight = new(0, "Points per % of efficiency");
        public DoubleOption cellWeight = new(0, "Points per Reactor Fuel Cell");
        public DoubleOption airWeight = new(0, "Points per block of Air");
        public DoubleOption unsafePenalty = new(0, "Penalty for being unsafe");
        public BlockSetOption blocks = new(ALLOWED_BLOCKS, "Allowed components");
        public bool allowModerators;
        public Block[] placeableNonCore;
        public void LoadFromInput()
        {
            powerWeight.LoadFromInput();
            efficiencyWeight.LoadFromInput();
            cellWeight.LoadFromInput();
            airWeight.LoadFromInput();
            unsafePenalty.LoadFromInput();
            blocks.LoadFromInput();
            allowModerators = ((HashSet<Block>)blocks).Contains(Block.ACTIVE_MODERATOR);
            placeableNonCore = ((HashSet<Block>)blocks).Append(Block.AIR).ToArray();
        }
    }
}
