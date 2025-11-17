
using LUSharpTranspiler.Runtime.STL.Enums;

namespace LUSharpTranspiler.Runtime.STL.Types
{
    public class Vector3
    {
        // Constructors
        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3 FromNormalId(NormalId normalId)
        {
            throw new NotImplementedException();
        }

        public Vector3 FromAxis(Axis axis)
        {
            throw new NotImplementedException();
        }

        // Properties
        public Vector3 zero => new(0, 0, 0);
        public Vector3 one => new(1, 1, 1);

        public Vector3 xAxis = new(1, 0, 0);
        public Vector3 yAxis = new(0, 1, 0);
        public Vector3 zAxis = new(0, 0, 1);

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vector3 Unit { get; set; }

        // Methods, just shelled functions from Roblox

        public virtual Vector3 Abs(){ throw new NotImplementedException();}
        
        public virtual Vector3 Ceil(){ throw new NotImplementedException();}

        public virtual Vector3 Floor(){ throw new NotImplementedException();}

        public virtual Vector3 Sign(){ throw new NotImplementedException();}

        public virtual Vector3 Cross(Vector3 other){ throw new NotImplementedException();}

        public virtual double Angle(Vector3 other, Vector3 axis){ throw new NotImplementedException();}

        public virtual double Dot(Vector3 other){ throw new NotImplementedException();}

        public bool FuzzyEq(Vector3 other, double epsilon){ throw new NotImplementedException();}

        public Vector3 Lerp(Vector3 goal, double alpha){ throw new NotImplementedException();}

        public Vector3 Max(Vector3 vector){ throw new NotImplementedException();}

        public Vector3 Min(Vector3 vector){ throw new NotImplementedException();}

        public static Vector3 operator +(Vector3 left, Vector3 right){
            return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Vector3 operator -(Vector3 left, Vector3 right){
            return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static Vector3 operator *(Vector3 left, Vector3 right){
            return new Vector3(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
        }

        public static Vector3 operator /(Vector3 left, Vector3 right){
            return new Vector3(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
        }

    }
}
