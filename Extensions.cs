using System;
using VRageMath;

namespace Digi.ParticleEditor
{
    public static class Extensions
    {
        public static string FormatVector(this Vector4 vec, int round = 1)
        {
            return $"{Math.Round(vec.X, round)} {Math.Round(vec.Y, round)} {Math.Round(vec.Z, round)} {Math.Round(vec.W, round)}";
        }

        public static string FormatVector(this Vector3 vec, int round = 1)
        {
            return $"{Math.Round(vec.X, round)} {Math.Round(vec.Y, round)} {Math.Round(vec.Z, round)}";
        }

        public static float GetDim(this Vector4 vec, int dim)
        {
            switch(dim)
            {
                case 0: return vec.X;
                case 1: return vec.Y;
                case 2: return vec.Z;
                case 3: return vec.W;
                default: throw new ArgumentException($"Vector4.GetDim() not supported dim: {dim}");
            }
        }

        public static void SetDim(ref this Vector4 vec, int dim, float value)
        {
            switch(dim)
            {
                case 0: vec.X = value; break;
                case 1: vec.Y = value; break;
                case 2: vec.Z = value; break;
                case 3: vec.W = value; break;
                default: throw new ArgumentException($"Vector4.GetDim() not supported dim: {dim}");
            }
        }

        public static Vector4 GetChangedDim(this Vector4 vec, int dim, float value)
        {
            switch(dim)
            {
                case 0: return new Vector4(value, vec.Y, vec.Z, vec.W);
                case 1: return new Vector4(vec.X, value, vec.Z, vec.W);
                case 2: return new Vector4(vec.X, vec.Y, value, vec.W);
                case 3: return new Vector4(vec.X, vec.Y, vec.Z, value);
                default: throw new ArgumentException($"Vector4.GetDim() not supported dim: {dim}");
            }
        }
    }
}
