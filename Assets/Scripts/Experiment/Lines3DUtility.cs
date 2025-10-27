using System.Collections.Generic;
using UnityEngine;
using ImmersiveMapInterface.Board;

namespace ImmersiveMapInterface.Experiment
{
    public static class Lines3DUtility
    {
        // Generate all unique 4-length lines in the 8x8x8 space.
        public static List<(int p1,int s1,int p2,int s2,int p3,int s3,int p4,int s4)> GenerateAllLines()
        {
            var lines = new List<(int,int,int,int,int,int,int,int)>(4096);

            Vector3Int[] dirs = new[]
            {
                new Vector3Int(1,0,0), new Vector3Int(0,1,0), new Vector3Int(0,0,1),
                new Vector3Int(1,1,0), new Vector3Int(1,-1,0),
                new Vector3Int(1,0,1), new Vector3Int(1,0,-1),
                new Vector3Int(0,1,1), new Vector3Int(0,1,-1),
                new Vector3Int(1,1,1), new Vector3Int(1,1,-1), new Vector3Int(1,-1,1), new Vector3Int(1,-1,-1)
            };

            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                var origin = new Vector3Int(x,y,z);
                foreach (var d in dirs)
                {
                    var end = origin + d * 3;
                    if (!InBounds(end)) continue;
                    int p1 = PoleBasedBoardState.GridToPoleIndex(origin.x, origin.z);
                    int p2 = PoleBasedBoardState.GridToPoleIndex(origin.x + d.x, origin.z + d.z);
                    int p3 = PoleBasedBoardState.GridToPoleIndex(origin.x + d.x*2, origin.z + d.z*2);
                    int p4 = PoleBasedBoardState.GridToPoleIndex(end.x, end.z);
                    lines.Add((p1, origin.y, p2, origin.y + d.y, p3, origin.y + d.y*2, p4, end.y));
                }
            }
            return lines;
        }

        public static bool InBounds(Vector3Int p)
        {
            return (uint)p.x < 8 && (uint)p.y < 8 && (uint)p.z < 8;
        }
    }
}

