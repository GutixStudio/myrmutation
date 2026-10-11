using System.Collections.Generic;
using UnityEngine;

namespace Myrmutation.Core.Data
{
    [CreateAssetMenu(menuName = "Myrmutation/Gene", fileName = "Gene_")]
    public class GeneData : ScriptableObject
    {
        [Header("Info")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Origen: especie o evento del que procede")]
        public string origin;
        public Sprite icon;
        [Tooltip("Marcar si es una tara (defecto)")]
        public bool isFlaw;

        [Header("Visual")]
        public BodySlot slot = BodySlot.None;
        [Tooltip("Pieza que sustituye a la de la ranura (opcional)")]
        public Sprite bodyPart;
        public Color tint = Color.white;

        [Header("Efecto")]
        public List<StatModifier> modifiers = new List<StatModifier>();
        [Tooltip("Etiqueta para efectos de comportamiento que lee la IA (p. ej. 'Glotona', 'Evacuacion')")]
        public string behaviorTag;
    }
}
