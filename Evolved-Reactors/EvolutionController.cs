using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fission
{
    internal class EvolutionController
    {
        double bestFitness_ = double.NegativeInfinity;
        Layout bestLayout_ = new(Vector3.ZERO);
        public int gens = 0;
        Evolver[] mainEvolvers_;
        public readonly FuelOption fuel = new();
        readonly EvolutionSettings settings_ = new();
        public readonly EvolutionSettings publicSettings = new();
        public readonly Vector3Option size = new(new(5, 5, 5), "Size");
        static readonly int MaxThreads = Math.Max(2, Environment.ProcessorCount);
        readonly IntOption threadCount_ = new(2, $"Number of threads (2 to {MaxThreads})");
        int popSize_;
        public readonly object controlLock = new();
        (Layout specimen, double fitness)[] backupGen_;
        int loadBackupIn_ = -1;

        public void Start()
        {
            size.LoadFromInput();
            fuel.LoadFromInput();
            settings_.LoadFromInput();
            publicSettings.Clone(settings_);
            while (true)
            {
                threadCount_.LoadFromInput();
                if (threadCount_ >= 2 && threadCount_ <= MaxThreads)
                    break;
            }

            mainEvolvers_ = new Evolver[threadCount_];
            int volume = size.Value.Volume;
            popSize_ = Math.Max(50, (int)Math.Round(Math.Pow(volume, 0.333) * 10));
            var tasks = new Task[threadCount_ - 1];
            for (int i = 0; i < threadCount_ - 1; i++)
            {
                Evolver e = new(this, 0, size, popSize_, 1500);
                mainEvolvers_[i + 1] = e;
                Console.WriteLine("starting...");
                tasks[i] = Task.Run(() => RunEvolver(e));
            }

            Evolver ee = new(this, 0, size, popSize_, 4000, false);
            mainEvolvers_[0] = ee;
            Console.WriteLine("starting...");
            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                mainEvolvers_[0].OneGeneration();
                if (mainEvolvers_[0].stagnating && bestFitness_ > double.NegativeInfinity)
                {
                    mainEvolvers_[0].stagnating = false;
                    InjectDesign(bestLayout_, true, 5);
                }

                if (backupGen_ != null && loadBackupIn_ > 0)
                {
                    loadBackupIn_--;
                    if (loadBackupIn_ == 0)
                    {
                        loadBackupIn_ = -1;
                        var gen = mainEvolvers_[0].lastGeneration;
                        mainEvolvers_[0].lastGeneration = backupGen_;
                        lock (controlLock)
                        {
                            for (int i = 0; i < threadCount_; i++)
                            {
                                mainEvolvers_[i].toAdd = gen;
                            }
                        }
                        backupGen_ = null;
                    }
                }

                if (Console.KeyAvailable)
                    DisplayBest();

                if (sw.Elapsed.TotalMilliseconds >= 1000)
                {
                    if (Console.CursorTop > threadCount_)
                        Console.CursorTop -= threadCount_;
                    int currentLineCursor = Console.CursorTop;
                    Console.SetCursorPosition(0, Console.CursorTop);
                    for (int i = 0; i < threadCount_ + 1; i++)
                    {
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                    }

                    Console.SetCursorPosition(0, currentLineCursor);
                    lock (controlLock)
                    {
                        Console.WriteLine($"[{gens}] Best: {bestFitness_:F0} ({bestLayout_.GetSummary(fuel.Value.heat, fuel.Value.power)})");
                        for (int i = 0; i < threadCount_; i++)
                        {
                            Console.WriteLine($" ({i:D2}) {mainEvolvers_[i].message}");
                        }
                    }

                    sw.Restart();
                }
            }
            // ReSharper disable once FunctionNeverReturns
        }

        static void RunEvolver(Evolver e)
        {
            while (true)
                e.OneGeneration();
            // ReSharper disable once FunctionNeverReturns
        }

        public void LogInfo((Layout specimen, double fitness)[] generation)
        {
            double fit = generation[0].fitness;
            Layout rep = generation[0].specimen;

            if (!(fit > bestFitness_))
                return;
            lock (controlLock)
            {
                bestFitness_ = fit;
                bestLayout_ = rep;
            }
        }

        void ResetStats()
        {
            lock (controlLock)
            {
                for (int i = 0; i < threadCount_; i++)
                {
                    mainEvolvers_[i].shouldReset = true;
                }

                bestFitness_ = double.NegativeInfinity;
            }
        }

        public void DisplayBest()
        {
            lock (controlLock)
            {
                Console.ReadKey(true);
                Console.WriteLine();
                bestLayout_.Print();
                Console.WriteLine();
                bestLayout_.PrintFuelInfo(fuel);
                bestLayout_.PrintInfo();
            }

            Console.WriteLine();
            Console.WriteLine("Change settings [C], Change Fuel [F], Inject design [D], any other key to resume");
            var k = Console.ReadKey(true);
            switch (k.Key)
            {
                case ConsoleKey.C:
                    settings_.LoadFromInput();
                    publicSettings.Clone(settings_);
                    ResetStats();
                    break;
                case ConsoleKey.F:
                    fuel.LoadFromInput();
                    lock (controlLock)
                    {
                        for (int i = 0; i < threadCount_; i++)
                        {
                            mainEvolvers_[i].fuel = fuel;
                        }
                    }
                    ResetStats();
                    break;
                case ConsoleKey.D:
                    {
                        Layout l = Layout.LoadFromInput(size);
                        Console.WriteLine();
                        l.PrintInfo();
                        Console.WriteLine();
                        BoolOption b = new(true, "Fill air with randomized components");
                        b.LoadFromInput();
                        InjectDesign(l, b, 100);
                        ResetStats();
                        break;
                    }
            }

            Console.WriteLine();
        }

        void InjectDesign(Layout l, bool fillAir, int generations)
        {
            List<(Layout, double)> injection = new();
            for (int i = 0; i < popSize_; i++)
            {
                injection.Add((l.Copy(), double.NaN));
            }

            Evolver e = new(this, 0, size, popSize_, mainEvolvers_[0].stagnationThreshold, false, 0.0001);
            if (fillAir)
            {
                for (int i = 0; i < injection.Count; i++)
                {
                    e.Fill(injection[i].Item1);
                }
            }

            e.lastGeneration = injection.ToArray();
            loadBackupIn_ = generations;
            backupGen_ = mainEvolvers_[0].lastGeneration;
            mainEvolvers_[0] = e;
        }
    }
}
