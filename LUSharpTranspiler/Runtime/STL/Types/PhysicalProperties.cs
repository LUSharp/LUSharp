
using LUSharpTranspiler.Runtime.STL.Enums;

namespace LUSharpTranspiler.Runtime.STL.Types
{
    public class PhysicalProperties
    {
        public PhysicalProperties(Material material)
        {
            throw new NotImplementedException();
        }

        public PhysicalProperties(double density, double friction, double elasticity)
        {
            throw new NotImplementedException();
        }

        public PhysicalProperties(double density, double friction, double elasticity, double frictionWeight, double elasticityWeight)
        {

        }

        public double AcousticAbsorption { get; set; }
        public double Density { get; set; }
        public double Friction { get; set; }

        public double Elasticity { get; set; }
        public double FrictionWeight { get; set; }
        public double ElasticityWeight { get; set; }
    }
}
