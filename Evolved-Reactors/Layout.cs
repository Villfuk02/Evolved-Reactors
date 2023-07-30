using System;
using System.Collections.Generic;
using System.Text;

namespace Fission
{
    internal record struct Vector3(int X, int Y, int Z)
    {
        public static readonly Vector3[] DIRS = { new(0, 1, 0), new(0, -1, 0), new(0, 0, -1), new(0, 0, 1), new(1, 0, 0), new(-1, 0, 0) };
        public static readonly Vector3 ZERO = new(0, 0, 0);
        public readonly int Volume => X * Y * Z;
        public readonly bool InBounds(Vector3 min, Vector3 max) => X >= min.X && X < max.X && Y >= min.Y && Y < max.Y && Z >= min.Z && Z < max.Z;

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
        public readonly void ForEachInside(Action<Vector3> action) => ForEachInside(ZERO, this, action);
        public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 v, int m) => new(v.X * m, v.Y * m, v.Z * m);
        public static Vector3 operator *(int m, Vector3 v) => new(v.X * m, v.Y * m, v.Z * m);
    }
    internal class Layout
    {
        public readonly Vector3 size;
        readonly Block[,,] blocks_;
        float efficiency_;
        float heatMultiplier_;
        int cooling_;
        int cells_;
        int moderators_;
        int air_;
        bool cached_;
        public Layout(Vector3 size)
        {
            this.size = size;
            blocks_ = new Block[size.X, size.Y, size.Z];
            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    for (int z = 0; z < size.Z; z++)
                    {
                        blocks_[x, y, z] = Block.AIR;
                    }
                }
            }
            cached_ = false;
        }
        public Layout(Block[,,] blocks)
        {
            size = new(blocks.GetLength(0), blocks.GetLength(1), blocks.GetLength(2));
            blocks_ = blocks;
            cached_ = false;
        }

        Block this[Vector3 pos]
        {
            get => blocks_[pos.X, pos.Y, pos.Z];
            set => blocks_[pos.X, pos.Y, pos.Z] = value;
        }

        public Block GetAt(Vector3 pos) => pos.InBounds(Vector3.ZERO, size) ? this[pos] : Block.CASING;

        public void SetAt(Vector3 pos, Block b)
        {
            if (!pos.InBounds(Vector3.ZERO, size))
                return;
            cached_ = false;
            this[pos] = b;
        }

        public void Validate(Vector3 position)
        {
            HashSet<Vector3> toValidate = new() { position };
            Queue<Vector3> queue = new();
            queue.Enqueue(position);

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
            if (!cached_)
                Recalculate();
            return efficiency_;
        }
        public float GetHeatMultiplier()
        {
            if (!cached_)
                Recalculate();
            return heatMultiplier_;
        }
        public float GetCooling()
        {
            if (!cached_)
                Recalculate();
            return cooling_;
        }
        public int GetCells()
        {
            if (!cached_)
                Recalculate();
            return cells_;
        }

        public int GetModerators()
        {
            if (!cached_)
                Recalculate();
            return moderators_;
        }
        public int GetAir()
        {
            if (!cached_)
                Recalculate();
            return air_;
        }

        void Recalculate()
        {
            cells_ = 0;
            moderators_ = 0;
            air_ = 0;
            efficiency_ = 0;
            heatMultiplier_ = 0;
            cooling_ = 0;
            size.ForEachInside(p =>
            {
                Block b = this[p];
                cooling_ += b.Cooling;
                if (b == Block.REACTOR_CELL)
                {
                    cells_++;
                    int cellEfficiency = 1;
                    int activeModerators = 0;
                    foreach (Vector3 d in Vector3.DIRS)
                    {
                        for (int i = 1; i <= 5; i++)
                        {
                            Block bb = GetAt(p + i * d);
                            if (bb == Block.REACTOR_CELL)
                            {
                                cellEfficiency++;
                                break;
                            }

                            if (bb != Block.ACTIVE_MODERATOR && bb != Block.MODERATOR)
                            {
                                break;
                            }
                            if (i == 1)
                                activeModerators++;
                        }
                    }
                    efficiency_ += cellEfficiency * (6 + activeModerators);
                    heatMultiplier_ += cellEfficiency * (3 + 3 * cellEfficiency + 2 * activeModerators);
                }
                else if (b == Block.ACTIVE_MODERATOR || b == Block.MODERATOR)
                {
                    moderators_++;
                }
                else if (b == Block.AIR)
                {
                    air_++;
                }
            });
            efficiency_ /= 6 * cells_;
            heatMultiplier_ /= 6 * cells_;
            cached_ = true;
        }

        public void PrintFuelInfo(FuelOption fuel)
        {
            float heat = fuel.Value.heat;
            float power = fuel.Value.power;
            string safe = IsSafe(heat) ? "SAFE" : "UNSAFE";
            Console.WriteLine($"Fuel: {fuel.Value} | {safe} (Net heat: {NetHeat(heat):F0}H/t, Duty Cycle: {DutyCycle(heat) * 100:F1}%)");
            Console.WriteLine($"Fuel Usage Rate: {FuelUsageRate(heat):F1}x, Max Power: {MaxPower(power):F0}RF/t, Effective Power: {EffectivePower(heat, power):F0}RF/t");
        }

        public void PrintInfo()
        {
            if (!cached_)
                Recalculate();
            Console.WriteLine($"Cells: {cells_}, Efficiency: {efficiency_ * 100:F1}%, Heat multiplier: {heatMultiplier_ * 100:F1}%, Cooling: -{cooling_}H/t");
            Console.WriteLine();
            Console.WriteLine($"{2 * (size.X * size.Y + size.Y * size.Z + size.Z * size.X)}x Reactor Casing");
            Dictionary<Block, int> blockCounts = new();
            size.ForEachInside(p =>
            {
                Block b = this[p];
                blockCounts.TryAdd(b, 0);
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
                        sb.Append(blocks_[x, y, z].Symbol);
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
                    while (line!.Contains(':'))
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

        public float NetHeat(float baseHeat) => GetHeatMultiplier() * baseHeat * GetCells() - GetCooling();
        public bool IsSafe(float baseHeat) => NetHeat(baseHeat) <= 0;
        public float DutyCycle(float baseHeat) => IsSafe(baseHeat) ? 1 : GetCooling() / (baseHeat * GetHeatMultiplier() * GetCells());
        public float MaxPower(float basePower) => basePower * GetPowerEfficiency() * GetCells();
        public float EffectivePower(float baseHeat, float basePower) => MaxPower(basePower) * DutyCycle(baseHeat);
        public float FuelUsageRate(float baseHeat) => GetCells() * DutyCycle(baseHeat);
        public float EnergyPerFuel(float basePower, float timeMin) => basePower * GetPowerEfficiency() * timeMin * 1200;
        public string GetSummary(float baseHeat, float basePower) => $"{FuelUsageRate(baseHeat):F1}x, {GetPowerEfficiency() * 100:F1}%, {EffectivePower(baseHeat, basePower):F0} RF/t";

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
                if (this[pos].IsValid(GetNeighbors(pos)))
                    return;
                if (this[pos] == Block.MODERATOR)
                    this[pos] = Block.ACTIVE_MODERATOR;
                else if (this[pos] == Block.ACTIVE_MODERATOR)
                    this[pos] = Block.MODERATOR;
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
                        sb.Append(blocks_[x, y, z].Symbol);
                    }
                }
            }
            return sb.ToString();
        }
    }
}
