using System.Collections.Generic;
using UnityEngine;

namespace LordsLegacy.Game.Tools.Shared
{
    /// <summary>What a press hit, in priority order (lower value wins).</summary>
    public enum ToolHitKind
    {
        /// <summary>A draft vertex.</summary>
        Vertex = 0,
        /// <summary>An edge's bend handle.</summary>
        Handle = 1,
        /// <summary>A draft edge.</summary>
        Edge = 2,
        /// <summary>A confirmed object of the tool's kind.</summary>
        ConfirmedObject = 3,
        /// <summary>Nothing hit: the ground.</summary>
        Ground = 4,
    }

    /// <summary>A screen-space hit candidate: a point (A == B) or a segment A-B.</summary>
    public readonly struct HitCandidate
    {
        /// <summary>Kind of candidate.</summary>
        public readonly ToolHitKind Kind;
        /// <summary>Caller-defined id, returned with the hit.</summary>
        public readonly int Id;
        /// <summary>Segment start (or the point).</summary>
        public readonly Vector2 A;
        /// <summary>Segment end (or the point).</summary>
        public readonly Vector2 B;

        /// <summary>Creates a segment candidate.</summary>
        public HitCandidate(ToolHitKind kind, int id, Vector2 a, Vector2 b)
        {
            Kind = kind; Id = id; A = a; B = b;
        }

        /// <summary>Creates a point candidate.</summary>
        public static HitCandidate Point(ToolHitKind kind, int id, Vector2 p)
        {
            return new HitCandidate(kind, id, p, p);
        }
    }

    /// <summary>Result of a hit test.</summary>
    public readonly struct ToolHit
    {
        /// <summary>Kind hit; Ground when nothing was in range.</summary>
        public readonly ToolHitKind Kind;
        /// <summary>Id of the candidate hit, or -1 for ground.</summary>
        public readonly int Id;
        /// <summary>Screen distance to the candidate (0 for ground).</summary>
        public readonly float Distance;

        /// <summary>Creates a result.</summary>
        public ToolHit(ToolHitKind kind, int id, float distance)
        {
            Kind = kind; Id = id; Distance = distance;
        }
    }

    /// <summary>Decides what a cursor press hits: vertex, handle, edge, confirmed object, then ground.</summary>
    public static class ToolHitTester
    {
        /// <summary>Default hit radius in screen pixels.</summary>
        public const float DefaultRadius = 12f;

        /// <summary>Returns the highest-priority candidate within radius; ties in a kind go to the nearest.</summary>
        public static ToolHit Test(IReadOnlyList<HitCandidate> candidates, Vector2 cursor, float radius = DefaultRadius)
        {
            var best = new ToolHit(ToolHitKind.Ground, -1, 0f);
            if (candidates == null) return best;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                float d = DistanceToSegment(cursor, c.A, c.B);
                if (d > radius) continue;
                if (best.Kind == ToolHitKind.Ground || c.Kind < best.Kind
                    || (c.Kind == best.Kind && d < best.Distance))
                    best = new ToolHit(c.Kind, c.Id, d);
            }
            return best;
        }

        /// <summary>Distance from point p to segment a-b.</summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 <= 0f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
