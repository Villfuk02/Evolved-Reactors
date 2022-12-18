using System;
using System.Collections.Generic;
using System.Text;

namespace Fission
{
    internal record struct Vector3(int X, int Y, int Z)
    {
        public static readonly Vector3[] DIRS = new Vector3[] { new(0, 1, 0), new(0, -1, 0), new(0, 0, -1), new(0, 0, 1), new(1, 0, 0), new(-1, 0, 0) };
        public static readonly Vector3 ZERO = new(0, 0, 0);
        public int Volume => X * Y * Z;
        public bool InBounds(Vector3 min, Vector3 max)
        {
            return X >= min.X && X < max.X && Y >= min.Y && Y < max.Y && Z >= min.Z && Z < max.Z;
        }
        public static void ForEachInside(Vector3 min, Vector3 max, Action<Vector3> action)
        {
            for (int x = min.X; x < max.X; x++)
            {
                for (int y = min.Y; y < max.Y; y++)
                {
                    for (int z = min.Z; z < max.Z; z++)
                    {
                        action(new(x, y, z));
                    }
                }
            }
        }
        public void ForEachInside(Action<Vector3> action)
        {
            ForEachInside(ZERO, this, action);
        }
        public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 v, int m) => new(v.X * m, v.Y * m, v.Z * m);
        public static Vector3 operator *(int m, Vector3 v) => new(v.X * m, v.Y * m, v.Z * m);
    }
    internal class Layout
    {
        public readonly Vector3 size;
        readonly Block[,,] blocks;
        float efficiency;
        float heatMultiplier;
        int cooling;
        int cells;
        int moderators;
        int air;
        bool cached;
        public Layout(Vector3 size)
        {
            this.size = size;
            blocks = new Block[size.X, size.Y, size.Z];
            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    for (int z = 0; z < size.Z; z++)
                    {
                        blocks[x, y, z] = Block.AIR;
                    }
                }
            }
            cached = false;
        }
        public Layout(Block[,,] blocks)
        {
            size = new(blocks.GetLength(0), blocks.GetLength(1), blocks.GetLength(2));
            this.blocks = blocks;
            cached = false;
        }
        private Block this[Vector3 pos]
        {
            get => blocks[pos.X, pos.Y, pos.Z];
            set => blocks[pos.X, pos.Y, pos.Z] = value;
        }

        public Block GetAt(Vector3 pos)
        {
            if (!pos.InBounds(Vector3.ZERO, size))
                return Block.CASING;
            return this[pos];
        }

        public void SetAt(Vector3 pos, Block b)
        {
            if (!pos.InBounds(Vector3.ZERO, size))
                return;
            cached = false;
            this[pos] = b;
        }

        public void Validate(Vector3 pos)
        {
            HashSet<Vector3> toValidate = new() { pos };
            Queue<Vector3> queue = new();
            queue.Enqueue(pos);

            void ValidateInternal(Vector3 pos)
            {
                if (!pos.InBounds(Vector3.ZERO, size))
                    return;
                if (this[pos].IsValid(GetNeighbors(pos)))
                    return;
                if (this[pos] == Block.MODERATOR)
                    this[pos] = Block.ACTIVE_MODERATOR;
                else if (this[pos] == Block.ACTIVE_MODERATOR)
                    this[pos] = Block.MODERATOR;
                else
                    this[pos] = Block.AIR;
                ForEachNeighborPos(pos, p =>
                {
                    if (toValidate.Add(p))
                        queue.Enqueue(p);
                });
            }
            while (toValidate.Count > 0)
            {
                Vector3 current = queue.Dequeue();
                toValidate.Remove(current);
                ValidateInternal(current);
            }
        }

        public Neighbors GetNeighbors(Vector3 pos)
        {
            return new(GetAt(pos + Vector3.DIRS[0]), GetAt(pos + Vector3.DIRS[1]), GetAt(pos + Vector3.DIRS[2]), GetAt(pos + Vector3.DIRS[3]), GetAt(pos + Vector3.DIRS[4]), GetAt(pos + Vector3.DIRS[5]));
        }

        public static void ForEachNeighborPos(Vector3 pos, Action<Vector3> f)
        {
            foreach (Vector3 d in Vector3.DIRS)
            {
                f(pos + d);
            }
        }

        public float GetPowerEfficiency()
        {
            if (!cached)
                Recalculate();
            return efficiency;
        }
        public float GetHeatMultiplier()
        {
            if (!cached)
                Recalculate();
            return heatMultiplier;
        }
        public float GetCooling()
        {
            if (!cached)
                Recalculate();
            return cooling;
        }
        public int GetCells()
        {
            if (!cached)
                Recalculate();
            return cells;
        }

        public int GetModerators()
        {
            if (!cached)
                Recalculate();
            return moderators;
        }
        public int GetAir()
        {
            if (!cached)
                Recalculate();
            return air;
        }

        void Recalculate()
        {
            cells = 0;
            moderators = 0;
            air = 0;
            efficiency = 0;
            heatMultiplier = 0;
            cooling = 0;
            size.ForEachInside(p =>
            {
                Block b = this[p];
                cooling += b.Cooling;
                if (b == Block.REACTOR_CELL)
                {
                    cells++;
                    int cell_efficiency = 1;
                    int active_moderators = 0;
                    foreach (Vector3 d in Vector3.DIRS)
                    {
                        for (int i = 1; i <= 5; i++)
                        {
                            Block bb = GetAt(p + i * d);
                            if (bb == Block.REACTOR_CELL)
                            {
                                cell_efficiency++;
                                break;
                            }
                            else if (bb != Block.ACTIVE_MODERATOR && bb != Block.MODERATOR)
                            {
                                break;
                            }
                            if (i == 1)
                                active_moderators++;
                        }
                    }
                    efficiency += cell_efficiency * (6 + active_moderators);
                    heatMultiplier += cell_efficiency * (3 + 3 * cell_efficiency + 2 * active_moderators);
                }
                else if (b == Block.ACTIVE_MODERATOR || b == Block.MODERATOR)
                {
                    moderators++;
                }
                else if (b == Block.AIR)
                {
                    air++;
                }
            });
            efficiency /= 6 * cells;
            heatMultiplier /= 6 * cells;
            cached = true;
        }

        public void PrintFuelInfo(FuelStatsOption fuel)
        {
            string safe = IsSafe(fuel.Value.heat) ? "Safe" : "Unsafe";
            Console.WriteLine($"{safe}, Effective Power: {EffectivePower(fuel.Value.heat, fuel.Value.power):F0}RF/t, Energy per fuel: {EnergyPerFuel(fuel.Value.power, fuel.Value.time) / 1_000_000:F2} MRF");
        }

        public void PrintInfo()
        {
            if (!cached)
                Recalculate();
            Console.WriteLine($"Cells: {cells}, Efficiency: {efficiency * 100:F1}%, Heat multiplier: {heatMultiplier * 100:F1}%, Cooling: -{cooling}H/t");
            Console.WriteLine();
            Console.WriteLine($"{2 * (size.X * size.Y + size.Y * size.Z + size.Z * size.X)}x Reactor Casing");
            Dictionary<Block, int> blockCounts = new();
            size.ForEachInside(p =>
            {
                Block b = this[p];
                if (!blockCounts.ContainsKey(b))
                    blockCounts[b] = 0;
                blockCounts[b]++;
            });
            if (blockCounts.ContainsKey(Block.ACTIVE_MODERATOR) && blockCounts.ContainsKey(Block.MODERATOR))
            {
                blockCounts[Block.MODERATOR] += blockCounts[Block.ACTIVE_MODERATOR];
                blockCounts.Remove(Block.ACTIVE_MODERATOR);
            }
            foreach ((Block b, int count) in blockCounts)
            {
                Console.WriteLine($"{count}x {b}");
            }
        }

        public void Print()
        {
            StringBuilder sb = new();
            for (int y = 0; y < size.Y; y++)
            {
                Console.WriteLine($":\nLayer {y + 1}:");
                for (int z = 0; z < size.Z; z++)
                {
                    sb.Clear();
                    for (int x = 0; x < size.X; x++)
                    {
                        sb.Append(blocks[x, y, z].Symbol);
                    }
                    Console.WriteLine(sb.ToString());
                }
            }
        }

        public static Layout LoadFromInput()
        {
            Vector3Option s = new(new(5, 5, 5), "Size");
            s.LoadFromInput();
            return LoadFromInput(s);
        }
        public static Layout LoadFromInput(Vector3 size)
        {
            Block[,,] blocks = new Block[size.X, size.Y, size.Z];
            for (int y = 0; y < size.Y; y++)
            {
                Console.WriteLine($"\nEnter layer {y + 1}:");
                for (int z = 0; z < size.Z; z++)
                {
                    string line = Console.ReadLine();
                    while (line.Contains(':'))
                        line = Console.ReadLine();
                    for (int x = 0; x < size.X; x++)
                    {
                        blocks[x, y, z] = line.Length > x ? Block.FromSymbol(line[x]) : Block.AIR;
                    }
                }
            }
            Layout l = new(blocks);
            l.FixModerators();
            size.ForEachInside(l.Validate);
            return l;
        }

        public float ProjectedHeat(float baseHeat)
        {
            return GetHeatMultiplier() * baseHeat * GetCells() - GetCooling();
        }

        public bool IsSafe(float baseHeat)
        {
            return ProjectedHeat(baseHeat) <= 0;
        }

        public float EffectivePower(float baseHeat, float basePower)
        {
            float projectedPower = basePower * GetPowerEfficiency() * GetCells();
            return IsSafe(baseHeat) ? projectedPower : projectedPower * GetCooling() / (baseHeat * GetHeatMultiplier() * GetCells());
        }

        public float EnergyPerFuel(float basePower, float timeMin)
        {
            return basePower * GetPowerEfficiency() * timeMin * 1200;
        }

        public Layout Copy()
        {
            Layout l = new(size);
            size.ForEachInside(p => l.SetAt(p, this[p]));
            return l;
        }

        public void FixModerators()
        {
            size.ForEachInside(pos =>
            {
                if (!this[pos].IsValid(GetNeighbors(pos)))
                {
                    if (this[pos] == Block.MODERATOR)
                        this[pos] = Block.ACTIVE_MODERATOR;
                    else if (this[pos] == Block.ACTIVE_MODERATOR)
                        this[pos] = Block.MODERATOR;
                }
            });
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            for (int y = 0; y < size.Y; y++)
            {
                if (y != 0) sb.Append(' ');
                for (int z = 0; z < size.Z; z++)
                {
                    if (z != 0) sb.Append('|');
                    for (int x = 0; x < size.X; x++)
                    {
                        sb.Append(blocks[x, y, z].Symbol);
                    }
                }
            }
            return sb.ToString();
        }
    }
}
