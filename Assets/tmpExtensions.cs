using TrueSync;
using UnityEngine;

namespace Core.Algorithm
{
    public static class tmpExtensions
    {
        public static Vector2 RotateRad(this Vector2 self, float rad)
        {
            float cos = Mathf.Cos(rad), sin = Mathf.Cos(rad);
            return new Vector2(self.x * cos - self.y * sin, self.x * sin + self.y * cos);
        }
        /// <summary>
        /// 旋转一个二维向量
        /// </summary>
        /// <param name="self">该向量</param>
        /// <param name="rad">弧度</param>
        /// <returns></returns>
        public static TSVector2 RotateRad(this TSVector2 self, FP rad, TSVector2 original = (default))
        {
            self -= original;
            FP cos = FP.FastCos(rad), sin = FP.FastSin(rad);
            return new TSVector2(self.x * cos - self.y * sin, self.x * sin + self.y * cos) + original;
        }
        public static TSVector RotateRad(this TSVector self, FP rad, TSVector original = (default))
        {
            self -= original;
            FP cos = FP.FastCos(rad), sin = FP.FastSin(rad);
            return new TSVector(self.x * cos - self.y * sin, self.x * sin + self.y * cos, self.z) + original;
        }
        /// <summary>
        /// 旋转一个二维向量
        /// </summary>
        /// <param name="self">该向量</param>
        /// <param name="angle">角度</param>
        /// <returns></returns>
        public static TSVector2 RotateAngle(this TSVector2 self, FP angle, TSVector2 original = (default)) {
            return RotateRad(self, angle * TSMath.Deg2Rad, original);
        }
        public static TSVector RotateAngle(this TSVector self, FP angle, TSVector original = (default))
        {
            return RotateRad(self, angle * TSMath.Deg2Rad, original);
        }
    }
}