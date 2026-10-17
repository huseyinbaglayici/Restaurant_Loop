using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Builds planar UVs for a corner belt mesh, centered on the chosen corner, so ConveyorCornerVisual can
    // rotate them around a pivot instead of scrolling them in a straight line.
    public static class CornerUvGenerator
    {
        public static Vector2[] Generate(
            Vector3[] verts,
            ConveyorCornerVisual.ProjectionPlane plane,
            ConveyorCornerVisual.CornerAnchor anchor,
            float textureScale,
            Vector2 uvOffset,
            Vector2 manualPivot)
        {
            if (verts == null || verts.Length == 0) return null;

            ResolveAxes(verts, plane, out int axisU, out int axisV);

            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;

            for (int i = 0; i < verts.Length; i++)
            {
                float uVal = GetAxis(verts[i], axisU);
                float vVal = GetAxis(verts[i], axisV);
                minU = Mathf.Min(minU, uVal); maxU = Mathf.Max(maxU, uVal);
                minV = Mathf.Min(minV, vVal); maxV = Mathf.Max(maxV, vVal);
            }

            float spanU = maxU - minU > 0.0001f ? maxU - minU : 1f;
            float spanV = maxV - minV > 0.0001f ? maxV - minV : 1f;
            float radius = Mathf.Max(spanU, spanV);

            Vector2 origin;
            Vector2 dirSign;

            switch (anchor)
            {
                case ConveyorCornerVisual.CornerAnchor.BottomRight:
                    origin = new Vector2(maxU, minV);
                    dirSign = new Vector2(-1f, 1f);
                    break;
                case ConveyorCornerVisual.CornerAnchor.TopLeft:
                    origin = new Vector2(minU, maxV);
                    dirSign = new Vector2(1f, -1f);
                    break;
                case ConveyorCornerVisual.CornerAnchor.TopRight:
                    origin = new Vector2(maxU, maxV);
                    dirSign = new Vector2(-1f, -1f);
                    break;
                case ConveyorCornerVisual.CornerAnchor.Center:
                    origin = new Vector2((minU + maxU) * 0.5f, (minV + maxV) * 0.5f);
                    dirSign = new Vector2(1f, 1f);
                    break;
                case ConveyorCornerVisual.CornerAnchor.ManualCustom:
                    origin = manualPivot;
                    dirSign = Vector2.one;
                    break;
                default: // BottomLeft
                    origin = new Vector2(minU, minV);
                    dirSign = new Vector2(1f, 1f);
                    break;
            }

            var uv = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float uVal = GetAxis(verts[i], axisU);
                float vVal = GetAxis(verts[i], axisV);

                float normU = ((uVal - origin.x) / radius) * dirSign.x * textureScale;
                float normV = ((vVal - origin.y) / radius) * dirSign.y * textureScale;

                float u = 0.5f + normU * 0.5f + uvOffset.x;
                float v = 0.5f + normV * 0.5f + uvOffset.y;

                uv[i] = new Vector2(u, v);
            }

            return uv;
        }

        // AutoDetect projects onto the plane perpendicular to the mesh's thinnest axis.
        private static void ResolveAxes(Vector3[] verts, ConveyorCornerVisual.ProjectionPlane plane, out int axisU, out int axisV)
        {
            axisU = 0; // X
            axisV = 2; // Z

            if (plane == ConveyorCornerVisual.ProjectionPlane.XY_Vertical)
            {
                axisU = 0;
                axisV = 1;
                return;
            }

            if (plane != ConveyorCornerVisual.ProjectionPlane.AutoDetect) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < verts.Length; i++)
            {
                minX = Mathf.Min(minX, verts[i].x); maxX = Mathf.Max(maxX, verts[i].x);
                minY = Mathf.Min(minY, verts[i].y); maxY = Mathf.Max(maxY, verts[i].y);
                minZ = Mathf.Min(minZ, verts[i].z); maxZ = Mathf.Max(maxZ, verts[i].z);
            }

            float spanX = maxX - minX;
            float spanY = maxY - minY;
            float spanZ = maxZ - minZ;

            if (spanY <= spanX && spanY <= spanZ)
            {
                axisU = 0; axisV = 2; // XZ plane
            }
            else if (spanZ <= spanX && spanZ <= spanY)
            {
                axisU = 0; axisV = 1; // XY plane (e.g. models imported with -90 X)
            }
            else
            {
                axisU = 1; axisV = 2; // YZ plane
            }
        }

        private static float GetAxis(Vector3 v, int axis)
        {
            return axis switch
            {
                0 => v.x,
                1 => v.y,
                _ => v.z
            };
        }
    }
}
