using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fission
{
    internal abstract class Option<T>
    {
        public T value;
        protected readonly string description;
        public T Value => value;

        protected Option(T value, string description)
        {
            this.value = value;
            this.description = description;
        }
        public void LoadFromInput()
        {
            string inputInstructions = InputInstructions();
            string input;
            do
            {
                if (inputInstructions is null)
                    Console.Write($"{description} ({DisplayValue()}): ");
                else
                    Console.Write($"{description} ({DisplayValue()}) [{inputInstructions}]: ");
                input = Console.ReadLine();
                if (value != null && (input is null || input.Trim() is ""))
                    break;
            } while (!ParseInput(input.Trim(), out value));
        }
        protected abstract bool ParseInput(string input, out T value);
        protected virtual string DisplayValue() => value.ToString();
        protected virtual string InputInstructions() { return null; }
        public static implicit operator T(Option<T> o) => o.value;
    }

    internal class IntOption : Option<int>
    {
        public IntOption(int value, string description) : base(value, description) { }
        protected override bool ParseInput(string input, out int value) => int.TryParse(input, out value);
    }
    internal class DoubleOption : Option<double>
    {
        public DoubleOption(double value, string description) : base(value, description) { }
        protected override bool ParseInput(string input, out double value) => double.TryParse(input, out value);
    }
    internal class BoolOption : Option<bool>
    {
        public BoolOption(bool value, string description) : base(value, description) { }
        protected override bool ParseInput(string input, out bool value)
        {
            value = input.ToLower() is "y";
            return input.ToLower() is "y" or "n";
        }
        protected override string InputInstructions() => "y/n";
    }

    internal class Vector3Option : Option<Vector3>
    {
        public Vector3Option(Vector3 value, string description) : base(value, description) { }
        protected override bool ParseInput(string input, out Vector3 value)
        {
            value = new();
            string[] parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0].Trim(), out int x)) return false;
            if (!int.TryParse(parts[1].Trim(), out int y)) return false;
            if (!int.TryParse(parts[2].Trim(), out int z)) return false;
            value = new(x, y, z);
            return true;
        }
        protected override string InputInstructions() => "x, y, z";
        protected override string DisplayValue() => $"{value.X}, {value.Y}, {value.Z}";
    }
    internal class BlockSetOption : Option<HashSet<Block>>
    {
        readonly Block[] options_;
        public BlockSetOption(IEnumerable<Block> options, string description) : base(new(options), "Configure " + description)
        {
            options_ = options.ToArray();
        }

        protected override bool ParseInput(string input, out HashSet<Block> value)
        {
            value = this.value;
            if (input.ToLower() == "n")
                return true;
            if (input.ToLower() != "y")
                return false;
            HashSet<Block> newBlocks = new();
            foreach (Block o in options_)
            {
                BoolOption include = new(this.value.Contains(o), o.ToString());
                include.LoadFromInput();
                if (include)
                    newBlocks.Add(o);
            }
            value = newBlocks;
            return true;
        }
        protected override string InputInstructions() => "y";
        protected override string DisplayValue()
        {
            StringBuilder sb = new();
            foreach (Block b in value)
            {
                sb.Append(b.Symbol);
            }
            return sb.ToString();
        }
    }

    internal class FuelOption : Option<Fuel>
    {
        public FuelOption() : base(null, "Fuel") { }

        protected override bool ParseInput(string input, out Fuel value)
        {
            value = null;
            if (input.Trim() == "" || string.Equals(input.Trim(), "ADD", StringComparison.InvariantCultureIgnoreCase))
            {
                var o = new AddFuelOption();
                o.LoadFromInput();
                value = o;
            }
            else
            {
                value = Fuel.ALL.Find(f => string.Equals(input.Trim(), f.Name, StringComparison.InvariantCultureIgnoreCase));
            }
            return value != null;
        }
        protected override string InputInstructions() => $" {string.Concat(Fuel.ALL.Select(f => f.Name.ToLowerInvariant() + " | "))}ADD ";

        protected override string DisplayValue() => value == null ? "" : value.ToString();
    }

    internal class AddFuelOption : Option<Fuel>
    {
        public AddFuelOption() : base(null, "Add fuel") { }

        protected override bool ParseInput(string input, out Fuel value)
        {
            value = null;
            string[] parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return false;
            string n = parts[0].Trim();
            if (!float.TryParse(parts[1].Trim(), out float p)) return false;
            if (!float.TryParse(parts[2].Trim(), out float h)) return false;
            if (Fuel.ALL.Any(f => string.Equals(n, f.Name, StringComparison.InvariantCultureIgnoreCase)))
            {
                Console.WriteLine($"Fuel with name {n} already exists!");
                return false;
            }

            value = new(n, p, h);
            Fuel.ALL.Add(value);
            return true;
        }
        protected override string InputInstructions() => "name, base power (RF/t), base heat (H/t)";
        protected override string DisplayValue() => value == null ? "" : value.ToString();
        public bool Parse(string input, out Fuel value) => ParseInput(input, out value);
    }
}
