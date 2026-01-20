
using System.ComponentModel;
using System.Numerics;
using LUSharpAPI.Runtime.STL.Enums;

namespace LUSharpAPI.Runtime.STL.Types
{
    public class CFrame
    {
        // Constructors
        public CFrame()
        {
        }

        public CFrame(Vector3 position)
        {

        }

        public CFrame(Vector3 pos, Vector3 lookAt)
        {

        }

        public CFrame(double x, double y, double z)
        {

        }

        public CFrame(double x, double y, double z, double qX, double qY, double qZ, double qW)
        {

        }

        public CFrame(double x, double y, double z, double R00, double R01, double R02, double R10, double R11, double R12, double R20, double R21, double R22)
        {

        }

        public CFrame lookAt(Vector3 at, Vector3 lookAt, Vector3 up)
        {
            throw new NotImplementedException();
        }

        public CFrame lookAlong(Vector3 at, Vector3 direction, Vector3 up)
        {
            throw new NotImplementedException();
        }

        public CFrame fromRotationBetweenVectors(Vector3 from, Vector3 to)
        {
            throw new NotImplementedException();
        }

        public CFrame fromEulerAngles(double rx, double ry, double rz, RotationOrder order = RotationOrder.XYZ)
        {
            throw new NotImplementedException();
        }

        public CFrame fromEulerAnglesXYZ(double rx, double ry, double rz)
        {
            throw new NotImplementedException();
        }

        public CFrame fromEulerAnglesYXZ(double rx, double ry, double rz)
        {
            throw new NotImplementedException();
        }

        public CFrame Angles(double rx, double ry, double rz)
        {
            throw new NotImplementedException();
        }

        public CFrame fromOrientation(double rx, double ry, double rz)
        {
            throw new NotImplementedException();
        }

        public CFrame fromAxisAngle(Vector3 axis, double angle)
        {
            throw new NotImplementedException();
        }

        public CFrame fromMatrix(Vector3 pos, Vector3 vX, Vector3 vY, Vector3 vZ)
        {
            throw new NotImplementedException();
        }
    }
}