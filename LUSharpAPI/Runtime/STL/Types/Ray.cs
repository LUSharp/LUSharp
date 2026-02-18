
namespace LUSharpAPI.Runtime.STL.Types
{
    public class Ray
    {
        public Ray(Vector3 origin, Vector3 direction)
        {
            throw new NotImplementedException();
        }

        public Ray Unit{ get; set; }
        public Vector3 Origin{ get; set; }
        public Vector3 Direction{ get; set; }

        public Vector3 ClosestPoint(Vector3 point)
        {
            throw new NotImplementedException();
        }
        public double Distance(Vector3 point)
        {
            throw new NotImplementedException();
        }
    }
}
