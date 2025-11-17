
using System.Diagnostics.Contracts;
using System.Reflection.Metadata;
using System.Runtime.Serialization;

namespace LUSharpTranspiler.Runtime.STL.Types
{
    public class Color3
    {
        public Color3(double r, double g, double b)
        {
            throw new NotImplementedException();
        }

        public static Color3 fromRGB(double r, double g, double b)
        {
            throw new NotImplementedException();
        }

        public static Color3 fromHSV(double h, double s, double v)
        {
            throw new NotImplementedException();
        }

        public static Color3 fromHex(string hex)
        {
            throw new NotImplementedException();
        }

        public double R { get; set; }
        public double G { get; set; }
        public double B { get; set; }

        public static Color3 Lerp(Color3 color, double alpha)
        {
            throw new NotImplementedException();
        }

        public static (double, double, double) ToHSV()
        {
            throw new NotImplementedException();
        }

        public static string ToHex()
        {
            throw new NotImplementedException();
        }

        public (double, double, double) toHSV(Color3 color)
        {
            throw new NotImplementedException();
        }
    }
}