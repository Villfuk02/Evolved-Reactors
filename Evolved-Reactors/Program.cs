﻿using System;

namespace Fission
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string configPath = "";
            if (args.Length == 1)
                configPath = args[0];
            while (configPath == "")
            {
                Console.Write("Provide a path to a configuration file: ");
                configPath = Console.ReadLine();
                if (configPath == null)
                    return;
            }

            string error = ConfigLoader.LoadConfig(configPath);
            if (error != null)
            {
                Console.WriteLine($"ERROR: {error}");
                Console.ReadKey(true);
                return;
            }

            Console.WriteLine("Select mode: [T] Test layout | [G] Generate layout");
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.G)
            {
                EvolutionController controller = new();
                controller.Start();
            }
            else if (k.Key == ConsoleKey.T)
            {
                Layout l = Layout.LoadFromInput();
                Console.WriteLine();
                l.PrintInfo();
                Console.WriteLine();
                FuelOption fuel = new();
                while (true)
                {
                    fuel.LoadFromInput();
                    l.PrintFuelInfo(fuel);
                    Console.WriteLine();
                }
            }
        }
    }
}