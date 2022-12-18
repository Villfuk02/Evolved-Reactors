using System;

namespace Fission
{
    internal class Program
    {
        static void Main(string[] args)
        {
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
                FuelStatsOption fuel = new();
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