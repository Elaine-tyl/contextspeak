using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Cafe
{
    public static class LifeRoutes
    {
        public static IEnumerable<Vector3> To(Vector3 from, LifeNpc npc)
        {
            // Accept complete paths only. A partial route would strand the learner at a wall.
            if (!NavMesh.SamplePosition(from, out var start, 1, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(npc.Stand, out var end, 0.4f, NavMesh.AllAreas))
                return Array.Empty<Vector3>();
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
                return Array.Empty<Vector3>();
            var points = new List<Vector3>(path.corners);
            if (points.Count > 0) points.RemoveAt(0);
            points.Add(npc.Stand);
            return points;
        }
    }
}
