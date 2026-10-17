using Myrmutation.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Myrmutation.Core.Data
{
    [Serializable]
    public struct AntStats
    {
        public float strength;       // trabajo de excavación/construcción
        public float speed;          // unidades/segundo
        public float hungerRate;     // hambre que sube por segundo
        public float energyRate;     // energía que baja por segundo
        public float carryCapacity;  // unidades que transporta
        public float workRate;       // multiplicador de producción

        public static AntStats Default => new AntStats
        {
            strength = 1f, speed = 1.5f, hungerRate = 0.02f, energyRate = 0.015f, carryCapacity = 1f, workRate = 1f
        };

        public float Get(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return strength;
                case StatType.Speed: return speed;
                case StatType.HungerRate: return hungerRate;
                case StatType.EnergyRate: return energyRate;
                case StatType.CarryCapacity: return carryCapacity;
                case StatType.WorkRate: return workRate;
                default: return 0f;
            }
        }

        public void Set(StatType stat, float value)
        {
            switch (stat)
            {
                case StatType.Strength: strength = value; break;
                case StatType.Speed: speed = value; break;
                case StatType.HungerRate: hungerRate = value; break;
                case StatType.EnergyRate: energyRate = value; break;
                case StatType.CarryCapacity: carryCapacity = value; break;
                case StatType.WorkRate: workRate = value; break;
            }
        }

        /// <summary>Aplica modificadores: valor = (base + additive) * (1 + percent/100).</summary>
        public AntStats WithModifiers(IEnumerable<StatModifier> modifiers)
        {
            var result = this;
            foreach (var m in modifiers)
                result.Set(m.stat, (result.Get(m.stat) + m.additive) * (1f + m.percent / 100f));
            return result;
        }
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        [Tooltip("Suma fija al valor base (0 = nada)")]
        public float additive;
        [Tooltip("Porcentaje: +50 = +50 %, -30 = -30 % (0 = nada)")]
        public float percent;
    }
}
