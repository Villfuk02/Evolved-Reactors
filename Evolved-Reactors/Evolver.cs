using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Fission
{
    internal class Evolver
    {
        readonly int depth_;
        readonly Random rand_ = new();
        readonly Stopwatch sw_ = new();
        readonly int populationSize_;
        public int stagnationThreshold;
        readonly double singleParentChance_;
        readonly bool canGoDeeper_;
        double temperature_;
        readonly Vector3 size_;
        public (Layout specimen, double fitness)[] lastGeneration = Array.Empty<(Layout specimen, double fitness)>();
        int stagnation_;
        public int generation;
        (Layout specimen, double fitness) lastBest_ = (null, double.NaN);
        readonly EvolutionController controller_;
        public EvolutionSettings settings;
        public Fuel fuel;
        public bool shouldReset;
        public string message = "starting...";
        public bool stagnating;
        public (Layout specimen, double fitness)[] toAdd;
        public Evolver(EvolutionController controller, int depth, Vector3 size, int populationSize = 50, int stagnationThreshold = 2000, bool canGoDeeper = true, double temperature = 0.1, double singleParentChance = 0.2)
        {
            controller_ = controller;
            lock (controller_.controlLock)
            {
                fuel = controller.fuel;
                settings = controller.publicSettings;
            }
            depth_ = depth;
            size_ = size;
            populationSize_ = populationSize;
            this.stagnationThreshold = stagnationThreshold;
            singleParentChance_ = singleParentChance;
            temperature_ = temperature;
            canGoDeeper_ = canGoDeeper;
            sw_.Start();
        }

        public void OneGeneration()
        {
            generation++;
            List<(Layout specimen, double fitness)> newGeneration = new();
            if (toAdd is not null)
            {
                newGeneration.AddRange(toAdd);
                toAdd = null;
            }
            if (generation == 1)
            {
                //keep all previous
                newGeneration.AddRange(lastGeneration);
                //fill out with new
                while (newGeneration.Count < populationSize_)
                {
                    newGeneration.Add((GenerateNew(), double.NaN));
                }
            }
            else
            {
                // survive
                for (int i = 0; i < lastGeneration.Length; i++)
                {
                    if (rand_.NextDouble() >= (rand_.NextDouble() < temperature_ ? 0.5 : (double)i / lastGeneration.Length))
                        newGeneration.Add(lastGeneration[i]);
                }

                // add new            
                for (int i = 0; i < populationSize_ * temperature_ * 4; i++)
                {
                    newGeneration.Add((GenerateNew(), double.NaN));
                }

                // reproduce
                while (newGeneration.Count < populationSize_)
                {
                    Layout n;
                    if (rand_.NextDouble() < singleParentChance_)
                        n = newGeneration[rand_.Next(newGeneration.Count)].specimen;
                    else
                        n = Cross(newGeneration[rand_.Next(newGeneration.Count)].specimen, newGeneration[rand_.Next(newGeneration.Count)].specimen);
                    n = Mutate(n);
                    newGeneration.Add((n, double.NaN));
                }
            }

            // score new layouts
            for (int i = 0; i < newGeneration.Count; i++)
            {
                if (shouldReset || double.IsNaN(newGeneration[i].fitness))
                {
                    Layout specimen = newGeneration[i].specimen;
                    newGeneration[i] = (specimen, GetFitness(specimen));
                }
            }

            // sort and store
            lastGeneration = newGeneration.OrderBy(p => -p.fitness).ToArray();

            lock (controller_.controlLock)
            {
                controller_.gens++;
            }

            PostGeneration();
        }

        protected double GetFitness(Layout l)
        {
            if (l.GetCells() == 0)
                return double.NegativeInfinity;
            double s = settings.powerWeight * l.EffectivePower(fuel.heat, fuel.power)
                       + settings.efficiencyWeight * l.GetPowerEfficiency() * 100
                       + settings.cellWeight * l.GetCells()
                       + settings.airWeight * l.GetAir()
                       + settings.fuelUsageWeight * l.FuelUsageRate(fuel.heat);
            if (l.IsSafe(fuel.heat) || settings.unsafePenalty == 0)
                return s;
            if (l.GetCooling() == 0)
                return double.NegativeInfinity;
            return s - settings.unsafePenalty * (1 + l.NetHeat(fuel.heat) / l.GetCooling());
        }
        protected Layout GenerateNew()
        {
            Layout l = new(size_);
            double volume = size_.Volume;
            double cc = lastGeneration.Length > 0 ? lastGeneration[0].specimen.GetCells() / volume : 0.2;
            double mc = settings.allowModerators ? (lastGeneration.Length > 0 ? lastGeneration[0].specimen.GetModerators() / volume : cc) : 0;
            size_.ForEachInside(p =>
            {
                double r = rand_.NextDouble();
                if (r < 0.1)
                {
                    //FULL RANDOM
                    r = rand_.NextDouble();
                    if (r < 0.2)
                        l.SetAt(p, Block.REACTOR_CELL);
                    else if (settings.allowModerators && r < 0.2 + 0.4)
                        l.SetAt(p, Block.ACTIVE_MODERATOR);
                }
                else
                {
                    //WEIGHTED
                    r = rand_.NextDouble();
                    if (r < cc)
                        l.SetAt(p, Block.REACTOR_CELL);
                    else if (settings.allowModerators && r < cc + mc)
                        l.SetAt(p, Block.ACTIVE_MODERATOR);
                }
            });
            l.FixModerators();
            Fill(l);
            Fill(l);
            Fill(l);
            size_.ForEachInside(l.Validate);
            return l;
        }
        protected Layout Cross(Layout a, Layout b)
        {
            Vector3 start = new(rand_.Next(size_.X), rand_.Next(size_.Y), rand_.Next(size_.Z));
            Vector3 end = new(rand_.Next(start.X, size_.X), rand_.Next(start.Y, size_.Y), rand_.Next(start.Z, size_.Z));
            Layout l = new(size_);
            size_.ForEachInside(p => l.SetAt(p, (p.InBounds(start, end) ? b : a).GetAt(p)));
            return l;
        }
        public void Fill(Layout l)
        {
            size_.ForEachInside(p =>
            {
                if (l.GetAt(p) != Block.AIR) return;
                var valid = settings.placeableNonCore.Where(b => b.IsValid(l.GetNeighbors(p))).ToList();
                if (valid.Count > 0)
                    l.SetAt(p, valid[rand_.Next(valid.Count)]);
            });
        }
        protected Layout Mutate(Layout o)
        {
            Layout l = new(size_);
            bool changeX = rand_.NextDouble() < 0.5;
            bool changeY = rand_.NextDouble() < 0.5;
            bool changeZ = rand_.NextDouble() < 0.5;
            if (rand_.NextDouble() < 0.4)
            {
                //rotations
                size_.ForEachInside(p => l.SetAt(new((p.X + (changeX ? 1 : 0)) % size_.X, (p.Y + (changeY ? 1 : 0)) % size_.Y, (p.Z + (changeZ ? 1 : 0)) % size_.Z), o.GetAt(p)));
            }
            else
            {
                // flips
                size_.ForEachInside(p => l.SetAt(new(changeX ? size_.X - 1 - p.X : p.X, changeY ? size_.Y - 1 - p.Y : p.Y, changeZ ? size_.Z - 1 - p.Z : p.Z), o.GetAt(p)));
            }
            int iterations = rand_.NextDouble() < 0.2 ? (size_.X + size_.Y + size_.Z) / 3 : 1;
            for (int i = 0; i < iterations; i++)
            {
                Vector3 pos = new(rand_.Next(size_.X), rand_.Next(size_.Y), rand_.Next(size_.Z));
                if (rand_.NextDouble() < 0.07)
                {
                    l.SetAt(pos, Block.REACTOR_CELL);
                }
                else
                {
                    List<Block> valid = new();
                    foreach (Block b in settings.placeableNonCore)
                    {
                        if (b.IsValid(l.GetNeighbors(pos)))
                            valid.Add(b);
                    }
                    if (valid.Count > 0)
                        l.SetAt(pos, valid[rand_.Next(valid.Count)]);
                }
            }
            l.FixModerators();
            Fill(l);
            size_.ForEachInside(l.Validate);
            return l;
        }
        protected void PostGeneration()
        {
            if (sw_.Elapsed > TimeSpan.FromMilliseconds(444))
            {
                sw_.Restart();
                controller_.LogInfo(lastGeneration);
                if (lastBest_.specimen != null)
                    message = $"{(depth_ > 0 ? '>' : ' ')} Current best: {lastBest_.fitness:F0} ({lastBest_.specimen.GetSummary(fuel.heat, fuel.power)}), avg: {lastGeneration.Average(p => p.fitness):F0}  [{generation}]";
            }
            temperature_ *= 0.99;
            if (lastBest_.fitness == lastGeneration[0].fitness)
            {
                stagnation_++;
                if (stagnation_ >= stagnationThreshold)
                {
                    if (canGoDeeper_)
                    {
                        Evolver e;
                        lock (controller_.controlLock)
                        {
                            e = new(controller_, depth_ + 1, size_, populationSize_, stagnationThreshold, true, 0.1, singleParentChance_);
                        }

                        for (int i = 0; i < generation; i++)
                        {
                            e.OneGeneration();
                            message = e.message;
                            if (shouldReset)
                                break;
                        }

                        lastGeneration = lastGeneration.Concat(e.lastGeneration).OrderBy(p => -p.fitness).ToArray();
                        temperature_ *= 10;
                        stagnation_ = 0;
                        stagnationThreshold = stagnationThreshold * 3 / 2;
                    }
                    else
                    {
                        stagnating = true;
                    }
                }
            }
            else
            {
                lastBest_ = lastGeneration[0];
                stagnation_ = 0;
            }
            shouldReset = false;
        }
    }
}
