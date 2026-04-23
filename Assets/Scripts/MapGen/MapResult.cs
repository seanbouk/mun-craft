using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    public struct MapResult
    {
        public List<BlockAddress> FilledBlocks;
        public Vector3 SpawnPosition;
        public Vector3 SpawnUp;
    }
}
