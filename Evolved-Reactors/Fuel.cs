using System.Collections.Generic;

namespace Fission
{
    internal class Fuel
    {
        public string Name { get; init; }
        public float power;
        public float heat;

        public static readonly List<Fuel> ALL = new();

        public Fuel(string name, float power, float heat)
        {
            Name = name;
            this.power = power;
            this.heat = heat;
        }

        public override string ToString() => $"{Name} ({power}RF/t, {heat}H/t)";
    }
}
