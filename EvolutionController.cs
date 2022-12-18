using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Fission
{
    internal class EvolutionController
    {
        double bestFitness = double.NegativeInfinity;
        Layout bestLayout = new(Vector3.ZERO);
        (Layout specimen, double fitness)[] bestGen = null;
        public int gens = 0;
        Evolver mainBranch;
        public readonly FuelStatsOption fuel = new();
        public readonly EvolutionSettings settings = new();
        public readonly Vector3Option size = new(new(5, 5, 5), "Size");
        public bool resetStats = false;
        int popSize, stagnationThreshold;
        public void Start()
        {
            size.LoadFromInput();
            fuel.LoadFromInput();
            settings.LoadFromInput();
            int volume = size.Value.Volume;
            popSize = Math.Max(50, (int)Math.Round(Math.Pow(volume, 0.333) * 10));
            stagnationThreshold = 1200;
            mainBranch = new(this, 0, size, popSize, stagnationThreshold);
            while (true)
            {
                mainBranch.OneGeneration();
            }
        }
        public void Message(string msg)
        {
            Console.WriteLine(msg);
            if (Console.KeyAvailable)
            {
                DisplayBest();
            }
        }

        public void LogInfo((Layout specimen, double fitness)[] generation, int genNum, int depth, double temperature)
        {
            double fit = generation[0].fitness;
            Layout rep = generation[0].specimen;
            if (fit > bestFitness)
            {
                bestFitness = fit;
                bestLayout = rep;
                bestGen = generation;
            }
            int bestInstCount = generation.Count(p => p.fitness == bestFitness);
            double avg = generation.Average(p => p.fitness);
            Message($"[{gens}] Best: {bestFitness:F0} ({bestInstCount}x) |{new string('>', depth)} {genNum}: Current best: {fit:F0} ({rep.GetPowerEfficiency() * 100:F1}%, {rep.EffectivePower(fuel.Value.heat, fuel.Value.power):F0} RF/t), avg: {avg:F0}, temp: {temperature:G2}");
        }

        void ResetStats()
        {
            resetStats = true;
            bestFitness = double.NegativeInfinity;
        }

        public void DisplayBest()
        {
            Console.ReadKey(true);
            Console.WriteLine();
            bestLayout.Print();
            Console.WriteLine();
            bestLayout.PrintFuelInfo(fuel);
            bestLayout.PrintInfo();
            Console.WriteLine();
            Console.WriteLine($"Change settings [C], Change Fuel [F], Inject design [D], any other key to resume");
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.C)
            {
                settings.LoadFromInput();
                ResetStats();
            }
            else if (k.Key == ConsoleKey.F)
            {
                fuel.LoadFromInput();
                ResetStats();
            }
            else if (k.Key == ConsoleKey.D)
            {
                Layout l = Layout.LoadFromInput(size);
                Console.WriteLine();
                l.PrintInfo();
                Console.WriteLine();
                List<(Layout, double)> injection = new();
                for (int i = 0; i < bestGen.Length; i++)
                {
                    injection.Add((l.Copy(), double.NaN));
                }
                BoolOption b = new(true, "Fill air with randomized components");
                b.LoadFromInput();
                Evolver e = new(this, 1, size, popSize, stagnationThreshold);
                if (b)
                {
                    for (int i = 0; i < injection.Count; i++)
                    {
                        e.Fill(injection[i].Item1);
                    }
                }
                e.lastGeneration = injection.ToArray();
                for (int i = 0; i < mainBranch.generation; i++)
                {
                    e.OneGeneration();
                }
                mainBranch.lastGeneration = e.lastGeneration.Concat(bestGen).OrderBy(p => -p.fitness).ToArray();
                ResetStats();
            }
            Console.WriteLine();
        }
    }
}
