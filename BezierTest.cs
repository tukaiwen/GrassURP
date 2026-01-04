using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bezier
{
    public class BezierTest : MonoBehaviour
    {
        public Transform p0;
        public Transform p1;
        public Transform p2;
        public Transform p3;
        public int segments = 20;
        public float gizmoSize = 0.1f;
        public Color curveColor = Color.green;
        public Color controlPointColor = Color.yellow;

        public static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float omt = 1f - t;
            float omt2 = omt * omt;
            float t2 = t * t;
            return p0 * (omt2 * omt) +
                p1 * (3 * t * omt2) +
                p2 * (3 * omt * t2) +
                p3 * (t2 * t);
        }

        private void OnDrawGizmos()
        {
            if(p0 == null || p1 == null || p2 == null || p3 == null)
            {
                return;
            }
            Gizmos.color = controlPointColor;
            Gizmos.DrawSphere(p0.position, gizmoSize);
            Gizmos.DrawSphere(p1.position, gizmoSize);
            Gizmos.DrawSphere(p2.position, gizmoSize);
            Gizmos.DrawSphere(p3.position, gizmoSize);

            Gizmos.color = Color.gray;
            Gizmos.DrawLine(p0.position, p1.position);
            Gizmos.DrawLine(p2.position, p3.position);
            Gizmos.DrawLine(p1.position, p2.position);

            Gizmos.color = curveColor;
            Vector3 previousPoint = p0.position;
            for(int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 currentPoint = CubicBezier(p0.position, p1.position, p2.position, p3.position, t);
                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }
    }
}