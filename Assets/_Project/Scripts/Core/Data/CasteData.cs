using System;
using System.Collections.Generic;
using UnityEngine;

namespace Myrmutation.Core.Data
{
    [CreateAssetMenu(menuName = "Myrmutation/Caste", fileName = "Caste_")]
    public class CasteData : ScriptableObject
    {
        public CasteType caste;
        public string displayName;

        [Header("Visual")]
        public float bodyScale = 1f;
        public float headScale = 1f;
        public Color tint = Color.white;

        [Header("Cría")]
        [Tooltip("Comida necesaria para criar una larva de esta casta")]
        public int foodCostToRaise = 5;

        [Header("Stats")]
        public AntStats baseStats = AntStats.Default;

        [Header("Afinidades: bonus de eficiencia por sala (0 = normal, +50 = x1.5, -50 = x0.5)")]
        public List<RoomAffinity> affinities = new List<RoomAffinity>();

        public float GetAffinity(RoomType room)
        {
            foreach (var a in affinities) if (a.room == room) return Mathf.Max(0f, 1f + a.bonusPercent / 100f);
            return 1f;
        }
    }

    [Serializable]
    public struct RoomAffinity
    {
        public RoomType room;
        [Tooltip("0 = normal, +50 = x1.5, -50 = x0.5")]
        public float bonusPercent;
    }
}
