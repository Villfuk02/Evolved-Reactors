using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Fission
{
    internal class Evolver
    {
        readonly int depth;
        readonly Random rand = new();
        readonly Stopwatch sw = new();
        readonly int populationSize, stagnationThreshold;
        readonly double singleParentChance;
        double temperature = 0.1;
        Vector3 size;
        public (Layout specimen, double fitness)[] lastGeneration = Array.Empty<(Layout specimen, double fitness)>();
        int stagnation = 0;
        public int generation = 0;
        (Layout specimen, double fitness) lastBest = (null, double.NaN);
        readonly EvolutionController controller;
        readonly EvolutionSettings settings;
        readonly FuelStatsOption fuel;
        public Evolver(EvolutionController controller, int depth, Vector3 size, int populationSize = 50, int stagnationThreshold = 1200, double singleParentChance = 0.2)
        {
            this.controller = controller;
            fuel = controller.fuel;
            settings = controller.settings;
            this.depth = depth;
            this.size = size;
            this.populationSize = populationSize;
            this.stagnationThreshold = stagnationThreshold;
            this.singleParentChance = singleParentChance;
            sw.Start();
        }

        public void OneGeneration()
        {
            generation++;
            List<(Layout specimen, double fitness)> newGeneration = new();
            if (generation == 1)
            {
                //keep all previous
                newGeneration.AddRange(lastGeneration);
                //fill out with new
                while (newGeneration.Count < populationSize)
                {
                    newGeneration.Add((GenerateNew(), double.NaN));
                }
            }
            else
            {
                // survive
                for (int i = 0; i < lastGeneration.Length; i++)
                {
                    if (rand.NextDouble() >= (rand.NextDouble() < temperature ? 0.5 : (double)i / lastGeneration.Length))
                        newGeneration.Add(lastGeneration[i]);
                }
                // add new            
                for (int i = 0; i < populationSize * temperature * 4; i++)
                {
                    newGeneration.Add((GenerateNew(), double.NaN));
                }
                // reproduce
                while (newGeneration.Count < populationSize)
                {
                    Layout n;
                    if (rand.NextDouble() < singleParentChance)
                        n = newGeneration[rand.Next(newGeneration.Count)].specimen;
                    else
                        n = Cross(newGeneration[rand.Next(newGeneration.Count)].specimen, newGeneration[rand.Next(newGeneration.Count)].specimen);
                    n = Mutate(n);
                    newGeneration.Add((n, double.NaN));
                }
            }
            // score
            for (int i = 0; i < newGeneration.Count; i++)
            {
                if (controller.resetStats || double.IsNaN(newGeneration[i].fitness))
                {
                    Layout specimen = newGeneration[i].specimen;
                    newGeneration[i] = (specimen, GetFitness(specimen));
                }
            }
            // sort and store
            lastGeneration = newGeneration.OrderBy(p => -p.fitness).ToArray();

            controller.gens++;
            PostGeneration();
        }
        protected double GetFitness(Layout l)
        {
            if (l.GetCells() == 0)
                return double.NegativeInfinity;
            double s = settings.powerWeight * l.EffectivePower(fuel.Value.heat, fuel.Value.power)
                + settings.efficiencyWeight * l.GetPowerEfficiency() * 100
                + settings.cellWeight * l.GetCells()
                + settings.airWeight * l.GetAir();
            if (l.IsSafe(fuel.Value.heat) || settings.unsafePenalty == 0)
                return s;
            if (l.GetCooling() == 0)
                return double.NegativeInfinity;
            return s - settings.unsafePenalty * (1 + l.ProjectedHeat(fuel.Value.heat) / l.GetCooling());
        }
        protected Layout GenerateNew()
        {
            Layout l = new(size);
            double volume = size.Volume;
            double cc = lastGeneration.Length > 0 ? lastGeneration[0].specimen.GetCells() / volume : 0.2;
            double mc = settings.allowModerators ? (lastGeneration.Length > 0 ? lastGeneration[0].specimen.GetModerators() / volume : cc) : 0;
            size.ForEachInside(p =>
            {
                double r = rand.NextDouble();
                if (r < 0.1)
                {
                    //FULL RANDOM
                    r = rand.NextDouble();
                    if (r < 0.2)
                        l.SetAt(p, Block.REACTOR_CELL);
                    else if (r < 0.2 + 0.4)
                        l.SetAt(p, Block.ACTIVE_MODERATOR);
                }
                else
                {
                    //WEIGHTED
                    r = rand.NextDouble();
                    if (r < cc)
                        l.SetAt(p, Block.REACTOR_CELL);
                    else if (r < cc + mc)
                        l.SetAt(p, Block.ACTIVE_MODERATOR);
                }
            });
            l.FixModerators();
            Fill(l);
            Fill(l);
            Fill(l);
            size.ForEachInside(l.Validate);
            return l;
        }
        protected Layout Cross(Layout a, Layout b)
        {
            Vector3 start = new(rand.Next(size.X), rand.Next(size.Y), rand.Next(size.Z));
            Vector3 end = new(rand.Next(start.X, size.X), rand.Next(start.Y, size.Y), rand.Next(start.Z, size.Z));
            Layout l = new(size);
            size.ForEachInside(p => l.SetAt(p, (p.InBounds(start, end) ? b : a).GetAt(p)));
            return l;
        }
        public void Fill(Layout l)
        {
            size.ForEachInside(p =>
            {
                if (l.GetAt(p) == Block.AIR)
                {
                    List<Block> valid = new();
                    foreach (Block b in settings.placeableNonCore)
                    {
                        if (b.IsValid(l.GetNeighbors(p)))
                            valid.Add(b);
                    }
                    if (valid.Count > 0)
                        l.SetAt(p, valid[rand.Next(valid.Count)]);
                }
            });
        }
        protected Layout Mutate(Layout o)
        {
            Layout l = new(size);
            bool changeX = rand.NextDouble() < 0.5;
            bool changeY = rand.NextDouble() < 0.5;
            bool changeZ = rand.NextDouble() < 0.5;
            if (rand.NextDouble() < 0.4)
            {
                //rotations
                size.ForEachInside(p => l.SetAt(new((p.X + (changeX ? 1 : 0)) % size.X, (p.Y + (changeY ? 1 : 0)) % size.Y, (p.Z + (changeZ ? 1 : 0)) % size.Z), o.GetAt(p)));
            }
            else
            {
                // flips
                size.ForEachInside(p => l.SetAt(new(changeX ? size.X - 1 - p.X : p.X, changeY ? size.Y - 1 - p.Y : p.Y, changeZ ? size.Z - 1 - p.Z : p.Z), o.GetAt(p)));
            }
            int iter = rand.NextDouble() < 0.2 ? (size.X + size.Y + size.Z) / 3 : 1;
            for (int i = 0; i < iter; i++)
            {
                Vector3 pos = new(rand.Next(size.X), rand.Next(size.Y), rand.Next(size.Z));
                if (rand.NextDouble() < 0.07)
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
                        l.SetAt(pos, valid[rand.Next(valid.Count)]);
                }
            }
            l.FixModerators();
            Fill(l);
            size.ForEachInside(l.Validate);
            return l;
        }
        protected void PostGeneration()
        {
            if (depth == 0 && controller.resetStats)
                controller.resetStats = false;
            if (sw.Elapsed > TimeSpan.FromMilliseconds(100))
            {
                sw.Restart();
                controller.LogInfo(lastGeneration, generation, depth, temperature);
            }
            temperature *= 0.99;
            if (lastBest.fitness == lastGeneration[0].fitness)
            {
                stagnation++;
                if (stagnation >= stagnationThreshold)
                {
                    Evolver e = new(controller, depth + 1, size, populationSize, stagnationThreshold, singleParentChance);
                    for (int i = 0; i < generation; i++)
                    {
                        e.OneGeneration();
                        if (controller.resetStats)
                            break;
                    }
                    lastGeneration = lastGeneration.Concat(e.lastGeneration).OrderBy(p => -p.fitness).ToArray();
                    temperature *= 10;
                    stagnation = 0;
                }
            }
            else
            {
                lastBest = lastGeneration[0];
                stagnation = 0;
            }
        }
    }
}
