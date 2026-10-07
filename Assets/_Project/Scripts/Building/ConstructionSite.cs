using System.Collections.Generic;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Sala en obras. Recluta obreras libres (AssignedRoom = esta sala) y avanza según la suma de su fuerza.
    /// La IA (B) debe tratar "trabajar en una sala no construida" como excavar.
    /// Al terminar: libera a las constructoras, llama a Room.CompleteBuild() y se destruye.
    /// </summary>
    public class ConstructionSite : MonoBehaviour
    {
        private BuildManager manager;
        private Room room;
        private float cost, speedMultiplier, progress;
        private int minWorkers, maxWorkers;
        private readonly List<Ant> builders = new List<Ant>();

        private SpriteRenderer roomRenderer;
        private Transform barRoot, barFill;
        private SpriteRenderer barFillRenderer;
        private float recruitTimer;
        private float spriteWidth = 1f;

        public float Progress => progress;
        public int BuilderCount => builders.Count;
        public int MinWorkers => minWorkers;
        public bool HasEnoughWorkers => builders.Count >= minWorkers;

        public void Init(BuildManager m, Room r, float workCost, float speed, int min, int max)
        {
            manager = m;
            room = r;
            cost = Mathf.Max(0.01f, workCost);
            speedMultiplier = speed;
            minWorkers = Mathf.Max(1, min);
            maxWorkers = Mathf.Max(minWorkers, max);
            roomRenderer = GetComponent<SpriteRenderer>();
            CreateBar();
            Recruit();
            UpdateVisuals();
        }

        private void Update()
        {
            builders.RemoveAll(a => a == null || !a.IsAlive || a.AssignedRoom != room);

            recruitTimer -= Time.deltaTime;
            if (recruitTimer <= 0f && builders.Count < maxWorkers) { Recruit(); recruitTimer = 0.5f; }

            float strength = 0f;
            if (builders.Count >= minWorkers)
                foreach (var b in builders) strength += b.Stats.strength;
            else if (Ant.All.Count == 0 && manager.AllowWithoutAnts)
                strength = manager.TestStrength;

            progress += strength * speedMultiplier * Time.deltaTime / cost;
            room.BuildProgress = Mathf.Clamp01(progress);
            UpdateVisuals();

            if (progress >= 1f) Finish();
        }

        private void Recruit()
        {
            foreach (var a in Ant.All)
            {
                if (builders.Count >= maxWorkers) break;
                if (a == null || a.IsQueen || !a.IsAlive || a.AssignedRoom != null) continue;
                a.AssignedRoom = room;
                builders.Add(a);
            }
        }

        private void Finish()
        {
            foreach (var b in builders)
                if (b != null && b.AssignedRoom == room) b.AssignedRoom = null;
            builders.Clear();

            if (roomRenderer != null) { var c = roomRenderer.color; c.a = 1f; roomRenderer.color = c; }
            if (barRoot != null) Destroy(barRoot.gameObject);
            room.CompleteBuild();
            Destroy(this);
        }

        // ---------- Visual ----------
        private void UpdateVisuals()
        {
            if (roomRenderer != null)
            {
                var c = roomRenderer.color;
                c.a = 0.35f + 0.5f * Mathf.Clamp01(progress);
                roomRenderer.color = c;
            }
            if (barFill != null)
            {
                float p = Mathf.Clamp01(progress);
                barFill.localScale = new Vector3(p, 1f, 1f);
                barFill.localPosition = new Vector3((p - 1f) * 0.5f * spriteWidth, 0f, 0f);
                barFillRenderer.color = HasEnoughWorkers || (Ant.All.Count == 0 && manager.AllowWithoutAnts)
                    ? new Color(0.95f, 0.8f, 0.2f)
                    : new Color(0.9f, 0.25f, 0.2f);   // rojo = faltan obreras
            }
        }

        private void CreateBar()
        {
            var sprite = manager.SquareSprite;
            if (sprite == null || roomRenderer == null) return;

            Bounds rb = roomRenderer.bounds;
            float width = rb.size.x * 0.8f, height = 0.25f;

            barRoot = new GameObject("BuildBar").transform;
            barRoot.position = new Vector3(rb.center.x, rb.max.y + 0.35f, 0f);
            Vector2 s = sprite.bounds.size;
            spriteWidth = s.x;
            barRoot.localScale = new Vector3(width / Mathf.Max(s.x, 0.0001f), height / Mathf.Max(s.y, 0.0001f), 1f);
            barRoot.SetParent(transform, true);

            var bg = barRoot.gameObject.AddComponent<SpriteRenderer>();
            bg.sprite = sprite;
            bg.color = new Color(0.1f, 0.07f, 0.05f, 0.9f);
            bg.sortingOrder = manager.RoomSortingOrder + 1;

            var fillGo = new GameObject("Fill");
            barFill = fillGo.transform;
            barFill.SetParent(barRoot, false);
            barFillRenderer = fillGo.AddComponent<SpriteRenderer>();
            barFillRenderer.sprite = sprite;
            barFillRenderer.sortingOrder = manager.RoomSortingOrder + 2;
        }
    }
}
