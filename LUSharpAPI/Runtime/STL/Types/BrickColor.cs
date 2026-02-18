
using System.ComponentModel;
using System.Diagnostics.Contracts;

namespace LUSharpAPI.Runtime.STL.Types
{
    public class BrickColor
    {
        public BrickColor(double val)
        {

        }

        public BrickColor(double r, double g, double b)
        {

        }

        public BrickColor(string name)
        {

        }
        public BrickColor pallete(double paletteValue)
        {
            return new BrickColor(paletteValue);
        }

        public BrickColor random()
        {
            throw new NotImplementedException();
        }

        public BrickColor White()
        {
            throw new NotImplementedException();
        }
        public BrickColor Gray()
        {
            throw new NotImplementedException();
        
        }
        public BrickColor DarkGray()
        {
            throw new NotImplementedException();
        }
        public BrickColor Black()
        {
            throw new NotImplementedException();
        }
        public BrickColor Red()
        {
            throw new NotImplementedException();
        }
        public BrickColor Yellow()
        {
            throw new NotImplementedException();
        }
        public BrickColor Green()
        {
            throw new NotImplementedException();
        }
        public BrickColor Blue()
        {
            throw new NotImplementedException();
        }

        public double Number{ get; set; }

        public double r{ get; set; }
        public double g{ get; set; }
        public double b{ get; set; }

        public string Name { get; set; }
        public Color3 Color { get; set; }
    }
}